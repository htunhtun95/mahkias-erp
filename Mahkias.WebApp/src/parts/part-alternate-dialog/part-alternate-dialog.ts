import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject } from '@angular/core';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { MaterialModule } from '../../app/material-module';
import { blockEnterSubmit } from '../../core/block-enter-submit';
import { requiredText } from '../../core/required-text';
import { whileBusy } from '../../core/while-busy';
import { PartAlternate, PartSuggestion, ProjectApiService } from '../../core/services/project-api.service';
import { PartSuggestComponent } from '../../projects/part-suggest/part-suggest';
import { retainsSelectedPart } from '../../projects/part-numbers';

export interface PartAlternateDialogData {
  partId: number;
  alternate?: PartAlternate;
}

@Component({
  selector: 'app-part-alternate-dialog',
  standalone: true,
  imports: [ReactiveFormsModule, MaterialModule, PartSuggestComponent],
  templateUrl: './part-alternate-dialog.html',
  styleUrls: ['../../projects/add-project-dialog/add-project-dialog.css', './part-alternate-dialog.css'],
})
export class PartAlternateDialog {
  private fb = inject(FormBuilder);
  private api = inject(ProjectApiService);
  private dialogRef = inject(MatDialogRef<PartAlternateDialog, boolean>);
  private data = inject<PartAlternateDialogData>(MAT_DIALOG_DATA);

  isSaving = false;
  error = '';
  private selectedPartId = this.data.alternate?.partId ?? null;
  private selectedPartNo = this.data.alternate?.partNo ?? null;

  form = this.fb.group({
    partNo: [this.data.alternate?.partNo ?? '', requiredText],
    description: [this.data.alternate?.descriptions[0]?.description ?? ''],
  });

  get isEdit() {
    return !!this.data.alternate;
  }

  get partNoInvalid() {
    const control = this.form.controls.partNo;
    return control.invalid && (control.dirty || control.touched);
  }

  onFieldEnter(event: Event) {
    blockEnterSubmit(event);
  }

  setPartNo(value: string) {
    this.form.controls.partNo.setValue(value);
    this.form.controls.partNo.markAsDirty();
    if (!retainsSelectedPart(value, this.selectedPartNo)) {
      this.selectedPartId = null;
      this.selectedPartNo = null;
    }
  }

  pickPart(suggestion: PartSuggestion) {
    this.selectedPartId = suggestion.partId;
    this.selectedPartNo = suggestion.partNo;
    this.form.controls.partNo.setValue(suggestion.partNo);
    this.form.controls.partNo.markAsDirty();
    const description = suggestion.description?.trim();
    if (description && !this.form.controls.description.value?.trim()) {
      this.form.controls.description.setValue(description);
    }
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
    this.form.controls.partNo.markAsTouched();
    if (this.form.controls.partNo.invalid) {
      return;
    }

    const partNo = this.form.controls.partNo.value?.trim() ?? '';
    const description = this.form.controls.description.value?.trim() ?? '';
    const body = {
      partNo,
      description,
      partId: retainsSelectedPart(partNo, this.selectedPartNo) ? this.selectedPartId : null,
    };
    const request = this.data.alternate
      ? this.api.updatePartAlternate(this.data.partId, this.data.alternate.id, body)
      : this.api.addPartAlternate(this.data.partId, body);
    this.error = '';
    request.pipe(whileBusy((busy) => (this.isSaving = busy))).subscribe({
      next: () => this.dialogRef.close(true),
      error: (err: HttpErrorResponse) => {
        const message = typeof err.error === 'string' ? err.error : err.error?.message;
        this.error = message || 'The alternate part could not be saved.';
      },
    });
  }
}
