import { Component, Input, Output, EventEmitter } from '@angular/core';
import { NgFor, NgIf, DecimalPipe } from '@angular/common';
import { COMPONENT_LIBRARY } from '../../core/mock/sim-mock';
import { Direction } from '../../core/models/protocol';

export interface PaletteItem {
  id: string; name: string; sym: string; cost: number;
  hotkey: string; kind: string; live: boolean;
}

@Component({
  selector: 'app-palette',
  standalone: true,
  imports: [NgFor, NgIf, DecimalPipe],
  template: `
    <div class="pal">
      <div class="panel-head">
        <span>COMPONENTS</span>
        <span class="idx">LAYOUT</span>
      </div>

      <div class="scroll">
        <ng-container *ngFor="let g of lib">
          <div class="grp-head">
            <span>{{ g.group }}</span>
            <span class="dim2">{{ g.items.length }}</span>
          </div>
          <button *ngFor="let it of g.items"
               class="item"
               [class.sel]="selectedId === it.id"
               [class.dead]="!it.live"
               [title]="it.live ? '' : 'Not yet implemented in backend (#33)'"
               (click)="it.live && select(it)">
            <div class="ico">{{ it.sym }}</div>
            <div class="info">
              <div class="name">
                {{ it.name }}
                <span *ngIf="!it.live" class="x">✕</span>
              </div>
              <div class="cost">¢ {{ it.cost | number }}</div>
            </div>
            <div class="hk">{{ it.hotkey }}</div>
          </button>
        </ng-container>
      </div>

      <!-- Facing selector (shown when a live component is selected) -->
      <div class="facing" *ngIf="selectedId">
        <div class="facing-lbl">FACING</div>
        <div class="facing-btns">
          <button *ngFor="let d of directions"
                  class="dbtn" [class.on]="selectedFacing === d"
                  [title]="d"
                  (click)="setFacing(d)">{{ d[0] }}</button>
        </div>
      </div>

      <!-- Turn side selector (shown only for conveyor_turn) -->
      <div class="facing" *ngIf="isConveyorTurn">
        <div class="facing-lbl">TURN SIDE</div>
        <div class="facing-btns">
          <button class="dbtn" [class.on]="selectedTurnSide === 'Left'"  title="Left turn"  (click)="setTurnSide('Left')">L</button>
          <button class="dbtn" [class.on]="selectedTurnSide === 'Right'" title="Right turn" (click)="setTurnSide('Right')">R</button>
        </div>
      </div>

      <!-- Speed input (shown for conveyors) -->
      <div class="facing" *ngIf="isConveyor">
        <div class="facing-lbl">SPEED <span class="dim2">cell/s</span></div>
        <input class="speed-input" type="number"
               [value]="selectedSpeed"
               (change)="setSpeed(+$any($event.target).value)"
               min="0.1" max="10" step="0.1"/>
      </div>

      <div class="note">
        <span class="dim2">✕ = backend not yet implemented</span>
      </div>
    </div>
  `,
  styles: [`
    :host { display: contents; }
    .pal {
      width: 168px;
      background: var(--bg-1);
      border-right: 1px solid var(--border);
      display: flex;
      flex-direction: column;
      overflow: hidden;
      flex-shrink: 0;
      font-family: var(--mono);
    }
    .scroll { flex: 1; overflow-y: auto; }
    .scroll::-webkit-scrollbar { width: 3px; }
    .scroll::-webkit-scrollbar-track { background: var(--bg-1); }
    .scroll::-webkit-scrollbar-thumb { background: var(--border-bright); }
    .grp-head {
      display: flex;
      justify-content: space-between;
      padding: 5px 10px 3px;
      font-size: 8px;
      letter-spacing: .1em;
      color: var(--text-4);
      text-transform: uppercase;
      background: var(--bg-0);
      border-bottom: 1px solid var(--border);
      position: sticky; top: 0; z-index: 1;
    }
    .item {
      display: flex;
      align-items: center;
      gap: 7px;
      padding: 5px 8px;
      width: 100%;
      border: none;
      border-bottom: 1px solid var(--bg-2);
      background: transparent;
      color: var(--text-2);
      text-align: left;
      cursor: pointer;
      transition: background .1s;
    }
    .item:hover:not(.dead) { background: var(--bg-2); color: var(--text-0); }
    .item.sel { background: var(--bg-3); color: var(--amber); border-left: 2px solid var(--amber); padding-left: 6px; }
    .item.dead { opacity: .38; cursor: not-allowed; }
    .ico {
      width: 22px; height: 22px;
      border: 1px solid var(--border-bright);
      background: var(--bg-2);
      display: flex; align-items: center; justify-content: center;
      font-size: 11px;
      flex-shrink: 0;
    }
    .info { flex: 1; overflow: hidden; }
    .name { font-size: 10px; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
    .cost { font-size: 9px; color: var(--text-3); margin-top: 1px; }
    .x { color: var(--red); font-size: 9px; margin-left: 3px; }
    .hk { font-size: 8px; color: var(--text-4); flex-shrink: 0; }
    .facing {
      padding: 6px 10px;
      border-top: 1px solid var(--border);
      background: var(--bg-0);
    }
    .facing-lbl {
      font-size: 8px;
      color: var(--text-4);
      letter-spacing: .08em;
      margin-bottom: 5px;
    }
    .facing-btns { display: flex; gap: 3px; }
    .dbtn {
      flex: 1;
      padding: 5px 0;
      border: 1px solid var(--border-bright);
      background: transparent;
      color: var(--text-3);
      font-family: var(--mono);
      font-size: 10px;
      cursor: pointer;
      transition: all .1s;
    }
    .dbtn.on { color: var(--amber); border-color: var(--amber); background: rgba(245,166,35,.08); font-weight: 700; }
    .dbtn:hover:not(.on) { background: var(--bg-2); color: var(--text-1); }
    .speed-input {
      width: 100%;
      box-sizing: border-box;
      background: var(--bg-0);
      border: 1px solid var(--border-bright);
      color: var(--text-1);
      font-family: var(--mono);
      font-size: 10px;
      padding: 4px 6px;
      outline: none;
    }
    .speed-input:focus { border-color: var(--cyan); }
    .note {
      padding: 6px 10px;
      border-top: 1px solid var(--border);
      font-size: 9px;
      line-height: 1.5;
    }
  `],
})
export class PaletteComponent {
  @Input()  selectedId: string | null = null;
  @Output() itemSelect    = new EventEmitter<PaletteItem | null>();
  @Output() facingChange  = new EventEmitter<Direction>();
  @Output() turnSideChange = new EventEmitter<'Left' | 'Right'>();
  @Output() speedChange   = new EventEmitter<number>();

