import { Component, OnInit, inject } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { Router, RouterModule } from '@angular/router';
import { MatDialog } from '@angular/material/dialog';
import { PageEvent } from '@angular/material/paginator';
import { MatSnackBar } from '@angular/material/snack-bar';
import { Sort } from '@angular/material/sort';
import { MaterialModule } from '../app/material-module';
import { whileBusy } from '../core/while-busy';
import { ProjectApiService, QuotationListItem } from '../core/services/project-api.service';
import { ConfirmDialog } from '../projects/confirm-dialog/confirm-dialog';
import { formatMoney } from '../projects/money-format';
import { quotationValidity } from '../projects/quotation-terms';
import { openQuotationEditor, openQuotationImport } from './quotation-pane';

type QuoteSort = 'code' | 'supplier' | 'quotedDate' | 'total';

@Component({
  selector: 'app-quotations',
  standalone: true,
  imports: [RouterModule, MaterialModule],
  templateUrl: './quotations.html',
  styleUrl: './quotations.css',
})
export class QuotationsComponent implements OnInit {
  private api = inject(ProjectApiService);
  private dialog = inject(MatDialog);
  private router = inject(Router);
  private snackBar = inject(MatSnackBar);

  quotations: QuotationListItem[] = [];
  searchTerm = '';
  sortBy: QuoteSort = 'code';
  sortDirection: 'asc' | 'desc' = 'desc';
  loading = true;
  deletingId: number | null = null;
  error = '';
  pageIndex = 0;
  pageSize = 10;

  ngOnInit() {
    this.load();
  }

  get isDeleting() {
    return this.deletingId != null;
  }

  get filtered() {
    const query = this.searchTerm.trim().toLowerCase();
    const rows = query
      ? this.quotations.filter((quotation) => this.searchText(quotation).includes(query))
      : this.quotations;
    const direction = this.sortDirection === 'asc' ? 1 : -1;
    return [...rows].sort((left, right) => {
      const compared = this.compare(left, right);
      return compared === 0 ? right.id - left.id : compared * direction;
    });
  }

  get page() {
    const start = this.pageIndex * this.pageSize;
    return this.filtered.slice(start, start + this.pageSize);
  }

  onSort(event: Sort) {
    const active = event.active;
    this.sortBy = active === 'code' || active === 'supplier' || active === 'total' ? active : 'quotedDate';
    this.sortDirection = event.direction === 'asc' ? 'asc' : 'desc';
    this.pageIndex = 0;
  }

  onSearch(value: string) {
    this.searchTerm = value;
    this.pageIndex = 0;
  }

  onPage(event: PageEvent) {
    this.pageIndex = event.pageIndex;
    this.pageSize = event.pageSize;
  }

  load() {
    this.loading = this.quotations.length === 0;
    this.error = '';
    this.api.allQuotations().subscribe({
      next: (quotations) => {
        this.quotations = quotations ?? [];
        const lastPage = Math.max(0, Math.ceil(this.filtered.length / this.pageSize) - 1);
        if (this.pageIndex > lastPage) {
          this.pageIndex = lastPage;
        }
        this.loading = false;
      },
      error: () => {
        this.error = 'Quotations could not be loaded.';
        this.loading = false;
      },
    });
  }

  projectText(quotation: QuotationListItem) {
    const label = quotation.projectLabel?.trim();
    return label || '';
  }

  projectLinks(quotation: QuotationListItem) {
    const ids = quotation.projectIds ?? [];
    const labels = (quotation.projectLabel ?? '')
      .split(',')
      .map((part) => part.trim())
      .filter((part) => part.length > 0);
    if (ids.length === 1) {
      return [{ id: ids[0], label: (quotation.projectLabel ?? '').trim() || labels[0] || 'Project' }];
    }
    return ids.map((id, index) => ({
      id,
      label: labels[index] || 'Project',
    }));
  }

  linked(quotation: QuotationListItem) {
    return (quotation.projectIds?.length ?? 0) > 0;
  }

  money(quotation: QuotationListItem) {
    return formatMoney(quotation.totalPrice, '$', 'USD');
  }

  when(value?: string | null) {
    if (!value) {
      return '—';
    }
    const date = new Date(value);
    if (Number.isNaN(date.getTime())) {
      return '—';
    }
    return date.toLocaleDateString('en-US', { month: 'short', day: 'numeric', year: 'numeric' });
  }

  validity(quotation: QuotationListItem) {
    return quotationValidity(quotation.validUntil);
  }

  openDetails(quotation: QuotationListItem, event?: Event) {
    if (this.isDeleting) {
      return;
    }
    const target = event?.target;
    if (target instanceof Element && target.closest('.row-actions, a, button')) {
      return;
    }
    this.router.navigate(['/quotations', quotation.id]);
  }

  openAdd() {
    const ref = openQuotationEditor(this.dialog, { projectId: null, activities: [] });
    ref.afterClosed().subscribe((saved) => {
      if (saved) {
        this.snackBar.open('Quotation saved.', undefined, { duration: 3000 });
        this.load();
      }
    });
  }

  openImport() {
    const ref = openQuotationImport(this.dialog);
    ref.afterClosed().subscribe((saved) => {
      if (saved) {
        this.snackBar.open('Quotation imported.', undefined, { duration: 3000 });
        this.load();
      }
    });
  }

  remove(event: Event, quotation: QuotationListItem) {
    event.stopPropagation();
    if (this.isDeleting) {
      return;
    }
    const ref = this.dialog.open(ConfirmDialog, {
      disableClose: true,
      width: '480px',
      maxWidth: 'calc(100vw - 32px)',
      panelClass: 'add-project-dialog-panel',
      backdropClass: 'add-project-dialog-backdrop',
      data: {
        title: 'Delete Quotation',
        message: 'Are you sure you want to delete this quotation? Its line items will be permanently removed.',
        confirmLabel: 'Delete',
        cancelLabel: 'Cancel',
      },
    });
    ref.afterClosed().subscribe((confirmed) => {
      if (!confirmed || this.isDeleting) {
        return;
      }
      this.api.deleteQuotation(quotation.id).pipe(whileBusy((busy) => (this.deletingId = busy ? quotation.id : null))).subscribe({
        next: () => {
          const projectId = quotation.projectIds?.[0];
          if (projectId) {
            this.api.forgetProject(projectId);
          }
          this.snackBar.open('Quotation deleted.', undefined, { duration: 3000 });
          this.load();
        },
        error: (error: HttpErrorResponse) => {
          this.error = error.error?.message || 'This quotation could not be deleted.';
        },
      });
    });
  }

  private searchText(quotation: QuotationListItem) {
    return [
      quotation.code,
      quotation.supplierName,
      quotation.supplierReference,
      this.projectText(quotation),
    ].filter((value) => !!value).join(' ').toLowerCase();
  }

  private compare(left: QuotationListItem, right: QuotationListItem) {
    if (this.sortBy === 'code') {
      return left.code.localeCompare(right.code, undefined, { numeric: true, sensitivity: 'base' });
    }
    if (this.sortBy === 'supplier') {
      return (left.supplierName || '').localeCompare(right.supplierName || '', undefined, { sensitivity: 'base' });
    }
    if (this.sortBy === 'total') {
      return (left.totalPrice || 0) - (right.totalPrice || 0);
    }
    return this.time(left.quotedDate) - this.time(right.quotedDate);
  }

  private time(value?: string | null) {
    const time = Date.parse(value ?? '');
    return Number.isNaN(time) ? 0 : time;
  }
}
