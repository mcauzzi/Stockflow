import { Component, OnInit, signal, computed, effect } from '@angular/core';
import { NgIf } from '@angular/common';

import { SimStateService } from './core/services/sim-state.service';
import { Direction, SimSpeed } from './core/models/protocol';

import { Tab, TopbarComponent } from './features/topbar/topbar.component';
import { KpiStripComponent } from './features/kpi-strip/kpi-strip.component';
import { PaletteComponent, PaletteItem } from './features/palette/palette.component';
import { GridCanvasComponent } from './features/grid-canvas/grid-canvas.component';
import { InspectorComponent } from './features/inspector/inspector.component';
import { EventLogComponent } from './features/event-log/event-log.component';
import { OrdersViewComponent } from './features/orders-view/orders-view.component';
import { MetricsViewComponent } from './features/metrics-view/metrics-view.component';
import { StubViewComponent } from './features/stub-view/stub-view.component';
import { ComponentState } from './core/models/protocol';
import { genSpark } from './core/mock/sim-mock';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [
    NgIf,
    TopbarComponent, KpiStripComponent,
    PaletteComponent, GridCanvasComponent, InspectorComponent,
    EventLogComponent,
    OrdersViewComponent, MetricsViewComponent, StubViewComponent,
  ],
  templateUrl: './app.html',
  styleUrl: './app.scss',
})
export class App implements OnInit {
  readonly activeTab      = signal<Tab>('OPERATE');
  readonly selectedComp   = signal<ComponentState | null>(null);
  readonly selectedTool   = signal<PaletteItem | null>(null);
  readonly placeFacing    = signal<Direction>('North');
  readonly placeTurnSide  = signal<'Left' | 'Right'>('Right');
  readonly paused         = signal(false);
  readonly currentSpeed   = signal<SimSpeed>(1);

  // Seed sparklines with flat lines until real data arrives
  readonly sparks = signal({
    throughput:  genSpark(0, 0),
    saturation:  genSpark(0, 0),
    fulfillment: genSpark(0, 0),
  });

  constructor(readonly sim: SimStateService) {
    // Update sparklines whenever metrics arrive
    effect(() => {
      const s = this.sim.sparks();
      if (s.throughput.some(v => v > 0)) this.sparks.set(s);
    });
  }

  ngOnInit(): void {}

  get showMainGrid(): boolean {
    return this.activeTab() === 'OPERATE' || this.activeTab() === 'LAYOUT';
  }

  get showBottom(): boolean { return this.activeTab() === 'OPERATE'; }

  onTabChange(tab: Tab): void { this.activeTab.set(tab); }

  onSpeedChange(speed: SimSpeed): void {
    this.currentSpeed.set(speed);
    this.paused.set(speed === 0);
    this.sim.setSpeed(speed);
  }

  onPauseToggle(): void {
    const next = !this.paused();
    this.paused.set(next);
    this.sim.setSpeed(next ? 0 : this.currentSpeed() || 1);
  }

  onComponentSelect(c: ComponentState | null): void {
    this.selectedComp.set(c);
  }

  onToolSelect(item: PaletteItem | null): void {
    this.selectedTool.set(item);
    if (item) this.selectedComp.set(null);
  }

  onFacingChange(dir: Direction): void {
    this.placeFacing.set(dir);
  }

  onTurnSideChange(side: 'Left' | 'Right'): void {
    this.placeTurnSide.set(side);
  }

  onCellClick(cell: { x: number; y: number }): void {
    const tool = this.selectedTool();
    if (!tool) return;
    const params = tool.kind === 'conveyor_turn'
      ? { turn: this.placeTurnSide() }
      : undefined;
    this.sim.placeComponent(tool.kind, cell.x, cell.y, this.placeFacing(), params);
    this.selectedTool.set(null);
  }
}
