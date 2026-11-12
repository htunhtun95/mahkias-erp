import { Component, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { MaterialModule } from '../../app/material-module';

export interface ActivityItemDetails {
  dsnNo: string;
  partNo: string;
  label: string;
  serialNo: string;
  tsn: string;
  tso: string;
  csn: string;
  cso: string;
}

@Component({
  selector: 'app-activity-item-details-dialog',
  standalone: true,
  imports: [FormsModule, MaterialModule],
  templateUrl: './activity-item-details-dialog.html',
  styleUrl: './activity-item-details-dialog.css',
})
export class ActivityItemDetailsDialog {
  private dialogRef = inject(MatDialogRef<ActivityItemDetailsDialog, ActivityItemDetails | false>);
  data = inject<ActivityItemDetails>(MAT_DIALOG_DATA);

  label = this.data.label ?? '';
  serialNo = this.data.serialNo ?? '';
  tsn = this.data.tsn ?? '';
  tso = this.data.tso ?? '';
  csn = this.data.csn ?? '';
  cso = this.data.cso ?? '';

  get heading(): string {
    const dsn = (this.data.dsnNo ?? '').trim();
    const part = (this.data.partNo ?? '').trim();
    if (dsn && part) {
      return `DSN ${dsn} · ${part}`;
    }
    return dsn ? `DSN ${dsn}` : part || 'Activity';
  }

  cancel() {
    this.dialogRef.close(false);
  }

  confirm() {
    this.dialogRef.close({
      dsnNo: this.data.dsnNo,
      partNo: this.data.partNo,
      label: this.label.trim(),
      serialNo: this.serialNo.trim(),
      tsn: this.tsn.trim(),
      tso: this.tso.trim(),
      csn: this.csn.trim(),
      cso: this.cso.trim(),
    });
  }
}
