import { Component, OnInit, inject } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { Router, RouterModule } from '@angular/router';
import { MatDialog } from '@angular/material/dialog';
import { PageEvent } from '@angular/material/paginator';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MaterialModule } from '../app/material-module';
import { whileBusy } from '../core/while-busy';
import { ProjectApiService, Supplier } from '../core/services/project-api.service';
import { ConfirmDialog } from '../projects/confirm-dialog/confirm-dialog';
import { formatProjectListDate } from '../projects/projects';
import { SupplierDialog } from './supplier-dialog/supplier-dialog';

@Component({
  selector: 'app-suppliers',
  standalone: true,
  imports: [RouterModule, MaterialModule],
  templateUrl: './suppliers.html',
  styleUrl: './suppliers.css',
})
export class SuppliersComponent implements OnInit {
  private api = inject(ProjectApiService);
  private dialog = inject(MatDialog);
  private router = inject(Router);
  private snackBar = inject(MatSnackBar);

  suppliers: Supplier[] = [];
  searchTerm = '';
  loading = true;
  deletingId: number | null = null;
  error = '';
  pageIndex = 0;
  pageSize = 10;
  private searchTimer: ReturnType<typeof setTimeout> | undefined;

  ngOnInit() {
    this.load();
  }

  get isDeleting() {
    return this.deletingId != null;
  }

  get page() {
    const start = this.pageIndex * this.pageSize;
    return this.suppliers.slice(start, start + this.pageSize);
  }

  onSearch(value: string) {
    this.searchTerm = value;
    this.pageIndex = 0;
    clearTimeout(this.searchTimer);
    this.searchTimer = setTimeout(() => this.load(), 250);
  }

  onPage(event: PageEvent) {
    this.pageIndex = event.pageIndex;
    this.pageSize = event.pageSize;
  }

  load() {
    this.loading = this.suppliers.length === 0;
    this.error = '';
    this.api.suppliers(this.searchTerm).subscribe({
      next: (suppliers) => {
        this.suppliers = suppliers;
        const lastPage = Math.max(0, Math.ceil(suppliers.length / this.pageSize) - 1);
        if (this.pageIndex > lastPage) {
          this.pageIndex = lastPage;
        }
        this.loading = false;
      },
      error: () => {
        this.error = 'Suppliers could not be loaded.';
        this.loading = false;
      },
    });
  }

  listDate(supplier: Supplier) {
    return formatProjectListDate(supplier.createdAt);
  }

  openAdd() {
    const ref = this.dialog.open(SupplierDialog, this.dialogConfig());
    ref.afterClosed().subscribe((saved: Supplier | boolean | undefined) => {
      if (saved && typeof saved === 'object') {
        this.snackBar.open('Supplier saved.', undefined, { duration: 3000 });
        this.router.navigate(['/suppliers', saved.id]);
      }
    });
  }

  openEdit(event: Event, supplier: Supplier) {
    event.stopPropagation();
    if (this.isDeleting) {
      return;
    }
    const ref = this.dialog.open(SupplierDialog, this.dialogConfig({ supplier }));
    ref.afterClosed().subscribe((saved) => {
      if (saved) {
        this.snackBar.open('Supplier saved.', undefined, { duration: 3000 });
        this.load();
      }
    });
  }

  openDetails(supplier: Supplier, event?: Event) {
    if (this.isDeleting || this.eventFromActions(event)) {
      return;
    }
    this.router.navigate(['/suppliers', supplier.id]);
  }

  openDelete(event: Event, supplier: Supplier) {
    event.stopPropagation();
    if (this.isDeleting) {
      return;
    }
    const blocked = (supplier.quotationCount ?? 0) > 0;
    const ref = this.dialog.open(ConfirmDialog, {
      disableClose: true,
      width: '480px',
      maxWidth: 'calc(100vw - 32px)',
      panelClass: 'add-project-dialog-panel',
      backdropClass: 'add-project-dialog-backdrop',
      data: blocked
        ? {
            title: 'Delete Supplier',
            message: 'This supplier cannot be deleted because it has quotations.',
            confirmLabel: 'Delete',
            allowConfirm: false,
            cancelLabel: 'Close',
          }
        : {
            title: 'Delete Supplier',
            message: 'Are you sure you want to delete this supplier?',
            confirmLabel: 'Delete',
          },
    });
    ref.afterClosed().subscribe((confirmed) => {
      if (!confirmed || blocked || this.isDeleting) {
        return;
      }
      this.api.deleteSupplier(supplier.id).pipe(whileBusy((busy) => (this.deletingId = busy ? supplier.id : null))).subscribe({
        next: () => {
          this.snackBar.open('Supplier deleted.', undefined, { duration: 3000 });
          this.load();
        },
        error: (error: HttpErrorResponse) => {
          this.error = error.status === 409
            ? 'This supplier cannot be deleted because it has quotations.'
            : 'This supplier could not be deleted.';
        },
      });
    });
  }

  private dialogConfig(data?: { supplier: Supplier }) {
    return {
      disableClose: true,
      width: '520px',
      maxWidth: 'calc(100vw - 32px)',
      panelClass: 'add-project-dialog-panel',
      backdropClass: 'add-project-dialog-backdrop',
      autoFocus: 'first-tabbable' as const,
      data,
    };
  }

  private eventFromActions(event?: Event) {
    return !!event && (event.target as HTMLElement).closest('.row-actions');
  }
}
