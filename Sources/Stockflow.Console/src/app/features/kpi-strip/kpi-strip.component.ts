import { Component, Input, Output, EventEmitter } from '@angular/core';
import { NgFor, NgClass, NgIf } from '@angular/common';
import { SparklineComponent } from '../../shared/sparkline.component';
import { MetricsSnapshot, SimSpeed, SIM_SPEEDS } from '../../core/models/protocol';

export interface KpiItem {
  label: string; unit: string;
  value: string; klass: string;
  spark: number[]; sparkColor: string;
}

@Component({
  selector: 'app-kpi-strip',
  standalone: true,
  imports: [NgFor, NgClass, NgIf, SparklineComponent],
  template: `
    <div class="strip">

      <!-- Clock + speed controls -->
      <div class="ctrl">
        <div class="clock-row">
          <button class="pp" [class.paused]="paused" (click)="pauseToggle.emit()">
            {{ paused ? '▶' : '❚❚' }}
          </button>
          <div class="clock-info">
            <div class="clk">SIM CLOCK <span class="cv">{{ simClock }}</span></div>
            <div class="clk">SCALE <span class="cv">{{ timeScale }}×</span></div>
          </div>
        </div>
        <div class="speed-row">
          <button *ngFor="let s of speedBtns"
                  class="sb"
                  [class.on]="!paused && currentSpeedId === s.id"
                  (click)="speedChange.emit(s.id)">{{ s.label }}</button>
        </div>
      </div>

      <!-- KPI tiles -->
      <div *ngFor="let k of kpis" class="tile" [ngClass]="'c-' + k.klass">
        <div class="tlabel">{{ k.label }}</div>
        <div class="tval">{{ k.value }}<span class="tunit">{{ k.unit }}</span></div>
        <div *ngIf="k.spark.length" class="tspark">
          <app-sparkline [data]="k.spark" [color]="k.sparkColor" [width]="120" [height]="20"></app-sparkline>
        </div>
      </div>

    </div>
  `,
  styles: [`
    :host { display: contents; }
    .strip {
      height: 48px;
      background: var(--bg-1);
      border-bottom: 1px solid var(--border);
      display: flex;
      align-items: stretch;
      flex-shrink: 0;
      overflow: hidden;
      font-family: var(--mono);
    }
    .ctrl {
      display: flex;
      flex-direction: column;
      justify-content: center;
      gap: 3px;
      padding: 3px 10px;
      border-right: 1px solid var(--border);
      min-width: 170px;
      flex-shrink: 0;
    }
    .clock-row { display: flex; align-items: center; gap: 7px; }
    .pp {
      width: 22px; height: 22px;
      border: 1px solid var(--border-bright);
      background: transparent;
      color: var(--text-2);
      font-size: 9px;
      display: flex; align-items: center; justify-content: center;
      cursor: pointer;
      flex-shrink: 0;
      transition: all .12s;
    }
    .pp:hover, .pp.paused { color: var(--amber); border-color: var(--amber-dim); }
    .clock-info { display: flex; flex-direction: column; gap: 1px; }
    .clk { font-size: 8px; color: var(--text-3); letter-spacing: .04em; white-space: nowrap; }
    .cv { color: var(--text-1); margin-left: 3px; }
    .speed-row { display: flex; gap: 3px; }
    .sb {
      padding: 1px 7px;
      border: 1px solid var(--border-bright);
      background: transparent;
      color: var(--text-3);
      font-family: var(--mono);
      font-size: 9px;
      letter-spacing: .04em;
      cursor: pointer;
      transition: all .1s;
    }
    .sb:hover { border-color: var(--amber-dim); color: var(--amber); }
    .sb.on { background: var(--amber); color: var(--bg-0); border-color: var(--amber); font-weight: 600; }
    .tile {
      display: flex;
      flex-direction: column;
      justify-content: center;
      padding: 0 10px;
      border-right: 1px solid var(--border);
      min-width: 80px;
      gap: 1px;
      overflow: hidden;
      flex-shrink: 0;
    }
    .c-good .tval { color: var(--green); }
    .c-hot  .tval { color: var(--amber); }
    .c-cyan .tval { color: var(--cyan); }
    .tlabel { font-size: 7px; letter-spacing: .1em; color: var(--text-4); text-transform: uppercase; white-space: nowrap; }
    .tval { font-size: 15px; font-weight: 500; color: var(--text-0); line-height: 1; white-space: nowrap; }
    .tunit { font-size: 8px; color: var(--text-3); margin-left: 2px; font-weight: 400; }
    .tspark { line-height: 0; }
  `],
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
    const nd = '—';
    return [
      { label: 'THROUGHPUT',    unit: ' UdC/h', value: m ? m.throughput.toFixed(0) : nd,             klass: 'good', sparkColor: 'var(--green)',  spark: this.sparks.throughput  },
      { label: 'AVG FULFIL',    unit: 's',       value: m ? m.avgFulfillmentTime.toFixed(0) : nd,      klass: 'hot',  sparkColor: 'var(--amber)',  spark: this.sparks.fulfillment },
      { label: 'SATURATION',    unit: '%',       value: m ? m.warehouseSaturation.toFixed(1) : nd,     klass: '',     sparkColor: 'var(--text-2)', spark: this.sparks.saturation  },
      { label: 'ACTIVE ORDERS', unit: '',        value: m ? String(m.activeOrders) : nd,               klass: '',     sparkColor: 'var(--cyan)',   spark: []                      },
      { label: 'COMPLETED',     unit: '',        value: m ? String(m.completedOrders) : nd,            klass: 'good', sparkColor: 'var(--green)',  spark: []                      },
      { label: 'ENTITIES',      unit: '',        value: String(this.entityCount),                       klass: '',     sparkColor: 'var(--text-2)', spark: []                      },
      { label: 'COMPONENTS',    unit: '',        value: String(this.componentCount),                    klass: '',     sparkColor: 'var(--text-2)', spark: []                      },
    ];
  }
}
