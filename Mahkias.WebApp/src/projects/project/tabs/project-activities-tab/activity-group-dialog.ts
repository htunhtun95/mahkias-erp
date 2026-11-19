import { Component, inject } from '@angular/core';
import { AbstractControl, FormBuilder, ReactiveFormsModule, ValidationErrors, Validators } from '@angular/forms';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { MaterialModule } from '../../../../app/material-module';
import { blockEnterSubmit } from '../../../../core/block-enter-submit';
import { requiredText } from '../../../../core/required-text';
import { ActivityGroup, ActivityType } from '../../../../core/services/project-api.service';

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
  data = inject<{ group?: ActivityGroup; types?: ActivityType[] } | null>(MAT_DIALOG_DATA, { optional: true });

  form = this.fb.group({
    name: [this.data?.group?.name ?? '', requiredText],
    description: [this.data?.group?.description ?? ''],
    typeId: [this.data?.group?.typeId ?? null as number | null, this.data?.group ? Validators.required : []],
    budget: [this.data?.group?.budget ?? null as number | null, ActivityGroupDialog.optionalBudget],
  });

  get types(): ActivityType[] {
    return this.data?.types ?? [];
  }

  get editing(): boolean {
    return !!this.data?.group;
  }

  get nameInvalid() {
    const name = this.form.controls.name;
    return name.invalid && (name.touched || name.dirty);
  }

  get typeInvalid() {
    const typeId = this.form.controls.typeId;
    return this.editing && typeId.invalid && (typeId.touched || typeId.dirty);
  }

  get budgetInvalid() {
    const budget = this.form.controls.budget;
    return this.editing && budget.invalid && (budget.touched || budget.dirty);
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
      typeId: this.editing ? this.form.controls.typeId.value : null,
      budget: this.editing ? this.budgetAmount() : null,
    });
  }

  private budgetAmount(): number | null {
    const value = this.form.controls.budget.value;
    if (value == null || (value as unknown) === '') {
      return null;
    }
    return Number(value);
  }

  private static optionalBudget(control: AbstractControl): ValidationErrors | null {
    const value = control.value;
    if (value == null || value === '') {
      return null;
    }
    const amount = Number(value);
    if (!Number.isFinite(amount) || amount < 0) {
      return { budget: true };
    }
    return null;
  }
}
