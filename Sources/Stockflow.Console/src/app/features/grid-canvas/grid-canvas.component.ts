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

      <!-- Active tool banner -->
      <div class="ovl tool-banner" *ngIf="activeTool">
        <span class="tool-sym" [style.color]="toolColor">{{ toolSym }}</span>
        PLACING <span [style.color]="toolColor">{{ toolLabel }}</span>
        &nbsp;· Click cell to place &nbsp; <span class="esc-hint">ESC to cancel</span>
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
           preserveAspectRatio="none"
           [style.cursor]="activeTool ? 'cell' : 'crosshair'"
           style="flex:1;width:100%;display:block"
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
        <g *ngFor="let c of visibleComponents; trackBy: trackById"
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
                    fill="#151e2a"
                    [attr.stroke]="c.id === selectedId ? '#f5a623' : '#2a3a4a'"
                    [attr.stroke-width]="c.id === selectedId ? 1.5 : 1"/>
              <g [attr.transform]="'rotate('+facingRot(c.facing)+' '+CELL/2+' '+CELL/2+')'">
                <!-- Quarter-circle arc: entry from left (West), exit to bottom (South) for right turn -->
                <!--                    entry from left (West), exit to top   (North) for left turn  -->
                <path [attr.d]="turnArcPath(c)" stroke="#22d3ee" stroke-width="1.5" fill="none"/>
                <!-- Exit arrowhead -->
                <polygon [attr.points]="turnArrowPts(c)" fill="#22d3ee"/>
                <!-- Entry dot at left-center -->
                <circle cx="4" [attr.cy]="CELL/2" r="2" fill="#22d3ee" opacity="0.55"/>
              </g>
            </ng-container>

            <!-- Package Generator: green square with direction arrow -->
            <ng-container *ngSwitchCase="'package_generator'">
              <rect x="1" y="1" [attr.width]="CELL-2" [attr.height]="CELL-2"
                    fill="#0f2018"
                    [attr.stroke]="c.id === selectedId ? '#f5a623' : '#1e4a2e'"
                    [attr.stroke-width]="c.id === selectedId ? 1.5 : 1"/>
              <g [attr.transform]="'rotate('+facingRot(c.facing)+' '+CELL/2+' '+CELL/2+')'">
                <line x1="5" [attr.y1]="CELL/2" [attr.x2]="CELL-7" [attr.y2]="CELL/2" stroke="#4ade80" stroke-width="1.5"/>
                <polygon [attr.points]="arrowPtsGen()" fill="#4ade80"/>
              </g>
              <text [attr.x]="CELL/2" [attr.y]="CELL-5"
                    font-size="5" fill="#4ade80" font-family="JetBrains Mono,monospace"
                    text-anchor="middle" opacity="0.8">GEN</text>
            </ng-container>

            <!-- Package Exit: coral square with input-side arrow and EXIT label -->
            <ng-container *ngSwitchCase="'package_exit'">
              <rect x="1" y="1" [attr.width]="CELL-2" [attr.height]="CELL-2"
                    fill="#1e0e0e"
                    [attr.stroke]="c.id === selectedId ? '#f5a623' : '#4a1e1e'"
                    [attr.stroke-width]="c.id === selectedId ? 1.5 : 1"/>
              <!-- Arrow enters from the input side (Facing.Opposite) toward center -->
              <g [attr.transform]="'rotate('+facingRot(c.facing)+' '+CELL/2+' '+CELL/2+')'">
                <line x1="3" [attr.y1]="CELL/2" x2="10" [attr.y2]="CELL/2" stroke="#f87171" stroke-width="1.2"/>
                <polygon [attr.points]="arrowPtsExit()" fill="#f87171"/>
              </g>
              <text [attr.x]="CELL/2" [attr.y]="CELL-5"
                    font-size="5" fill="#f87171" font-family="JetBrains Mono,monospace"
                    text-anchor="middle" letter-spacing="0.03em" opacity="0.8">EXIT</text>
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

        <!-- Hover highlight / placement preview -->
        <rect *ngIf="hover && !activeTool"
              [attr.x]="hover.x*CELL" [attr.y]="hover.y*CELL"
              [attr.width]="CELL" [attr.height]="CELL"
              fill="rgba(245,166,35,.07)" stroke="#f5a623" stroke-width="0.5"
              pointer-events="none"/>

        <rect *ngIf="hover && activeTool"
              [attr.x]="hover.x*CELL" [attr.y]="hover.y*CELL"
              [attr.width]="CELL" [attr.height]="CELL"
              [attr.fill]="toolPreviewFill"
              [attr.stroke]="toolColor"
              stroke-width="1.5"
              stroke-dasharray="3 2"
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
    .tool-banner {
      top: 50%; left: 50%;
      transform: translate(-50%, -50%);
      pointer-events: none;
      padding: 8px 16px;
      font-size: 10px;
      letter-spacing: .06em;
      white-space: nowrap;
    }
    .tool-sym { font-size: 14px; margin-right: 4px; }
    .esc-hint { color: var(--text-4); }
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
  @Input() components  = new Map<number, ComponentState>();
  @Input() entities    = new Map<number, EntityState>();
  @Input() selectedId: number | null = null;
  @Input() cols = 50;
  @Input() rows = 50;
  @Input() activeTool: { id: string; kind: string; name: string; sym: string } | null = null;
  @Output() componentSelect = new EventEmitter<ComponentState | null>();
  @Output() cellClick       = new EventEmitter<{ x: number; y: number }>();

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

  get toolColor(): string {
    if (!this.activeTool) return '#f5a623';
    return this.activeTool.kind === 'package_generator' ? '#4ade80'
         : this.activeTool.kind === 'package_exit'      ? '#f87171'
         : '#f5a623';
  }

  get toolPreviewFill(): string {
    if (!this.activeTool) return 'rgba(245,166,35,.07)';
    return this.activeTool.kind === 'package_generator' ? 'rgba(74,222,128,.15)'
         : this.activeTool.kind === 'package_exit'      ? 'rgba(248,113,113,.15)'
         : 'rgba(245,166,35,.07)';
  }

  get toolLabel(): string { return this.activeTool?.name.toUpperCase() ?? ''; }
  get toolSym(): string   { return this.activeTool?.sym ?? ''; }

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
    const ctm = svg.getScreenCTM();
    if (!ctm) return;
    const pt = svg.createSVGPoint();
    pt.x = e.clientX;
    pt.y = e.clientY;
    const { x, y } = pt.matrixTransform(ctm.inverse());
    const cx = Math.floor(x / CELL);
    const cy = Math.floor(y / CELL);
    this.hover = (cx >= 0 && cx < this.cols && cy >= 0 && cy < this.rows)
      ? { x: cx, y: cy } : null;
  }

  onGridClick(): void {
    if (this.activeTool && this.hover) {
      this.cellClick.emit({ x: this.hover.x, y: this.hover.y });
    } else {
      this.componentSelect.emit(null);
    }
  }

  selectComponent(c: ComponentState): void {
    if (!this.activeTool) this.componentSelect.emit(c);
  }

  // Screen convention: North = Y-1 (up in SVG), East = X+1 (right).
  // Base arrow points East (0°); rotate to match compass direction on screen.
  facingRot(f: Direction): number {
    return { East: 0, South: 90, West: 180, North: 270 }[f] ?? 0;
  }

  arrowPts(): string {
    const x2 = CELL - 5, y = CELL / 2;
    return `${x2-5},${y-3} ${x2},${y} ${x2-5},${y+3}`;
  }

  trackById(_: number, c: ComponentState): number { return c.id; }

  // Arrowhead pointing right at x=13, centered vertically — used for PackageExit input indicator
  arrowPtsExit(): string {
    const x = 13, y = CELL / 2;
    return `${x-4},${y-3} ${x},${y} ${x-4},${y+3}`;
  }

  // Quarter-circle arc: radius = CELL/2 - 4 = 10 (for CELL=28)
  // Unrotated (facing=East, rotation=0°):
  //   entry at left-center (4, CELL/2), exit at bottom-center (CELL/2, CELL-4) → Right turn
  //   entry at left-center (4, CELL/2), exit at top-center    (CELL/2,  4)     → Left  turn
  turnArcPath(c: ComponentState): string {
    const r = CELL / 2 - 4;  // radius = 10
    const isLeft = c.properties?.['turn'] === 'left';
    if (isLeft) {
      // Counter-clockwise arc to top-center
      return `M 4 ${CELL / 2} A ${r} ${r} 0 0 0 ${CELL / 2} 4`;
    }
    // Clockwise arc to bottom-center (default: Right)
    return `M 4 ${CELL / 2} A ${r} ${r} 0 0 1 ${CELL / 2} ${CELL - 4}`;
  }

  turnArrowPts(c: ComponentState): string {
    const isLeft = c.properties?.['turn'] === 'left';
    const cx = CELL / 2;
    if (isLeft) {
      // Arrowhead pointing up at top-center (exit North)
      return `${cx - 3},7 ${cx},4 ${cx + 3},7`;
    }
    // Arrowhead pointing down at bottom-center (exit South, default Right)
    return `${cx - 3},${CELL - 7} ${cx},${CELL - 4} ${cx + 3},${CELL - 7}`;
  }

  arrowPtsGen(): string {
    const x2 = CELL - 6, y = CELL / 2;
    return `${x2-5},${y-3.5} ${x2},${y} ${x2-5},${y+3.5}`;
  }

  kindColor(kind: string): string {
    return kind === 'conveyor_oneway'   ? '#4ade80'
         : kind === 'conveyor_turn'     ? '#22d3ee'
         : kind === 'package_generator' ? '#86efac'
         : kind === 'package_exit'      ? '#fca5a5'
         : '#3d4652';
  }

  pad(n: number): string { return String(n).padStart(2, '0'); }
}
