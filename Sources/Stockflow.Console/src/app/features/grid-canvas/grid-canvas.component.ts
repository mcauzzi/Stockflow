import {
  Component, Input, Output, EventEmitter,
  ElementRef, ViewChild, AfterViewInit, OnChanges,
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
    <div class="canvas-wrap">

      <!-- Floor selector -->
      <div class="canvas-overlay tl">
        <div>WAREHOUSE · SANDBOX-01 · {{ cols }}×{{ rows }} CELLS</div>
        <div class="dim2">CELL 1.00 × 1.00 m</div>
        <div style="margin-top:8px;display:flex;gap:0;border:1px solid var(--border-bright);background:var(--bg-1);">
          <button *ngFor="let f of floors"
                  (click)="floor = f.id"
                  [style.background]="floor === f.id ? 'var(--amber)' : 'var(--bg-1)'"
                  [style.color]="floor === f.id ? 'var(--bg-0)' : 'var(--text-2)'"
                  style="padding:6px 10px;border:none;border-right:1px solid var(--border);
                         font-family:var(--mono);font-size:10px;letter-spacing:.08em;
                         display:flex;flex-direction:column;align-items:flex-start;gap:1px;min-width:78px;cursor:pointer">
            <span style="font-weight:700">{{ f.label }} · {{ f.name }}</span>
            <span style="font-size:9px;opacity:.75">{{ f.h.toFixed(1) }}m · L{{ f.id }}</span>
          </button>
        </div>
      </div>

      <!-- Top-right cursor info -->
      <div class="canvas-overlay tr">
        <div>FLOOR <span class="amber">L{{ floor }}</span>
          · CURSOR {{ hover ? '(' + pad(hover.x) + ',' + pad(hover.y) + ')' : '(--,--)' }}</div>
        <div class="dim2">{{ componentCount }} components · {{ entityCount }} entities</div>
      </div>

      <!-- Overlay toggles -->
      <div class="canvas-overlay bl">
        <div class="overlay-toggle" style="background:var(--bg-2);cursor:default;color:var(--text-2)">OVERLAYS</div>
        <div class="overlay-toggle" [class.active]="showEntities" (click)="showEntities = !showEntities">
          <span class="bullet"></span>Flow entities
        </div>
        <div class="overlay-toggle" [class.active]="showHeat" (click)="showHeat = !showHeat">
          <span class="bullet"></span>Util heatmap
        </div>
      </div>

      <!-- Minimap -->
      <div class="canvas-overlay br">
        <div style="font-family:var(--mono);font-size:10px;color:var(--text-3);letter-spacing:.08em;
                    text-transform:uppercase;margin-bottom:4px">MINIMAP</div>
        <svg [attr.viewBox]="'0 0 ' + cols + ' ' + rows" width="180" height="90">
          <rect [attr.width]="cols" [attr.height]="rows" fill="#0a0c0e"/>
          <rect *ngFor="let c of visibleComponents"
                [attr.x]="c.gridX" [attr.y]="c.gridY" width="1" height="1"
                [attr.fill]="kindColor(c.kind)"/>
        </svg>
      </div>

      <!-- Main SVG grid -->
      <svg #svgEl
           [attr.viewBox]="viewBox"
           preserveAspectRatio="xMidYMid meet"
           style="width:100%;height:100%;display:block;cursor:crosshair"
           (mousemove)="onMouseMove($event)"
           (mouseleave)="hover = null"
           (click)="onGridClick()">

        <!-- Background -->
        <defs>
          <pattern id="gridDot" [attr.width]="CELL" [attr.height]="CELL" patternUnits="userSpaceOnUse">
            <circle cx="0.5" cy="0.5" r="0.6" fill="#232932"/>
          </pattern>
          <pattern id="gridMajor" [attr.width]="CELL*4" [attr.height]="CELL*4" patternUnits="userSpaceOnUse">
            <path [attr.d]="'M ' + CELL*4 + ' 0 L 0 0 0 ' + CELL*4" fill="none" stroke="#1a1f26" stroke-width="0.5"/>
          </pattern>
        </defs>
        <rect [attr.width]="svgW" [attr.height]="svgH" fill="#0a0c0e"/>
        <rect [attr.width]="svgW" [attr.height]="svgH" fill="url(#gridMajor)"/>
        <rect [attr.width]="svgW" [attr.height]="svgH" fill="url(#gridDot)"/>

        <!-- Components -->
        <g *ngFor="let c of visibleComponents"
           [attr.transform]="'translate(' + c.gridX*CELL + ',' + c.gridY*CELL + ')'"
           (click)="selectComponent(c); $event.stopPropagation()">
          <ng-container [ngSwitch]="c.kind">

            <!-- ONE-WAY CONVEYOR -->
            <ng-container *ngSwitchCase="'conveyor_oneway'">
              <rect x="1" y="1" [attr.width]="CELL-2" [attr.height]="CELL-2"
                    fill="#2c333d"
                    [attr.stroke]="c.id === selectedId ? '#f5a623' : '#3d4652'"
                    [attr.stroke-width]="c.id === selectedId ? 1.5 : 1"/>
              <g [attr.transform]="'rotate(' + facingRot(c.facing) + ' ' + CELL/2 + ' ' + CELL/2 + ')'">
                <line x1="3" [attr.y1]="CELL/2" [attr.x2]="CELL-3" [attr.y2]="CELL/2" stroke="#5a6472" stroke-width="1"/>
                <polygon [attr.points]="arrowPts()" fill="#9ba3af"/>
                <line *ngFor="let x of [7,14,21]" [attr.x1]="x" [attr.y1]="CELL/2-4" [attr.x2]="x" [attr.y2]="CELL/2+4" stroke="#3d4652" stroke-width="0.5"/>
              </g>
            </ng-container>

            <!-- CONVEYOR TURN -->
            <ng-container *ngSwitchCase="'conveyor_turn'">
              <rect x="1" y="1" [attr.width]="CELL-2" [attr.height]="CELL-2"
                    fill="#2c333d"
                    [attr.stroke]="c.id === selectedId ? '#f5a623' : '#3d4652'"
                    [attr.stroke-width]="c.id === selectedId ? 1.5 : 1"/>
              <g [attr.transform]="'rotate(' + facingRot(c.facing) + ' ' + CELL/2 + ' ' + CELL/2 + ')'">
                <path [attr.d]="'M ' + CELL/2 + ' ' + (CELL-3) + ' Q ' + CELL/2 + ' ' + CELL/2 + ' ' + (CELL-3) + ' ' + CELL/2"
                      stroke="#5a6472" stroke-width="1" fill="none"/>
                <polygon [attr.points]="arrowPts()" fill="#9ba3af"/>
              </g>
            </ng-container>

            <!-- GENERIC / UNKNOWN -->
            <ng-container *ngSwitchDefault>
              <rect x="1" y="1" [attr.width]="CELL-2" [attr.height]="CELL-2"
                    fill="#1a1f26"
                    [attr.stroke]="c.id === selectedId ? '#f5a623' : '#3d4652'"
                    stroke-width="1"/>
              <text [attr.x]="CELL/2" [attr.y]="CELL/2+3"
                    font-size="7" fill="#5a6472" font-family="JetBrains Mono" text-anchor="middle">
                {{ c.kind.slice(0,3).toUpperCase() }}
              </text>
            </ng-container>

          </ng-container>
        </g>

        <!-- Entity pips -->
        <g *ngIf="showEntities">
          <g *ngFor="let e of visibleEntities">
            <rect [attr.x]="e.position.x * CELL - 3"
                  [attr.y]="e.position.y * CELL - 3"
                  width="6" height="6"
                  [attr.fill]="e.status === 'Queued' ? '#ef4444' : '#f5a623'"
                  stroke="#0a0c0e" stroke-width="0.5"/>
          </g>
        </g>

        <!-- Hover cell -->
        <rect *ngIf="hover"
              [attr.x]="hover.x * CELL" [attr.y]="hover.y * CELL"
              [attr.width]="CELL" [attr.height]="CELL"
              class="cell-hover" pointer-events="none"/>

        <!-- Axis labels -->
        <g font-family="JetBrains Mono" font-size="8" fill="#3d4652">
          <text *ngFor="let i of colLabels" [attr.x]="i*4*CELL+2" y="10">{{ pad(i*4) }}</text>
        </g>

      </svg>
    </div>
  `,
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
  visibleEntities: EntityState[] = [];
  colLabels: number[] = [];

  ngOnChanges(): void {
    this.visibleComponents = [...this.components.values()];
    this.visibleEntities   = [...this.entities.values()];
    this.colLabels = Array.from({ length: Math.floor(this.cols / 4) }, (_, i) => i);
  }

  onMouseMove(e: MouseEvent): void {
    const svg = this.svgEl?.nativeElement;
    if (!svg) return;
    const rect = svg.getBoundingClientRect();
    const vx = (e.clientX - rect.left) * (this.svgW / rect.width);
    const vy = (e.clientY - rect.top)  * (this.svgH / rect.height);
    const cx = Math.floor(vx / CELL);
    const cy = Math.floor(vy / CELL);
    if (cx >= 0 && cx < this.cols && cy >= 0 && cy < this.rows)
      this.hover = { x: cx, y: cy };
    else
      this.hover = null;
  }

  onGridClick(): void {
    this.componentSelect.emit(null);
  }

  selectComponent(c: ComponentState): void {
    this.componentSelect.emit(c);
  }

  facingRot(f: Direction): number {
    return { East: 0, South: 90, West: 180, North: 270 }[f] ?? 0;
  }

  arrowPts(): string {
    const x2 = CELL - 3, y = CELL / 2;
    return `${x2-4},${y-3} ${x2},${y} ${x2-4},${y+3}`;
  }

  kindColor(kind: string): string {
    return kind === 'conveyor_oneway' ? '#4ade80'
         : kind === 'conveyor_turn'   ? '#22d3ee'
         : '#3d4652';
  }

  pad(n: number): string { return String(n).padStart(2, '0'); }
}
