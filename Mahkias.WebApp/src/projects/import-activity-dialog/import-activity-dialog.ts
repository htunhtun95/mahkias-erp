import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject } from '@angular/core';
import { FormBuilder, FormsModule, ReactiveFormsModule } from '@angular/forms';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { finalize } from 'rxjs';
import { MaterialModule } from '../../app/material-module';
import { DriveUploadCardComponent } from '../drive-upload-card/drive-upload-card';
import { blockEnterSubmit } from '../../core/block-enter-submit';
import { closeOnEscape } from '../../core/close-on-escape';
import { whileBusy } from '../../core/while-busy';
import { ActivityImportIssue, ActivityType, ExcelColumn, LookupValue, ProjectApiService } from '../../core/services/project-api.service';
import { LookupComboboxComponent } from '../lookup-combobox/lookup-combobox';
import { canonicalCondition, canonicalUnit } from '../quotation-lookups';

const requiredFieldWarning = 'Please select a Column Index or enter a Default Value for this required field.';

@Component({
  selector: 'app-import-activity-dialog',
  standalone: true,
  imports: [FormsModule, ReactiveFormsModule, MaterialModule, DriveUploadCardComponent, LookupComboboxComponent],
  templateUrl: './import-activity-dialog.html',
  styleUrl: './import-activity-dialog.css',
})
export class ImportActivityDialog {
  private fb = inject(FormBuilder);
  private api = inject(ProjectApiService);
  private dialogRef = inject(MatDialogRef<ImportActivityDialog>);
  private data = inject<{ projectId: number; types: ActivityType[] }>(MAT_DIALOG_DATA);

  onFieldEnter(event: Event) {
    blockEnterSubmit(event);
  }
  readonly requiredFieldWarning = requiredFieldWarning;
  types = this.data.types ?? [];
  units: LookupValue[] = [];
  conditions: LookupValue[] = [];
  unitOptions: string[] = [];
  conditionOptions: string[] = [];
  columns: ExcelColumn[] = [];
  rowCount = 0;
  file: File | null = null;
  reading = false;
  validating = false;
  isImporting = false;
  error = '';
  validation: 'idle' | 'checking' | 'passed' | 'failed' = 'idle';
  readyCount = 0;
  errorCount = 0;
  issues: ActivityImportIssue[] = [];
  private validationToken = 0;
  private readToken = 0;

  form = this.fb.group({
    dsnNo: [''],
    defaultDsn: [''],
    partNo: [''],
    defaultPart: [''],
    description: [''],
    type: [''],
    defaultTypeId: [null as number | null],
    quantity: [''],
    defaultQuantity: [1 as number | null],
    budget: [''],
    defaultBudget: [null as number | null],
    label: [''],
    unit: [''],
    defaultUnit: ['EA'],
    condition: [''],
    defaultCondition: ['NE'],
    serialNo: [''],
    tsn: [''],
    tso: [''],
    csn: [''],
    cso: [''],
  });

  constructor() {
    closeOnEscape(this.dialogRef, () => this.cancel());
    this.api.units().subscribe({
      next: (units) => {
        this.units = units ?? [];
        this.unitOptions = this.units.map((unit) => unit.name).filter((name) => !!name);
        const current = this.form.controls.defaultUnit.value ?? '';
        if (!current.trim() || current.trim().toUpperCase() === 'EA') {
          this.form.controls.defaultUnit.setValue(canonicalUnit(current || 'EA', this.units) || 'EA', { emitEvent: false });
        }
      },
    });
    this.api.conditions().subscribe({
      next: (conditions) => {
        this.conditions = conditions ?? [];
        this.conditionOptions = this.conditions.map((condition) => condition.name).filter((name) => !!name);
        const current = this.form.controls.defaultCondition.value ?? '';
        if (!current.trim() || current.trim().toUpperCase() === 'NE') {
          this.form.controls.defaultCondition.setValue(canonicalCondition(current || 'NE', this.conditions) || 'NE', { emitEvent: false });
        }
      },
    });
    this.form.valueChanges.subscribe(() => this.resetValidation());
  }

  get dsnReady() {
    return this.hasText(this.form.controls.dsnNo.value) || this.hasText(this.form.controls.defaultDsn.value);
  }