  readonly lib = COMPONENT_LIBRARY;
  readonly directions: Direction[] = ['North', 'East', 'South', 'West'];
  selectedFacing: Direction = 'North';
  selectedTurnSide: 'Left' | 'Right' = 'Right';
  selectedSpeed = 1;

  get selectedItem(): PaletteItem | null {
    if (!this.selectedId) return null;
    for (const g of this.lib) {
      const item = g.items.find(it => it.id === this.selectedId);
      if (item) return item;
    }
    return null;
  }

  get isConveyorTurn(): boolean {
    return this.selectedItem?.kind === 'conveyor_turn';
  }

  get isConveyor(): boolean {
    const kind = this.selectedItem?.kind;
    return kind === 'conveyor_oneway' || kind === 'conveyor_turn';
  }

  select(it: PaletteItem): void {
    const next = this.selectedId === it.id ? null : it;
    this.itemSelect.emit(next);
  }

  setFacing(d: Direction): void {
    this.selectedFacing = d;
    this.facingChange.emit(d);
  }

  setTurnSide(side: 'Left' | 'Right'): void {
    this.selectedTurnSide = side;
    this.turnSideChange.emit(side);
  }

  setSpeed(value: number): void {
    this.selectedSpeed = Math.max(0.1, Math.min(10, value));
    this.speedChange.emit(this.selectedSpeed);
  }
}
