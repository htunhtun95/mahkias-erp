import { Component, inject, OnInit } from '@angular/core';
import { MAT_DIALOG_DATA, MatDialog, MatDialogRef } from '@angular/material/dialog';
import { forkJoin } from 'rxjs';
import { MaterialModule } from '../../../../app/material-module';
import { ProjectApiService, QuotationCharge, QuotationDetail, QuotationDocument, QuotationLine, QuotationListItem } from '../../../../core/services/project-api.service';
import { closeOnEscape } from '../../../../core/close-on-escape';
import { formatFileSize } from '../../../document-manager/document-manager';
import { documentAccept, openDocumentLibrary } from '../../../document-library-dialog/document-library-dialog';
import { formatMoney } from '../../../money-format';
import { leadTimeLabel } from '../../../lead-time';
import { quotationQuantity, quotationValidity } from '../../../quotation-terms';
import { usageDetails } from '../../../usage-fields';

export interface QuotationDetailDialogData {
  projectId: number;
  quotation: QuotationListItem;
}

@Component({
  selector: 'app-quotation-detail-dialog',
  standalone: true,
  imports: [MaterialModule],
  templateUrl: './quotation-detail-dialog.html',
  styleUrl: './quotation-detail-dialog.css',
})
export class QuotationDetailDialog implements OnInit {
  readonly leadTimeLabel = leadTimeLabel;
  readonly usageDetails = usageDetails;
  private api = inject(ProjectApiService);
  private dialog = inject(MatDialog);
  private dialogRef = inject(MatDialogRef<QuotationDetailDialog>);
  data = inject<QuotationDetailDialogData>(MAT_DIALOG_DATA);

  loading = true;
  error = '';
  detail: QuotationDetail | null = null;
  documents: QuotationDocument[] = [];

  ngOnInit() {
    closeOnEscape(this.dialogRef, () => this.close());
    forkJoin({
      detail: this.api.quotation(this.data.quotation.id),
      documents: this.api.quotationDocuments(this.data.projectId, this.data.quotation.id),
    }).subscribe({
      next: ({ detail, documents }) => {
        this.detail = detail;
        this.documents = documents ?? [];
        this.loading = false;
      },
      error: () => {
        this.loading = false;
        this.error = 'Quotation details could not be loaded.';
      },
    });
  }

  get quotation(): QuotationListItem {
    return this.detail ?? this.data.quotation;
  }

  get lines(): QuotationLine[] {
    return this.detail?.items ?? [];
  }

  get charges(): QuotationCharge[] {
    return this.detail?.charges ?? [];
  }

  get chargesTotal(): number {
    return this.charges.reduce((sum, charge) => sum + (Number(charge.amount) || 0), 0);
  }

  get validityLabel(): string {
    return quotationValidity(this.quotation.validUntil)?.label ?? '';
  }

  get validityExpired(): boolean {
    return quotationValidity(this.quotation.validUntil)?.expired ?? false;
  }

  money(value?: number | null, symbol?: string | null, code?: string | null): string {
    return formatMoney(value, symbol, code);
  }

  headerMoney(value?: number | null): string {
    return formatMoney(value, this.quotation.currencySymbol, this.quotation.currencyCode);
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

  quantity(line: QuotationLine) {
    return quotationQuantity(line.quantity, line.minimumQty);
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
      note: `Files for ${this.data.quotation.code}. Choose the original, then set the order.`,
      kind: 'quotation',
      projectId: this.data.projectId,
      ownerId: this.data.quotation.id,
      documents: this.documents,
      accept: documentAccept,
    }).afterClosed().subscribe((result) => {
      if (!result?.saved) {
        return;
      }
      this.api.quotationDocuments(this.data.projectId, this.data.quotation.id).subscribe({
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
