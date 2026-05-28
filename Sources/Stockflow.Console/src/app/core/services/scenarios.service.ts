import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';

import { REST_BASE } from '../config';
import { Scenario, ScenarioSummary } from '../models/scenario';

@Injectable({ providedIn: 'root' })
export class ScenariosService {
  private http = inject(HttpClient);
  private base = `${REST_BASE}/api/scenarios`;

  list(): Observable<ScenarioSummary[]> {
    return this.http.get<ScenarioSummary[]>(this.base);
  }

  get(id: string): Observable<Scenario> {
    return this.http.get<Scenario>(`${this.base}/${encodeURIComponent(id)}`);
  }

  create(scenario: Scenario): Observable<Scenario> {
    return this.http.post<Scenario>(this.base, scenario);
  }

  update(scenario: Scenario): Observable<Scenario> {
    return this.http.put<Scenario>(`${this.base}/${encodeURIComponent(scenario.id)}`, scenario);
  }

  delete(id: string): Observable<void> {
    return this.http.delete<void>(`${this.base}/${encodeURIComponent(id)}`);
  }
}
