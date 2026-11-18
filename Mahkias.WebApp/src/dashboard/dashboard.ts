import { Component, OnInit, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { provideNativeDateAdapter } from '@angular/material/core';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { PageEvent } from '@angular/material/paginator';
import { MaterialModule } from '../app/material-module';
import { GoogleAuthService } from '../auth/google-auth.service';
import { DashboardTrackItem, ProjectApiService } from '../core/services/project-api.service';
import { formatMoney } from '../projects/money-format';
import { quotationValidity } from '../projects/quotation-terms';
import { CreatedRange, createDashboardFilters, statusOptionsFor } from './dashboard-filters';

interface TrackRow {
  key: string;
  dataset: 'quotations' | 'purchase-orders';
  status: string;
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
  validUntil: string | null;
  section: 'quotations' | 'purchases';
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
  private loadToken = 0;

  readonly pageSizeOptions = [10, 20, 50, 100];
  userName = '';
  loading = true;
  error = '';
  rows: TrackRow[] = [];
  projectTotal = 0;
  activeProjectCount = 0;
  supplierTotal = 0;
  quotationTotal = 0;
  quotedAmount = 0;
  openPurchaseOrders = 0;
  totalCount = 0;
  pageNumber = 1;
  pageSize = 10;
  filters = createDashboardFilters();
  startDate: Date | null = null;
  endDate: Date | null = null;

  constructor(googleAuth: GoogleAuthService) {
    this.userName = googleAuth.user?.given_name || googleAuth.user?.name || '';
  }

  ngOnInit() {
    this.load();
  }

  get statusOptions() {
    return statusOptionsFor(this.filters.dataset);
  }

  get quotationCount(): number {
    return this.quotationTotal;
  }

  get openPurchaseOrderCount(): number {
    return this.openPurchaseOrders;
  }

  get quotedValue(): string {
    return formatMoney(this.quotedAmount, '$', 'USD');
  }

  get boardNote(): string {
    return this.filters.dataset === 'purchase-orders'
      ? 'Purchase orders across projects, newest activity first.'
      : 'Supplier quotes across projects, newest activity first.';
  }

  get emptyMessage(): string {
    const dates = this.dateBounds();
    const narrowed = this.filters.status !== 'all' || !!(dates.from || dates.to);
    if (!narrowed) {
      return this.filters.dataset === 'purchase-orders' ? 'No purchase orders yet.' : 'No quotations yet.';
    }
    return 'Nothing matches this filter.';
  }

  setDataset(dataset: string) {
    if (dataset === this.filters.dataset) {
      return;
    }
    this.filters.dataset = dataset;
    const allowed = new Set(statusOptionsFor(dataset).map((option) => option.id));
    if (this.filters.status !== 'all' && !allowed.has(this.filters.status)) {
      this.filters.status = 'all';
    }
    this.pageNumber = 1;
    this.load();
  }

  setStatus(status: string) {
    this.filters.status = status || 'all';
    this.pageNumber = 1;
    this.load();
  }

  setCreated(range: CreatedRange) {
    this.filters.created = range;
    if (range !== 'custom') {
      this.startDate = null;
      this.endDate = null;
      this.filters.createdFrom = '';
      this.filters.createdTo = '';
    }
    this.pageNumber = 1;
    this.load();
  }

  syncRange() {
    this.filters.createdFrom = this.toIso(this.startDate);
    this.filters.createdTo = this.toIso(this.endDate);
    this.pageNumber = 1;
    this.load();
  }

  onPage(event: PageEvent) {
    const sizeChanged = event.pageSize !== this.pageSize;
    this.pageSize = event.pageSize;
    this.pageNumber = sizeChanged ? 1 : event.pageIndex + 1;
    this.load();
  }

  statusLabel(status: string): string {
    const match = statusOptionsFor('all').find((option) => option.id === status);
    return match?.label ?? status;
  }

  canOpenProject(row: TrackRow): boolean {
    return row.projectId > 0 && this.hasProjectName(row.projectName);
  }

  canOpenSupplier(row: TrackRow): boolean {
    return row.supplierId > 0 && this.hasProjectName(row.supplierName);
  }

  validity(validUntil?: string | null) {
    return quotationValidity(validUntil);
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

  hasProjectName(value?: string | null): boolean {
    const name = (value ?? '').trim();
    return name.length > 0 && name !== '-' && name !== '—' && name !== '–';
  }

  private load() {
    this.loading = true;
    this.error = '';
    const token = ++this.loadToken;
    const dates = this.dateBounds();
    this.api.dashboard(
      this.filters.dataset,
      this.filters.status,
      dates.from,
      dates.to,
      this.pageNumber,
      this.pageSize,
    ).subscribe({
      next: (page) => {
        if (token !== this.loadToken) {
          return;
        }
        this.projectTotal = page.projectTotal ?? 0;
        this.activeProjectCount = page.activeProjectCount ?? 0;
        this.supplierTotal = page.supplierTotal ?? 0;
        this.quotationTotal = page.quotationCount ?? 0;
        this.quotedAmount = page.quotedValue ?? 0;
        this.openPurchaseOrders = page.openPurchaseOrderCount ?? 0;
        this.totalCount = page.totalCount ?? 0;
        const lastPage = Math.max(1, Math.ceil(this.totalCount / this.pageSize));
        if (this.totalCount > 0 && this.pageNumber > lastPage) {
          this.pageNumber = lastPage;
          this.load();
          return;
        }
        this.rows = (page.items ?? []).map((item) => this.toRow(item));
        this.loading = false;
      },
      error: () => {
        if (token !== this.loadToken) {
          return;
        }
        this.error = 'Dashboard could not be loaded.';
        this.rows = [];
        this.loading = false;
      },
    });
  }

  private toRow(item: DashboardTrackItem): TrackRow {
    const dataset = item.dataset === 'purchase-orders' ? 'purchase-orders' : 'quotations';
    return {
      key: `${dataset}-${item.id}`,
      dataset,
      status: (item.status || '').trim().toLowerCase(),
      code: item.code,
      supplierId: item.supplierId,
      supplierName: item.supplierName,
      supplierReference: item.supplierReference?.trim() || '',
      projectId: item.projectId || 0,
      projectName: item.projectName || '',
      projectReference: item.projectReference || '',
      itemCount: item.itemCount,
      baseTotalAmount: item.baseTotalAmount ?? 0,
      updatedAt: item.updatedAt || null,
      createdAt: item.createdAt || null,
      validUntil: item.validUntil || null,
      section: dataset === 'purchase-orders' ? 'purchases' : 'quotations',
    };
  }

  private dateBounds(): { from: string; to: string } {
    if (this.filters.created === 'custom') {
      return { from: this.filters.createdFrom, to: this.filters.createdTo };
    }
    if (this.filters.created === 'all') {
      return { from: '', to: '' };
    }
    const end = new Date();
    const start = new Date();
    if (this.filters.created === 'week') {
      start.setDate(start.getDate() - 7);
    } else if (this.filters.created === 'month') {
      start.setMonth(start.getMonth() - 1);
    } else if (this.filters.created === 'year') {
      start.setFullYear(start.getFullYear() - 1);
    }
    return { from: this.toIso(start), to: this.toIso(end) };
  }

  private toIso(value: Date | null): string {
    if (!value || Number.isNaN(value.getTime())) {
      return '';
    }
    const month = String(value.getMonth() + 1).padStart(2, '0');
    const day = String(value.getDate()).padStart(2, '0');
    return `${value.getFullYear()}-${month}-${day}`;
  }
}
