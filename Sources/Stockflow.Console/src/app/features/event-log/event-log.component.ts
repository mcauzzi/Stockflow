import { Component, Input } from '@angular/core';
import { NgFor } from '@angular/common';
import { MockEvent } from '../../core/mock/sim-mock';

@Component({
  selector: 'app-event-log',
  standalone: true,
  imports: [NgFor],
  template: `
    <div class="log">
      <div class="panel-head">
        <span>EVENT LOG</span>
        <span class="idx">/sim/stream · WS</span>
      </div>
      <div class="rows">
        <div *ngFor="let e of events" class="row">
          <span class="t">{{ e.t }}</span>
          <span class="src dim2">{{ e.src }}</span>
          <span [class.amber]="e.sev==='w'" [class.red]="e.sev==='e'" class="sev">
            {{ e.sev === 'i' ? '·' : e.sev === 'w' ? '!' : '✕' }}
          </span>
          <span class="msg">{{ e.msg }}</span>
        </div>
      </div>
    </div>
  `,
  styles: [`
    :host { display: contents; }
    .log {
      display: flex;
      flex-direction: column;
      height: 100%;
      overflow: hidden;
    }
    .rows {
      flex: 1;
      overflow-y: auto;
      font-family: var(--mono);
    }
    .rows::-webkit-scrollbar { width: 3px; }
    .rows::-webkit-scrollbar-track { background: var(--bg-1); }
    .rows::-webkit-scrollbar-thumb { background: var(--border-bright); }
    .row {
      display: grid;
      grid-template-columns: 88px 52px 14px 1fr;
      gap: 6px;
      padding: 2px 10px;
      border-bottom: 1px solid var(--bg-2);
      font-size: 10px;
      color: var(--text-2);
      align-items: center;
    }
    .row:hover { background: var(--bg-2); }
    .t   { color: var(--text-4); font-size: 9px; }
    .src { font-size: 9px; letter-spacing: .04em; }
    .sev { font-size: 11px; text-align: center; }
    .msg { color: var(--text-1); white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
  `],
})
export class EventLogComponent {
  @Input() events: MockEvent[] = [];
}
