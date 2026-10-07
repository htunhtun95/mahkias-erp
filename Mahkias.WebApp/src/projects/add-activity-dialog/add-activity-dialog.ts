import { AfterViewInit, Component, ElementRef, OnDestroy, ViewChild, inject } from '@angular/core';
import { FormArray, FormBuilder, FormGroup, FormsModule, ReactiveFormsModule } from '@angular/forms';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { HttpErrorResponse } from '@angular/common/http';
import { Subscription } from 'rxjs';
import { MaterialModule } from '../../app/material-module';
import { AutoGrowDirective } from '../../core/auto-grow';
import { blockEnterSubmit } from '../../core/block-enter-submit';
import { whileBusy } from '../../core/while-busy';
import { ActivityType, ProjectApiService } from '../../core/services/project-api.service';

interface ActivityRowValue {
  dsnNo: string | null;
  partNo: string | null;
  typeId: number | null;
  quantity: number | string | null;
  budget: number | string | null;
  description: string | null;
}

@Component({
  selector: 'app-add-activity-dialog',
  standalone: true,
  imports: [FormsModule, ReactiveFormsModule, MaterialModule, AutoGrowDirective],
  templateUrl: './add-activity-dialog.html',
  styleUrl: './add-activity-dialog.css',
})
export class AddActivityDialog implements AfterViewInit, OnDestroy {
  private fb = inject(FormBuilder);
  private api = inject(ProjectApiService);
  private dialogRef = inject(MatDialogRef<AddActivityDialog>);
  private host = inject(ElementRef<HTMLElement>);
  private data = inject<{ projectId: number; types: ActivityType[] }>(MAT_DIALOG_DATA);
  private lastRowSub: Subscription | null = null;
  private suppressLastRowWatch = false;
  private readonly onHostWheel = (event: WheelEvent) => this.handleHostWheel(event);
  private readonly onGridWheelBound = (event: WheelEvent) => this.handleGridWheel(event);

  @ViewChild('gridScroll') private gridScroll?: ElementRef<HTMLElement>;

  types = this.data.types ?? [];
  submitted = false;
  isSaving = false;
  error = '';

  form = this.fb.group({
    rows: this.fb.array(Array.from({ length: 10 }, () => this.createRow())),
  });

  constructor() {
    this.watchLastRow();
    this.form.valueChanges.subscribe(() => {
      if (!this.submitted || this.isSaving || !this.error) {
        return;
      }

      const filled = this.rows.controls
        .map((row) => row.getRawValue() as ActivityRowValue)
        .filter((row) => !this.isBlank(row));
      if (filled.length && filled.every((row) => this.isComplete(row))) {
        this.error = '';
      }
    });
  }

  ngAfterViewInit() {
    this.host.nativeElement.addEventListener('wheel', this.onHostWheel, { passive: false });
    this.gridScroll?.nativeElement.addEventListener('wheel', this.onGridWheelBound, { passive: false });
  }

  ngOnDestroy() {
    this.lastRowSub?.unsubscribe();
    this.host.nativeElement.removeEventListener('wheel', this.onHostWheel);
    this.gridScroll?.nativeElement.removeEventListener('wheel', this.onGridWheelBound);
  }

  onFieldEnter(event: Event) {
    blockEnterSubmit(event);
  }

  get rows() {
    return this.form.controls.rows as FormArray<FormGroup>;
  }

  addRow() {
    this.rows.push(this.createRow());
    this.watchLastRow();
  }

  removeRow(index: number) {
    if (this.rows.length === 1) {
      this.suppressLastRowWatch = true;
      this.rows.at(0).reset({
        dsnNo: '',
        partNo: '',
        typeId: null,
        quantity: 1,
        budget: null,
        description: '',
      });
      this.suppressLastRowWatch = false;
      return;
    }

    this.rows.removeAt(index);
    this.watchLastRow();
  }

  fieldInvalid(index: number, field: 'dsnNo' | 'partNo' | 'typeId') {
    const control = this.rows.at(index).get(field);
    const row = this.rowValue(index);
    if (!control || this.isBlank(row) || !(this.submitted || control.touched)) {
      return false;
    }

    if (field === 'typeId') {
      return !row.typeId;
    }

    return !String(row[field] ?? '').trim();
  }

