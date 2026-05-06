import { Component, Input, OnChanges, inject } from '@angular/core';
import { NgIf, NgFor } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ComponentState } from '../../core/models/protocol';
import { PropertySchema } from '../../core/models/protocol';
import { SimStateService } from '../../core/services/sim-state.service';
import { SchemaService } from '../../core/services/schema.service';

@Component({
  selector: 'app-inspector',
  standalone: true,
  imports: [NgIf, NgFor, FormsModule],
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

        <!-- ── Writable properties (schema-driven) ──────────────────────── -->
        <ng-container *ngIf="writableProps.length > 0">
          <div class="panel-head"><span>CONFIG</span></div>
          <div class="sec form-sec">
            <ng-container *ngFor="let prop of writableProps">
              <div class="field" *ngIf="prop.type === 'float' || prop.type === 'int'">
                <label class="field-lbl">{{ prop.displayName }}</label>
                <input class="field-input" type="number"
                       [min]="prop.min" [max]="prop.max"
                       [step]="prop.type === 'float' ? 0.1 : 1"
                       [(ngModel)]="editValues[prop.key]" />
              </div>
              <div class="field" *ngIf="prop.type === 'string'">
                <label class="field-lbl">{{ prop.displayName }}</label>
                <input class="field-input" type="text" [(ngModel)]="editValues[prop.key]" />
              </div>
              <div class="field-row" *ngIf="prop.type === 'bool'">
                <label class="field-lbl">{{ prop.displayName }}</label>
                <button class="tog-btn" [class.on]="editValues[prop.key] === 'true'"
                        (click)="toggleBool(prop.key)">
                  {{ editValues[prop.key] === 'true' ? 'ON' : 'OFF' }}
                </button>
              </div>
              <div class="field" *ngIf="prop.type === 'enum'">
                <label class="field-lbl">{{ prop.displayName }}</label>
                <div class="facing-btns">
                  <button *ngFor="let v of prop.enumValues"
                          class="dbtn" [class.on]="editValues[prop.key] === v"
                          (click)="editValues[prop.key] = v">
                    {{ v.toUpperCase() }}
                  </button>
                </div>
              </div>
            </ng-container>
            <button class="save-btn" (click)="save()">APPLY CHANGES</button>
          </div>
        </ng-container>

        <!-- ── Read-only metrics (schema-driven) ────────────────────────── -->
        <ng-container *ngIf="readOnlyProps.length > 0">
          <div class="panel-head"><span>LIVE METRICS</span></div>
          <div class="sec metrics-sec">
            <div class="metric-card" *ngFor="let prop of readOnlyProps">
              <div class="metric-val">{{ currentProps[prop.key] ?? '—' }}</div>
              <div class="metric-lbl">{{ prop.displayName }}</div>
            </div>
          </div>
        </ng-container>

        <!-- ── Position info ─────────────────────────────────────────────── -->
        <div class="panel-head"><span>PROPERTIES</span></div>
        <div class="sec">
          <div class="row"><div class="k">Grid</div><div class="v">({{ selected.gridX }}, {{ selected.gridY }})</div></div>
          <div class="row"><div class="k">Facing</div><div class="v">{{ selected.facing }}</div></div>
        </div>

        <!-- ── DELETE ────────────────────────────────────────────────────── -->
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

    .form-sec { display: flex; flex-direction: column; gap: 8px; }
    .field { display: flex; flex-direction: column; gap: 3px; }
    .field-row { display: flex; justify-content: space-between; align-items: center; }
    .field-lbl { font-size: 9px; color: var(--text-3); letter-spacing: .04em; }
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

    .facing-btns { display: flex; gap: 3px; }
    .dbtn {
      flex: 1;
      padding: 4px 0;
      border: 1px solid var(--border-bright);
      background: transparent;
      color: var(--text-3);
      font-family: var(--mono);
      font-size: 9px;
      cursor: pointer;
      letter-spacing: .04em;
      transition: all .1s;
    }
    .dbtn.on { color: var(--cyan); border-color: var(--cyan-dim); background: rgba(34,211,238,.06); font-weight: 700; }
    .dbtn:hover:not(.on) { background: var(--bg-2); color: var(--text-1); }

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

  private sim       = inject(SimStateService);
  private schemaSvc = inject(SchemaService);

  editValues: Record<string, string> = {};

  get kindLabel(): string {
    return this.selected?.kind.toUpperCase().replace(/_/g, ' ') ?? '';
  }

  get writableProps(): PropertySchema[] {
    return this.schemaSvc.getSchema(this.selected?.kind ?? '').filter(p => !p.isReadOnly);
  }

  get readOnlyProps(): PropertySchema[] {
    return this.schemaSvc.getSchema(this.selected?.kind ?? '').filter(p => p.isReadOnly);
  }

  get currentProps(): Record<string, string> {
    return this.selected?.properties ?? {};
  }

  ngOnChanges(): void {
    this.editValues = {};
    const schema  = this.schemaSvc.getSchema(this.selected?.kind ?? '');
    const current = this.selected?.properties ?? {};
    for (const prop of schema) {
      if (!prop.isReadOnly)
        this.editValues[prop.key] = current[prop.key] ?? prop.defaultValue ?? '';
    }
  }

  toggleBool(key: string): void {
    this.editValues[key] = this.editValues[key] === 'true' ? 'false' : 'true';
  }

  save(): void {
    if (!this.selected) return;
    const props: Record<string, string> = {};
    for (const [k, v] of Object.entries(this.editValues))
      props[k] = String(v);
    this.sim.configureComponent(this.selected.id, props);
  }

  deleteComponent(): void {
    if (!this.selected) return;
    this.sim.removeComponent(this.selected.id);
  }
}
