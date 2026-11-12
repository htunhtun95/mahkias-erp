import { Component, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { MaterialModule } from '../../app/material-module';

export interface QuotationItemDetails {
  dsnNo: string;
  partNo: string;
  minimumQty: string;
  serialNo: string;
  tsn: string;
  tso: string;
  csn: string;
  cso: string;
  readOnly?: boolean;
}

@Component({
  selector: 'app-quotation-item-details-dialog',
  standalone: true,
  imports: [FormsModule, MaterialModule],
  templateUrl: './quotation-item-details-dialog.html',
  styleUrl: './quotation-item-details-dialog.css',
})
export class QuotationItemDetailsDialog {
  private dialogRef = inject(MatDialogRef<QuotationItemDetailsDialog, QuotationItemDetails | false>);
  data = inject<QuotationItemDetails>(MAT_DIALOG_DATA);

  minimumQty = this.data.minimumQty ?? '';
  serialNo = this.data.serialNo ?? '';
  tsn = this.data.tsn ?? '';
  tso = this.data.tso ?? '';
  csn = this.data.csn ?? '';
  cso = this.data.cso ?? '';
  readonly readOnly = !!this.data.readOnly;
  error = '';

  get heading(): string {
    const dsn = this.data.dsnNo.trim();
    const part = this.data.partNo.trim();
    if (dsn && part) {
      return `DSN ${dsn} · ${part}`;
    }
    return dsn ? `DSN ${dsn}` : part || 'Quotation item';
  }

  cancel() {
    this.dialogRef.close(false);
  }

  confirm() {
    const minimumQty = this.minimumQty.trim();
    if (minimumQty) {
      const parsed = Number(minimumQty);
      if (!Number.isInteger(parsed) || parsed < 1) {
        this.error = 'MOQ must be a whole number, or left blank.';
        return;
      }
    }
    this.dialogRef.close({
      dsnNo: this.data.dsnNo,
      partNo: this.data.partNo,
      minimumQty,
      serialNo: this.serialNo.trim(),
      tsn: this.tsn.trim(),
      tso: this.tso.trim(),
      csn: this.csn.trim(),
      cso: this.cso.trim(),
    });
  }
}
