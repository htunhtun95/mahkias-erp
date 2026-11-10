import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject } from '@angular/core';
import { AbstractControl, FormBuilder, FormsModule, ReactiveFormsModule, ValidationErrors, Validators } from '@angular/forms';
import { ErrorStateMatcher } from '@angular/material/core';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { MaterialModule } from '../../app/material-module';
import { AutoGrowDirective } from '../../core/auto-grow';
import { blockEnterSubmit } from '../../core/block-enter-submit';
import { closeOnEscape } from '../../core/close-on-escape';
import { whileBusy } from '../../core/while-busy';
import { Activity, ActivityType, LookupValue, ProjectApiService } from '../../core/services/project-api.service';
import { LookupComboboxComponent } from '../lookup-combobox/lookup-combobox';
import { canonicalCondition, canonicalUnit } from '../quotation-lookups';

class InvalidWhenInvalidMatcher implements ErrorStateMatcher {
  isErrorState(control: AbstractControl | null): boolean {
    return !!control && control.invalid && (control.touched || control.dirty);
  }
}

@Component({
  selector: 'app-edit-activity-dialog',
  standalone: true,
  imports: [FormsModule, ReactiveFormsModule, MaterialModule, AutoGrowDirective, LookupComboboxComponent],
  templateUrl: './edit-activity-dialog.html',
  styleUrl: './edit-activity-dialog.css',
  providers: [{ provide: ErrorStateMatcher, useClass: InvalidWhenInvalidMatcher }],
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
  units: LookupValue[] = [];
  conditions: LookupValue[] = [];
  unitOptions: string[] = [];
  conditionOptions: string[] = [];

  form = this.fb.group({
    dsnNo: [this.data.activity.dsnNo ?? '', EditActivityDialog.requiredText],
    partNo: [this.data.activity.partNo ?? '', EditActivityDialog.requiredText],
    typeId: [this.data.activity.typeId, Validators.required],
    quantity: [this.data.activity.quantity ?? 1, EditActivityDialog.wholeQuantity],
    budget: [this.data.activity.budget],
    description: [this.data.activity.description ?? ''],
    unit: [this.data.activity.unit ?? ''],
    condition: [this.data.activity.condition ?? ''],
    serialNo: [this.data.activity.serialNo ?? ''],
    tsn: [this.data.activity.tsn ?? ''],
    tso: [this.data.activity.tso ?? ''],
    csn: [this.data.activity.csn ?? ''],
    cso: [this.data.activity.cso ?? ''],
  });

  constructor() {
    closeOnEscape(this.dialogRef, () => this.cancel());
    this.api.units().subscribe({
      next: (units) => {
        this.units = units ?? [];
        this.unitOptions = this.units.map((unit) => unit.name).filter((name) => !!name);
        this.form.controls.unit.setValue(canonicalUnit(this.form.controls.unit.value ?? '', this.units), { emitEvent: false });
      },
    });
    this.api.conditions().subscribe({
      next: (conditions) => {
        this.conditions = conditions ?? [];
        this.conditionOptions = this.conditions.map((condition) => condition.name).filter((name) => !!name);
        this.form.controls.condition.setValue(canonicalCondition(this.form.controls.condition.value ?? '', this.conditions), { emitEvent: false });
      },
    });
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
        unit: canonicalUnit(String(value.unit ?? ''), this.units) || null,
        condition: canonicalCondition(String(value.condition ?? ''), this.conditions) || null,
        serialNo: textOrNull(value.serialNo),
        tsn: textOrNull(value.tsn),
        tso: textOrNull(value.tso),
        csn: textOrNull(value.csn),
        cso: textOrNull(value.cso),
      })
      .pipe(whileBusy((busy) => {
        this.isSaving = busy;
      }))
      .subscribe({
        next: (activity) => this.dialogRef.close(activity),
        error: (err: HttpErrorResponse) => {
          this.dialogRef.disableClose = false;
          const message = typeof err.error === 'string' ? err.error : err.error?.message;
          this.error = message || 'The activity could not be updated.';
        },
      });
  }

  setLookup(field: 'unit' | 'condition', value: string) {
    this.form.controls[field].setValue(value);
    this.form.controls[field].markAsDirty();
  }

  normalizeUnit() {
    this.form.controls.unit.setValue(canonicalUnit(this.form.controls.unit.value ?? '', this.units));
  }

  normalizeCondition() {
    this.form.controls.condition.setValue(canonicalCondition(this.form.controls.condition.value ?? '', this.conditions));
  }

  private showError(control: AbstractControl) {
    return control.invalid && (control.touched || control.dirty);
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

function textOrNull(value: string | null | undefined): string | null {
  const text = String(value ?? '').trim();
  return text || null;
}
