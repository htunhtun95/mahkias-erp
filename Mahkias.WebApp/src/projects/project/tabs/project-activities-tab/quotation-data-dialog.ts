import { Component, inject } from '@angular/core';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { MaterialModule } from '../../../../app/material-module';
import { ActivityQuotationLink } from '../../../../core/services/project-api.service';
import { formatMoney } from '../../../money-format';

export type QuotationViewKind = 'item' | 'group';

export interface QuotationDataRow {
  dsnNo: string | null;
  partNo: string | null;
  link: ActivityQuotationLink | null;
}

export interface QuotationDataState {
  kind: QuotationViewKind;
  activityId?: number;
  groupId?: number;
  title: string;
  subtitle: string;
  requiredQuantity?: number | null;
  rows: QuotationDataRow[];
}

@Component({
  selector: 'app-quotation-data-dialog',
  standalone: true,
  imports: [MaterialModule],
  templateUrl: './quotation-data-dialog.html',
  styleUrl: './quotation-data-dialog.css',
})
export class QuotationDataDialog {
  private dialogRef = inject(MatDialogRef<QuotationDataDialog>);
  data = inject<QuotationDataState>(MAT_DIALOG_DATA);

  get isGroup(): boolean {
    return this.data.kind === 'group';
  }

  get rows(): QuotationDataRow[] {
    return this.data.rows ?? [];
  }

  get heading(): string {
    return this.isGroup ? 'Lowest prices' : 'Quoted options';
  }

  get emptyMessage(): string {
    return this.isGroup ? 'No activities in this group.' : 'No quotations for this activity.';
  }

  get quoteReceived(): number {
    const required = Number(this.data.requiredQuantity ?? 0);
    if (!Number.isFinite(required) || required <= 0) {
      return 0;
    }
    const quoted = this.pricedRows().reduce((sum, link) => sum + (Number(link.quantity) || 0), 0);
    if (quoted >= required) {
      return 100;
    }
    return Math.max(0, Math.min(100, Math.round((100 * quoted) / required)));
  }

  get quotationStatus(): string {
    if (!this.pricedRows().length) {
      return 'Not quoted';
    }
    return this.quoteReceived >= 100 ? 'Quoted' : 'Partial';
  }

  close() {
    this.dialogRef.close(false);
  }

  quotedUnit(link: ActivityQuotationLink): string {
    if (link.unitPrice != null) {
      return formatMoney(link.unitPrice, link.currencySymbol, link.currencyCode);
    }
    return formatMoney(link.baseUnitPrice, '$', 'USD');
  }

  quotedTotal(link: ActivityQuotationLink): string {
    if (link.totalPrice != null) {
      return formatMoney(link.totalPrice, link.currencySymbol, link.currencyCode);
    }
    return formatMoney(link.baseTotalPrice, '$', 'USD');
  }

  private pricedRows(): ActivityQuotationLink[] {
    return this.rows
      .map((row) => row.link)
      .filter((link): link is ActivityQuotationLink => !!link && (link.unitPrice != null || link.totalPrice != null));
  }

  quantity(value?: number | null): string {
    if (value == null) {
      return '—';
    }
    return Number.isInteger(Number(value)) ? String(Number(value)) : String(value);
  }
}
