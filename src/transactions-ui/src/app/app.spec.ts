import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import { App } from './app';
import { Transaction } from './transactions-api';

describe('Transaction workspace', () => {
  let http: HttpTestingController;
  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [App],
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    http = TestBed.inject(HttpTestingController);
  });
  afterEach(() => http.verify());

  function create(rows: Transaction[] = []) {
    const fixture = TestBed.createComponent(App);
    http.expectOne('/api/transactions').flush(rows);
    http.expectOne('/api/import/runs').flush([]);
    http.expectOne('/api/import/scenarios').flush(['baseline', 'corrected']);
    fixture.detectChanges();
    return fixture;
  }

  const row: Transaction = {
    id: 1,
    sourceSystem: 'Online',
    transactionId: 100001,
    cardNumber: '1111',
    locationCode: 'ONLINE',
    productName: 'Mouse',
    amount: 24.99,
    transactionTime: '2026-11-01T10:00:00Z',
    status: 'Active',
    revisionNumber: 1,
    createdAtUtc: '2026-11-01T11:00:00Z',
    updatedAtUtc: '2026-11-01T11:00:00Z',
  };

  it('renders records and filters by source, status, and text', () => {
    const fixture = create([
      row,
      { ...row, id: 2, sourceSystem: 'Store Uptown', productName: 'Keyboard', status: 'Revoked' },
    ]);
    expect(fixture.nativeElement.textContent).toContain('Mouse');
    const app = fixture.componentInstance;
    app.source.set('Online');
    expect(app.visible()).toHaveLength(1);
    app.source.set('All sources');
    app.status.set('Revoked');
    expect(app.visible()[0].id).toBe(2);
    app.status.set('All statuses');
    app.search.set('mouse');
    expect(app.visible()[0].id).toBe(1);
    expect(app.activeTotal()).toBe(24.99);
  });

  it('imports the selected scenario and reloads records', () => {
    const fixture = create();
    const app = fixture.componentInstance;
    app.scenario = 'corrected';
    app.import();
    const request = http.expectOne('/api/import');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({ scenario: 'corrected' });
    expect(app.importing()).toBe(true);
    request.flush({
      id: 1,
      status: 'Succeeded',
      sources: [{ inserted: 1, updated: 0, revoked: 0, restored: 0 }],
    });
    http.expectOne('/api/transactions').flush([row]);
    http.expectOne('/api/import/runs').flush([]);
    http.expectOne('/api/import/scenarios').flush(['baseline']);
    expect(app.importing()).toBe(false);
    expect(app.notice()).toContain('1 added');
    expect(app.transactions()).toHaveLength(1);
  });

  it('displays an import conflict without discarding records', () => {
    const app = create([row]).componentInstance;
    app.import();
    http
      .expectOne('/api/import')
      .flush({ title: 'An import is already running.' }, { status: 409, statusText: 'Conflict' });
    expect(app.error()).toContain('already running');
    expect(app.transactions()).toHaveLength(1);
    expect(app.importing()).toBe(false);
  });

  it('keeps the current audit selection when older requests finish later', () => {
    const app = create([row]).componentInstance;
    app.showAudit(row);
    const first = http.expectOne('/api/transactions/1/audit');
    app.showAudit({ ...row, id: 2 });
    http.expectOne('/api/transactions/2/audit').flush([{ id: 2, fieldName: 'Amount' }]);
    first.flush([{ id: 1, fieldName: 'Status' }]);
    expect(app.audit()[0].id).toBe(2);
  });

  it('shows a useful empty state', () => {
    const fixture = create();
    expect(fixture.nativeElement.textContent).toContain('Run the Baseline import');
  });
});
