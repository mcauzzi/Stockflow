import { Component, Input } from '@angular/core';
import { NgFor } from '@angular/common';
import { SparklineComponent } from '../../shared/sparkline.component';
import { MetricsSnapshot } from '../../core/models/protocol';

interface ChartSeries { name: string; color: string; data: number[]; }

@Component({
  selector: 'app-metrics-view',
  standalone: true,
  imports: [NgFor, SparklineComponent],
  template: `
    <div style="flex:1;display:grid;grid-template-columns:1fr 1fr;grid-template-rows:1fr 1fr;
                gap:1px;background:var(--border);min-height:0;overflow:hidden">

      <!-- Throughput chart (live if metrics available) -->
      <div class="chart-panel">
        <div class="panel-head"><span>THROUGHPUT · 60s</span><span class="idx">UdC/h · WS</span></div>
        <div style="flex:1;padding:8px 12px;display:flex;flex-direction:column;gap:6px">
          <div style="display:flex;gap:14px;font-family:var(--mono);font-size:9px;text-transform:uppercase;letter-spacing:.05em">
            <span style="display:flex;align-items:center;gap:4px;color:var(--text-3)">
              <span style="width:10px;height:2px;background:var(--green);display:inline-block"></span>LIVE
            </span>
          </div>
          <app-sparkline [data]="sparks.throughput" color="var(--green)" [width]="400" [height]="80"></app-sparkline>
          <div style="font-family:var(--mono);font-size:11px;color:var(--text-2)">
            Current: <span class="green">{{ metrics ? metrics.throughput.toFixed(1) : '—' }} UdC/h</span>
          </div>
        </div>
      </div>

      <!-- Order fulfillment time -->
      <div class="chart-panel">
        <div class="panel-head"><span>AVG FULFILLMENT TIME</span><span class="idx">seconds · WS</span></div>
        <div style="flex:1;padding:8px 12px;display:flex;flex-direction:column;gap:6px">
          <app-sparkline [data]="sparks.fulfillment" color="var(--amber)" [width]="400" [height]="80"></app-sparkline>
          <div style="font-family:var(--mono);font-size:11px;color:var(--text-2)">
            Current: <span class="amber">{{ metrics ? metrics.avgFulfillmentTime.toFixed(1) : '—' }}s</span>
          </div>
        </div>
      </div>

      <!-- Saturation -->
      <div class="chart-panel">
        <div class="panel-head"><span>WAREHOUSE SATURATION</span><span class="idx">% · WS</span></div>
        <div style="flex:1;padding:8px 12px;display:flex;flex-direction:column;gap:6px">
          <app-sparkline [data]="sparks.saturation" color="var(--cyan)" [width]="400" [height]="80"></app-sparkline>
          <div style="font-family:var(--mono);font-size:11px;color:var(--text-2)">
            Current: <span class="cyan">{{ metrics ? metrics.warehouseSaturation.toFixed(1) : '—' }}%</span>
          </div>
        </div>
      </div>

      <!-- Live telemetry table -->
      <div class="chart-panel" style="overflow:hidden">
        <div class="panel-head"><span>LIVE TELEMETRY</span><span class="idx">REST /metrics · WS</span></div>
        <div style="padding:10px 14px;display:grid;grid-template-columns:1fr 1fr;gap:8px 18px;
                    font-family:var(--mono);font-size:11px;overflow-y:auto;flex:1">
          <div *ngFor="let row of telemetryRows()"
               style="display:flex;justify-content:space-between;border-bottom:1px solid var(--bg-2);padding:3px 0">
            <span class="dim2" style="text-transform:uppercase;letter-spacing:.05em;font-size:9px">{{ row[0] }}</span>
            <span [class]="row[2]">{{ row[1] }}</span>
          </div>
        </div>
        <div style="padding:6px 14px;font-family:var(--mono);font-size:9px;color:var(--text-4);
                    border-top:1px solid var(--border)">
          ⚠ SLA / Traslo util / Orders pending future milestone
        </div>
      </div>

    </div>
  `,
  styles: [`
    .chart-panel { background: var(--bg-1); display: flex; flex-direction: column; min-height: 0; }
  `],
})
export class MetricsViewComponent {
  @Input() metrics: MetricsSnapshot | null = null;
  @Input() sparks: { throughput: number[]; saturation: number[]; fulfillment: number[] } =
    { throughput: [], saturation: [], fulfillment: [] };
  @Input() entityCount = 0;
  @Input() componentCount = 0;

  telemetryRows(): [string, string, string][] {
    const m = this.metrics;
    const nd = '—';
    return [
      ['Throughput',       m ? m.throughput.toFixed(1) + ' UdC/h' : nd,           'green'],
      ['Avg Fulfillment',  m ? m.avgFulfillmentTime.toFixed(1) + 's' : nd,        'amber'],
      ['Saturation',       m ? m.warehouseSaturation.toFixed(1) + '%' : nd,       ''],
      ['Active Orders',    m ? String(m.activeOrders) : nd,                         ''],
      ['Completed Orders', m ? String(m.completedOrders) : nd,                     'green'],
      ['Entities (live)',  String(this.entityCount),                                ''],
      ['Components',       String(this.componentCount),                             ''],
      ['SLA',              nd,                                                       'dim2'],
      ['Traslo util',      nd,                                                       'dim2'],
      ['Conv util',        nd,                                                       'dim2'],
      ['Late orders / h',  nd,                                                       'dim2'],
      ['Errors (24h)',     nd,                                                       'dim2'],
    ];
  }
}
