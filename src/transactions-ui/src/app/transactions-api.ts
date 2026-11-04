import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';

export interface Transaction {
  id: number;
  sourceSystem: string;
  transactionId: number;
  cardNumber: string;
  locationCode: string;
  productName: string;
  amount: number;
  transactionTime: string;
  status: string;
  revisionNumber: number;
  createdAtUtc: string;
  updatedAtUtc: string;
}
export interface SourceResult {
  sourceSystem: string;
  status: string;
  inserted: number;
  updated: number;
  revoked: number;
  restored: number;
  unchanged: number;
  error: string | null;
}
export interface ImportRun {
  id: number;
  scenario: string;
  status: string;
  startedAtUtc: string;
  completedAtUtc: string | null;
  sources: SourceResult[];
}
export interface AuditChange {
  id: number;
  importRunId: number;
  eventType: string;
  fieldName: string;
  oldValue: string | null;
  newValue: string | null;
  detectedAtUtc: string;
}
@Injectable({ providedIn: 'root' })
export class TransactionsApi {
  private readonly http = inject(HttpClient);
  transactions() {
    return this.http.get<Transaction[]>('/api/transactions');
  }
  runs() {
    return this.http.get<ImportRun[]>('/api/import/runs');
  }
  scenarios() {
    return this.http.get<string[]>('/api/import/scenarios');
  }
  audit(id: number) {
    return this.http.get<AuditChange[]>(`/api/transactions/${id}/audit`);
  }
  import(scenario: string) {
    return this.http.post<ImportRun>('/api/import', { scenario });
  }
}
