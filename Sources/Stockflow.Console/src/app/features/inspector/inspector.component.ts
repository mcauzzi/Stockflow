import { Component, Input, OnChanges, inject } from '@angular/core';
import { NgIf } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ComponentState } from '../../core/models/protocol';
import { SimStateService } from '../../core/services/sim-state.service';

const KIND_LABELS: Record<string, string> = {
  'conveyor_oneway':   'ONE-WAY CONVEYOR',
  'conveyor_turn':     'CONVEYOR TURN 90°',
  'package_generator': 'PACKAGE GENERATOR',
  'package_exit':      'PACKAGE EXIT',
  'merge':             'MERGE',
  'diverter':          'DIVERTER',
  'accum':             'ACCUMULATOR',
  'rack':              'RACK BAY',
  'rack-d':            'RACK DOUBLE-DEPTH',
  'traslo':            'TRASLOELEVATOR',
  'bay-in':            'INBOUND BAY',
  'bay-out':           'OUTBOUND BAY',
  'pick':              'PICKING STATION',
  'qc':                'QC STATION',
};

@Component({
  selector: 'app-inspector',
  standalone: true,
  imports: [NgIf, FormsModule],
  template: `
    <div class="insp">

      <div class="panel-head">
        <span>INSPECTOR</span><span class="idx">F3</span>
      </div>

      <!-- Nothing selected -->
      <ng-container *ngIf="!selected">
        <div class="empty">
          <div class="caps">NO SELECTION</div>
          <div class="hint">Click a component on the grid to inspect.</div>
        </div>

        <div class="panel-head"><span>PROTOCOL</span></div>
        <div class="sec">
          <div class="row"><div class="k">WS Endpoint</div><div class="v">ws://localhost:9600/ws</div></div>
          <div class="row"><div class="k">Codec</div><div class="v">MessagePack + LZ4</div></div>
          <div class="row"><div class="k">REST</div><div class="v">http://localhost:9601</div></div>
          <div class="row"><div class="k">Commands</div><div class="v ok">Speed ✓ · Place ✓</div></div>
        </div>
      </ng-container>

      <!-- Component selected -->
      <ng-container *ngIf="selected">
        <div class="sel-head">
          <div class="kind-lbl">{{ kindLabel }}</div>
          <div class="comp-id amber">{{ selected.id }}</div>
          <div class="badges">
            <span class="insp-badge info">{{ selected.facing }}</span>
            <span class="insp-badge" style="color:var(--text-2);border-color:var(--border-bright)">
              ({{ selected.gridX }},{{ selected.gridY }})
            </span>
          </div>
        </div>

        <!-- ── PACKAGE GENERATOR ─────────────────────────── -->
        <ng-container *ngIf="isGenerator">
          <div class="panel-head"><span>CONFIG</span></div>
          <div class="sec form-sec">
            <div class="field">
              <label class="field-lbl">Spawn Rate <span class="unit">pkg/s</span></label>
              <input class="field-input" type="number" [(ngModel)]="editSpawnRate"
                     min="0.01" max="100" step="0.1"/>
            </div>
            <div class="field">
              <label class="field-lbl">SKU</label>
              <input class="field-input" type="text" [(ngModel)]="editSku" maxlength="16"/>
            </div>
            <div class="field-row">
              <label class="field-lbl">Enabled</label>
              <button class="tog-btn" [class.on]="editEnabled" (click)="editEnabled = !editEnabled">
                {{ editEnabled ? 'ON' : 'OFF' }}
              </button>
            </div>
            <button class="save-btn" (click)="saveGenerator()">APPLY CHANGES</button>
          </div>

          <div class="panel-head"><span>PROPERTIES</span></div>
          <div class="sec">
            <div class="row"><div class="k">Weight</div><div class="v">{{ prop('weight') }} kg</div></div>
            <div class="row"><div class="k">Size</div><div class="v">{{ prop('size') }}</div></div>
            <div class="row"><div class="k">Grid</div><div class="v">({{ selected.gridX }}, {{ selected.gridY }})</div></div>
            <div class="row"><div class="k">Facing</div><div class="v">{{ selected.facing }}</div></div>
          </div>
        </ng-container>

        <!-- ── PACKAGE EXIT ──────────────────────────────── -->
        <ng-container *ngIf="isExit">
          <div class="panel-head"><span>LIVE METRICS</span></div>
          <div class="sec metrics-sec">
            <div class="metric-card">
              <div class="metric-val">{{ prop('totalProcessed') || '0' }}</div>
              <div class="metric-lbl">PROCESSED</div>
            </div>
            <div class="metric-card">
              <div class="metric-val">{{ prop('throughput') || '0.000' }}</div>
              <div class="metric-lbl">THROUGHPUT <span class="metric-unit">pkg/s</span></div>
            </div>
            <div class="metric-card">
              <div class="metric-val">{{ prop('avgFulfillmentTime') || '0.000' }}</div>
              <div class="metric-lbl">AVG FULFILLMENT <span class="metric-unit">s</span></div>
            </div>
          </div>

          <div class="panel-head"><span>PROPERTIES</span></div>
          <div class="sec">
            <div class="row"><div class="k">Grid</div><div class="v">({{ selected.gridX }}, {{ selected.gridY }})</div></div>
            <div class="row"><div class="k">Facing</div><div class="v">{{ selected.facing }}</div></div>
          </div>
        </ng-container>

        <!-- ── CONVEYORS ─────────────────────────────────── -->
        <ng-container *ngIf="isConveyor">
          <div class="panel-head"><span>CONFIG</span></div>
          <div class="sec form-sec">
            <div class="field">
              <label class="field-lbl">Speed <span class="unit">cell/s</span></label>
              <input class="field-input" type="number" [(ngModel)]="editSpeed"
                     min="0.1" max="10" step="0.1"/>
            </div>
            <button class="save-btn" (click)="saveConveyor()">APPLY CHANGES</button>
          </div>
          <div class="panel-head"><span>PROPERTIES</span></div>
          <div class="sec">
            <div class="row"><div class="k">Grid</div><div class="v">({{ selected.gridX }}, {{ selected.gridY }})</div></div>
            <div class="row"><div class="k">Facing</div><div class="v">{{ selected.facing }}</div></div>
          </div>
        </ng-container>

        <!-- ── OTHER COMPONENTS ──────────────────────────── -->
        <ng-container *ngIf="!isGenerator && !isExit && !isConveyor">
          <div class="panel-head"><span>PROPERTIES</span></div>
          <div class="sec">
            <div class="row"><div class="k">Kind</div><div class="v">{{ selected.kind }}</div></div>
            <div class="row"><div class="k">Grid</div><div class="v">({{ selected.gridX }}, {{ selected.gridY }})</div></div>
            <div class="row"><div class="k">Facing</div><div class="v">{{ selected.facing }}</div></div>
          </div>
        </ng-container>

        <!-- ── DELETE ────────────────────────────────────── -->
        <div class="sec">
          <button class="del-btn" (click)="deleteComponent()">DELETE COMPONENT</button>
        </div>
      </ng-container>

    </div>
  `,
  styles: [`
    :host { display: contents; }
    .insp {
      width: 220px;
      background: var(--bg-1);
      border-left: 1px solid var(--border);
      display: flex;
      flex-direction: column;
      overflow-y: auto;
      flex-shrink: 0;
      font-family: var(--mono);
    }
    .insp::-webkit-scrollbar { width: 3px; }
    .insp::-webkit-scrollbar-thumb { background: var(--border-bright); }
    .empty { padding: 12px 14px; }
    .caps { font-size: 9px; letter-spacing: .1em; color: var(--text-4); text-transform: uppercase; margin-bottom: 6px; }
    .hint { font-size: 10px; color: var(--text-3); line-height: 1.6; }
    .sec { padding: 6px 12px 8px; }
    .row {
      display: flex;
      justify-content: space-between;
      align-items: baseline;
      padding: 4px 0;
      border-bottom: 1px solid var(--bg-2);
      font-size: 10px;
    }
    .k { font-size: 9px; color: var(--text-3); letter-spacing: .04em; }
    .v { color: var(--text-1); }
    .v.ok { color: var(--green); }
    .sel-head { padding: 10px 12px 8px; }
    .kind-lbl { font-size: 9px; color: var(--text-3); letter-spacing: .07em; text-transform: uppercase; }
    .comp-id { font-size: 20px; margin-top: 2px; line-height: 1; }
    .badges { display: flex; gap: 5px; margin-top: 7px; flex-wrap: wrap; }
    .insp-badge {
      display: inline-block;
      padding: 1px 6px;
      font-size: 9px;
      letter-spacing: .06em;
      border: 1px solid;
      text-transform: uppercase;
    }
    .insp-badge.info { color: var(--cyan); border-color: var(--cyan-dim); background: rgba(34,211,238,.06); }

    /* Generator form */
    .form-sec { display: flex; flex-direction: column; gap: 8px; }
    .field { display: flex; flex-direction: column; gap: 3px; }
    .field-row { display: flex; justify-content: space-between; align-items: center; }
    .field-lbl { font-size: 9px; color: var(--text-3); letter-spacing: .04em; }
    .unit, .metric-unit { font-size: 8px; color: var(--text-4); }
    .field-input {
      background: var(--bg-0);
      border: 1px solid var(--border-bright);
      color: var(--text-1);
      font-family: var(--mono);
      font-size: 10px;
      padding: 4px 6px;
      width: 100%;
      box-sizing: border-box;
      outline: none;
    }
    .field-input:focus { border-color: var(--cyan); }
    .tog-btn {
      padding: 2px 10px;
      border: 1px solid var(--border-bright);
      background: transparent;
      color: var(--text-3);
      font-family: var(--mono);
      font-size: 9px;
      cursor: pointer;
      letter-spacing: .06em;
      transition: all .1s;
    }
    .tog-btn.on { color: var(--green); border-color: var(--green-dim); background: rgba(74,222,128,.06); }
    .save-btn {
      padding: 6px;
      border: 1px solid var(--cyan-dim);
      background: rgba(34,211,238,.06);
      color: var(--cyan);
      font-family: var(--mono);
      font-size: 9px;
      letter-spacing: .08em;
      cursor: pointer;
      width: 100%;
      transition: all .12s;
    }
    .save-btn:hover { background: rgba(34,211,238,.14); }
    .del-btn {
      padding: 6px;
      border: 1px solid rgba(248,113,113,.4);
      background: rgba(248,113,113,.06);
      color: #f87171;
      font-family: var(--mono);
      font-size: 9px;
      letter-spacing: .08em;
      cursor: pointer;
      width: 100%;
      transition: all .12s;
    }
    .del-btn:hover { background: rgba(248,113,113,.16); }

    /* Exit metrics */
    .metrics-sec { display: flex; flex-direction: column; gap: 6px; }
    .metric-card {
      background: var(--bg-0);
      border: 1px solid var(--border);
      padding: 8px 10px;
    }
    .metric-val {
      font-size: 22px;
      font-weight: 600;
      color: var(--text-0);
      line-height: 1;
      letter-spacing: .02em;
    }
    .metric-lbl {
      font-size: 8px;
      color: var(--text-4);
      letter-spacing: .1em;
      margin-top: 4px;
      text-transform: uppercase;
    }
  `],
})
export class InspectorComponent implements OnChanges {
  @Input() selected: ComponentState | null = null;

