import { Component, OnInit, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { provideNativeDateAdapter } from '@angular/material/core';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { forkJoin, of } from 'rxjs';
import { catchError, map } from 'rxjs/operators';
import { MaterialModule } from '../app/material-module';
import { GoogleAuthService } from '../auth/google-auth.service';
import { Project, ProjectApiService, QuotationListItem } from '../core/services/project-api.service';
import { formatMoney } from '../projects/money-format';
import { CreatedRange, applyDashboardFilters, createDashboardFilters } from './dashboard-filters';

type QuoteStage = 'pending' | 'processing';

interface QuoteRow {
  id: number;
  code: string;
  supplierId: number;
  supplierName: string;
  supplierReference: string;
  projectId: number;
  projectName: string;
  projectReference: string;
  itemCount: number;
  baseTotalAmount: number;
  updatedAt: string | null;
  createdAt: string | null;
  dataset: string;
  status: QuoteStage;
}

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [MaterialModule, RouterModule, FormsModule, MatDatepickerModule],
  providers: [provideNativeDateAdapter()],
  templateUrl: './dashboard.html',
  styleUrl: './dashboard.css',
})
export class DashboardComponent implements OnInit {
  private api = inject(ProjectApiService);

  userName = '';
  loading = true;
  error = '';
  rows: QuoteRow[] = [];
  filters = createDashboardFilters();
  startDate: Date | null = null;
  endDate: Date | null = null;

  constructor(googleAuth: GoogleAuthService) {
    this.userName = googleAuth.user?.given_name || googleAuth.user?.name || '';
  }

  ngOnInit() {
    this.api.list().subscribe({
      next: (projects) => this.loadQuotations(projects),
      error: () => {
        this.error = 'Quotations could not be loaded.';
        this.loading = false;
      },
    });
  }

  get scopedRows(): QuoteRow[] {
    return applyDashboardFilters(this.rows, { ...this.filters, status: 'all' });
  }

  get pendingCount(): number {
    return this.scopedRows.filter((row) => row.status === 'pending').length;
  }

  get processingCount(): number {
    return this.scopedRows.filter((row) => row.status === 'processing').length;
  }

  get quotedValue(): string {
    const total = this.scopedRows.reduce((sum, row) => sum + (row.baseTotalAmount || 0), 0);
    return formatMoney(total, '$', 'USD');
  }

  get visibleRows(): QuoteRow[] {
    return applyDashboardFilters(this.rows, this.filters);
  }

  setStatus(status: string) {
    this.filters.status = this.filters.status === status ? 'all' : status;
  }

  setCreated(range: CreatedRange) {
    this.filters.created = range;
    if (range !== 'custom') {
      this.startDate = null;
      this.endDate = null;
      this.filters.createdFrom = '';
      this.filters.createdTo = '';
    }
  }

  syncRange() {
    this.filters.createdFrom = this.toIso(this.startDate);
    this.filters.createdTo = this.toIso(this.endDate);
  }

  private toIso(value: Date | null): string {
    if (!value || Number.isNaN(value.getTime())) {
      return '';
    }
    const month = String(value.getMonth() + 1).padStart(2, '0');
    const day = String(value.getDate()).padStart(2, '0');
    return `${value.getFullYear()}-${month}-${day}`;
  }

  when(value?: string | null): string {
    if (!value) {
      return '—';
    }
    const date = new Date(value);
    if (Number.isNaN(date.getTime())) {
      return '—';
    }
    return date.toLocaleDateString('en-US', { month: 'short', day: 'numeric', year: 'numeric' });
  }

  money(value: number): string {
    return formatMoney(value, '$', 'USD');
  }

  private loadQuotations(projects: Project[]) {
    if (!projects.length) {
      this.loading = false;
      return;
    }

    forkJoin(
      projects.map((project) =>
        this.api.quotations(project.id).pipe(
          map((quotes) => quotes.map((quote) => this.toRow(quote, project))),
          catchError(() => of([] as QuoteRow[])),
        ),
      ),
    ).subscribe({
      next: (groups) => {
        const byId = new Map<number, QuoteRow>();
        for (const row of groups.flat()) {
          if (!byId.has(row.id)) {
            byId.set(row.id, row);
          }
        }
        this.rows = [...byId.values()].sort((left, right) => (right.updatedAt ?? '').localeCompare(left.updatedAt ?? ''));
        this.loading = false;
      },
      error: () => {
        this.error = 'Quotations could not be loaded.';
        this.loading = false;
      },
    });
  }

  private toRow(quote: QuotationListItem, project: Project): QuoteRow {
    return {
      id: quote.id,
      code: quote.code,
      supplierId: quote.supplierId,
      supplierName: quote.supplierName,
      supplierReference: quote.supplierReference?.trim() || '',
      projectId: project.id,
      projectName: project.name,
      projectReference: project.reference,
      itemCount: quote.itemCount,
      baseTotalAmount: quote.baseTotalAmount ?? quote.totalPrice ?? 0,
      updatedAt: quote.modifiedAt || quote.createdAt || null,
      createdAt: quote.createdAt || null,
      dataset: 'quotations',
      status: quote.itemCount > 0 ? 'processing' : 'pending',
    };
  }
}
