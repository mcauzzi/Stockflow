import { Component, Input, Output, EventEmitter } from '@angular/core';
import { NgFor, NgClass, NgIf, DecimalPipe } from '@angular/common';
import { COMPONENT_LIBRARY } from '../../core/mock/sim-mock';

export interface PaletteItem {
  id: string; name: string; sym: string; cost: number;
  hotkey: string; kind: string; live: boolean;
}

@Component({
  selector: 'app-palette',
  standalone: true,
  imports: [NgFor, NgIf, DecimalPipe],
  template: `
    <div class="palette">
      <div class="panel-head">
        <span>COMPONENTS</span>
        <span class="idx">LAYOUT</span>
      </div>

      <div *ngFor="let g of lib" class="group">
        <div class="group-head">
          <span>{{ g.group }}</span>
          <span class="count">{{ g.items.length }}</span>
        </div>
        <div class="items">
          <div *ngFor="let it of g.items"
               class="item"
               [class.selected]="selectedId === it.id"
               [class.unavailable]="!it.live"
               [title]="it.live ? '' : 'Not yet implemented in backend (#33)'"
               (click)="it.live && select(it)">
            <div class="icon">{{ it.sym }}</div>
            <div>
              <div class="name">
                {{ it.name }}
                <span *ngIf="!it.live" style="color:var(--text-4);font-size:9px;margin-left:4px">✕</span>
              </div>
              <div class="cost">¢ {{ it.cost | number }}</div>
            </div>
            <div class="hotkey">{{ it.hotkey }}</div>
          </div>
        </div>
      </div>

      <div style="flex:1"></div>

      <div class="panel-head" style="border-top:1px solid var(--border);border-bottom:none">
        <span>NOTE</span>
      </div>
      <div style="padding:8px 12px;font-family:var(--mono);font-size:10px;color:var(--text-3);line-height:1.6">
        ✕ = backend not yet implemented<br>
        Place/Remove/Configure deferred to issue #33
      </div>
    </div>
  `,
  styles: [`
    .unavailable { opacity: 0.45; cursor: not-allowed !important; }
    .unavailable:hover { background: none !important; }
    .unavailable .icon { border-color: var(--border) !important; }
  `],
})
export class PaletteComponent {
  @Input()  selectedId: string | null = null;
  @Output() itemSelect = new EventEmitter<PaletteItem | null>();

  readonly lib = COMPONENT_LIBRARY;

  select(it: PaletteItem): void {
    const next = this.selectedId === it.id ? null : it;
    this.itemSelect.emit(next);
  }
}