  private sim = inject(SimStateService);

  editSpawnRate = 1;
  editSku = 'PKG';
  editEnabled = true;
  editSpeed = 1;

  get kindLabel(): string {
    return this.selected ? (KIND_LABELS[this.selected.kind] ?? this.selected.kind.toUpperCase()) : '';
  }

  get isGenerator(): boolean { return this.selected?.kind === 'package_generator'; }
  get isExit():      boolean { return this.selected?.kind === 'package_exit'; }
  get isConveyor():  boolean {
    return this.selected?.kind === 'conveyor_oneway' || this.selected?.kind === 'conveyor_turn';
  }

  ngOnChanges(): void {
    const p = this.selected?.properties ?? {};
    if (this.selected?.kind === 'package_generator') {
      this.editSpawnRate = parseFloat(p['spawnRate'] ?? '1');
      this.editSku       = p['sku'] ?? 'PKG';
      this.editEnabled   = (p['enabled'] ?? 'true') !== 'false';
    }
    if (this.isConveyor) {
      this.editSpeed = parseFloat(p['speed'] ?? '1');
    }
  }

  prop(key: string): string {
    return this.selected?.properties?.[key] ?? '';
  }

  saveGenerator(): void {
    if (!this.selected) return;
    this.sim.configureComponent(this.selected.id, {
      spawnRate: String(this.editSpawnRate),
      sku:       this.editSku,
      enabled:   this.editEnabled ? 'true' : 'false',
    });
  }

  saveConveyor(): void {
    if (!this.selected) return;
    this.sim.configureComponent(this.selected.id, { speed: String(this.editSpeed) });
  }

  deleteComponent(): void {
    if (!this.selected) return;
    this.sim.removeComponent(this.selected.id);
  }
}
