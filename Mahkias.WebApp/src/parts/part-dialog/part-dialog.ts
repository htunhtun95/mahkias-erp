import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject } from '@angular/core';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { MaterialModule } from '../../app/material-module';
import { blockEnterSubmit } from '../../core/block-enter-submit';
import { requiredText } from '../../core/required-text';
import { whileBusy } from '../../core/while-busy';
import { PartRecord, ProjectApiService } from '../../core/services/project-api.service';

@Component({
  selector: 'app-part-dialog',
  standalone: true,
  imports: [ReactiveFormsModule, MaterialModule],
  templateUrl: './part-dialog.html',
  styleUrls: ['../../projects/add-project-dialog/add-project-dialog.css', './part-dialog.css'],
})
export class PartDialog {
  private fb = inject(FormBuilder);
  private api = inject(ProjectApiService);
  private dialogRef = inject(MatDialogRef<PartDialog, PartRecord | false>);
  private data = inject<{ part?: PartRecord } | null>(MAT_DIALOG_DATA, { optional: true });

  isSaving = false;
  error = '';

  form = this.fb.group({
    partNo: [this.data?.part?.partNo ?? '', requiredText],
    descriptions: this.fb.array(
      (this.data?.part?.descriptions?.length ? this.data.part.descriptions : [{ id: null, description: '' }])
        .map((item) => this.descriptionGroup(item.id, item.description)),
    ),
  });

  get isEdit() {
    return !!this.data?.part;
  }

  get descriptions() {
    return this.form.controls.descriptions;
  }

  get partNoInvalid() {
    const partNo = this.form.controls.partNo;
    return partNo.invalid && (partNo.touched || partNo.dirty);
  }

  onFieldEnter(event: Event) {
    blockEnterSubmit(event);
  }

  addDescription() {
    this.descriptions.push(this.descriptionGroup(null, ''));
  }

  removeDescription(index: number) {
    this.descriptions.removeAt(index);
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
    if (this.form.controls.partNo.invalid) {
      this.form.controls.partNo.markAsTouched();
      return;
    }

    const descriptions = this.descriptions.controls
      .map((row) => ({
        id: row.controls.id.value,
        description: String(row.controls.description.value ?? '').trim(),
      }))
      .filter((row) => row.description);
    const seen = new Set<string>();
    for (const row of descriptions) {
      const key = row.description.toLowerCase();
      if (seen.has(key)) {
        this.error = 'Each description can only be added once.';
        return;
      }
      seen.add(key);
    }

    const body = {
      partNo: this.form.controls.partNo.value?.trim() ?? '',
      descriptions,
    };
    const request = this.data?.part
      ? this.api.updatePart(this.data.part.id, body)
      : this.api.createPart(body);
    request.pipe(whileBusy((busy) => {
      this.isSaving = busy;
    })).subscribe({
      next: (part) => this.dialogRef.close(part),
      error: (err: HttpErrorResponse) => {
        const message = typeof err.error === 'string' ? err.error : err.error?.message;
        this.error = message || 'The part could not be saved.';
      },
    });
  }

  private descriptionGroup(id: number | null, description: string) {
    return this.fb.group({
      id: [id],
      description: [description ?? ''],
    });
  }
}
