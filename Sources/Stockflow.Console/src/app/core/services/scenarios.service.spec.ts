import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';

import { ScenariosService } from './scenarios.service';
import { Scenario, ScenarioSummary } from '../models/scenario';
import { REST_BASE } from '../config';

describe('ScenariosService', () => {
  let service: ScenariosService;
  let http: HttpTestingController;
  const base = `${REST_BASE}/api/scenarios`;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(ScenariosService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('list() GETs /api/scenarios and returns summaries', () => {
    const stub: ScenarioSummary[] = [{ id: 's1', name: 'One', description: 'a' }];
    let received: ScenarioSummary[] | undefined;

    service.list().subscribe(r => (received = r));

    const req = http.expectOne(base);
    expect(req.request.method).toBe('GET');
    req.flush(stub);

    expect(received).toEqual(stub);
  });

  it('get() encodes id and GETs /api/scenarios/:id', () => {
    const stub: Scenario = { id: 'abc.def', name: 'X' };

    service.get('abc.def').subscribe();

    const req = http.expectOne(`${base}/abc.def`);
    expect(req.request.method).toBe('GET');
    req.flush(stub);
  });

  it('create() POSTs the scenario as body', () => {
    const s: Scenario = { id: 's1', name: 'New', description: 'hi' };

    service.create(s).subscribe();

    const req = http.expectOne(base);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual(s);
    req.flush(s);
  });

  it('update() PUTs to /api/scenarios/:id with body', () => {
    const s: Scenario = { id: 's1', name: 'Updated' };

    service.update(s).subscribe();

    const req = http.expectOne(`${base}/s1`);
    expect(req.request.method).toBe('PUT');
    expect(req.request.body).toEqual(s);
    req.flush(s);
  });

  it('delete() DELETEs /api/scenarios/:id', () => {
    service.delete('s1').subscribe();

    const req = http.expectOne(`${base}/s1`);
    expect(req.request.method).toBe('DELETE');
    req.flush(null, { status: 204, statusText: 'No Content' });
  });

  it('propagates 404 errors to subscribers', () => {
    let status: number | undefined;

    service.get('missing').subscribe({
      next:  () => {},
      error: err => (status = err.status),
    });

    http.expectOne(`${base}/missing`).flush(
      { errorMessage: 'not found' },
      { status: 404, statusText: 'Not Found' });

    expect(status).toBe(404);
  });
});
