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
    <div class="inspector">

      <div class="panel-head">
        <span>INSPECTOR</span><span class="idx">F3</span>
      </div>

      <!-- Nothing selected -->
      <ng-container *ngIf="!selected">
        <div style="padding:18px 14px;font-size:11px;color:var(--text-3);font-family:var(--mono);line-height:1.6">
          <div class="caps dim2" style="margin-bottom:8px">NO SELECTION</div>
          Click a component on the grid to inspect.
        </div>

        <div class="panel-head"><span>ALERTS</span></div>
        <div style="padding:0 12px 10px">
          <div class="alert warn">
            <div style="display:flex;justify-content:space-between;color:var(--amber)">
              <span style="font-weight:600;letter-spacing:.05em">WARN</span><span>BACKEND</span>
            </div>
            <div style="color:var(--text-2);margin-top:2px">
              Place/Remove/Configure commands pending issue #33
            </div>
          </div>
        </div>

        <div class="panel-head"><span>PROTOCOL</span></div>
        <div class="insp-rows">
          <div class="insp-row"><div class="k">WS Endpoint</div><div class="v">ws://localhost:9600/ws</div></div>
          <div class="insp-row"><div class="k">Codec</div><div class="v">MessagePack + LZ4</div></div>
          <div class="insp-row"><div class="k">REST</div><div class="v">http://localhost:9601</div></div>
          <div class="insp-row"><div class="k">Commands</div><div class="v">Speed ✓ · Place ✕</div></div>
        </div>
      </ng-container>

      <!-- Component selected -->
      <ng-container *ngIf="selected">
        <div style="padding:10px 12px 6px;font-family:var(--mono)">
          <div style="font-size:10px;color:var(--text-3);letter-spacing:.08em">{{ kindLabel }}</div>
          <div style="font-size:18px;margin-top:2px;color:var(--amber)">{{ selected.id }}</div>
          <div style="margin-top:6px;display:flex;gap:6px">
            <span class="insp-badge ok">ACTIVE</span>
            <span class="insp-badge info">{{ selected.facing }}</span>
            <span class="insp-badge">({{ selected.gridX }},{{ selected.gridY }})</span>
          </div>
        </div>

        <div class="panel-head"><span>PROPERTIES</span></div>
        <div class="insp-rows">
          <div class="insp-row">
            <div class="k">Kind</div>
            <div class="v">{{ selected.kind }}</div>
          </div>
          <div class="insp-row">
            <div class="k">Grid</div>
            <div class="v">({{ selected.gridX }}, {{ selected.gridY }})</div>
          </div>
          <div class="insp-row">
            <div class="k">Facing</div>
            <div class="v">{{ selected.facing }}</div>
          </div>
        </div>

        <div class="panel-head"><span>NOTE</span></div>
        <div style="padding:8px 12px;font-family:var(--mono);font-size:10px;color:var(--text-3);line-height:1.6">
          Live telemetry and property editing available after issue #33 lands.
        </div>
      </ng-container>

    </div>
  `,
  styles: [`
    .alert { padding: 6px 8px; border-left: 2px solid; margin-bottom: 4px; font-family: var(--mono); font-size: 10px; }
    .alert.warn { border-color: var(--amber); background: rgba(245,166,35,.06); }
  `],
})
export class InspectorComponent {
  @Input() selected: ComponentState | null = null;

  get kindLabel(): string {
    return this.selected ? (KIND_LABELS[this.selected.kind] ?? this.selected.kind.toUpperCase()) : '';
  }
}
