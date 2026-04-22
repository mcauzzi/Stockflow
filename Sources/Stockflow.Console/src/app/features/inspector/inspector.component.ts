import { Component, Input } from '@angular/core';
import { NgIf } from '@angular/common';
import { ComponentState } from '../../core/models/protocol';

const KIND_LABELS: Record<string, string> = {
  'conveyor_oneway': 'ONE-WAY CONVEYOR',
  'conveyor_turn':   'CONVEYOR TURN 90°',
  'merge':           'MERGE',
  'diverter':        'DIVERTER',
  'accum':           'ACCUMULATOR',
  'rack':            'RACK BAY',
  'rack-d':          'RACK DOUBLE-DEPTH',
  'traslo':          'TRASLOELEVATOR',
  'bay-in':          'INBOUND BAY',
  'bay-out':         'OUTBOUND BAY',
  'pick':            'PICKING STATION',
  'qc':              'QC STATION',
};

@Component({
  selector: 'app-inspector',
  standalone: true,
  imports: [NgIf],
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

        <div class="panel-head"><span>ALERTS</span></div>
        <div class="sec">
          <div class="alert warn">
            <div class="alert-hd"><span>WARN</span><span>BACKEND</span></div>
            <div class="alert-body">Place/Remove/Configure commands pending issue #33</div>
          </div>
        </div>

        <div class="panel-head"><span>PROTOCOL</span></div>
        <div class="sec">
          <div class="row"><div class="k">WS Endpoint</div><div class="v">ws://localhost:9600/ws</div></div>
          <div class="row"><div class="k">Codec</div><div class="v">MessagePack + LZ4</div></div>
          <div class="row"><div class="k">REST</div><div class="v">http://localhost:9601</div></div>
          <div class="row"><div class="k">Commands</div><div class="v amber">Speed ✓ &nbsp;<span class="red">Place ✕</span></div></div>
        </div>
      </ng-container>

      <!-- Component selected -->
      <ng-container *ngIf="selected">
        <div class="sel-head">
          <div class="kind-lbl">{{ kindLabel }}</div>
          <div class="comp-id amber">{{ selected.id }}</div>
          <div class="badges">
            <span class="insp-badge ok">ACTIVE</span>
            <span class="insp-badge info">{{ selected.facing }}</span>
            <span class="insp-badge"
                  style="color:var(--text-2);border-color:var(--border-bright)">
              ({{ selected.gridX }},{{ selected.gridY }})
            </span>
          </div>
        </div>

        <div class="panel-head"><span>PROPERTIES</span></div>
        <div class="sec">
          <div class="row"><div class="k">Kind</div><div class="v">{{ selected.kind }}</div></div>
          <div class="row"><div class="k">Grid</div><div class="v">({{ selected.gridX }}, {{ selected.gridY }})</div></div>
          <div class="row"><div class="k">Facing</div><div class="v">{{ selected.facing }}</div></div>
        </div>

        <div class="panel-head"><span>NOTE</span></div>
        <div class="empty"><div class="hint">Live telemetry and property editing available after issue #33.</div></div>
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
      overflow: hidden;
      flex-shrink: 0;
      font-family: var(--mono);
    }
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
    .alert {
      padding: 7px 9px;
      border-left: 2px solid;
      margin-bottom: 4px;
      font-size: 10px;
    }
    .alert.warn { border-color: var(--amber); background: rgba(245,166,35,.06); }
    .alert-hd { display: flex; justify-content: space-between; color: var(--amber); font-weight: 600; font-size: 9px; letter-spacing: .06em; margin-bottom: 3px; }
    .alert-body { color: var(--text-2); }
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
    .insp-badge.ok   { color: var(--green); border-color: var(--green-dim); background: rgba(74,222,128,.06); }
    .insp-badge.info { color: var(--cyan);  border-color: var(--cyan-dim);  background: rgba(34,211,238,.06); }
    .insp-badge.warn { color: var(--amber); border-color: var(--amber-dim); background: rgba(245,166,35,.06); }
  `],
})
export class InspectorComponent {
  @Input() selected: ComponentState | null = null;

  get kindLabel(): string {
    return this.selected ? (KIND_LABELS[this.selected.kind] ?? this.selected.kind.toUpperCase()) : '';
  }
}
