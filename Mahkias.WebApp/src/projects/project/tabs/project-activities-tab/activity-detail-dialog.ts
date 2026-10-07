import { Component, inject } from '@angular/core';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { MaterialModule } from '../../../../app/material-module';
import { Activity, ActivityQuotationLink } from '../../../../core/services/project-api.service';

export interface ActivityDetailState {
  activity: Activity;
  links: ActivityQuotationLink[];
}

@Component({
  selector: 'app-activity-detail-dialog',
  standalone: true,
  imports: [MaterialModule],
  templateUrl: './activity-detail-dialog.html',
  styleUrl: './activity-detail-dialog.css',
})
export class ActivityDetailDialog {
  private dialogRef = inject(MatDialogRef<ActivityDetailDialog>);
  data = inject<ActivityDetailState>(MAT_DIALOG_DATA);

  get activity(): Activity {
    return this.data.activity;
  }

  get links(): ActivityQuotationLink[] {
    return this.data.links ?? [];
  }

  get alternativeParts(): string[] {
    return (this.activity.alternativePartNos ?? []).filter((part) => !!part?.trim());
  }

  get quoteReceived(): number {
    const required = Number(this.activity.quantity ?? 0);
    if (!Number.isFinite(required) || required <= 0) {
      return 0;
    }
    const quoted = this.links.reduce((sum, link) => sum + (Number(link.quantity) || 0), 0);
    return this.clamp((quoted / required) * 100);
  }

  get deliveryProgress(): number {
    return this.clamp(this.activity.deliveryProgress);
  }

  get quotationStatus(): string {
    const received = this.quoteReceived;
    if (!this.links.length) {
      return 'Not quoted';
    }
    if (received >= 100) {
      return 'Quoted';
    }
    return 'Partial';
  }

  close() {
    this.dialogRef.close(false);
  }

  money(value?: number | null): string {
    if (value == null) {
      return '—';
    }
    return value.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 4 });
  }

  quantity(value?: number | null): string {
    if (value == null) {
      return '—';
    }
    return Number.isInteger(Number(value)) ? String(Number(value)) : String(value);
  }

  private clamp(value?: number | null): number {
    const number = Number(value ?? 0);
    if (!Number.isFinite(number)) {
      return 0;
    }
    return Math.max(0, Math.min(100, Math.round(number)));
  }
}