  get partReady() {
    return this.hasText(this.form.controls.partNo.value) || this.hasText(this.form.controls.defaultPart.value);
  }

  get typeReady() {
    return this.hasText(this.form.controls.type.value) || !!this.form.controls.defaultTypeId.value;
  }

  mapInvalid(field: 'dsn' | 'part' | 'type'): boolean {
    const ready = field === 'dsn' ? this.dsnReady : field === 'part' ? this.partReady : this.typeReady;
    if (ready) {
      return false;
    }
    const column = field === 'dsn' ? this.form.controls.dsnNo : field === 'part' ? this.form.controls.partNo : this.form.controls.type;
    const fallback = field === 'dsn' ? this.form.controls.defaultDsn : field === 'part' ? this.form.controls.defaultPart : this.form.controls.defaultTypeId;
    return column.touched || column.dirty || fallback.touched || fallback.dirty;
  }

  get canValidate() {
    return !!this.file && this.columns.length > 0 && this.dsnReady && this.partReady && this.typeReady && !this.validating && !this.isImporting;
  }

  get canImport() {
    return this.validation === 'passed' && this.readyCount > 0 && !this.isImporting && !this.validating;
  }

  columnLabel(column: ExcelColumn) {
    return column.header ? `${column.letter} — ${column.header}` : column.letter;
  }

  fieldFlagged(field: string) {
    return this.issues.some((issue) => issue.field === field);
  }

  issueText(issue: ActivityImportIssue) {
    const detail = `[${issue.field}] ${issue.message}`;
    return issue.row > 0 ? `Row ${issue.row}: ${detail}` : detail;
  }

  cancel() {
    if (this.isImporting) {
      return;
    }
    this.dialogRef.close(false);
  }

  onFile(file: File | null) {
    if (file === this.file) {
      return;
    }
    this.file = file;
    this.columns = [];
    this.rowCount = 0;
    this.error = '';
    this.resetValidation();
    if (!file) {
      return;
    }

    const token = ++this.readToken;
    this.reading = true;
    this.api.excelMapping(this.data.projectId, file).pipe(finalize(() => {
      if (token === this.readToken) {
        this.reading = false;
      }
    })).subscribe({
      next: (mapping) => {
        if (token !== this.readToken) {
          return;
        }
        this.columns = mapping.columns ?? [];
        this.rowCount = mapping.rowCount ?? 0;
        this.form.patchValue({
          dsnNo: mapping.suggested?.dsnNo ?? '',
          partNo: mapping.suggested?.partNo ?? '',
          description: mapping.suggested?.description ?? '',
          type: mapping.suggested?.type ?? '',
          quantity: mapping.suggested?.quantity ?? '',
          budget: mapping.suggested?.budget ?? '',
          label: mapping.suggested?.label ?? '',
          unit: mapping.suggested?.unit ?? '',
          condition: mapping.suggested?.condition ?? '',
          serialNo: mapping.suggested?.serialNo ?? '',
          tsn: mapping.suggested?.tsn ?? '',
          tso: mapping.suggested?.tso ?? '',
          csn: mapping.suggested?.csn ?? '',
          cso: mapping.suggested?.cso ?? '',
          defaultQuantity: 1,
          defaultUnit: canonicalUnit(this.form.controls.defaultUnit.value || 'EA', this.units) || 'EA',
          defaultCondition: canonicalCondition(this.form.controls.defaultCondition.value || 'NE', this.conditions) || 'NE',
        });
        if (!this.columns.length) {
          this.error = 'The file has no columns to map.';
        }
      },
      error: () => {
        if (token !== this.readToken) {
          return;
        }
        this.error = 'The file could not be read.';
      },
    });
  }

