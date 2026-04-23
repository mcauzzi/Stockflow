import { Injectable, OnDestroy, computed, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Subscription } from 'rxjs';
import { WebSocketService } from './websocket.service';
import {
  ComponentState, Direction, EntityState, MetricsSnapshot, SimEvent, SimSpeed,
  StateDeltaMessage, FullStateMessage,
} from '../models/protocol';
import { MockEvent, INITIAL_EVENTS, genSpark } from '../mock/sim-mock';

const REST_BASE = 'http://localhost:9601';
const MAX_EVENTS = 120;

@Injectable({ providedIn: 'root' })
export class SimStateService implements OnDestroy {
  // ── connection ────────────────────────────────────────────────────────────
  readonly connected  = signal(false);
  readonly restOnline = signal(false);

  // ── sim time ──────────────────────────────────────────────────────────────
  readonly simulationTime = signal(0);
  readonly timeScale      = signal(1);

  // ── world state ───────────────────────────────────────────────────────────
  readonly components = signal(new Map<number, ComponentState>());
  readonly entities   = signal(new Map<number, EntityState>());
  readonly metrics    = signal<MetricsSnapshot | null>(null);

  // ── event log (live events from WS + mock seed) ───────────────────────────
  readonly events = signal<MockEvent[]>(INITIAL_EVENTS);

  // ── KPI sparklines (updated from metrics ticks) ───────────────────────────
  readonly sparks = signal({
    throughput: genSpark(0, 0),
    saturation: genSpark(0, 0),
    fulfillment: genSpark(0, 0),
  });

  // ── derived ───────────────────────────────────────────────────────────────
  readonly simClock = computed(() => {
    const s = Math.floor(this.simulationTime());
    const hh = String(Math.floor(s / 3600)).padStart(2, '0');
    const mm = String(Math.floor((s % 3600) / 60)).padStart(2, '0');
    const ss = String(s % 60).padStart(2, '0');
    return `T+${hh}:${mm}:${ss}`;
  });

  // ── grid dimensions from initial REST load ────────────────────────────────
  readonly gridWidth  = signal(50);
  readonly gridLength = signal(50);
  readonly gridFloors = signal(1);

  private subs = new Subscription();

  constructor(
    private ws: WebSocketService,
    private http: HttpClient,
  ) {
    this._init();
  }

  ngOnDestroy(): void {
    this.subs.unsubscribe();
    this.ws.disconnect();
  }

  // ── public commands ───────────────────────────────────────────────────────

  setSpeed(speed: SimSpeed): void {
    this.http.post(`${REST_BASE}/api/sim/speed`, { speed }).subscribe({
      next: (r: any) => {
        if (r?.timeScale !== undefined) this.timeScale.set(r.timeScale);
      },
      error: err => console.warn('[REST] speed change failed:', err),
    });
  }

  placeComponent(
    kind: string, gridX: number, gridY: number, facing: Direction,
    params?: Partial<{ spawnRate: number; sku: string; weight: number; size: number }>,
  ): void {
    const body = { kind, gridX, gridY, facing, ...params };
    this.http.post(`${REST_BASE}/api/components`, body).subscribe({
      next: () => this._appendEvent('i', 'REST', `Placed ${kind} at (${gridX},${gridY})`),
      error: (err: any) => this._appendEvent('e', 'REST', `Place failed: ${err.status}`),
    });
  }

  configureComponent(id: number, props: Record<string, string>): void {
    this.http.put(`${REST_BASE}/api/components/${id}`, props).subscribe({
      next: () => this._appendEvent('i', 'REST', `Configured component #${id}`),
      error: (err: any) => this._appendEvent('e', 'REST', `Configure failed: ${err.status}`),
    });
  }

  // ─────────────────────────────────────────────────────────────────────────

