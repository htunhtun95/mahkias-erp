import { Component, ElementRef, ViewChild, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { Observable, of, switchMap, tap } from 'rxjs';
import { MaterialModule } from '../../app/material-module';
import { blockEnterSubmit } from '../../core/block-enter-submit';
import { whileBusy } from '../../core/while-busy';
import { Activity, ActivityImportIssue, ProjectApiService, QuotationItemInput, Supplier } from '../../core/services/project-api.service';
import { directActivityId } from '../quotation-map';

interface ImportLine {
  row: number;
  dsnNo: string;
  partNo: string;
  description: string;
  unitPrice: string;
  quantity: string;
  totalPrice: string;
  unitExplicit: boolean;
  totalExplicit: boolean;
  activityId: number | null;
  priceWarning: boolean;
}

interface QuoteMapping {
  [key: string]: string;
  dsnNoColumn: string;
  partNoColumn: string;
  descriptionColumn: string;
  unitPriceColumn: string;
  quantityColumn: string;
  totalPriceColumn: string;
  defaultDsnNo: string;
  defaultPartNo: string;
  defaultDescription: string;
  defaultUnitPrice: string;
  defaultQuantity: string;
  defaultTotalPrice: string;
}

type CheckState = 'idle' | 'checking' | 'passed' | 'failed';

@Component({
  selector: 'app-import-quotation-dialog',
  standalone: true,
  imports: [FormsModule, MaterialModule],
  templateUrl: './import-quotation-dialog.html',
  styleUrl: './import-quotation-dialog.css',
})
export class ImportQuotationDialogComponent {
  @ViewChild('supplierInput') private supplierInput?: ElementRef<HTMLInputElement>;

  private api = inject(ProjectApiService);
  private dialogRef = inject(MatDialogRef<ImportQuotationDialogComponent, boolean>);
  private data = inject<{ projectId: number; activities?: Activity[] }>(MAT_DIALOG_DATA);

  supplierQuotationRef = '';
  supplierQuery = '';
  supplierId: number | null = null;
  suppliers: Supplier[] = [];
  supplierOpen = false;
  creatingSupplier = false;
  newSupplierName = '';
  newSupplierReference = '';
  submitted = false;
  lines: ImportLine[] = [];
  file: File | null = null;
  fileName = '';
  columns: { letter: string; header: string | null }[] = [];
  mapping: QuoteMapping = this.blankMapping();
  checkState: CheckState = 'idle';
  importErrors: ActivityImportIssue[] = [];
  isImporting = false;
  error = '';
  private checkToken = 0;

  readonly fields = [
    { key: 'dsnNoColumn', def: 'defaultDsnNo', label: 'DSN No' },
    { key: 'partNoColumn', def: 'defaultPartNo', label: 'Part No' },
    { key: 'descriptionColumn', def: 'defaultDescription', label: 'Description' },
    { key: 'unitPriceColumn', def: 'defaultUnitPrice', label: 'Unit Price' },
    { key: 'quantityColumn', def: 'defaultQuantity', label: 'Quantity' },
    { key: 'totalPriceColumn', def: 'defaultTotalPrice', label: 'Total Price' },
  ];

  constructor() {
    this.api.suppliers().subscribe({
      next: (suppliers) => {
        this.suppliers = suppliers;
      },
    });
  }

  get activities(): Activity[] {
    return this.data.activities ?? [];
  }

  onDsn(line: ImportLine) {
    this.linkLine(line);
  }

  onPart(line: ImportLine) {
    this.linkLine(line);
  }

  onUnit(line: ImportLine) {
    line.unitExplicit = !!line.unitPrice.trim();
    line.priceWarning = line.unitExplicit && line.totalExplicit;
    if (!line.unitExplicit && !line.totalExplicit) {
      line.totalPrice = '';
      return;
    }
    this.recalculate(line);
  }

  onTotal(line: ImportLine) {
    line.totalExplicit = !!line.totalPrice.trim();
    line.priceWarning = line.unitExplicit && line.totalExplicit;
    if (!line.totalExplicit && !line.unitExplicit) {
      line.unitPrice = '';
      return;
    }
    this.recalculate(line);
  }

  onQuantity(line: ImportLine) {
    this.recalculate(line);
  }

  onQuantityBlur(line: ImportLine) {
    if (!line.quantity.trim()) {
      line.quantity = '1';
    }
    this.recalculate(line);
  }

  get filteredSuppliers(): Supplier[] {
    const query = this.supplierQuery.trim().toLowerCase();
    if (!query) {
      return this.suppliers.slice(0, 8);
    }
    return this.suppliers
      .filter((supplier) => supplier.name.toLowerCase().includes(query) || (supplier.reference ?? '').toLowerCase().includes(query))
      .slice(0, 8);
  }

  get dsnMissing(): boolean {
    return !this.mapping.dsnNoColumn.trim() && !this.mapping.defaultDsnNo.trim();
  }

  get partMissing(): boolean {
    return !this.mapping.partNoColumn.trim() && !this.mapping.defaultPartNo.trim();
  }

  get requiredMissing(): boolean {
    return this.dsnMissing || this.partMissing;
  }

  get choicesReady(): boolean {
    return this.lines.some((line) => this.isPriced(line));
  }

  get canCheck(): boolean {
    return !!this.file && !this.requiredMissing && this.checkState !== 'checking' && !this.isImporting;
  }

  get busy(): boolean {
    return this.checkState === 'checking' || this.isImporting;
  }

  onFieldEnter(event: Event) {
    blockEnterSubmit(event);
  }

  onSupplierInput(value: string) {
    this.supplierQuery = value;
    this.supplierId = null;
    this.supplierOpen = true;
  }

  chooseSupplier(supplier: Supplier) {
    this.supplierId = supplier.id;
    this.supplierQuery = supplier.name;
    this.supplierOpen = false;
  }

  startNewSupplier() {
    if (this.busy) {
      return;
    }
    this.creatingSupplier = true;
    this.newSupplierName = this.supplierQuery.trim();
    this.newSupplierReference = '';
    this.supplierId = null;
    this.supplierOpen = false;
  }

  cancelNewSupplier() {
    if (this.busy) {
      return;
    }
    this.creatingSupplier = false;
    this.newSupplierName = '';
    this.newSupplierReference = '';
    this.supplierQuery = '';
    this.supplierId = null;
    this.supplierOpen = false;
  }

  clearSupplier() {
    if (this.busy) {
      return;
    }
    this.creatingSupplier = false;
    this.newSupplierName = '';
    this.newSupplierReference = '';
    this.supplierQuery = '';
    this.supplierId = null;
    this.supplierOpen = true;
    setTimeout(() => this.supplierInput?.nativeElement.focus());
  }

  closeSuppliers() {
    setTimeout(() => {
      if (document.activeElement === this.supplierInput?.nativeElement) {
        return;
      }
      this.supplierOpen = false;
    }, 150);
  }

  onFile(event: Event) {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0] ?? null;
    this.file = file;
    this.fileName = file?.name ?? '';
    this.resetCheck();
    this.columns = [];
    this.error = '';
    if (!file) {
      return;
    }
    this.api.quotationExcelMapping(this.data.projectId, file).subscribe({
      next: (mapping) => {
        this.columns = mapping.columns;
        this.mapping = {
          ...this.blankMapping(),
          dsnNoColumn: mapping.suggested.dsnNo ?? '',
          partNoColumn: mapping.suggested.partNo ?? '',
          descriptionColumn: mapping.suggested.description ?? '',
          unitPriceColumn: mapping.suggested.unitPrice ?? '',
          quantityColumn: mapping.suggested.quantity ?? '',
          totalPriceColumn: mapping.suggested.totalPrice ?? '',
        };
      },
      error: () => {
        this.error = 'The spreadsheet could not be read.';
      },
    });
  }

  dirtyMapping() {
    this.resetCheck();
  }

  fieldInvalid(fieldKey: string): boolean {
    if (!this.columns.length) {
      return false;
    }
    if (fieldKey === 'dsnNoColumn') {
      return this.dsnMissing;
    }
    if (fieldKey === 'partNoColumn') {
      return this.partMissing;
    }
    return false;
  }

  checkData() {
    if (!this.file || !this.canCheck || this.checkState === 'checking') {
      return;
    }
    const token = ++this.checkToken;
    this.checkState = 'checking';
    this.importErrors = [];
    this.lines = [];
    this.error = '';
    this.dialogRef.disableClose = true;
    this.api.quotationExcelValidate(this.data.projectId, this.file, { ...this.mapping }).subscribe({
      next: (result) => {
        if (token !== this.checkToken) {
          return;
        }
        if (!result.valid) {
          this.checkState = 'failed';
          this.importErrors = result.errors ?? [];
          return;
        }
        const items = result.items ?? [];
        const choices = result.choices ?? [];
        if (items.length === 0 && choices.length === 0) {
          this.checkState = 'failed';
          this.error = 'No quotation items were found in the spreadsheet.';
          return;
        }
        this.lines = [...items, ...choices].map((item, index) => this.toLine(item, index + 1));
        this.checkState = 'passed';
      },
      error: (error: HttpErrorResponse) => {
        if (token !== this.checkToken) {
          return;
        }
        this.checkState = 'failed';
        this.importErrors = error.error?.errors ?? [];
        this.error = error.error?.message || 'The spreadsheet could not be checked.';
      },
    });
  }

  startImport() {
    const items = this.lineItems();
    if (this.isImporting || this.checkState !== 'passed' || items.length === 0) {
      return;
    }
    if (items.some((item) => !item.dsnNo || !item.partNo || item.unitPrice == null && item.totalPrice == null)) {
      this.error = 'DSN number, part number, and a price are required on every imported row.';
      return;
    }
    this.submitted = true;
    this.error = '';
    if (!this.supplierReady()) {
      return;
    }
    this.dialogRef.disableClose = true;
    this.withSupplier()
      .pipe(
        switchMap(() => this.api.createQuotation({
          supplierId: this.supplierId!,
          supplierReference: this.supplierQuotationRef.trim(),
          projectIds: [this.data.projectId],
          items,
        })),
        whileBusy((busy) => (this.isImporting = busy)),
      )
      .subscribe({
        next: () => {
          this.dialogRef.close(true);
        },
        error: (error: HttpErrorResponse) => {
          if (this.creatingSupplier) {
            this.error = error.error?.message || 'The supplier could not be created.';
            return;
          }
          const issues = (error.error?.errors as ActivityImportIssue[] | undefined) ?? [];
          this.checkState = 'failed';
          this.importErrors = issues;
          this.lines = [];
          this.error = issues.length
            ? issues.map((issue) => `Row ${issue.row}: [${issue.field}] ${issue.message}`).join(' ')
            : error.error?.message || 'The quotation could not be imported.';
        },
      });
  }

  cancel() {
    if (!this.busy) {
      this.dialogRef.close(false);
    }
  }

  private supplierReady(): boolean {
    if (this.creatingSupplier) {
      if (!this.newSupplierName.trim()) {
        this.error = 'Supplier name is required.';
        return false;
      }
      return true;
    }
    if (!this.supplierId) {
      this.error = 'Choose a supplier.';
      return false;
    }
    return true;
  }

  private withSupplier(): Observable<Supplier | null> {
    if (!this.creatingSupplier) {
      return of(null);
    }
    const reference = this.newSupplierReference.trim();
    return this.api.createSupplier(this.newSupplierName.trim(), reference || undefined).pipe(
      tap((supplier) => this.adoptSupplier(supplier)),
    );
  }

  private adoptSupplier(supplier: Supplier) {
    this.suppliers = [...this.suppliers.filter((item) => item.id !== supplier.id), supplier]
      .sort((left, right) => left.name.localeCompare(right.name));
    this.supplierId = supplier.id;
    this.supplierQuery = supplier.name;
    this.creatingSupplier = false;
    this.newSupplierName = '';
    this.newSupplierReference = '';
    this.supplierOpen = false;
  }

  resetCheck() {
    this.checkToken++;
    this.checkState = 'idle';
    this.importErrors = [];
    this.lines = [];
  }

  private toLine(source: {
    row?: number;
    dsnNo?: string | null;
    partNo?: string | null;
    description?: string | null;
    unitPrice?: number | null;
    quantity?: number | null;
    totalPrice?: number | null;
    priceWarning?: boolean;
    unitExplicit?: boolean;
    totalExplicit?: boolean;
    activityId?: number | null;
  }, fallbackRow: number): ImportLine {
    const unitText = source.unitPrice == null ? '' : String(source.unitPrice);
    const totalText = source.totalPrice == null ? '' : String(source.totalPrice);
    const line: ImportLine = {
      row: source.row ?? fallbackRow,
      dsnNo: source.dsnNo ?? '',
      partNo: source.partNo ?? '',
      description: source.description ?? '',
      unitPrice: unitText,
      quantity: String(source.quantity ?? 1),
      totalPrice: totalText,
      unitExplicit: source.unitExplicit ?? !!unitText,
      totalExplicit: source.totalExplicit ?? !!totalText,
      activityId: source.activityId ?? directActivityId(source.dsnNo ?? '', source.partNo ?? '', this.activities),
      priceWarning: !!source.priceWarning,
    };
    return line;
  }

  private linkLine(line: ImportLine) {
    line.activityId = directActivityId(line.dsnNo, line.partNo, this.activities);
  }

  private recalculate(line: ImportLine) {
    const quantity = this.quantity(line);
    if (line.unitExplicit && !line.totalExplicit) {
      const unit = this.money(line.unitPrice);
      line.totalPrice = unit == null ? '' : this.formatMoney(unit * quantity);
      return;
    }
    if (line.totalExplicit && !line.unitExplicit) {
      const total = this.money(line.totalPrice);
      line.unitPrice = total == null ? '' : this.formatMoney(total / quantity);
    }
  }

  private lineItems(): QuotationItemInput[] {
    return this.lines.filter((line) => this.isPriced(line)).map((line) => {
      if (!line.quantity.trim()) {
        line.quantity = '1';
      }
      this.recalculate(line);
      return {
        dsnNo: line.dsnNo.trim(),
        partNo: line.partNo.trim(),
        description: line.description.trim(),
        unitPrice: this.money(line.unitPrice),
        quantity: this.quantity(line),
        totalPrice: this.money(line.totalPrice),
        activityId: line.activityId,
        activityGroupId: null,
      };
    });
  }

  private isPriced(line: ImportLine): boolean {
    return !!line.unitPrice.trim() || !!line.totalPrice.trim();
  }

  private quantity(line: ImportLine): number {
    const parsed = Number(line.quantity);
    return Number.isInteger(parsed) && parsed >= 1 ? parsed : 1;
  }

  private formatMoney(value: number): string {
    return String(Math.round(value * 10000) / 10000);
  }

  private money(value: string): number | null {
    const text = value.trim().replace(/^\$/, '');
    if (!text || /[a-z]/i.test(text)) {
      return null;
    }
    const parsed = Number(text);
    return Number.isFinite(parsed) && parsed >= 0 ? parsed : null;
  }

  private blankMapping(): QuoteMapping {
    return {
      dsnNoColumn: '',
      partNoColumn: '',
      descriptionColumn: '',
      unitPriceColumn: '',
      quantityColumn: '',
      totalPriceColumn: '',
      defaultDsnNo: '',
      defaultPartNo: '',
      defaultDescription: '',
      defaultUnitPrice: '',
      defaultQuantity: '',
      defaultTotalPrice: '',
    };
  }
}
