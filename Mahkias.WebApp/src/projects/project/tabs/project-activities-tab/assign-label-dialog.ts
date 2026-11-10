import { Component, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { MaterialModule } from '../../../../app/material-module';
import { ActivityLabel } from '../../../../core/services/project-api.service';

@Component({
  selector: 'app-assign-label-dialog',
  standalone: true,
  imports: [FormsModule, MaterialModule],
  templateUrl: './assign-label-dialog.html',
  styleUrl: './assign-label-dialog.css',
})
export class AssignLabelDialog {
  private dialogRef = inject(MatDialogRef<AssignLabelDialog>);
  data = inject<{ labels: ActivityLabel[]; activityIds?: number[]; groupIds?: number[]; selectedIds?: number[]; kind?: 'activity' | 'group' }>(MAT_DIALOG_DATA);
  selected = new Set<number>(this.sharedLabelIds());
  name = '';
  submitted = false;

  cancel() {
    this.dialogRef.close(false);
  }

  toggle(id: number, checked: boolean) {
    if (checked) {
      this.selected.add(id);
      return;
    }
    this.selected.delete(id);
  }

  save() {
    this.submitted = true;
    const name = this.name.trim();
    if (!this.selected.size && !name) {
      return;
    }
    this.dialogRef.close({ labelIds: [...this.selected], name });
  }

  private sharedLabelIds(): number[] {
    const activityIds = this.data.activityIds ?? (this.data.kind === 'group' ? [] : this.data.selectedIds ?? []);
    const groupIds = this.data.groupIds ?? (this.data.kind === 'group' ? this.data.selectedIds ?? [] : []);
    if (!activityIds.length && !groupIds.length) {
      return [];
    }
    return this.data.labels
      .filter((label) => activityIds.every((id) => (label.activityIds ?? []).includes(id))
        && groupIds.every((id) => (label.activityGroupIds ?? []).includes(id)))
      .map((label) => label.id);
  }
}
