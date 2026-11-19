import { AfterViewInit, Component, ElementRef, inject, ViewChild } from '@angular/core';
import { AbstractControl, FormBuilder, ReactiveFormsModule, ValidationErrors } from '@angular/forms';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { MaterialModule } from '../../../../app/material-module';
import { blockEnterSubmit } from '../../../../core/block-enter-submit';
import { requiredText } from '../../../../core/required-text';
import { ActivityLabel } from '../../../../core/services/project-api.service';

@Component({
  selector: 'app-rename-label-dialog',
  standalone: true,
  imports: [ReactiveFormsModule, MaterialModule],
  templateUrl: './rename-label-dialog.html',
  styleUrl: './rename-label-dialog.css',
})
export class RenameLabelDialog implements AfterViewInit {
  private fb = inject(FormBuilder);
  private dialogRef = inject(MatDialogRef<RenameLabelDialog>);
  data = inject<{ label: ActivityLabel; labels?: ActivityLabel[] }>(MAT_DIALOG_DATA);
  @ViewChild('nameInput') nameInput?: ElementRef<HTMLInputElement>;

  ngAfterViewInit() {
    setTimeout(() => {
      const input = this.nameInput?.nativeElement;
      if (!input) {
        return;
      }
      input.focus();
      input.select();
    });
  }

  form = this.fb.group({
    name: [this.data.label?.name ?? '', [requiredText, (control: AbstractControl) => this.duplicateName(control)]],
  });

  get nameInvalid() {
    const name = this.form.controls.name;
    return name.invalid && (name.touched || name.dirty);
  }

  get duplicate() {
    return this.form.controls.name.hasError('duplicate');
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
    this.dialogRef.close({ name: this.form.controls.name.value?.trim() ?? '' });
  }

  private duplicateName(control: AbstractControl): ValidationErrors | null {
    const name = String(control.value ?? '').trim().toLowerCase();
    if (!name) {
      return null;
    }
    const current = this.data?.label;
    const taken = (this.data?.labels ?? []).some((label) =>
      label.id !== current?.id
      && label.typeId === current?.typeId
      && label.name.trim().toLowerCase() === name);
    return taken ? { duplicate: true } : null;
  }
}
