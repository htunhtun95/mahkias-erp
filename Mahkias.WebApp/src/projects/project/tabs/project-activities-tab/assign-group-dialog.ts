import { Component, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { MatRadioModule } from '@angular/material/radio';
import { MaterialModule } from '../../../../app/material-module';
import { ActivityGroup } from '../../../../core/services/project-api.service';

@Component({
  selector: 'app-assign-group-dialog',
  standalone: true,
  imports: [FormsModule, MaterialModule, MatRadioModule],
  templateUrl: './assign-group-dialog.html',
  styleUrl: './assign-group-dialog.css',
})
export class AssignGroupDialog {
  private dialogRef = inject(MatDialogRef<AssignGroupDialog>);
  data = inject<{ groups: ActivityGroup[] }>(MAT_DIALOG_DATA);
  mode: 'existing' | 'new' = this.data.groups.length ? 'existing' : 'new';
  groupId: number | null = this.data.groups[0]?.id ?? null;
  name = '';
  submitted = false;

  cancel() {
    this.dialogRef.close(false);
  }

  save() {
    this.submitted = true;
    if (this.mode === 'new') {
      if (!this.name.trim()) {
        return;
      }
      this.dialogRef.close({ name: this.name.trim() });
      return;
    }
    if (!this.groupId) {
      return;
    }
    this.dialogRef.close(this.groupId);
  }
}
