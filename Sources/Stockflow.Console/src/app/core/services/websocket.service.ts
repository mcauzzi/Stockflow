import { Injectable, NgZone, OnDestroy } from '@angular/core';
import { Subject } from 'rxjs';
import { decode, ExtensionCodec } from '@msgpack/msgpack';
import * as lz4 from 'lz4js';
import {
  ServerMessage, StateDeltaMessage, FullStateMessage, CommandResultMessage,
  EntityState, ComponentState, SimEvent, MetricsSnapshot, Direction, EntityStatus,
} from '../models/protocol';

// MessagePack-CSharp Lz4Block extension type code (hard-coded in their source)
const LZ4_EXT_TYPE = 99;

// Codec that transparently decompresses LZ4-wrapped MessagePack payloads
const extensionCodec = new ExtensionCodec();
extensionCodec.register({
  type: LZ4_EXT_TYPE,
  encode: () => null,
  decode(data: Uint8Array): unknown {
    const view = new DataView(data.buffer, data.byteOffset, data.byteLength);
    const originalSize = view.getInt32(0, false); // big-endian int32
    const compressed = new Uint8Array(data.buffer, data.byteOffset + 4, data.byteLength - 4);
    const decompressed = lz4.decompress(compressed, originalSize);
    return decode(decompressed, { extensionCodec });
  },
});

const WS_URL  = 'ws://localhost:9600/ws';

@Injectable({ providedIn: 'root' })
export class WebSocketService implements OnDestroy {
  readonly messages$ = new Subject<ServerMessage>();
  readonly connected$ = new Subject<boolean>();

  private ws: WebSocket | null = null;
  private reconnectTimer: ReturnType<typeof setTimeout> | null = null;
  private destroyed = false;

  constructor(private zone: NgZone) {}

  connect(): void {
    if (this.ws?.readyState === WebSocket.OPEN || this.ws?.readyState === WebSocket.CONNECTING) return;
    this._open();
  }

  disconnect(): void {
    this.destroyed = true;
    this._clearReconnect();
    this.ws?.close();
    this.ws = null;
  }

  ngOnDestroy(): void { this.disconnect(); }

  // ─────────────────────────────────────────────────────────────────────────

  private _open(): void {
    const ws = new WebSocket(WS_URL);
    ws.binaryType = 'arraybuffer';
    this.ws = ws;

    ws.onopen = () => this.zone.run(() => this.connected$.next(true));

    ws.onclose = () => this.zone.run(() => {
      this.connected$.next(false);
      if (!this.destroyed) this._scheduleReconnect();
    });

    ws.onerror = () => this.zone.run(() => this.connected$.next(false));

    ws.onmessage = (ev: MessageEvent) => {
      if (!(ev.data instanceof ArrayBuffer)) return;
      const msg = this._decode(new Uint8Array(ev.data));
      if (msg) this.zone.run(() => this.messages$.next(msg));
    };
  }

  private _scheduleReconnect(): void {
    this._clearReconnect();
    this.reconnectTimer = setTimeout(() => this._open(), 3000);
  }

  private _clearReconnect(): void {
    if (this.reconnectTimer !== null) { clearTimeout(this.reconnectTimer); this.reconnectTimer = null; }
  }

  private _decode(bytes: Uint8Array): ServerMessage | null {
    try {
      const raw = decode(bytes, { extensionCodec }) as unknown[];
      return this._parseUnion(raw);
    } catch (e) {
      console.warn('[WS] MessagePack decode failed:', e);
      return null;
    }
  }

  // MessagePack-CSharp Union format: [unionKey, payloadPositionalArray]
  private _parseUnion(raw: unknown[]): ServerMessage | null {
    if (!Array.isArray(raw) || raw.length < 2) return null;
    const key = raw[0] as number;
    const p   = raw[1] as unknown[];
    switch (key) {
      case 0: return this._parseDelta(p);
      case 1: return this._parseFull(p);
      case 2: return this._parseResult(p);
      default: return null;
    }
  }

  // StateDeltaMessage — fields ordered by [Key(N)]
  private _parseDelta(p: unknown[]): StateDeltaMessage {
    return {
      type: 'StateDelta',
      serverTime:         (p[0]  as number)    ?? 0,
      simulationTime:     (p[1]  as number)    ?? 0,
      timeScale:          (p[2]  as number)    ?? 1,
      updatedEntities:    this._mapArr(p[3],  this._parseEntity),
      createdEntities:    this._mapArr(p[4],  this._parseEntity),
      removedEntityIds:   (p[5]  as number[]) ?? [],
      updatedComponents:  this._mapArr(p[6],  this._parseComponent),
      createdComponents:  this._mapArr(p[7],  this._parseComponent),
      removedComponentIds:(p[8]  as number[]) ?? [],
      events:             this._mapArr(p[9],  this._parseEvent),
      metrics:            p[10] ? this._parseMetrics(p[10] as unknown[]) : null,
    };
  }

  private _parseFull(p: unknown[]): FullStateMessage {
    return {
      type: 'FullState',
      serverTime:     (p[0] as number) ?? 0,
      simulationTime: (p[1] as number) ?? 0,
      timeScale:      (p[2] as number) ?? 1,
      entities:       this._mapArr(p[3], this._parseEntity),
      components:     this._mapArr(p[4], this._parseComponent),
      metrics:        p[5] ? this._parseMetrics(p[5] as unknown[]) : null,
    };
  }

  private _parseResult(p: unknown[]): CommandResultMessage {
    return {
      type: 'CommandResult',
      serverTime:   (p[0] as number)  ?? 0,
      commandId:    (p[1] as number)  ?? 0,
      success:      (p[2] as boolean) ?? false,
      errorMessage: (p[3] as string)  ?? null,
    };
  }

  private _parseEntity = (p: unknown[]): EntityState => ({
    id:       (p[0] as number) ?? 0,
    sku:      (p[1] as string) ?? '',
    position: p[2] ? { x: (p[2] as number[])[0] ?? 0, y: (p[2] as number[])[1] ?? 0, z: (p[2] as number[])[2] ?? 0 } : { x: 0, y: 0, z: 0 },
    status:   (['Idle','Moving','Queued'][(p[3] as number) ?? 0] ?? 'Idle') as EntityStatus,
  });

  private _parseComponent = (p: unknown[]): ComponentState => ({
    id:     (p[0] as number) ?? 0,
    kind:   (p[1] as string) ?? '',
    gridX:  (p[2] as number) ?? 0,
    gridY:  (p[3] as number) ?? 0,
    facing: (['North','East','South','West'][(p[4] as number) ?? 1] ?? 'East') as Direction,
    properties: p[5] instanceof Map
      ? Object.fromEntries(p[5] as Map<string, string>)
      : p[5] ? p[5] as Record<string, string> : undefined,
  });

  private _parseEvent = (p: unknown[]): SimEvent => ({
    eventType:   (p[0] as string) ?? '',
    simTime:     (p[1] as number) ?? 0,
    payloadJson: (p[2] as string) ?? '',
  });

  private _parseMetrics(p: unknown[]): MetricsSnapshot {
    return {
      throughput:          (p[0] as number) ?? 0,
      avgFulfillmentTime:  (p[1] as number) ?? 0,
      warehouseSaturation: (p[2] as number) ?? 0,
      activeOrders:        (p[3] as number) ?? 0,
      completedOrders:     (p[4] as number) ?? 0,
    };
  }

  private _mapArr<T>(raw: unknown, fn: (p: unknown[]) => T): T[] {
    if (!Array.isArray(raw)) return [];
    return raw.map(item => fn(item as unknown[]));
  }
}
