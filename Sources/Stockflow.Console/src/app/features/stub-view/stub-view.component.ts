import { Component, Input } from '@angular/core';
import { NgFor } from '@angular/common';

const SPECS: Record<string, { title: string; sub: string; phase: string; items: string[] }> = {
  WORKSHOP: {
    title: 'WORKSHOP',
    sub: 'Custom components · visual editor · Steam Workshop sync',
    phase: 'PHASE 1D · 2',
    items: [
      'Visual component editor (Level 1)',
      'Plugin loader (Level 2 · C# DLL)',
      'Steam Workshop publish',
      'Component catalog browser',
      'Tag system & search',
    ],
  },
  PRO: {
    title: 'STOCKFLOW · PRO',
    sub: 'WMS integration · digital-twin mode · enterprise adapters',
    phase: 'PHASE 2',
    items: [
      'REST JSON adapter (Manhattan, Körber, Odoo)',
      'WebSocket adapter (real-time bidirectional)',
      'CSV / XML file import',
      'OPC-UA adapter (PLC digital twin)',
      'LIVE MODE · 1× lock when external WMS is active',
      'Session replay & post-hoc analysis',
    ],
  },
};

@Component({
  selector: 'app-stub-view',
  standalone: true,
  imports: [NgFor],
  template: `
    <div style="flex:1;background:var(--bg-0);display:flex;align-items:center;justify-content:center;
                padding:40px;min-height:0;overflow:auto">
      <div style="max-width:640px;width:100%;border:1px solid var(--border);background:var(--bg-1);
                  font-family:var(--mono)">

        <div style="padding:18px 24px;border-bottom:1px solid var(--border);
                    display:flex;justify-content:space-between;align-items:baseline">
          <div>
            <div style="font-size:22px;color:var(--amber);letter-spacing:.02em">{{ spec.title }}</div>
            <div style="font-size:11px;color:var(--text-3);margin-top:4px">{{ spec.sub }}</div>
          </div>
          <span class="insp-badge info">{{ spec.phase }}</span>
        </div>

        <div style="padding:18px 24px">
          <div style="font-size:10px;letter-spacing:.1em;color:var(--text-3);margin-bottom:10px">PLANNED CAPABILITIES</div>
          <div *ngFor="let item of spec.items; let i = index"
               style="display:grid;grid-template-columns:28px 1fr auto;
                      padding:7px 0;border-bottom:1px solid var(--bg-2);font-size:11px">
            <span style="color:var(--text-4)">{{ (i+1).toString().padStart(2,'0') }}</span>
            <span>{{ item }}</span>
            <span style="color:var(--text-3);font-size:9px;letter-spacing:.08em">PLANNED</span>
          </div>
        </div>

        <div style="padding:12px 24px;border-top:1px solid var(--border);font-size:10px;
                    color:var(--text-3);display:flex;justify-content:space-between">
          <span>See GDD §5 · §12 for full spec</span>
          <span class="amber">mcauzzi/Stockflow ▸</span>
        </div>
      </div>
    </div>
  `,
})
export class StubViewComponent {
  @Input() tab: 'WORKSHOP' | 'PRO' = 'WORKSHOP';
  get spec() { return SPECS[this.tab]; }
}
