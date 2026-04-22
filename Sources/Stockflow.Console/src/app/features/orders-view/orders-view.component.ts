import { Component, signal, computed } from '@angular/core';
import { NgFor } from '@angular/common';
import { INITIAL_ORDERS, MockOrder } from '../../core/mock/sim-mock';

type Filter = 'all' | 'active' | 'pending' | 'done' | 'late';
type SortKey = 'id' | 'pri' | 'eta' | 'progress';
const PRI_ORDER = { HI: 0, MED: 1, LOW: 2 };

@Component({
  selector: 'app-orders-view',
  standalone: true,
  imports: [NgFor],
  template: `
    <div style="flex:1;display:flex;flex-direction:column;background:var(--bg-0);min-height:0;overflow:hidden">

      <!-- Header bar -->
      <div class="panel-head" style="height:32px">
        <div style="display:flex;gap:0;align-items:center">
          <span style="padding-right:14px">ORDER MANAGEMENT</span>
          <button *ngFor="let f of filters"
                  (click)="activeFilter.set(f.id)"
                  [style.background]="activeFilter() === f.id ? 'var(--amber)' : 'transparent'"
                  [style.color]="activeFilter() === f.id ? 'var(--bg-0)' : 'var(--text-2)'"
                  style="padding:3px 10px;font-family:var(--mono);font-size:10px;border:none;
                         border-left:1px solid var(--border);letter-spacing:.08em;cursor:pointer">
            {{ f.label }} <span style="opacity:.6">{{ counts()[f.id] }}</span>
          </button>
        </div>
        <div style="display:flex;gap:10px;align-items:center">
          <span class="dim2" style="font-family:var(--mono);font-size:10px">SORT</span>
          <button *ngFor="let k of sortKeys"
                  (click)="sortKey.set(k)"
                  style="font-family:var(--mono);font-size:10px;border:none;background:none;cursor:pointer"
                  [style.color]="sortKey() === k ? 'var(--amber)' : 'var(--text-3)'">
            {{ k.toUpperCase() }}
          </button>
        </div>
      </div>

      <!-- Mock data notice -->
      <div style="padding:4px 14px;background:var(--bg-2);font-family:var(--mono);font-size:9px;color:var(--text-3);
                  letter-spacing:.05em;border-bottom:1px solid var(--border)">
        ⚠ MOCK DATA — Orders not yet implemented in backend
      </div>

      <!-- Table header -->
      <div style="display:grid;grid-template-columns:110px 90px 60px 60px 1fr 90px 90px 70px;
                  padding:6px 14px;background:var(--bg-2);color:var(--text-3);font-size:9px;
                  letter-spacing:.1em;border-bottom:1px solid var(--border-bright);gap:12px;
                  position:sticky;top:0;font-family:var(--mono)">
        <span>ORDER</span><span>SKU</span><span>LINES</span><span>PRI</span>
        <span>PROGRESS</span>
        <span style="text-align:right">ETA</span>
        <span style="text-align:right">ELAPSED</span>
        <span style="text-align:right">STATUS</span>
      </div>

      <!-- Rows -->
      <div style="flex:1;overflow-y:auto">
        <div *ngFor="let o of filtered()"
             style="display:grid;grid-template-columns:110px 90px 60px 60px 1fr 90px 90px 70px;
                    padding:7px 14px;border-bottom:1px solid var(--bg-2);gap:12px;align-items:center;
                    font-family:var(--mono);font-size:11px">
          <span style="color:var(--amber)">{{ o.id }}</span>
          <span>{{ o.sku }}</span>
          <span class="dim">{{ o.lines }}</span>
          <span [class.red]="o.pri==='HI'" [class.amber]="o.pri==='MED'" [class.dim2]="o.pri==='LOW'">{{ o.pri }}</span>
          <div style="display:flex;align-items:center;gap:8px">
            <div style="flex:1;height:6px;background:var(--bg-1)">
              <div [style.width]="(o.progress*100)+'%'"
                   [style.background]="o.status==='late'?'var(--red)':o.status==='done'?'var(--text-3)':'var(--green)'"
                   style="height:100%"></div>
            </div>
            <span style="font-size:9px;color:var(--text-3);min-width:32px;text-align:right">{{ (o.progress*100).toFixed(0) }}%</span>
          </div>
          <span style="text-align:right;color:var(--text-2)">
            {{ o.status==='done' ? '—' : o.eta > 0 ? o.eta.toFixed(0)+'s' : 'DUE' }}
          </span>
          <span style="text-align:right;color:var(--text-3);font-size:10px">{{ o.elapsed.toFixed(0) }}s</span>
          <span style="text-align:center;font-size:9px;padding:1px 4px;text-transform:uppercase;letter-spacing:.04em;border:1px solid"
                [style.color]="statusColor(o.status)"
                [style.border-color]="statusBorderColor(o.status)"
                [style.background]="statusBg(o.status)">
            {{ o.status }}
          </span>
        </div>
      </div>
    </div>
  `,
})
export class OrdersViewComponent {
  activeFilter = signal<Filter>('all');
  sortKey = signal<SortKey>('id');

  readonly filters: { id: Filter; label: string }[] = [
    { id: 'all', label: 'ALL' }, { id: 'active', label: 'ACTIVE' },
    { id: 'pending', label: 'PENDING' }, { id: 'done', label: 'DONE' },
    { id: 'late', label: 'LATE' },
  ];
  readonly sortKeys: SortKey[] = ['id', 'pri', 'eta', 'progress'];

  private readonly orders = INITIAL_ORDERS;

  readonly counts = computed(() => ({
    all: this.orders.length,
    active:  this.orders.filter(o => o.status === 'active').length,
    pending: this.orders.filter(o => o.status === 'pending').length,
    done:    this.orders.filter(o => o.status === 'done').length,
    late:    this.orders.filter(o => o.status === 'late').length,
  }));

  readonly filtered = computed(() => {
    const f = this.activeFilter(), k = this.sortKey();
    let list = f === 'all' ? [...this.orders] : this.orders.filter(o => o.status === f);
    list.sort((a, b) => {
      if (k === 'id')       return a.id.localeCompare(b.id);
      if (k === 'eta')      return b.eta - a.eta;
      if (k === 'progress') return b.progress - a.progress;
      if (k === 'pri')      return PRI_ORDER[a.pri] - PRI_ORDER[b.pri];
      return 0;
    });
    return list;
  });

  statusColor(s: MockOrder['status']): string {
    return s === 'active' ? 'var(--amber)' : s === 'done' ? 'var(--green)' : s === 'late' ? 'var(--red)' : 'var(--text-3)';
  }
  statusBorderColor(s: MockOrder['status']): string {
    return s === 'active' ? 'var(--amber-dim)' : s === 'done' ? 'var(--green-dim)' : s === 'late' ? 'var(--red-dim)' : 'var(--border-bright)';
  }
  statusBg(s: MockOrder['status']): string {
    return s === 'active' ? 'rgba(245,166,35,.06)' : s === 'done' ? 'rgba(74,222,128,.06)' : s === 'late' ? 'rgba(239,68,68,.08)' : 'transparent';
  }
}
