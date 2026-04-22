import { Component, Input, Output, EventEmitter } from '@angular/core';
import { NgFor } from '@angular/common';

export type Tab = 'LAYOUT' | 'OPERATE' | 'ORDERS' | 'METRICS' | 'WORKSHOP' | 'PRO';
const TABS: { id: Tab; key: string }[] = [
  { id: 'LAYOUT',   key: 'F1' },
  { id: 'OPERATE',  key: 'F2' },
  { id: 'ORDERS',   key: 'F3' },
  { id: 'METRICS',  key: 'F4' },
  { id: 'WORKSHOP', key: 'F5' },
  { id: 'PRO',      key: 'F6' },
];

@Component({
  selector: 'app-topbar',
  standalone: true,
  imports: [NgFor],
  template: `
    <div class="topbar">
      <div class="brand">STOCKFLOW<span style="opacity:.7;font-weight:400">·CON</span></div>
      <div *ngFor="let t of tabs"
           class="tab" [class.active]="t.id === activeTab"
           (click)="tabChange.emit(t.id)">
        {{ t.id }} <span class="k">{{ t.key }}</span>
      </div>
      <div class="spacer"></div>
      <div class="status">
        <span>
          <span class="dot" [style.background]="connected ? 'var(--green)' : 'var(--red)'"
                [style.box-shadow]="connected ? '0 0 6px var(--green)' : 'none'"></span>
          {{ connected ? 'SIM ACTIVE' : 'DISCONNECTED' }}
        </span>
        <span>SESSION <span class="amber">#A4B21</span></span>
        <span class="live-chip">
          <span class="ws-dot" [style.background]="connected ? 'var(--green)' : 'var(--red-dim)'"></span>
          WS :9600
        </span>
        <span [class.dim2]="restOnline" [class.red]="!restOnline">REST :9601</span>
        <span class="dim">v0.1.0</span>
      </div>
    </div>
  `,
})
export class TopbarComponent {
  @Input() activeTab: Tab = 'OPERATE';
  @Input() connected = false;
  @Input() restOnline = false;
  @Output() tabChange = new EventEmitter<Tab>();

  readonly tabs = TABS;
}