  fieldHint(index: number, field: 'dsnNo' | 'partNo' | 'typeId') {
    if (!this.fieldInvalid(index, field)) {
      return null;
    }

    if (field === 'dsnNo') {
      return 'DSN No is required';
    }

    if (field === 'partNo') {
      return 'Part No is required';
    }

    return 'Type is required';
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

    const filled = this.rows.controls
      .map((row) => row.getRawValue() as ActivityRowValue)
      .filter((row) => !this.isBlank(row));

    if (!filled.length) {
      this.error = 'Enter at least one activity.';
      return;
    }

    const invalid = filled.some((row) => !this.isComplete(row));
    if (invalid) {
      this.error = 'DSN no, part number, and type are required on the highlighted rows.';
      return;
    }

    const activities = filled.map((row) => ({
      partNo: String(row.partNo ?? ''),
      budget: row.budget === null || row.budget === '' ? null : Number(row.budget),
      description: String(row.description ?? '').trim(),
      dsnNo: String(row.dsnNo ?? '').trim(),
      quantity: row.quantity === null || row.quantity === '' ? 1 : Math.round(Number(row.quantity)),
      typeId: Number(row.typeId),
    }));

    this.dialogRef.disableClose = true;
    this.api.bulkActivities(this.data.projectId, activities).pipe(whileBusy((busy) => {
      this.isSaving = busy;
    })).subscribe({
      next: () => this.dialogRef.close(true),
      error: (err: HttpErrorResponse) => {
        this.error = this.readError(err);
      },
    });
  }

  private watchLastRow() {
    this.lastRowSub?.unsubscribe();
    const control = this.rows.at(this.rows.length - 1);
    this.lastRowSub = control.valueChanges.subscribe(() => {
      if (this.suppressLastRowWatch || this.rows.at(this.rows.length - 1) !== control) {
        return;
      }

      if (this.isBlank(control.getRawValue() as ActivityRowValue)) {
        return;
      }

      this.appendEmptyRows(5);
    });
  }

  private appendEmptyRows(count: number) {
    for (let i = 0; i < count; i++) {
      this.rows.push(this.createRow());
    }

    this.watchLastRow();
  }

  private rowValue(index: number) {
    return this.rows.at(index).getRawValue() as ActivityRowValue;
  }

  private isBlank(row: ActivityRowValue) {
    const quantity = row.quantity;
    const quantityTouched = quantity !== null && quantity !== '' && Number(quantity) !== 1;

    return (
      !String(row.dsnNo ?? '').trim() &&
      !String(row.partNo ?? '').trim() &&
      !row.typeId &&
      (row.budget === null || row.budget === '') &&
      !String(row.description ?? '').trim() &&
      !quantityTouched
    );
  }

  private isComplete(row: ActivityRowValue) {
    return !!String(row.dsnNo ?? '').trim() && !!String(row.partNo ?? '').trim() && !!row.typeId;
  }

  private readError(err: HttpErrorResponse) {
    if (typeof err.error === 'string' && err.error.trim()) {
      return err.error;
    }

    if (err.error && typeof err.error === 'object' && typeof err.error.message === 'string') {
      return err.error.message;
    }

    return 'The activities could not be saved.';
  }

  private createRow() {
    return this.fb.group({
      dsnNo: [''],
      partNo: [''],
      typeId: [null as number | null],
      quantity: [1],
      budget: [null as number | null],
      description: [''],
    });
  }

  private handleHostWheel(event: WheelEvent) {
    const target = event.target as HTMLElement | null;
    if (!target?.closest('.grid-scroll')) {
      event.preventDefault();
    }
    event.stopPropagation();
  }

  private handleGridWheel(event: WheelEvent) {
    const el = event.currentTarget as HTMLElement;
    const delta = event.deltaY;
    const maxScroll = el.scrollHeight - el.clientHeight;
    const canScroll = maxScroll > 0;
    const atTop = el.scrollTop <= 0 && delta < 0;
    const atBottom = el.scrollTop >= maxScroll - 1 && delta > 0;

    if (!canScroll || atTop || atBottom) {
      event.preventDefault();
    } else {
      el.scrollTop += delta;
      event.preventDefault();
    }

    event.stopPropagation();
  }
}
