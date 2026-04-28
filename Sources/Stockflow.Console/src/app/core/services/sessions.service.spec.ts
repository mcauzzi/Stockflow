import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';

import { SessionsService } from './sessions.service';
import { SessionInfo } from '../models/session';
import { REST_BASE } from '../config';

const RUNNING: SessionInfo = {
  id: '11111111-2222-3333-4444-555555555555',
  status: 'Running',
  startedAt: '2026-04-28T12:00:00Z',
  endedAt: null,
  scenarioId: null,
  simulationTime: 0,
};

describe('SessionsService', () => {
  let service: SessionsService;
  let http: HttpTestingController;
  const base = `${REST_BASE}/api/sessions`;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(SessionsService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('start() POSTs and updates currentSession on success', () => {
    expect(service.currentSession()).toBeNull();

    service.start().subscribe();

    const req = http.expectOne(base);
    expect(req.request.method).toBe('POST');
    req.flush(RUNNING);

    expect(service.currentSession()).toEqual(RUNNING);
  });

  it('refresh() GETs /api/sessions/:id and updates currentSession', () => {
    service.refresh(RUNNING.id).subscribe();

    const req = http.expectOne(`${base}/${RUNNING.id}`);
    expect(req.request.method).toBe('GET');
    req.flush(RUNNING);

    expect(service.currentSession()).toEqual(RUNNING);
  });

  it('terminate() DELETEs /api/sessions/:id and clears currentSession', () => {
    // simulate previous start
    service.start().subscribe();
    http.expectOne(base).flush(RUNNING);
    expect(service.currentSession()).toEqual(RUNNING);

    service.terminate(RUNNING.id).subscribe();
    http.expectOne(`${base}/${RUNNING.id}`).flush(null, { status: 204, statusText: 'No Content' });

    expect(service.currentSession()).toBeNull();
  });

  it('loadScenario() POSTs to /scenario/load with scenarioId body', () => {
    const updated: SessionInfo = { ...RUNNING, scenarioId: 'smoke', simulationTime: 0.1 };

    service.loadScenario(RUNNING.id, 'smoke').subscribe();

    const req = http.expectOne(`${base}/${RUNNING.id}/scenario/load`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ scenarioId: 'smoke' });
    req.flush(updated);

    expect(service.currentSession()).toEqual(updated);
  });

  it('start() leaves currentSession unchanged on 409', () => {
    let status: number | undefined;

    service.start().subscribe({
      next:  () => {},
      error: err => (status = err.status),
    });

    http.expectOne(base).flush(
      { errorMessage: 'already running' },
      { status: 409, statusText: 'Conflict' });

    expect(status).toBe(409);
    expect(service.currentSession()).toBeNull();
  });
});
