import {
  Component, Input, Output, EventEmitter,
  ElementRef, ViewChild, OnChanges,
} from '@angular/core';
import { NgFor, NgIf, NgSwitch, NgSwitchCase, NgSwitchDefault } from '@angular/common';
import { ComponentState, EntityState, Direction } from '../../core/models/protocol';

const CELL = 28;

interface HoverCell { x: number; y: number; }

type Floor = 0 | 1 | 2;
const FLOORS = [
  { id: 0 as Floor, label: 'L0', name: 'GROUND',    h: 0.0 },
  { id: 1 as Floor, label: 'L1', name: 'MEZZANINE', h: 3.2 },
  { id: 2 as Floor, label: 'L2', name: 'SHUTTLE',   h: 4.8 },
];

@Component({
  selector: 'app-grid-canvas',
  standalone: true,
  imports: [NgFor, NgIf, NgSwitch, NgSwitchCase, NgSwitchDefault],
  template: `
    <div class="wrap">

      <!-- Floor / info overlay (top-left) -->
      <div class="ovl tl">
        <div class="ovl-info">WAREHOUSE · SANDBOX-01 · {{ cols }}×{{ rows }}</div>
        <div class="floor-btns">
          <button *ngFor="let f of floors"
                  class="fbtn" [class.on]="floor === f.id"
                  (click)="floor = f.id">
            {{ f.label }} · {{ f.name }}
          </button>
        </div>
      </div>

      <!-- Cursor info (top-right) -->
      <div class="ovl tr">
        <div class="ovl-info">
          FLOOR <span class="amber">L{{ floor }}</span>
          &nbsp;CURSOR {{ hover ? pad(hover.x)+','+pad(hover.y) : '--,--' }}
        </div>
        <div class="ovl-info dim2">{{ componentCount }} components · {{ entityCount }} entities</div>
      </div>

      <!-- Overlay toggles (bottom-left) -->
      <div class="ovl bl">
        <button class="tog" [class.on]="showEntities" (click)="showEntities = !showEntities">
          <span class="bul" [class.lit]="showEntities"></span>Flow entities
        </button>
        <button class="tog" [class.on]="showHeat" (click)="showHeat = !showHeat">
          <span class="bul" [class.lit]="showHeat"></span>Util heatmap
        </button>
      </div>

      <!-- Minimap (bottom-right) -->
      <div class="ovl br">
        <div class="ovl-info">MINIMAP</div>
        <svg [attr.viewBox]="'0 0 ' + cols + ' ' + rows" width="160" height="80" style="display:block">
          <rect [attr.width]="cols" [attr.height]="rows" fill="#0a0c0e"/>
          <rect *ngFor="let c of visibleComponents"
                [attr.x]="c.gridX" [attr.y]="c.gridY" width="1" height="1"
                [attr.fill]="kindColor(c.kind)"/>
        </svg>
      </div>

      <!-- Main SVG -->
      <svg #svgEl
           [attr.viewBox]="viewBox"
           preserveAspectRatio="xMinYMin slice"
           style="flex:1;width:100%;cursor:crosshair;display:block"
           (mousemove)="onMouseMove($event)"
           (mouseleave)="hover = null"
           (click)="onGridClick()">

        <defs>
          <pattern id="dot28" [attr.width]="CELL" [attr.height]="CELL" patternUnits="userSpaceOnUse">
            <circle cx="0" cy="0" r="0.8" fill="#1e2832"/>
          </pattern>
          <pattern id="maj28" [attr.width]="CELL*5" [attr.height]="CELL*5" patternUnits="userSpaceOnUse">
            <path [attr.d]="'M '+CELL*5+' 0 L 0 0 0 '+CELL*5" fill="none" stroke="#181d24" stroke-width="0.5"/>
          </pattern>
        </defs>

        <rect [attr.width]="svgW" [attr.height]="svgH" fill="#0c0f12"/>
        <rect [attr.width]="svgW" [attr.height]="svgH" fill="url(#maj28)"/>
        <rect [attr.width]="svgW" [attr.height]="svgH" fill="url(#dot28)"/>

        <!-- Components -->
        <g *ngFor="let c of visibleComponents"
           [attr.transform]="'translate('+c.gridX*CELL+','+c.gridY*CELL+')'"
           style="cursor:pointer"
           (click)="selectComponent(c); $event.stopPropagation()">
          <ng-container [ngSwitch]="c.kind">

            <ng-container *ngSwitchCase="'conveyor_oneway'">
              <rect x="1" y="1" [attr.width]="CELL-2" [attr.height]="CELL-2"
                    fill="#212830"
                    [attr.stroke]="c.id === selectedId ? '#f5a623' : '#2e3848'"
                    [attr.stroke-width]="c.id === selectedId ? 1.5 : 1"/>
              <g [attr.transform]="'rotate('+facingRot(c.facing)+' '+CELL/2+' '+CELL/2+')'">
                <line x1="4" [attr.y1]="CELL/2" [attr.x2]="CELL-6" [attr.y2]="CELL/2" stroke="#4a5668" stroke-width="1.2"/>
                <polygon [attr.points]="arrowPts()" fill="#8898aa"/>
                <line *ngFor="let x of tickXs" [attr.x1]="x" [attr.y1]="CELL/2-3" [attr.x2]="x" [attr.y2]="CELL/2+3" stroke="#2e3848" stroke-width="0.8"/>
              </g>
            </ng-container>

            <ng-container *ngSwitchCase="'conveyor_turn'">
              <rect x="1" y="1" [attr.width]="CELL-2" [attr.height]="CELL-2"
                    fill="#1e2830"
                    [attr.stroke]="c.id === selectedId ? '#f5a623' : '#2e3848'"
                    [attr.stroke-width]="c.id === selectedId ? 1.5 : 1"/>
              <g [attr.transform]="'rotate('+facingRot(c.facing)+' '+CELL/2+' '+CELL/2+')'">
                <path [attr.d]="'M '+CELL/2+' '+(CELL-4)+' Q '+CELL/2+' '+CELL/2+' '+(CELL-4)+' '+CELL/2"
                      stroke="#4a5668" stroke-width="1.2" fill="none"/>
                <polygon [attr.points]="arrowPtsTurn()" fill="#8898aa"/>
              </g>
            </ng-container>

            <ng-container *ngSwitchDefault>
              <rect x="1" y="1" [attr.width]="CELL-2" [attr.height]="CELL-2"
                    fill="#181d24"
                    [attr.stroke]="c.id === selectedId ? '#f5a623' : '#2e3848'"
                    stroke-width="1"/>
              <text [attr.x]="CELL/2" [attr.y]="CELL/2+3"
                    font-size="6" fill="#4a5668" font-family="JetBrains Mono,monospace" text-anchor="middle">
                {{ c.kind.slice(0,4).toUpperCase() }}
              </text>
            </ng-container>

          </ng-container>
        </g>

        <!-- Entity pips -->
        <g *ngIf="showEntities">
          <rect *ngFor="let e of visibleEntities"
                [attr.x]="e.position.x*CELL-3" [attr.y]="e.position.y*CELL-3"
                width="6" height="6"
                [attr.fill]="e.status === 'Queued' ? '#ef4444' : '#f5a623'"
                stroke="#0c0f12" stroke-width="0.5"/>
        </g>

        <!-- Hover highlight -->
        <rect *ngIf="hover"
              [attr.x]="hover.x*CELL" [attr.y]="hover.y*CELL"
              [attr.width]="CELL" [attr.height]="CELL"
              fill="rgba(245,166,35,.07)" stroke="#f5a623" stroke-width="0.5"
              pointer-events="none"/>

        <!-- Column labels -->
        <text *ngFor="let i of colLabels"
              [attr.x]="i*5*CELL+2" y="9"
              font-size="7" fill="#2e3848" font-family="JetBrains Mono,monospace">{{ pad(i*5) }}</text>

      </svg>
    </div>
  `,
  styles: [`
    :host { display: contents; }
    .wrap {
      flex: 1;
      background: #0c0f12;
      display: flex;
      flex-direction: column;
      overflow: hidden;
      position: relative;
      min-width: 0;
    }
    .ovl {
      position: absolute;
      z-index: 10;
      background: rgba(10,12,14,.88);
      border: 1px solid #1e2832;
      padding: 7px 10px;
      font-family: var(--mono);
      font-size: 9px;
      color: var(--text-3);
    }
    .tl { top: 8px; left: 8px; }
    .tr { top: 8px; right: 8px; text-align: right; }
    .bl { bottom: 8px; left: 8px; display: flex; flex-direction: column; gap: 3px; padding: 5px 8px; }
    .br { bottom: 8px; right: 8px; padding: 6px 8px; }
    .ovl-info { font-size: 9px; margin-bottom: 2px; white-space: nowrap; }
    .floor-btns { display: flex; gap: 0; margin-top: 5px; }
    .fbtn {
      padding: 4px 10px;
      border: 1px solid #1e2832;
      border-right: none;
      background: #0f1214;
      color: var(--text-3);
      font-family: var(--mono);
      font-size: 9px;
      letter-spacing: .05em;
      cursor: pointer;
      transition: all .12s;
    }
    .fbtn:last-child { border-right: 1px solid #1e2832; }
    .fbtn.on { background: var(--amber); color: #0a0c0e; border-color: var(--amber); font-weight: 600; }
    .tog {
      display: flex;
      align-items: center;
      gap: 6px;
      padding: 3px 6px;
      border: 1px solid #1e2832;
      background: transparent;
      color: var(--text-3);
      font-family: var(--mono);
      font-size: 8px;
      letter-spacing: .05em;
      cursor: pointer;
      transition: all .1s;
    }
    .tog.on { color: var(--text-1); border-color: #2a3540; }
    .bul { width: 6px; height: 6px; border-radius: 50%; background: #2e3848; flex-shrink: 0; }
    .bul.lit { background: var(--green); }
  `],
})
export class GridCanvasComponent implements OnChanges {
  @Input() components = new Map<number, ComponentState>();
  @Input() entities   = new Map<number, EntityState>();
  @Input() selectedId: number | null = null;
  @Input() cols = 50;
  @Input() rows = 50;
  @Output() componentSelect = new EventEmitter<ComponentState | null>();

