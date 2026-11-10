import { Component, OnInit, inject } from '@angular/core';
import { MAT_DIALOG_DATA, MatDialog, MatDialogRef } from '@angular/material/dialog';
import { forkJoin } from 'rxjs';
import { MaterialModule } from '../../../../app/material-module';
import { ProjectApiService, PurchaseOrderDetail, PurchaseOrderDocument, PurchaseOrderHistoryEntry, PurchaseOrderLine, PurchaseOrderListItem, QuotationCharge } from '../../../../core/services/project-api.service';
import { formatFileSize } from '../../../document-manager/document-manager';
import { documentAccept, openDocumentLibrary } from '../../../document-library-dialog/document-library-dialog';
import { formatMoney } from '../../../money-format';
import { leadTimeLabel } from '../../../lead-time';

export interface PurchaseOrderDetailDialogData {
  projectId: number;
  order: PurchaseOrderListItem;
}

@Component({
  selector: 'app-purchase-order-detail-dialog',
  standalone: true,
  imports: [MaterialModule],
  templateUrl: './purchase-order-detail-dialog.html',
  styleUrl: './purchase-order-detail-dialog.css',
})
export class PurchaseOrderDetailDialog implements OnInit {
  readonly leadTimeLabel = leadTimeLabel;
  private api = inject(ProjectApiService);
  private dialog = inject(MatDialog);
  private dialogRef = inject(MatDialogRef<PurchaseOrderDetailDialog>);
  data = inject<PurchaseOrderDetailDialogData>(MAT_DIALOG_DATA);

  loading = true;
  error = '';
  detail: PurchaseOrderDetail | null = null;
  documents: PurchaseOrderDocument[] = [];

  ngOnInit() {
    forkJoin({
      detail: this.api.purchaseOrder(this.data.order.id),
      documents: this.api.purchaseOrderDocuments(this.data.projectId, this.data.order.id),
    }).subscribe({
      next: ({ detail, documents }) => {
        this.detail = detail;
        this.documents = documents ?? [];
        this.loading = false;
      },
      error: () => {
        this.loading = false;
        this.error = 'Purchase order details could not be loaded.';
      },
    });
  }

  get order(): PurchaseOrderListItem {
    return this.detail ?? this.data.order;
  }

  get lines(): PurchaseOrderLine[] {
    return this.detail?.items ?? [];
  }

  get charges(): QuotationCharge[] {
    return this.detail?.charges ?? [];
  }

  get history(): PurchaseOrderHistoryEntry[] {
    return this.detail?.history ?? [];
  }

  headerMoney(value?: number | null): string {
    return formatMoney(value, this.order.currencySymbol, this.order.currencyCode);
  }

  lineMoney(amount: number, line: PurchaseOrderLine): string {
    return formatMoney(amount, line.currencySymbol || this.order.currencySymbol, line.currencyCode || this.order.currencyCode);
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

  activityLabel(line: PurchaseOrderLine): string {
    if (!line.activityId) {
      return 'Not linked';
    }
    const dsn = (line.dsnNo || '').trim();
    const part = (line.partNo || '').trim();
    const label = dsn ? `DSN ${dsn}` : `Activity ${line.activityId}`;
    return part ? `${label} · ${part}` : label;
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

  fileSize = formatFileSize;

  fileKind(fileName: string): string {
    const kind = extension(fileName);
    return kind ? kind.toUpperCase() : 'File';
  }

  manageDocuments() {
    openDocumentLibrary(this.dialog, {
      title: 'Manage Documents',
      note: `Files for ${this.data.order.code}. Choose the original, then set the order.`,
      kind: 'purchase-order',
      projectId: this.data.projectId,
      ownerId: this.data.order.id,
      documents: this.documents,
      accept: documentAccept,
    }).afterClosed().subscribe((result) => {
      if (!result?.saved) {
        return;
      }
      this.api.purchaseOrderDocuments(this.data.projectId, this.data.order.id).subscribe({
        next: (documents) => {
          this.documents = documents ?? [];
        },
      });
    });
  }

  close() {
    this.dialogRef.close(false);
  }
}

function extension(fileName: string): string {
  const index = fileName.lastIndexOf('.');
  return index >= 0 ? fileName.slice(index + 1).toLowerCase() : '';
}
