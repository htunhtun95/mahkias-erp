import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { MaterialModule } from '../../app/material-module';
import { blockEnterSubmit } from '../../core/block-enter-submit';
import { whileBusy } from '../../core/while-busy';
import { ProjectApiService, Supplier } from '../../core/services/project-api.service';

@Component({
  selector: 'app-supplier-dialog',
  standalone: true,
  imports: [ReactiveFormsModule, MaterialModule],
  templateUrl: './supplier-dialog.html',
  styleUrl: '../../projects/add-project-dialog/add-project-dialog.css',
})
export class SupplierDialog {
  private fb = inject(FormBuilder);
  private api = inject(ProjectApiService);
  private dialogRef = inject(MatDialogRef<SupplierDialog>);
  private data = inject<{ supplier?: Supplier } | null>(MAT_DIALOG_DATA, { optional: true });

  isSaving = false;
  error = '';

  form = this.fb.group({
    name: [this.data?.supplier?.name ?? '', Validators.required],
    reference: [this.data?.supplier?.reference ?? ''],
  });

  get isEdit() {
    return !!this.data?.supplier;
  }

  get nameInvalid() {
    const name = this.form.controls.name;
    return name.touched && name.invalid;
  }

  onFieldEnter(event: Event) {
    blockEnterSubmit(event);
  }

  cancel() {
    if (!this.isSaving) {
      this.dialogRef.close(false);
    }
  }

  save() {
    if (this.isSaving) {
      return;
    }
    this.error = '';
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const name = this.form.controls.name.value?.trim() ?? '';
    const reference = this.form.controls.reference.value?.trim() ?? '';
    this.dialogRef.disableClose = true;
    const request = this.isEdit
      ? this.api.updateSupplier(this.data!.supplier!.id, name, reference)
      : this.api.createSupplier(name, reference);

    request.pipe(whileBusy((busy) => (this.isSaving = busy))).subscribe({
      next: (saved) => {
        this.dialogRef.close(this.isEdit ? true : saved);
      },
      error: (error: HttpErrorResponse) => {
        this.error = error.error?.message || (this.isEdit ? 'The supplier could not be updated.' : 'The supplier could not be created.');
      },
    });
  }
}
