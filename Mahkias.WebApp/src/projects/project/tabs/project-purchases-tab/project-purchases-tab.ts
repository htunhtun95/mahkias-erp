import { HttpErrorResponse } from '@angular/common/http';
import { Component, EventEmitter, Input, OnChanges, OnDestroy, Output, SimpleChanges, inject } from '@angular/core';
import { MatDialog } from '@angular/material/dialog';
import { MatSnackBar } from '@angular/material/snack-bar';
import { Subscription } from 'rxjs';
import { MaterialModule } from '../../../../app/material-module';
import { Project, ProjectApiService, PurchaseOrderListItem } from '../../../../core/services/project-api.service';
import { formatMoney } from '../../../money-format';
import { ConfirmDialog } from '../../../confirm-dialog/confirm-dialog';
import { PurchaseOrderDialogComponent } from '../../../purchase-order-dialog/purchase-order-dialog';
import { PurchaseOrderSync } from '../../../purchase-order-sync';

@Component({
  selector: 'app-project-purchases-tab',
  standalone: true,
  imports: [MaterialModule],
  templateUrl: './project-purchases-tab.html',
  styleUrl: './project-purchases-tab.css',
})
export class ProjectPurchasesTabComponent implements OnChanges, OnDestroy {
  private api = inject(ProjectApiService);
  private dialog = inject(MatDialog);
  private snackBar = inject(MatSnackBar);
  private sync = inject(PurchaseOrderSync);
  private syncSub: Subscription;

  @Input() project: Project | null = null;
  @Output() countChange = new EventEmitter<number>();

  orders: PurchaseOrderListItem[] = [];
  loading = false;
  error = '';
  deletingId: number | null = null;

  constructor() {
    this.syncSub = this.sync.changes$.subscribe((projectId) => {
      if (projectId === this.project?.id) {
        this.load();
      }
    });
  }

  ngOnChanges(changes: SimpleChanges) {
    if (changes['project']?.currentValue?.id !== changes['project']?.previousValue?.id) {
      this.load();
    }
  }

  ngOnDestroy() {
    this.syncSub.unsubscribe();
  }

  get trackers() {
    return [
      { label: 'PO Sent', value: String(this.orders.length), tone: 'sent' },
      { label: 'In Transit', value: '0', tone: 'transit' },
      { label: 'Received', value: '0', tone: 'received' },
    ];
  }

  load() {
    if (!this.project) {
      this.orders = [];
      this.countChange.emit(0);
      return;
    }
    this.loading = true;
    this.error = '';
    this.api.purchaseOrders(this.project.id).subscribe({
      next: (orders) => {
        this.orders = orders;
        this.loading = false;
        this.countChange.emit(orders.length);
      },
      error: () => {
        this.loading = false;
        this.orders = [];
        this.error = 'Purchase orders could not be loaded.';
        this.countChange.emit(0);
      },
    });
  }

  edit(order: PurchaseOrderListItem) {
    if (!this.project || this.deletingId != null) {
      return;
    }
    const ref = this.dialog.open(PurchaseOrderDialogComponent, {
      disableClose: true,
      width: '95vw',
      maxWidth: '95vw',
      minWidth: 'min(720px, 95vw)',
      maxHeight: 'none',
      panelClass: ['add-project-dialog-panel', 'quotation-dialog-panel', 'app-dialog-pane'],
      backdropClass: 'add-project-dialog-backdrop',
      autoFocus: 'first-tabbable',
      data: {
        projectId: this.project.id,
        activities: this.project.activities ?? [],
        purchaseOrderId: order.id,
      },
    });
    ref.afterClosed().subscribe((saved) => {
      if (saved && this.project) {
        this.snackBar.open('Purchase order saved.', undefined, { duration: 3000 });
        this.sync.notify(this.project.id);
      }
    });
  }

  remove(order: PurchaseOrderListItem) {
    if (!this.project || this.deletingId != null) {
      return;
    }
    const ref = this.dialog.open(ConfirmDialog, {
      disableClose: true,
      width: '420px',
      panelClass: 'add-project-dialog-panel',
      backdropClass: 'add-project-dialog-backdrop',
      data: {
        title: 'Delete purchase order',
        message: 'Are you sure you want to delete this purchase order?',
        confirmLabel: 'Delete',
        cancelLabel: 'Cancel',
      },
    });
    ref.afterClosed().subscribe((confirmed) => {
      if (!confirmed || !this.project) {
        return;
      }
      this.deletingId = order.id;
      this.api.deletePurchaseOrder(order.id).subscribe({
        next: () => {
          this.deletingId = null;
          this.snackBar.open('Purchase order deleted.', undefined, { duration: 3000 });
          this.sync.notify(this.project!.id);
        },
        error: (error: HttpErrorResponse) => {
          this.deletingId = null;
          this.error = error.error?.message || 'The purchase order could not be deleted.';
        },
      });
    });
  }

  money(order: PurchaseOrderListItem): string {
    return formatMoney(order.baseTotalAmount, '$', 'USD');
  }

  when(value?: string | null): string {
    if (!value) {
      return '';
    }
    const date = new Date(value);
    if (Number.isNaN(date.getTime())) {
      return '';
    }
    return date.toLocaleDateString('en-US', { month: 'short', day: 'numeric', year: 'numeric' });
  }
}
