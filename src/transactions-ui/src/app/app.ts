import { Component, ElementRef, ViewChild, computed, inject, signal } from '@angular/core';
import { CurrencyPipe, DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { forkJoin } from 'rxjs';
import { TransactionsApi, Transaction, ImportRun, AuditChange } from './transactions-api';

@Component({
  selector: 'app-root',
  imports: [CurrencyPipe, DatePipe, FormsModule],
  templateUrl: './app.html',
  styleUrl: './app.css',
})
export class App {
  @ViewChild('auditDialog', { static: true })
  private auditDialog!: ElementRef<HTMLDialogElement>;

  private readonly api = inject(TransactionsApi);
  readonly transactions = signal<Transaction[]>([]);
  readonly runs = signal<ImportRun[]>([]);
  readonly scenarios = signal<string[]>([]);
  readonly loading = signal(true);
  readonly importing = signal(false);
  readonly error = signal('');
  readonly notice = signal('');
  readonly search = signal('');
  readonly source = signal('All sources');
  readonly status = signal('All statuses');
  readonly sort = signal('newest');
  readonly selected = signal<Transaction | null>(null);
  readonly audit = signal<AuditChange[]>([]);
  readonly auditLoading = signal(false);
  readonly auditError = signal('');
  scenario = 'baseline';
  readonly sources = ['All sources', 'Store Uptown', 'Store Downtown', 'Online'];
  readonly descriptions: Record<string, string> = {
    baseline:
      'Start here. Imports three transactions from each source. Repeat to verify no changes.',
    corrected: 'Corrects an amount and adds a new transaction to each source.',
    missing:
      'Removes an Uptown transaction from the recent snapshot. Older missing records stay active.',
    restored: 'Returns the missing transaction and records its restoration.',
    failure: 'Simulates an Online outage. Other sources still import successfully.',
    incomplete: 'Returns incomplete snapshots. All existing records are preserved.',
  };
  readonly active = computed(() => this.transactions().filter((x) => x.status === 'Active').length);
  readonly revoked = computed(
    () => this.transactions().filter((x) => x.status === 'Revoked').length,
  );
  readonly activeTotal = computed(
    () =>
      this.transactions()
        .filter((x) => x.status === 'Active')
        .reduce((sum, x) => sum + Math.round(x.amount * 100), 0) / 100,
  );
  readonly visible = computed(() => {
    const query = this.search().toLowerCase().trim();
    const rows = this.transactions().filter(
      (x) =>
        (this.source() === 'All sources' || x.sourceSystem === this.source()) &&
        (this.status() === 'All statuses' || x.status === this.status()) &&
        (!query ||
          `${x.transactionId} ${x.productName} ${x.locationCode} ${x.sourceSystem}`
            .toLowerCase()
            .includes(query)),
    );
    return rows.sort((a, b) =>
      this.sort() === 'amount'
        ? b.amount - a.amount
        : this.sort() === 'source'
          ? a.sourceSystem.localeCompare(b.sourceSystem) || a.transactionId - b.transactionId
          : Date.parse(b.transactionTime) - Date.parse(a.transactionTime),
    );
  });

  constructor() {
    this.refresh();
  }

  refresh(): void {
    this.loading.set(true);
    this.error.set('');
    forkJoin({
      transactions: this.api.transactions(),
      runs: this.api.runs(),
      scenarios: this.api.scenarios(),
    }).subscribe({
      next: (data) => {
        this.transactions.set(data.transactions);
        this.runs.set(data.runs);
        this.scenarios.set(data.scenarios);
        this.loading.set(false);
      },
      error: (error) => {
        this.error.set(this.describe(error));
        this.loading.set(false);
      },
    });
  }

  import(): void {
    this.importing.set(true);
    this.error.set('');
    this.notice.set('');
    this.api.import(this.scenario).subscribe({
      next: (run) => {
        const count = (field: 'inserted' | 'updated' | 'revoked' | 'restored') =>
          run.sources.reduce((sum, source) => sum + source[field], 0);
        this.notice.set(
          `${run.status}: ${count('inserted')} added, ${count('updated')} updated, ${count('revoked')} revoked, ${count('restored')} restored.`,
        );
        this.importing.set(false);
        this.closeAudit();
        this.refresh();
      },
      error: (error) => {
        this.error.set(this.describe(error));
        this.importing.set(false);
      },
    });
  }

  showAudit(transaction: Transaction): void {
    this.selected.set(transaction);
    this.audit.set([]);
    this.auditError.set('');
    this.auditLoading.set(true);
    this.auditDialog.nativeElement.showModal();
    this.api.audit(transaction.id).subscribe({
      next: (changes) => {
        if (this.selected()?.id !== transaction.id) return;
        this.audit.set(changes);
        this.auditLoading.set(false);
      },
      error: (error) => {
        if (this.selected()?.id !== transaction.id) return;
        this.auditError.set(this.describe(error));
        this.auditLoading.set(false);
      },
    });
  }

  closeAudit(): void {
    this.auditDialog.nativeElement.close();
    this.selected.set(null);
  }

  dismissAudit(event: MouseEvent): void {
    const dialog = this.auditDialog.nativeElement;
    if (event.target !== dialog) return;
    const bounds = dialog.getBoundingClientRect();
    if (
      event.clientX < bounds.left ||
      event.clientX > bounds.right ||
      event.clientY < bounds.top ||
      event.clientY > bounds.bottom
    ) {
      this.closeAudit();
    }
  }

  private describe(error: HttpErrorResponse): string {
    return (
      error.error?.detail ||
      error.error?.title ||
      'Could not reach the server. Check that the API is running.'
    );
  }
}
