import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject } from '@angular/core';
import { AbstractControl, FormBuilder, FormsModule, ReactiveFormsModule, ValidationErrors, Validators } from '@angular/forms';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { MaterialModule } from '../../app/material-module';
import { AutoGrowDirective } from '../../core/auto-grow';
import { blockEnterSubmit } from '../../core/block-enter-submit';
import { whileBusy } from '../../core/while-busy';
import { Activity, ActivityType, ProjectApiService } from '../../core/services/project-api.service';

@Component({
  selector: 'app-edit-activity-dialog',
  standalone: true,
  imports: [FormsModule, ReactiveFormsModule, MaterialModule, AutoGrowDirective],
  templateUrl: './edit-activity-dialog.html',
  styleUrl: './edit-activity-dialog.css',
})
export class EditActivityDialog {
  private fb = inject(FormBuilder);
  private api = inject(ProjectApiService);
  private dialogRef = inject(MatDialogRef<EditActivityDialog, Activity | false>);
  data = inject<{ activity: Activity; types: ActivityType[] }>(MAT_DIALOG_DATA);

  onFieldEnter(event: Event) {
    blockEnterSubmit(event);
  }
  isSaving = false;
  submitted = false;
  error = '';

  form = this.fb.group({
    dsnNo: [this.data.activity.dsnNo ?? '', EditActivityDialog.requiredText],
    partNo: [this.data.activity.partNo ?? '', EditActivityDialog.requiredText],
    typeId: [{ value: this.data.activity.typeId, disabled: !!this.data.activity.activityGroupId }, Validators.required],
    quantity: [this.data.activity.quantity ?? 1, EditActivityDialog.wholeQuantity],
    budget: [this.data.activity.budget],
    description: [this.data.activity.description ?? ''],
  });

  get typeLocked(): boolean {
    return !!this.data.activity.activityGroupId;
  }

  get typeInvalid() {
    return this.showError(this.form.controls.typeId);
  }

  get dsnInvalid() {
    return this.showError(this.form.controls.dsnNo);
  }

  get partInvalid() {
    return this.showError(this.form.controls.partNo);
  }

  get quantityInvalid() {
    return this.showError(this.form.controls.quantity);
  }

  cancel() {
    if (this.isSaving) {
      return;
    }
    this.dialogRef.close(false);
  }

  save() {
    if (this.isSaving) {
      return;
    }

    this.submitted = true;
    this.error = '';
    const value = this.form.getRawValue();
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.dialogRef.disableClose = true;
    this.api
      .updateActivity(this.data.activity.id, {
        projectId: this.data.activity.projectId,
        dsnNo: String(value.dsnNo ?? '').trim(),
        partNo: String(value.partNo ?? ''),
        typeId: value.typeId ? Number(value.typeId) : null,
        quantity: value.quantity == null ? null : Number(value.quantity),
        budget: value.budget == null ? null : Number(value.budget),
        description: String(value.description ?? '').trim(),
      })
      .pipe(whileBusy((busy) => {
        this.isSaving = busy;
      }))
      .subscribe({
        next: (activity) => this.dialogRef.close(activity),
        error: (err: HttpErrorResponse) => {
          const message = typeof err.error === 'string' ? err.error : err.error?.message;
          this.error = message || 'The activity could not be updated.';
        },
      });
  }

  private showError(control: AbstractControl) {
    return control.invalid && (control.touched || this.submitted);
  }

  private static requiredText(control: AbstractControl): ValidationErrors | null {
    return String(control.value ?? '').trim() ? null : { required: true };
  }

  private static wholeQuantity(control: AbstractControl): ValidationErrors | null {
    const value = control.value;
    if (value == null || value === '') {
      return { required: true };
    }
    const quantity = Number(value);
    if (!Number.isInteger(quantity) || quantity < 0) {
      return { quantity: true };
    }
    return null;
  }
}
