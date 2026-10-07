import { Component, inject } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { MaterialModule } from '../../../../app/material-module';
import { blockEnterSubmit } from '../../../../core/block-enter-submit';
import { ActivityGroup } from '../../../../core/services/project-api.service';

@Component({
  selector: 'app-activity-group-dialog',
  standalone: true,
  imports: [ReactiveFormsModule, MaterialModule],
  templateUrl: './activity-group-dialog.html',
  styleUrl: '../../../add-project-dialog/add-project-dialog.css',
})
export class ActivityGroupDialog {
  private fb = inject(FormBuilder);
  private dialogRef = inject(MatDialogRef<ActivityGroupDialog>);
  data = inject<{ group?: ActivityGroup } | null>(MAT_DIALOG_DATA, { optional: true });

  form = this.fb.group({
    name: [this.data?.group?.name ?? '', Validators.required],
    description: [this.data?.group?.description ?? ''],
  });

  get nameInvalid() {
    const name = this.form.controls.name;
    return name.touched && name.invalid;
  }

  onFieldEnter(event: Event) {
    blockEnterSubmit(event);
  }

  cancel() {
    this.dialogRef.close(false);
  }

  save() {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    this.dialogRef.close({
      name: this.form.controls.name.value?.trim() ?? '',
      description: this.form.controls.description.value?.trim() ?? '',
    });
  }
}
