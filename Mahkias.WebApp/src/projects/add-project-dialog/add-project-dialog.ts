import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { MaterialModule } from '../../app/material-module';
import { blockEnterSubmit } from '../../core/block-enter-submit';
import { whileBusy } from '../../core/while-busy';
import { Project, ProjectApiService } from '../../core/services/project-api.service';

@Component({
  selector: 'app-add-project-dialog',
  standalone: true,
  imports: [ReactiveFormsModule, MaterialModule],
  templateUrl: './add-project-dialog.html',
  styleUrl: './add-project-dialog.css',
})
export class AddProjectDialog {
  private fb = inject(FormBuilder);
  private api = inject(ProjectApiService);
  private dialogRef = inject(MatDialogRef<AddProjectDialog>);
  private data = inject<{ project?: Project } | null>(MAT_DIALOG_DATA, { optional: true });

  onFieldEnter(event: Event) {
    blockEnterSubmit(event);
  }
  isSaving = false;
  referenceError = '';
  error = '';

  form = this.fb.group({
    name: [this.data?.project?.name ?? '', Validators.required],
    reference: [this.data?.project?.reference ?? ''],
    description: [this.data?.project?.description ?? ''],
  });

  get isEdit() {
    return !!this.data?.project;
  }

  get nameInvalid() {
    const name = this.form.controls.name;
    return name.touched && name.invalid;
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

    this.referenceError = '';
    this.error = '';
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const value = this.form.getRawValue();
    const body = {
      name: value.name?.trim() ?? '',
      reference: value.reference?.trim() ?? '',
      description: value.description?.trim() ?? '',
    };
    this.dialogRef.disableClose = true;
    const request = this.isEdit
      ? this.api.update(this.data!.project!.id, body)
      : this.api.create(body);

    request.pipe(whileBusy((busy) => {
      this.isSaving = busy;
    })).subscribe({
      next: (project) => this.dialogRef.close(this.isEdit ? true : project),
      error: (err: HttpErrorResponse) => {
        if (this.isDuplicateReference(err)) {
          this.referenceError = 'This Reference code is already in use.';
          return;
        }

        this.error = this.isEdit ? 'The project could not be updated.' : 'The project could not be created.';
      },
    });
  }

  private isDuplicateReference(err: HttpErrorResponse) {
    const message = typeof err.error === 'string' ? err.error : err.error?.message;
    return err.status === 400 && String(message ?? '').toLowerCase().includes('reference');
  }
}
