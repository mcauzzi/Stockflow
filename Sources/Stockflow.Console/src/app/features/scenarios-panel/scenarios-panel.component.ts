import { Component, EventEmitter, OnInit, Output, inject, signal } from '@angular/core';
import { NgFor, NgIf } from '@angular/common';

import { ScenariosService } from '../../core/services/scenarios.service';
import { SessionsService }  from '../../core/services/sessions.service';
import { ScenarioSummary }  from '../../core/models/scenario';

@Component({
  selector: 'app-scenarios-panel',
  standalone: true,
  imports: [NgFor, NgIf],
  template: `
    <div class="backdrop" (click)="close.emit()"></div>
    <div class="panel" (click)="$event.stopPropagation()">
      <div class="head">
        <span>SCENARIOS</span>
        <span class="hint" *ngIf="!hasSession()">No active session — Start one from the topbar to load.</span>
        <span class="spacer"></span>
        <button class="btn ghost" (click)="reload()" [disabled]="loading()">REFRESH</button>
        <button class="btn ghost" (click)="close.emit()" aria-label="close">×</button>
      </div>

      <div class="body">
        <div class="empty" *ngIf="!loading() && scenarios().length === 0 && !error()">
          No scenarios on disk. Create one with
          <code>POST /api/scenarios</code>.
        </div>
        <div class="empty err" *ngIf="error()">{{ error() }}</div>
        <div class="empty" *ngIf="loading()">Loading…</div>

        <table *ngIf="scenarios().length > 0">
          <thead>
            <tr><th>ID</th><th>NAME</th><th>DESCRIPTION</th><th></th></tr>
          </thead>
          <tbody>
            <tr *ngFor="let s of scenarios()">
              <td class="amber mono">{{ s.id }}</td>
              <td>{{ s.name }}</td>
              <td class="dim">{{ s.description || '—' }}</td>
              <td class="actions">
                <button class="btn primary"
                        [disabled]="!hasSession() || busy()"
                        (click)="onLoad(s)">LOAD</button>
                <button class="btn danger"
                        [disabled]="busy()"
                        (click)="onDelete(s)">DELETE</button>
              </td>
            </tr>
          </tbody>
        </table>
      </div>
    </div>
  `,
  styles: [`
    :host {
      position: fixed; inset: 0;
      z-index: 100;
      font-family: var(--mono);
    }
    .backdrop {
      position: absolute; inset: 0;
      background: rgba(0,0,0,.55);
    }
    .panel {
      position: relative;
      margin: 8vh auto;
      max-width: 720px;
      max-height: 80vh;
      background: var(--bg-1);
      border: 1px solid var(--border-bright);
      box-shadow: 0 10px 30px rgba(0,0,0,.6);
      display: flex;
      flex-direction: column;
    }
    .head {
      display: flex; align-items: center; gap: 10px;
      padding: 0 10px;
      height: 32px;
      background: var(--bg-2);
      border-bottom: 1px solid var(--border);
      font-size: 11px;
      letter-spacing: .1em;
      color: var(--amber);
    }
    .head .hint {
      color: var(--text-3);
      letter-spacing: 0;
      font-weight: normal;
      font-size: 10px;
    }
    .head .spacer { flex: 1; }
    .body {
      padding: 10px;
      overflow: auto;
    }
    .empty {
      padding: 20px;
      color: var(--text-3);
      text-align: center;
      font-size: 11px;
    }
    .empty.err { color: var(--red); }
    table {
      width: 100%;
      border-collapse: collapse;
      font-size: 11px;
    }
    th, td {
      text-align: left;
      padding: 6px 8px;
      border-bottom: 1px solid var(--border);
    }
    th {
      color: var(--text-3);
      font-weight: 500;
      letter-spacing: .1em;
      font-size: 9px;
    }
    td.dim   { color: var(--text-3); }
    td.mono  { font-family: var(--mono); }
    td.amber { color: var(--amber); }
    .actions { text-align: right; white-space: nowrap; }
    .btn {
      background: transparent;
      border: 1px solid var(--border-bright);
      color: var(--text-1);
      font-family: var(--mono);
      font-size: 10px;
      letter-spacing: .1em;
      padding: 4px 10px;
      cursor: pointer;
      margin-left: 6px;
    }
    .btn:hover:not(:disabled) { background: var(--bg-2); }
    .btn:disabled { opacity: .35; cursor: not-allowed; }
    .btn.primary { border-color: var(--amber); color: var(--amber); }
    .btn.danger  { border-color: var(--red); color: var(--red); }
    .btn.ghost   { border: none; color: var(--text-3); padding: 2px 8px; }
    .btn.ghost:hover { color: var(--text-1); }
  `],
})
export class ScenariosPanelComponent implements OnInit {
  @Output() close = new EventEmitter<void>();

  private scenariosSvc = inject(ScenariosService);
  private sessionsSvc  = inject(SessionsService);

  readonly scenarios = signal<ScenarioSummary[]>([]);
  readonly loading   = signal(false);
  readonly busy      = signal(false);
  readonly error     = signal<string | null>(null);

  readonly hasSession = () =>
    this.sessionsSvc.currentSession()?.status === 'Running';

  ngOnInit(): void { this.reload(); }

  reload(): void {
    this.loading.set(true);
    this.error.set(null);
    this.scenariosSvc.list().subscribe({
      next: list => { this.scenarios.set(list); this.loading.set(false); },
      error: err => {
        this.error.set(`Load failed: HTTP ${err.status ?? '?'}`);
        this.loading.set(false);
      },
    });
  }

  onLoad(s: ScenarioSummary): void {
    const session = this.sessionsSvc.currentSession();
    if (!session || session.status !== 'Running') return;
    this.busy.set(true);
    this.sessionsSvc.loadScenario(session.id, s.id).subscribe({
      next: () => { this.busy.set(false); this.close.emit(); },
      error: err => {
        this.error.set(`Load '${s.id}' failed: HTTP ${err.status ?? '?'}`);
        this.busy.set(false);
      },
    });
  }

  onDelete(s: ScenarioSummary): void {
    if (!confirm(`Delete scenario '${s.id}'?`)) return;
    this.busy.set(true);
    this.scenariosSvc.delete(s.id).subscribe({
      next: () => {
        this.scenarios.update(xs => xs.filter(x => x.id !== s.id));
        this.busy.set(false);
      },
      error: err => {
        this.error.set(`Delete '${s.id}' failed: HTTP ${err.status ?? '?'}`);
        this.busy.set(false);
      },
    });
  }
}