  validate() {
    this.error = '';
    if (this.validating || this.isImporting || !this.canValidate || !this.file) {
      return;
    }

    const token = ++this.validationToken;
    this.validating = true;
    this.validation = 'checking';
    this.issues = [];
    this.readyCount = 0;
    this.errorCount = 0;
    this.api.excelValidate(this.data.projectId, this.file, this.mapping()).pipe(finalize(() => {
      if (token === this.validationToken) {
        this.validating = false;
      }
    })).subscribe({
      next: (result) => {
        if (token !== this.validationToken) {
          return;
        }

        this.issues = result.errors ?? [];
        this.errorCount = result.errorCount ?? this.issues.length;
        this.readyCount = result.ready ?? 0;
        this.validation = result.valid && this.issues.length === 0 ? 'passed' : 'failed';
        if (this.validation === 'failed' && !this.errorCount) {
          this.errorCount = this.issues.length;
        }
      },
      error: (err: HttpErrorResponse) => {
        if (token !== this.validationToken) {
          return;
        }

        this.validation = 'failed';
        this.readyCount = 0;
        const body = err.error;
        if (body && Array.isArray(body.errors)) {
          this.issues = body.errors;
          this.errorCount = body.errorCount ?? this.issues.length;
          return;
        }

        this.errorCount = 1;
        this.error = this.readError(err);
      },
    });
  }

  save() {
    this.error = '';
    if (this.isImporting || !this.canImport || !this.file) {
      return;
    }

    this.dialogRef.disableClose = true;
    this.api.excelUpload(this.data.projectId, this.file, this.mapping()).pipe(whileBusy((busy) => {
      this.isImporting = busy;
    })).subscribe({
      next: () => this.dialogRef.close(true),
      error: (err: HttpErrorResponse) => {
        this.validation = 'failed';
        const body = err.error;
        if (body && Array.isArray(body.errors)) {
          this.issues = body.errors;
          this.errorCount = body.errorCount ?? this.issues.length;
          this.readyCount = 0;
          return;
        }

        this.error = this.readError(err);
      },
    });
  }

  private mapping() {
    const value = this.form.getRawValue();
    const defaultQuantity = value.defaultQuantity == null || value.defaultQuantity === ('' as unknown as number)
      ? null
      : Number(value.defaultQuantity);
    const defaultBudget = value.defaultBudget == null || value.defaultBudget === ('' as unknown as number)
      ? null
      : Number(value.defaultBudget);

    return {
      dsnNoColumn: value.dsnNo ?? '',
      partNoColumn: value.partNo ?? '',
      descriptionColumn: value.description ?? '',
      typeColumn: value.type ?? '',
      quantityColumn: value.quantity ?? '',
      budgetColumn: value.budget ?? '',
      labelColumn: value.label ?? '',
      unitColumn: value.unit ?? '',
      conditionColumn: value.condition ?? '',
      serialNoColumn: value.serialNo ?? '',
      tsnColumn: value.tsn ?? '',
      tsoColumn: value.tso ?? '',
      csnColumn: value.csn ?? '',
      csoColumn: value.cso ?? '',
      defaultDsnNo: String(value.defaultDsn ?? '').trim(),
      defaultPartNo: String(value.defaultPart ?? '').trim(),
      defaultTypeId: value.defaultTypeId,
      defaultQuantity: Number.isFinite(defaultQuantity) ? defaultQuantity : null,
      defaultBudget: Number.isFinite(defaultBudget) ? defaultBudget : null,
      defaultUnit: canonicalUnit(String(value.defaultUnit ?? ''), this.units) || 'EA',
      defaultCondition: canonicalCondition(String(value.defaultCondition ?? ''), this.conditions) || 'NE',
    };
  }

  setDefaultLookup(field: 'defaultUnit' | 'defaultCondition', value: string) {
    this.form.controls[field].setValue(value);
  }

  normalizeDefaultUnit() {
    const next = canonicalUnit(this.form.controls.defaultUnit.value ?? '', this.units);
    this.form.controls.defaultUnit.setValue(next || 'EA');
  }

  normalizeDefaultCondition() {
    const next = canonicalCondition(this.form.controls.defaultCondition.value ?? '', this.conditions);
    this.form.controls.defaultCondition.setValue(next || 'NE');
  }

  private resetValidation() {
    if (this.validation === 'idle' && this.issues.length === 0 && this.errorCount === 0 && !this.validating) {
      return;
    }

    if (this.validating) {
      this.validationToken++;
      this.validating = false;
    }

    this.validation = 'idle';
    this.issues = [];
    this.readyCount = 0;
    this.errorCount = 0;
  }

  private hasText(value: string | null | undefined) {
    return !!String(value ?? '').trim();
  }

  private readError(err: HttpErrorResponse) {
    const body = err.error;
    const message = typeof body === 'string' ? body : body?.message;
    return message || 'The activities could not be checked.';
  }
}
