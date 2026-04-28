import { Injectable, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, tap } from 'rxjs';

import { REST_BASE } from '../config';
import { SessionInfo } from '../models/session';

@Injectable({ providedIn: 'root' })
export class SessionsService {
  private http = inject(HttpClient);
  private base = `${REST_BASE}/api/sessions`;

  /** Sessione attualmente nota al client (sincronizzata dalle risposte REST). */
  readonly currentSession = signal<SessionInfo | null>(null);

  start(): Observable<SessionInfo> {
    return this.http.post<SessionInfo>(this.base, null).pipe(
      tap(info => this.currentSession.set(info)),
    );
  }

  refresh(id: string): Observable<SessionInfo> {
    return this.http.get<SessionInfo>(`${this.base}/${id}`).pipe(
      tap(info => this.currentSession.set(info)),
    );
  }

  terminate(id: string): Observable<void> {
    return this.http.delete<void>(`${this.base}/${id}`).pipe(
      tap(() => this.currentSession.set(null)),
    );
  }

  loadScenario(id: string, scenarioId: string): Observable<SessionInfo> {
    return this.http
      .post<SessionInfo>(`${this.base}/${id}/scenario/load`, { scenarioId })
      .pipe(tap(info => this.currentSession.set(info)));
  }
}
