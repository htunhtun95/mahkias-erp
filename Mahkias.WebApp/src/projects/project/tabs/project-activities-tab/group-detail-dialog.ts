import { Component, inject } from '@angular/core';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { MaterialModule } from '../../../../app/material-module';
import { Activity, ActivityGroup } from '../../../../core/services/project-api.service';

@Component({
  selector: 'app-group-detail-dialog',
  standalone: true,
  imports: [MaterialModule],
  templateUrl: './group-detail-dialog.html',
  styleUrl: './group-detail-dialog.css',
})
export class GroupDetailDialog {
  private dialogRef = inject(MatDialogRef<GroupDetailDialog>);
  data = inject<{ group: ActivityGroup; activities: Activity[]; canDelete?: boolean }>(MAT_DIALOG_DATA);

  get activities(): Activity[] {
    return [...(this.data.activities ?? [])].sort((left, right) =>
      (left.dsnNo ?? '').localeCompare(right.dsnNo ?? '', undefined, { numeric: true, sensitivity: 'base' }),
    );
  }

  get itemCount(): number {
    return this.activities.length;
  }

  get itemLabel(): string {
    return this.itemCount === 1 ? '1 Activity' : `${this.itemCount} Activities`;
  }

  get totalQuantity(): string {
    const total = this.activities.reduce((sum, activity) => sum + (Number(activity.quantity) || 0), 0);
    return Number.isInteger(total) ? String(total) : String(Math.round(total * 100) / 100);
  }

  close() {
    this.dialogRef.close(false);
  }

  edit() {
    this.dialogRef.close('edit');
  }

  ungroup() {
    this.dialogRef.close('ungroup');
  }

  remove() {
    this.dialogRef.close('delete');
  }
}
