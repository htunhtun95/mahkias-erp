import { HttpErrorResponse } from '@angular/common/http';
import { Component, ElementRef, EventEmitter, Input, OnChanges, OnDestroy, Output, SimpleChanges, inject } from '@angular/core';
import { MatDialog } from '@angular/material/dialog';
import { MatSnackBar } from '@angular/material/snack-bar';
import { forkJoin, Subscription } from 'rxjs';
import { MaterialModule } from '../../../../app/material-module';
import { Project, ProjectApiService, PurchaseOrderDetail, PurchaseOrderDocument, PurchaseOrderHistoryEntry, PurchaseOrderLine, PurchaseOrderListItem, QuotationCharge } from '../../../../core/services/project-api.service';
import { formatMoney } from '../../../money-format';
import { ConfirmDialog } from '../../../confirm-dialog/confirm-dialog';
import { PurchaseOrderDialogComponent } from '../../../purchase-order-dialog/purchase-order-dialog';
import { ProcurementTrace } from '../../../procurement-trace';
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
  private trace = inject(ProcurementTrace);
  private host = inject(ElementRef<HTMLElement>);
  private syncSub: Subscription;

  @Input() project: Project | null = null;
  @Output() countChange = new EventEmitter<number>();

  orders: PurchaseOrderListItem[] = [];
  loading = false;
  error = '';
  deletingId: number | null = null;
  expanded = new Set<number>();
  lines = new Map<number, PurchaseOrderLine[]>();
  details = new Map<number, PurchaseOrderDetail>();
  documents = new Map<number, PurchaseOrderDocument[]>();
  loadingLine = new Set<number>();

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
    const count = (code: string) => String(this.orders.filter((order) => (order.statusCode || 'Draft') === code).length);
    return [
      { label: 'Draft', value: count('Draft'), tone: 'draft-count' },
      { label: 'Sent', value: count('Sent'), tone: 'sent' },
      { label: 'Revised', value: count('Revised'), tone: 'revised' },
      { label: 'Cancelled', value: count('Cancelled'), tone: 'cancelled' },
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
        const ids = new Set(orders.map((order) => order.id));
        for (const id of [...this.expanded]) {
          if (!ids.has(id)) {
            this.expanded.delete(id);
            this.lines.delete(id);
            this.details.delete(id);
            this.documents.delete(id);
            this.loadingLine.delete(id);
          } else {
            this.fetchLines(id);
          }
        }
      },
      error: () => {
        this.loading = false;
        this.orders = [];
        this.error = 'Purchase orders could not be loaded.';
        this.countChange.emit(0);
      },
    });
  }

  toggle(order: PurchaseOrderListItem) {
    if (this.expanded.has(order.id)) {
      this.expanded.delete(order.id);
      return;
    }
    this.expanded.add(order.id);
    if (!this.lines.has(order.id)) {
      this.fetchLines(order.id);
    }
  }

  isExpanded(id: number): boolean {
    return this.expanded.has(id);
  }

  reveal(id: number) {
    this.expanded.add(id);
    if (!this.lines.has(id)) {
      this.fetchLines(id);
    }
    setTimeout(() => {
      this.host.nativeElement.querySelector(`[data-order-id="${id}"]`)?.scrollIntoView({ block: 'center' });
    });
  }

  openActivity(line: PurchaseOrderLine) {
    if (this.project?.id && line.activityId) {
      this.trace.showActivity(this.project.id, line.activityId);
    }
  }

  lineItems(id: number): PurchaseOrderLine[] {
    return this.lines.get(id) ?? [];
  }

  orderDetail(id: number): PurchaseOrderDetail | null {
    return this.details.get(id) ?? null;
  }

  chargesFor(id: number): QuotationCharge[] {
    return this.orderDetail(id)?.charges ?? [];
  }

  filesFor(id: number): PurchaseOrderDocument[] {
    return this.documents.get(id) ?? [];
  }

  fileIcon(fileName: string): string {
    const kind = extension(fileName);
    if (kind === 'pdf') {
      return 'picture_as_pdf';
    }
    if (kind === 'xls' || kind === 'xlsx' || kind === 'csv') {
      return 'table_chart';
    }
    if (kind === 'png' || kind === 'jpg' || kind === 'jpeg' || kind === 'gif' || kind === 'webp') {
      return 'image';
    }
    if (kind === 'doc' || kind === 'docx') {
      return 'description';
    }
    return 'draft';
  }

  fileKind(fileName: string): string {
    const kind = extension(fileName);
    return kind ? kind.toUpperCase() : 'File';
  }

  headerMoney(order: PurchaseOrderListItem, value?: number | null): string {
    return formatMoney(value, order.currencySymbol, order.currencyCode);
  }

  activityLabel(line: PurchaseOrderLine): string {
    if (!line.activityId) {
      return 'Not linked';
    }
    const activity = this.project?.activities?.find((item) => item.id === line.activityId);
    const dsn = (line.dsnNo || activity?.dsnNo || '').trim();
    const part = (line.partNo || activity?.mainPartNo || activity?.partNo || '').trim();
    const label = dsn ? `DSN ${dsn}` : `Activity ${line.activityId}`;
    return part ? `${label} · ${part}` : label;
  }

  lineMoney(amount: number, line: PurchaseOrderLine): string {
    return formatMoney(amount, line.currencySymbol || '$', line.currencyCode || 'USD');
  }

  private fetchLines(id: number) {
    if (!this.project) {
      return;
    }
    this.loadingLine.add(id);
    forkJoin({
      detail: this.api.purchaseOrder(id),
      documents: this.api.purchaseOrderDocuments(this.project.id, id),
    }).subscribe({
      next: ({ detail, documents }) => {
        this.lines.set(id, detail.items ?? []);
        this.details.set(id, detail);
        this.documents.set(id, documents ?? []);
        this.loadingLine.delete(id);
      },
      error: () => {
        this.lines.set(id, []);
        this.details.delete(id);
        this.documents.delete(id);
        this.loadingLine.delete(id);
        this.error = 'Purchase order lines could not be loaded.';
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

  whenStamp(value?: string | null): string {
    if (!value) {
      return '';
    }
    const date = new Date(value);
    if (Number.isNaN(date.getTime())) {
      return '';
    }
    return date.toLocaleString(undefined, { year: 'numeric', month: 'short', day: 'numeric', hour: 'numeric', minute: '2-digit' });
  }

  historyFor(id: number): PurchaseOrderHistoryEntry[] {
    return this.orderDetail(id)?.history ?? [];
  }

  historyLabel(action: string): string {
    switch (action) {
      case 'StatusChanged':
        return 'Status changed';
      case 'Created':
        return 'Created';
      case 'Edited':
        return 'Edited';
      case 'Revised':
        return 'Revised';
      case 'Deleted':
        return 'Deleted';
      default:
        return action || 'Updated';
    }
  }

  historyActor(entry: PurchaseOrderHistoryEntry): string {
    const name = entry.actorName?.trim();
    const email = entry.actorEmail?.trim();
    if (name && email) {
      return `${name} · ${email}`;
    }
    return name || email || 'Unknown user';
  }

  statusClass(code?: string | null): string {
    return (code || 'Draft').toLowerCase();
  }
}

function extension(fileName: string): string {
  const index = fileName.lastIndexOf('.');
  return index >= 0 ? fileName.slice(index + 1).toLowerCase() : '';
}
