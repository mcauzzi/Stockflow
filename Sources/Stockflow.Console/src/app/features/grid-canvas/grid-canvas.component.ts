import {
  Component, Input, Output, EventEmitter,
  ElementRef, ViewChild, OnChanges, AfterViewInit, OnDestroy, NgZone,
} from '@angular/core';
import { NgFor, NgIf, NgSwitch, NgSwitchCase, NgSwitchDefault } from '@angular/common';
import { ComponentState, EntityState, Direction } from '../../core/models/protocol';

const CELL = 28;
const ZOOM_MIN = 0.05;
const ZOOM_MAX = 8;

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

      <!-- Zoom controls + Minimap (bottom-right) -->
      <div class="ovl br">
        <div class="zoom-bar">
          <button class="zbtn" (click)="zoomStep(-1)" title="Zoom out">−</button>
          <span class="zoom-pct">{{ zoomPct }}</span>
          <button class="zbtn" (click)="zoomStep(1)" title="Zoom in">+</button>
          <button class="zbtn fit-btn" (click)="fitGrid()" title="Fit to grid">⊡</button>
        </div>
        <div class="ovl-info">MINIMAP</div>
        <svg [attr.viewBox]="'0 0 ' + cols + ' ' + rows" width="160" height="80" style="display:block">
          <rect [attr.width]="cols" [attr.height]="rows" fill="#0a0c0e"/>
          <rect *ngFor="let c of visibleComponents"
                [attr.x]="c.gridX" [attr.y]="c.gridY" width="1" height="1"
                [attr.fill]="kindColor(c.kind)"/>
        </svg>
        <div class="ovl-info hint">Scroll · Zoom &nbsp; Mid-drag · Pan</div>
      </div>

      <!-- Main SVG — no viewBox, pan/zoom via inner <g> transform -->
      <svg #svgEl
           width="100%" height="100%"
           [style.cursor]="panCursor"
           style="flex:1;display:block"
           (mousedown)="onMouseDown($event)"
           (mousemove)="onMouseMove($event)"
           (mouseleave)="onMouseLeave()"
           (mouseup)="onMouseUp()"
           (contextmenu)="$event.preventDefault()"
           (click)="onSvgClick()">

        <g [attr.transform]="canvasTransform">

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
                  <path [attr.d]="turnArcPath(c)" stroke="#22d3ee" stroke-width="1.5" fill="none"/>
                  <polygon [attr.points]="turnArrowPts(c)" fill="#22d3ee"/>
                  <circle cx="4" [attr.cy]="CELL/2" r="2" fill="#22d3ee" opacity="0.55"/>
                </g>
              </ng-container>

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

              <ng-container *ngSwitchCase="'package_exit'">
                <rect x="1" y="1" [attr.width]="CELL-2" [attr.height]="CELL-2"
                      fill="#1e0e0e"
                      [attr.stroke]="c.id === selectedId ? '#f5a623' : '#4a1e1e'"
                      [attr.stroke-width]="c.id === selectedId ? 1.5 : 1"/>
                <g [attr.transform]="'rotate('+facingRot(c.facing)+' '+CELL/2+' '+CELL/2+')'">
                  <line x1="3" [attr.y1]="CELL/2" x2="10" [attr.y2]="CELL/2" stroke="#f87171" stroke-width="1.2"/>
                  <polygon [attr.points]="arrowPtsExit()" fill="#f87171"/>
                </g>
                <text [attr.x]="CELL/2" [attr.y]="CELL-5"
                      font-size="5" fill="#f87171" font-family="JetBrains Mono,monospace"
                      text-anchor="middle" letter-spacing="0.03em" opacity="0.8">EXIT</text>
              </ng-container>

              <ng-container *ngSwitchCase="'merge'">
                <rect x="1" y="1" [attr.width]="CELL-2" [attr.height]="CELL-2"
                      fill="#0f1e28"
                      [attr.stroke]="c.id === selectedId ? '#f5a623' : '#0e7490'"
                      [attr.stroke-width]="c.id === selectedId ? 1.5 : 1"/>
                <g [attr.transform]="'rotate('+facingRot(c.facing)+' '+CELL/2+' '+CELL/2+')'">
                  <line x1="3" [attr.y1]="CELL/2" [attr.x2]="CELL/2-1" [attr.y2]="CELL/2"
                        stroke="#38bdf8" stroke-width="1.2"/>
                  <line [attr.x1]="CELL/2" y1="3" [attr.x2]="CELL/2" [attr.y2]="CELL/2-1"
                        stroke="#38bdf8" stroke-width="1.2" opacity="0.65"/>
                  <line [attr.x1]="CELL/2+2" [attr.y1]="CELL/2" [attr.x2]="CELL-7" [attr.y2]="CELL/2"
                        stroke="#38bdf8" stroke-width="1.2"/>
                  <polygon [attr.points]="arrowPtsMerge()" fill="#38bdf8"/>
                  <circle [attr.cx]="CELL/2" [attr.cy]="CELL/2" r="1.8" fill="#38bdf8" opacity="0.9"/>
                </g>
                <text [attr.x]="CELL/2" y="7"
                      font-size="5" fill="#38bdf8" font-family="JetBrains Mono,monospace"
                      text-anchor="middle" opacity="0.7">MRG</text>
              </ng-container>

              <ng-container *ngSwitchCase="'diverter'">
                <rect x="1" y="1" [attr.width]="CELL-2" [attr.height]="CELL-2"
                      fill="#120d1e"
                      [attr.stroke]="c.id === selectedId ? '#f5a623' : '#6d28d9'"
                      [attr.stroke-width]="c.id === selectedId ? 1.5 : 1"/>
                <g [attr.transform]="'rotate('+facingRot(c.facing)+' '+CELL/2+' '+CELL/2+')'">
                  <line x1="3" [attr.y1]="CELL/2" [attr.x2]="CELL/2-1" [attr.y2]="CELL/2"
                        stroke="#a78bfa" stroke-width="1.2"/>
                  <line [attr.x1]="CELL/2+2" [attr.y1]="CELL/2" [attr.x2]="CELL-7" [attr.y2]="CELL/2"
                        stroke="#a78bfa" stroke-width="1.2"/>
                  <line [attr.x1]="CELL/2" [attr.y1]="CELL/2+2" [attr.x2]="CELL/2" [attr.y2]="CELL-7"
                        stroke="#a78bfa" stroke-width="1.2" opacity="0.65"/>
                  <polygon [attr.points]="arrowPtsMerge()" fill="#a78bfa"/>
                  <polygon [attr.points]="arrowPtsDivertSide()" fill="#a78bfa"/>
                  <circle [attr.cx]="CELL/2" [attr.cy]="CELL/2" r="1.8" fill="#a78bfa" opacity="0.9"/>
                </g>
                <text [attr.x]="CELL/2" y="7"
                      font-size="5" fill="#a78bfa" font-family="JetBrains Mono,monospace"
                      text-anchor="middle" opacity="0.7">DVT</text>
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
          <rect *ngIf="hover && !activeTool"
                [attr.x]="hover.x*CELL" [attr.y]="hover.y*CELL"
                [attr.width]="CELL" [attr.height]="CELL"
                fill="rgba(245,166,35,.07)" stroke="#f5a623" stroke-width="0.5"
                pointer-events="none"/>

          <!-- Placement preview -->
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

        </g>
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
    .hint { color: var(--text-4); font-size: 8px; margin-top: 4px; margin-bottom: 0; }
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
    .zoom-bar {
      display: flex;
      align-items: center;
      gap: 4px;
      margin-bottom: 5px;
    }
    .zbtn {
      width: 20px; height: 20px;
      border: 1px solid #1e2832;
      background: #0f1214;
      color: var(--text-2);
      font-family: var(--mono);
      font-size: 13px;
      display: flex; align-items: center; justify-content: center;
      cursor: pointer;
      transition: all .1s;
      line-height: 1;
      padding: 0;
    }
    .zbtn:hover { border-color: var(--amber-dim); color: var(--amber); }
    .fit-btn { font-size: 11px; }
    .zoom-pct {
      font-size: 9px;
      color: var(--text-3);
      min-width: 36px;
      text-align: center;
      font-family: var(--mono);
    }
  `],
})
export class GridCanvasComponent implements OnChanges, AfterViewInit, OnDestroy {
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

  zoom = 1;
  panX = 0;
  panY = 0;
  private isPanning = false;
  private didPan = false;
  private panStart = { x: 0, y: 0, panX: 0, panY: 0 };
  private wheelListener!: (e: WheelEvent) => void;

  get svgW() { return this.cols * CELL; }
  get svgH() { return this.rows * CELL; }
  get componentCount() { return this.components.size; }
  get entityCount()    { return this.entities.size; }
  get canvasTransform() { return `translate(${this.panX},${this.panY}) scale(${this.zoom})`; }
  get zoomPct() { return Math.round(this.zoom * 100) + '%'; }

  get panCursor(): string {
    if (this.isPanning && this.didPan) return 'grabbing';
    return this.activeTool ? 'cell' : 'crosshair';
  }

  get toolColor(): string {
    if (!this.activeTool) return '#f5a623';
    return this.activeTool.kind === 'package_generator' ? '#4ade80'
         : this.activeTool.kind === 'package_exit'      ? '#f87171'
         : this.activeTool.kind === 'merge'             ? '#38bdf8'
         : '#f5a623';
  }

  get toolPreviewFill(): string {
    if (!this.activeTool) return 'rgba(245,166,35,.07)';
    return this.activeTool.kind === 'package_generator' ? 'rgba(74,222,128,.15)'
         : this.activeTool.kind === 'package_exit'      ? 'rgba(248,113,113,.15)'
         : this.activeTool.kind === 'merge'             ? 'rgba(56,189,248,.15)'
         : 'rgba(245,166,35,.07)';
  }

  get toolLabel(): string { return this.activeTool?.name.toUpperCase() ?? ''; }
  get toolSym(): string   { return this.activeTool?.sym ?? ''; }

  visibleComponents: ComponentState[] = [];
  visibleEntities:   EntityState[]    = [];
  colLabels: number[] = [];

  constructor(private ngZone: NgZone) {}

  ngOnChanges(): void {
    this.visibleComponents = [...this.components.values()];
    this.visibleEntities   = [...this.entities.values()];
    this.colLabels = Array.from({ length: Math.floor(this.cols / 5) }, (_, i) => i);
  }

  ngAfterViewInit(): void {
    this.wheelListener = (e: WheelEvent) => {
      e.preventDefault();
      this.ngZone.run(() => {
        const svg = this.svgEl?.nativeElement;
        if (!svg) return;
        const rect = svg.getBoundingClientRect();
        this.applyZoom(e.deltaY > 0 ? 0.85 : 1 / 0.85, e.clientX - rect.left, e.clientY - rect.top);
      });
    };
    this.svgEl.nativeElement.addEventListener('wheel', this.wheelListener, { passive: false });
    setTimeout(() => this.fitGrid(), 0);
  }

  ngOnDestroy(): void {
    if (this.svgEl?.nativeElement && this.wheelListener) {
      this.svgEl.nativeElement.removeEventListener('wheel', this.wheelListener);
    }
  }

  fitGrid(): void {
    const svg = this.svgEl?.nativeElement;
    if (!svg) return;
    const rect = svg.getBoundingClientRect();
    if (rect.width === 0 || rect.height === 0) return;
    const newZoom = Math.min(rect.width / this.svgW, rect.height / this.svgH) * 0.92;
    this.zoom = Math.max(ZOOM_MIN, Math.min(ZOOM_MAX, newZoom));
    this.panX = (rect.width  - this.svgW * this.zoom) / 2;
    this.panY = (rect.height - this.svgH * this.zoom) / 2;
  }

  zoomStep(dir: 1 | -1): void {
    const svg = this.svgEl?.nativeElement;
    if (!svg) return;
    const rect = svg.getBoundingClientRect();
    this.applyZoom(dir > 0 ? 1.25 : 0.8, rect.width / 2, rect.height / 2);
  }

  private applyZoom(factor: number, mx: number, my: number): void {
    const newZoom = Math.max(ZOOM_MIN, Math.min(ZOOM_MAX, this.zoom * factor));
    this.panX = mx - (mx - this.panX) * (newZoom / this.zoom);
    this.panY = my - (my - this.panY) * (newZoom / this.zoom);
    this.zoom = newZoom;
  }

  onMouseDown(e: MouseEvent): void {
    if (e.button === 1) {
      e.preventDefault();
      this.isPanning = true;
      this.didPan = false;
      this.panStart = { x: e.clientX, y: e.clientY, panX: this.panX, panY: this.panY };
    }
  }

  onMouseMove(e: MouseEvent): void {
    if (this.isPanning) {
      const dx = e.clientX - this.panStart.x;
      const dy = e.clientY - this.panStart.y;
      if (Math.abs(dx) > 2 || Math.abs(dy) > 2) this.didPan = true;
      this.panX = this.panStart.panX + dx;
      this.panY = this.panStart.panY + dy;
    }
    const svg = this.svgEl?.nativeElement;
    if (!svg) return;
    const rect = svg.getBoundingClientRect();
    const x = (e.clientX - rect.left  - this.panX) / this.zoom;
    const y = (e.clientY - rect.top   - this.panY) / this.zoom;
    const cx = Math.floor(x / CELL);
    const cy = Math.floor(y / CELL);
    const inBounds = cx >= 0 && cx < this.cols && cy >= 0 && cy < this.rows;
    if (this.hover?.x !== cx || this.hover?.y !== cy || !inBounds) {
      this.hover = inBounds ? { x: cx, y: cy } : null;
    }
  }

  onMouseUp(): void {
    this.isPanning = false;
  }

  onMouseLeave(): void {
    this.hover = null;
    this.isPanning = false;
  }

  onSvgClick(): void {
    if (this.consumePan()) return;
    if (this.activeTool && this.hover) {
      this.cellClick.emit({ x: this.hover.x, y: this.hover.y });
    } else {
      this.componentSelect.emit(null);
    }
  }

  selectComponent(c: ComponentState): void {
    if (this.consumePan()) return;
    if (!this.activeTool) this.componentSelect.emit(c);
  }

  private consumePan(): boolean {
    if (!this.didPan) return false;
    this.didPan = false;
    return true;
  }

  facingRot(f: Direction): number {
    return { East: 0, South: 90, West: 180, North: 270 }[f] ?? 0;
  }

  arrowPts(): string {
    const x2 = CELL - 5, y = CELL / 2;
    return `${x2-5},${y-3} ${x2},${y} ${x2-5},${y+3}`;
  }

  trackById(_: number, c: ComponentState): number { return c.id; }

  arrowPtsExit(): string {
    const x = 13, y = CELL / 2;
    return `${x-4},${y-3} ${x},${y} ${x-4},${y+3}`;
  }

  turnArcPath(c: ComponentState): string {
    const r = CELL / 2 - 4;
    const isLeft = c.properties?.['turn'] === 'left';
    if (isLeft) {
      return `M 4 ${CELL / 2} A ${r} ${r} 0 0 0 ${CELL / 2} 4`;
    }
    return `M 4 ${CELL / 2} A ${r} ${r} 0 0 1 ${CELL / 2} ${CELL - 4}`;
  }

  turnArrowPts(c: ComponentState): string {
    const isLeft = c.properties?.['turn'] === 'left';
    const cx = CELL / 2;
    if (isLeft) {
      return `${cx - 3},7 ${cx},4 ${cx + 3},7`;
    }
    return `${cx - 3},${CELL - 7} ${cx},${CELL - 4} ${cx + 3},${CELL - 7}`;
  }

  arrowPtsGen(): string {
    const x2 = CELL - 6, y = CELL / 2;
    return `${x2-5},${y-3.5} ${x2},${y} ${x2-5},${y+3.5}`;
  }

  arrowPtsMerge(): string {
    const x2 = CELL - 5, y = CELL / 2;
    return `${x2-5},${y-3} ${x2},${y} ${x2-5},${y+3}`;
  }

  arrowPtsDivertSide(): string {
    const x = CELL / 2, y2 = CELL - 5;
    return `${x-3},${y2-5} ${x},${y2} ${x+3},${y2-5}`;
  }

  kindColor(kind: string): string {
    return kind === 'conveyor_oneway'   ? '#4ade80'
         : kind === 'conveyor_turn'     ? '#22d3ee'
         : kind === 'package_generator' ? '#86efac'
         : kind === 'package_exit'      ? '#fca5a5'
         : kind === 'merge'             ? '#38bdf8'
         : kind === 'diverter'          ? '#a78bfa'
         : '#3d4652';
  }

  pad(n: number): string { return String(n).padStart(2, '0'); }
}
