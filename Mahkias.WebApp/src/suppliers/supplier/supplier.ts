import { Component, OnInit, inject } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { MatDialog } from '@angular/material/dialog';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MaterialModule } from '../../app/material-module';
import { whileBusy } from '../../core/while-busy';
import { ProjectApiService, SupplierDetail, SupplierQuotation } from '../../core/services/project-api.service';
import { ConfirmDialog } from '../../projects/confirm-dialog/confirm-dialog';
import { formatProjectListDate } from '../../projects/projects';
import { SupplierDialog } from '../supplier-dialog/supplier-dialog';

@Component({
  selector: 'app-supplier',
  standalone: true,
  imports: [RouterModule, MaterialModule],
  templateUrl: './supplier.html',
  styleUrl: './supplier.css',
})
export class SupplierDetailsComponent implements OnInit {
  private api = inject(ProjectApiService);
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private dialog = inject(MatDialog);
  private snackBar = inject(MatSnackBar);

  supplier: SupplierDetail | null = null;
  loading = true;
  isDeleting = false;
  error = '';

  ngOnInit() {
    this.route.paramMap.subscribe((params) => this.load(Number(params.get('id'))));
  }

  load(id: number) {
    this.loading = !this.supplier;
    this.error = '';
    this.api.supplier(id).subscribe({
      next: (supplier) => {
        this.supplier = supplier;
        this.loading = false;
      },
      error: () => {
        this.supplier = null;
        this.loading = false;
        this.error = 'This supplier could not be loaded.';
      },
    });
  }

  listDate(value?: string | null) {
    return formatProjectListDate(value);
  }

  openEdit() {
    if (!this.supplier || this.isDeleting) {
      return;
    }
    const supplierId = this.supplier.id;
    const ref = this.dialog.open(SupplierDialog, {
      disableClose: true,
      width: '520px',
      maxWidth: 'calc(100vw - 32px)',
      panelClass: 'add-project-dialog-panel',
      backdropClass: 'add-project-dialog-backdrop',
      autoFocus: 'first-tabbable',
      data: { supplier: this.supplier },
    });
    ref.afterClosed().subscribe((saved) => {
      if (saved) {
        this.snackBar.open('Supplier saved.', undefined, { duration: 3000 });
        this.load(supplierId);
      }
    });
  }

  openDelete() {
    if (!this.supplier || this.isDeleting) {
      return;
    }
    const blocked = (this.supplier.quotationCount ?? 0) > 0;
    const supplierId = this.supplier.id;
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
      this.api.deleteSupplier(supplierId).pipe(whileBusy((busy) => (this.isDeleting = busy))).subscribe({
        next: () => {
          this.snackBar.open('Supplier deleted.', undefined, { duration: 3000 });
          this.router.navigate(['/suppliers']);
        },
        error: (error: HttpErrorResponse) => {
          this.error = error.status === 409
            ? 'This supplier cannot be deleted because it has quotations.'
            : 'This supplier could not be deleted.';
        },
      });
    });
  }

  projectLink(quotation: SupplierQuotation) {
    return quotation.projectId ? ['/projects', quotation.projectId] : null;
  }
}
