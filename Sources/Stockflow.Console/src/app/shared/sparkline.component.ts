import { Component, Input, OnChanges } from '@angular/core';
import { NgIf } from '@angular/common';

@Component({
  selector: 'app-sparkline',
  standalone: true,
  imports: [NgIf],
  template: `
    <svg [attr.width]="width" [attr.height]="height" style="display:block" *ngIf="pts">
      <polyline [attr.points]="pts" fill="none" [attr.stroke]="color" stroke-width="1"/>
      <circle [attr.cx]="lastX" [attr.cy]="lastY" r="1.5" [attr.fill]="color"/>
    </svg>
  `,
})
export class SparklineComponent implements OnChanges {
  @Input() data: number[] = [];
  @Input() color = 'var(--amber)';
  @Input() width = 100;
  @Input() height = 14;

  pts = '';
  lastX = 0;
  lastY = 0;

  ngOnChanges(): void {
    const d = this.data;
    if (!d || d.length < 2) { this.pts = ''; return; }
    const min = Math.min(...d), max = Math.max(...d);
    const rng = max - min || 1;
    const step = this.width / (d.length - 1);
    this.pts = d.map((v, i) =>
      `${(i * step).toFixed(1)},${(this.height - ((v - min) / rng) * this.height).toFixed(1)}`
    ).join(' ');
    this.lastX = (d.length - 1) * step;
    this.lastY = this.height - ((d[d.length - 1] - min) / rng) * this.height;
  }
}