  @ViewChild('svgEl') svgEl!: ElementRef<SVGSVGElement>;

  readonly CELL = CELL;
  readonly floors = FLOORS;
  readonly tickXs = [7, 14, 21];

  floor: Floor = 0;
  hover: HoverCell | null = null;
  showEntities = true;
  showHeat = true;

  get svgW() { return this.cols * CELL; }
  get svgH() { return this.rows * CELL; }
  get viewBox() { return `0 0 ${this.svgW} ${this.svgH}`; }
  get componentCount() { return this.components.size; }
  get entityCount()    { return this.entities.size; }

  visibleComponents: ComponentState[] = [];
  visibleEntities:   EntityState[]    = [];
  colLabels: number[] = [];

  ngOnChanges(): void {
    this.visibleComponents = [...this.components.values()];
    this.visibleEntities   = [...this.entities.values()];
    this.colLabels = Array.from({ length: Math.floor(this.cols / 5) }, (_, i) => i);
  }

  onMouseMove(e: MouseEvent): void {
    const svg = this.svgEl?.nativeElement;
    if (!svg) return;
    const rect = svg.getBoundingClientRect();
    const vx = (e.clientX - rect.left) * (this.svgW / rect.width);
    const vy = (e.clientY - rect.top)  * (this.svgH / rect.height);
    const cx = Math.floor(vx / CELL);
    const cy = Math.floor(vy / CELL);
    this.hover = (cx >= 0 && cx < this.cols && cy >= 0 && cy < this.rows)
      ? { x: cx, y: cy } : null;
  }

  onGridClick(): void { this.componentSelect.emit(null); }
  selectComponent(c: ComponentState): void { this.componentSelect.emit(c); }

  facingRot(f: Direction): number {
    return { East: 0, South: 90, West: 180, North: 270 }[f] ?? 0;
  }

  arrowPts(): string {
    const x2 = CELL - 5, y = CELL / 2;
    return `${x2-5},${y-3} ${x2},${y} ${x2-5},${y+3}`;
  }

  arrowPtsTurn(): string {
    const x = CELL - 4, y = CELL / 2;
    return `${x-4},${y-3} ${x},${y} ${x-4},${y+3}`;
  }

  kindColor(kind: string): string {
    return kind === 'conveyor_oneway' ? '#4ade80'
         : kind === 'conveyor_turn'   ? '#22d3ee'
         : '#3d4652';
  }

  pad(n: number): string { return String(n).padStart(2, '0'); }
}
