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
      <div class="brand">STOCKFLOW<span class="sub">·CON</span></div>
      <button *ngFor="let t of tabs"
              class="tab" [class.active]="t.id === activeTab"
              (click)="tabChange.emit(t.id)">
        {{ t.id }} <span class="k">{{ t.key }}</span>
      </button>
      <div class="spacer"></div>
      <div class="status">
        <span class="live-chip">
          <span class="dot" [style.background]="connected ? 'var(--green)' : 'var(--red)'"
                [style.box-shadow]="connected ? '0 0 6px var(--green)' : 'none'"></span>
          {{ connected ? 'SIM ACTIVE' : 'DISCONNECTED' }}
        </span>
        <span>SESSION <span class="amber">#A4B21</span></span>
        <span class="live-chip">
          <span class="dot" [style.background]="connected ? 'var(--green)' : 'var(--red-dim)'"></span>
          WS :9600
        </span>
        <span [style.color]="restOnline ? 'var(--text-3)' : 'var(--red)'">REST :9601</span>
        <span class="dim2">v0.1.0</span>
      </div>
    </div>
  `,
  styles: [`
    :host { display: contents; }
    .topbar {
      height: 36px;
      background: var(--bg-1);
      border-bottom: 1px solid var(--border);
      display: flex;
      align-items: stretch;
      flex-shrink: 0;
      font-family: var(--mono);
      user-select: none;
    }
    .brand {
      display: flex;
      align-items: center;
      padding: 0 16px;
      border-right: 1px solid var(--border);
      font-size: 13px;
      font-weight: 600;
      letter-spacing: .05em;
      color: var(--amber);
      white-space: nowrap;
    }
    .sub { opacity: .7; font-weight: 400; }
    .tab {
      display: flex;
      align-items: center;
      gap: 5px;
      padding: 0 12px;
      border: none;
      border-right: 1px solid var(--border);
      border-bottom: 2px solid transparent;
      background: transparent;
      color: var(--text-3);
      font-family: var(--mono);
      font-size: 10px;
      letter-spacing: .1em;
      cursor: pointer;
      transition: color .12s, background .12s;
      white-space: nowrap;
    }
    .tab:hover { background: var(--bg-2); color: var(--text-1); }
    .tab.active { background: var(--bg-2); color: var(--amber); border-bottom-color: var(--amber); }
    .k { opacity: .4; font-size: 8px; }
    .spacer { flex: 1; }
    .status {
      display: flex;
      align-items: center;
      gap: 10px;
      padding: 0 14px;
      border-left: 1px solid var(--border);
      font-size: 9px;
      color: var(--text-3);
      white-space: nowrap;
    }
    .dot {
      width: 6px; height: 6px;
      border-radius: 50%;
      display: inline-block;
      margin-right: 3px;
      flex-shrink: 0;
    }
    .live-chip { display: flex; align-items: center; }
  `],
})
export class TopbarComponent {
  @Input() activeTab: Tab = 'OPERATE';
  @Input() connected = false;
  @Input() restOnline = false;
  @Output() tabChange = new EventEmitter<Tab>();

  readonly tabs = TABS;
}
