import { Component, Input, Output, EventEmitter, computed, signal } from '@angular/core';
import { NgFor, NgClass } from '@angular/common';
import { SparklineComponent } from '../../shared/sparkline.component';
import { MetricsSnapshot, SimSpeed, SIM_SPEEDS } from '../../core/models/protocol';

export interface KpiItem {
  label: string; unit: string;
  value: string; klass: string;
  spark: number[]; sparkColor: string;
  trend: string; trendDown: boolean;
}

@Component({
  selector: 'app-kpi-strip',
  standalone: true,
  imports: [NgFor, NgClass, SparklineComponent],
  template: `
    <div class="kpistrip">

      <!-- Sim Controls -->
      <div class="simctl">
        <div class="row" style="gap:8px">
          <button class="playpause" [class.paused]="paused"
                  (click)="togglePause()">
            {{ paused ? '▶' : '❚❚' }}
          </button>
          <div style="flex:1;display:flex;flex-direction:column;gap:2px">
            <div class="tick">SIM CLOCK <span class="v">{{ simClock }}</span></div>
            <div class="tick">SCALE <span class="v">{{ timeScale }}×</span></div>
          </div>
        </div>
        <div class="row">
          <button *ngFor="let s of speedBtns"
                  class="speed-btn"
                  [class.active]="!paused && currentSpeedId === s.id"
                  (click)="onSpeed(s.id)">{{ s.label }}</button>
        </div>
      </div>

      <!-- KPI tiles -->
      <div *ngFor="let k of kpis" class="kpi" [ngClass]="k.klass">
        <div class="label">
          <span>{{ k.label }}</span>
          <span class="trend" [class.down]="k.trendDown">{{ k.trend }}</span>
        </div>
        <div class="val">{{ k.value }}<span class="unit">{{ k.unit }}</span></div>
        <div class="spark">
          <app-sparkline [data]="k.spark" [color]="k.sparkColor" [width]="160"></app-sparkline>
        </div>
      </div>

    </div>
  `,
})
export class KpiStripComponent {
  @Input() simClock = 'T+00:00:00';
  @Input() timeScale = 1;
  @Input() metrics: MetricsSnapshot | null = null;
  @Input() entityCount = 0;
  @Input() componentCount = 0;
  @Input() sparks: { throughput: number[]; saturation: number[]; fulfillment: number[] } =
    { throughput: [], saturation: [], fulfillment: [] };
  @Input() currentSpeedId: SimSpeed = 1;
  @Input() paused = false;
  @Output() speedChange = new EventEmitter<SimSpeed>();
  @Output() pauseToggle = new EventEmitter<void>();

  readonly speedBtns = SIM_SPEEDS.filter(s => s.id !== 0 && s.id !== 5);

  get kpis(): KpiItem[] {
    const m = this.metrics;
    const noData = '—';
    return [
      {
        label: 'THROUGHPUT', unit: 'UdC/h',
        value: m ? m.throughput.toFixed(0) : noData,
        klass: 'good', sparkColor: 'var(--green)',
        spark: this.sparks.throughput,
        trend: '', trendDown: false,
      },
      {
        label: 'AVG FULFIL', unit: 's',
        value: m ? m.avgFulfillmentTime.toFixed(0) : noData,
        klass: 'hot', sparkColor: 'var(--amber)',
        spark: this.sparks.fulfillment,
        trend: '', trendDown: false,
      },
      {
        label: 'SATURATION', unit: '%',
        value: m ? m.warehouseSaturation.toFixed(1) : noData,
        klass: '', sparkColor: 'var(--text-2)',
        spark: this.sparks.saturation,
        trend: '', trendDown: false,
      },
      {
        label: 'ACTIVE ORDERS', unit: '',
        value: m ? String(m.activeOrders) : noData,
        klass: '', sparkColor: 'var(--cyan)',
        spark: [],
        trend: '', trendDown: false,
      },
      {
        label: 'COMPLETED', unit: '',
        value: m ? String(m.completedOrders) : noData,
        klass: 'good', sparkColor: 'var(--green)',
        spark: [],
        trend: '', trendDown: false,
      },
      {
        label: 'ENTITIES', unit: '',
        value: String(this.entityCount),
        klass: '', sparkColor: 'var(--text-2)',
        spark: [],
        trend: '', trendDown: false,
      },
      {
        label: 'COMPONENTS', unit: '',
        value: String(this.componentCount),
        klass: '', sparkColor: 'var(--text-2)',
        spark: [],
        trend: '', trendDown: false,
      },
    ];
  }

  togglePause(): void {
    this.pauseToggle.emit();
    const newSpeed: SimSpeed = this.paused ? (this.currentSpeedId || 1) : 0;
    this.speedChange.emit(newSpeed);
  }

  onSpeed(id: SimSpeed): void {
    this.speedChange.emit(id);
  }
}
