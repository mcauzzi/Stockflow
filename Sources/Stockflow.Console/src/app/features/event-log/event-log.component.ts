import { Component, Input } from '@angular/core';
import { NgFor, NgClass } from '@angular/common';
import { MockEvent } from '../../core/mock/sim-mock';

@Component({
  selector: 'app-event-log',
  standalone: true,
  imports: [NgFor, NgClass],
  template: `
    <div class="col">
      <div class="panel-head">
        <span>EVENT LOG</span>
        <span class="idx">/sim/stream · WS</span>
      </div>
      <div class="eventlog">
        <div *ngFor="let e of events" class="eventrow">
          <span class="t">{{ e.t }}</span>
          <span class="src">{{ e.src }}</span>
          <span class="sev" [ngClass]="e.sev">
            {{ e.sev === 'i' ? '·' : e.sev === 'w' ? '!' : '✕' }}
          </span>
          <span class="msg">{{ e.msg }}</span>
        </div>
      </div>
    </div>
  `,
})
export class EventLogComponent {
  @Input() events: MockEvent[] = [];
}