  private _init(): void {
    this.ws.connect();

    this.subs.add(this.ws.connected$.subscribe(c => {
      this.connected.set(c);
      if (c) this._loadInitialState();
    }));

    this.subs.add(this.ws.messages$.subscribe(msg => {
      switch (msg.type) {
        case 'StateDelta': this._applyDelta(msg); break;
        case 'FullState':  this._applyFull(msg);  break;
      }
    }));

    this._pingRest();
  }

  private _pingRest(): void {
    this.http.get(`${REST_BASE}/api/health`).subscribe({
      next: () => {
        this.restOnline.set(true);
        this._loadInitialState();
      },
      error: () => {
        this.restOnline.set(false);
        setTimeout(() => this._pingRest(), 5000);
      },
    });
  }

  private _loadInitialState(): void {
    this.http.get<any>(`${REST_BASE}/api/sim/state`).subscribe({
      next: (s) => {
        this.simulationTime.set(s.simulationTime ?? 0);
        this.timeScale.set(s.timeScale ?? 1);
        this.gridWidth.set(s.gridWidth ?? 50);
        this.gridLength.set(s.gridLength ?? 50);
        this.gridFloors.set(s.gridFloors ?? 1);

        const map = new Map<number, ComponentState>();
        for (const c of (s.components ?? [])) {
          map.set(c.id, {
            id: c.id, kind: c.kind,
            gridX: c.gridX, gridY: c.gridY,
            facing: c.facing,
            properties: c.properties,
          });
        }
        this.components.set(map);

        this._appendEvent('i', 'REST', `Loaded ${map.size} components, ${s.entityCount} entities`);
      },
      error: err => this._appendEvent('w', 'REST', `State load failed: ${err.status}`),
    });
  }

  private _applyDelta(d: StateDeltaMessage): void {
    this.simulationTime.set(d.simulationTime);
    this.timeScale.set(d.timeScale);

    const comps = new Map(this.components());
    for (const c of d.createdComponents) comps.set(c.id, c);
    for (const c of d.updatedComponents) comps.set(c.id, c);
    for (const id of d.removedComponentIds) comps.delete(id);
    this.components.set(comps);

    const ents = new Map(this.entities());
    for (const e of d.createdEntities) ents.set(e.id, e);
    for (const e of d.updatedEntities) ents.set(e.id, e);
    for (const id of d.removedEntityIds) ents.delete(id);
    this.entities.set(ents);

    if (d.metrics) this._applyMetrics(d.metrics);

    for (const ev of d.events) {
      const isJam = ev.eventType === 'conveyor_jammed';
      this._appendEvent(isJam ? 'w' : 'i', 'SIM',
        `${ev.eventType} @ T+${ev.simTime.toFixed(1)}s`);
    }
  }

  private _applyFull(f: FullStateMessage): void {
    this.simulationTime.set(f.simulationTime);
    this.timeScale.set(f.timeScale);

    const comps = new Map<number, ComponentState>();
    for (const c of f.components) comps.set(c.id, c);
    this.components.set(comps);

    const ents = new Map<number, EntityState>();
    for (const e of f.entities) ents.set(e.id, e);
    this.entities.set(ents);

    if (f.metrics) this._applyMetrics(f.metrics);
    this._appendEvent('i', 'WS', `Full sync: ${f.components.length} components, ${f.entities.length} entities`);
  }

  private _applyMetrics(m: MetricsSnapshot): void {
    this.metrics.set(m);
    this.sparks.update(s => ({
      throughput:  [...s.throughput.slice(1),  m.throughput],
      saturation:  [...s.saturation.slice(1),  m.warehouseSaturation],
      fulfillment: [...s.fulfillment.slice(1), m.avgFulfillmentTime],
    }));
  }

  private _appendEvent(sev: 'i' | 'w' | 'e', src: string, msg: string): void {
    const t = this.simClock();
    this.events.update(evs => {
      const next = [{ t, src, sev, msg }, ...evs];
      return next.length > MAX_EVENTS ? next.slice(0, MAX_EVENTS) : next;
    });
  }
}
