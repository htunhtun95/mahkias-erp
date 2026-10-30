import { Component, ElementRef, QueryList, ViewChild, ViewChildren, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { Observable, of, switchMap, tap } from 'rxjs';
import { MaterialModule } from '../../app/material-module';
import { DriveUploadCardComponent } from '../drive-upload-card/drive-upload-card';
import { blockEnterSubmit } from '../../core/block-enter-submit';
import { whileBusy } from '../../core/while-busy';
import { Activity, ActivityImportIssue, Currency, ProjectApiService, QuotationItemInput, Supplier } from '../../core/services/project-api.service';
import { directActivityId } from '../quotation-map';
import { QuotationDateFieldComponent } from '../quotation-date-field/quotation-date-field';

interface ImportLine {
  row: number;
  dsnNo: string;
  partNo: string;
  description: string;
  condition: string;
  leadTime: string;
  minimumQty: string;
  unitPrice: string;
  quantity: string;
  totalPrice: string;
  currencyId: number | null;
  currencyExplicit: boolean;
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
  conditionColumn: string;
  leadTimeColumn: string;
  minimumQtyColumn: string;
  quotedDateColumn: string;
  validUntilColumn: string;
  unitPriceColumn: string;
  quantityColumn: string;
  totalPriceColumn: string;
  currencyColumn: string;
  defaultDsnNo: string;
  defaultPartNo: string;
  defaultDescription: string;
  defaultCondition: string;
  defaultLeadTime: string;
  defaultMinimumQty: string;
  defaultQuotedDate: string;
  defaultValidUntil: string;
  defaultUnitPrice: string;
  defaultQuantity: string;
  defaultTotalPrice: string;
}

type CheckState = 'idle' | 'checking' | 'passed' | 'failed';

@Component({
  selector: 'app-import-quotation-dialog',
  standalone: true,
  imports: [FormsModule, MaterialModule, DriveUploadCardComponent, QuotationDateFieldComponent],
  templateUrl: './import-quotation-dialog.html',
  styleUrl: './import-quotation-dialog.css',
})
export class ImportQuotationDialogComponent {
  @ViewChild('supplierInput') private supplierInput?: ElementRef<HTMLInputElement>;
  @ViewChildren(QuotationDateFieldComponent) private dateFields?: QueryList<QuotationDateFieldComponent>;

  private api = inject(ProjectApiService);
  private dialogRef = inject(MatDialogRef<ImportQuotationDialogComponent, boolean>);
  private data = inject<{ projectId: number; activities?: Activity[] }>(MAT_DIALOG_DATA);

  supplierQuotationRef = '';
  supplierQuery = '';
  supplierId: number | null = null;
  suppliers: Supplier[] = [];
  supplierOpen = false;
  creatingSupplier = false;
  currencies: Currency[] = [];
  currencyId: number | null = null;
  newSupplierName = '';
  newSupplierReference = '';
  submitted = false;
  lines: ImportLine[] = [];
  file: File | null = null;
  spreadsheetIsOriginal = true;
  originalFile: File | null = null;
  originalDragging = false;
  isVerifyStep = false;
  previewQuery = '';
  readonly documentAccept = '.pdf,.doc,.docx,.xls,.xlsx,.xlsm,.csv,.png,.jpg,.jpeg,.gif,.webp,.bmp,.tif,.tiff';
  columns: { letter: string; header: string | null }[] = [];
  mapping: QuoteMapping = this.blankMapping();
  checkState: CheckState = 'idle';
  reading = false;
  importErrors: ActivityImportIssue[] = [];
  isImporting = false;
  lineSubmitted = false;
  quotedDate = '';
  validUntil = '';
  error = '';
  private checkToken = 0;
  private mappingToken = 0;

  readonly fields = [
    { key: 'dsnNoColumn', def: 'defaultDsnNo', label: 'DSN No' },
    { key: 'partNoColumn', def: 'defaultPartNo', label: 'Part No' },
    { key: 'descriptionColumn', def: 'defaultDescription', label: 'Description' },
    { key: 'conditionColumn', def: 'defaultCondition', label: 'Condition' },
    { key: 'leadTimeColumn', def: 'defaultLeadTime', label: 'Lead Time' },
    { key: 'minimumQtyColumn', def: 'defaultMinimumQty', label: 'MOQ' },
    { key: 'unitPriceColumn', def: 'defaultUnitPrice', label: 'Unit Price' },
    { key: 'quantityColumn', def: 'defaultQuantity', label: 'Quantity' },
    { key: 'totalPriceColumn', def: 'defaultTotalPrice', label: 'Total Price' },
    { key: 'currencyColumn', def: '', label: 'Currency' },
  ];

  constructor() {
    this.api.suppliers().subscribe({
      next: (suppliers) => {
        this.suppliers = suppliers;
      },
    });
    this.api.currencies().subscribe({
      next: (currencies) => {
        this.currencies = currencies;
        const base = currencies.find((currency) => currency.isDefault) ?? currencies.find((currency) => currency.code === 'USD');
        this.currencyId = base?.id ?? this.currencyId;
        for (const line of this.lines) {
          if (!line.currencyExplicit && line.currencyId == null) {
            line.currencyId = this.currencyId;
          }
        }
      },
    });
  }

  get activities(): Activity[] {
    return this.data.activities ?? [];
  }

  onDsn(line: ImportLine) {
    this.linkLine(line);
  }

  onDefaultImportCurrency(value: number | null) {
    const next = value == null ? null : Number(value);
    this.currencyId = Number.isFinite(next) ? next : null;
    for (const line of this.lines) {
      if (!line.currencyExplicit) {
        line.currencyId = this.currencyId;
      }
    }
  }

  onLineCurrency(line: ImportLine) {
    line.currencyExplicit = true;
  }

  currencyMark(currencyId: number | null): string {
    const currency = this.currencies.find((item) => item.id === currencyId);
    return currency?.symbol || currency?.code || '$';
  }

  lineInvalid(line: ImportLine, field: 'dsn' | 'part' | 'price' | 'qty' | 'moq'): boolean {
    if (!this.lineSubmitted || !this.isPriced(line)) {
      return false;
    }
    if (field === 'dsn') {
      return !line.dsnNo.trim();
    }
    if (field === 'part') {
      return !line.partNo.trim();
    }
    if (field === 'qty') {
      return !this.validQuantity(line.quantity);
    }
    if (field === 'moq') {
      return !!line.minimumQty.trim() && !this.validQuantity(line.minimumQty);
    }
    if (line.unitPrice.trim() && this.money(line.unitPrice) == null) {
      return true;
    }
    return !!line.totalPrice.trim() && this.money(line.totalPrice) == null;
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

  onMinimum(line: ImportLine) {
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
    return !!this.file && this.columns.length > 0 && !this.requiredMissing && this.checkState !== 'checking' && !this.isImporting;
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

  onSpreadsheet(file: File | null) {
    if (file === this.file) {
      return;
    }
    this.file = file;
    const token = ++this.mappingToken;
    this.resetCheck();
    this.columns = [];
    this.mapping = this.blankMapping();
    this.error = '';
    this.reading = false;
    if (!file) {
      return;
    }
    this.reading = true;
    this.api.quotationExcelMapping(this.data.projectId, file).subscribe({
      next: (mapping) => {
        if (token !== this.mappingToken || this.file !== file) {
          return;
        }
        this.reading = false;
        this.columns = mapping.columns;
        this.mapping = {
          ...this.blankMapping(),
          dsnNoColumn: mapping.suggested.dsnNo ?? '',
          partNoColumn: mapping.suggested.partNo ?? '',
          descriptionColumn: mapping.suggested.description ?? '',
          conditionColumn: mapping.suggested.condition ?? '',
          leadTimeColumn: mapping.suggested.leadTime ?? '',
          minimumQtyColumn: mapping.suggested.minimumQty ?? '',
          unitPriceColumn: mapping.suggested.unitPrice ?? '',
          quantityColumn: mapping.suggested.quantity ?? '',
          totalPriceColumn: mapping.suggested.totalPrice ?? '',
          currencyColumn: mapping.suggested.currency ?? '',
        };
      },
      error: () => {
        if (token !== this.mappingToken) {
          return;
        }
        this.reading = false;
        this.error = 'The spreadsheet could not be read.';
      },
    });
  }

  onOriginalFile(event: Event) {
    const input = event.target as HTMLInputElement;
    this.originalFile = input.files?.[0] ?? null;
    input.value = '';
  }

  onOriginalDragOver(event: DragEvent) {
    event.preventDefault();
    event.stopPropagation();
    if (!this.busy) {
      this.originalDragging = true;
    }
  }

  onOriginalDragLeave(event: DragEvent) {
    event.preventDefault();
    event.stopPropagation();
    const next = event.relatedTarget as Node | null;
    if (next && (event.currentTarget as HTMLElement).contains(next)) {
      return;
    }
    this.originalDragging = false;
  }

  onOriginalDrop(event: DragEvent) {
    event.preventDefault();
    event.stopPropagation();
    this.originalDragging = false;
    if (this.busy) {
      return;
    }
    const file = Array.from(event.dataTransfer?.files ?? []).find((item) => this.originalAccepted(item));
    if (file) {
      this.originalFile = file;
    }
  }

  private originalAccepted(file: File) {
    const name = file.name.toLowerCase();
    return this.documentAccept
      .split(',')
      .map((item) => item.trim().toLowerCase())
      .filter((item) => item.startsWith('.'))
      .some((extension) => name.endsWith(extension));
  }

  clearOriginal() {
    if (!this.busy) {
      this.originalFile = null;
    }
  }

  dirtyMapping() {
    this.invalidatePreview();
  }

  showMapping() {
    if (this.isImporting) {
      return;
    }
    this.isVerifyStep = false;
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
    if (!this.file || !this.columns.length || this.checkState === 'checking' || this.isImporting) {
      return;
    }
    this.submitted = true;
    this.error = '';
    const datesInvalid = this.revealInvalidDates();
    if (!this.supplierReady() || this.requiredMissing) {
      return;
    }
    if (datesInvalid || this.dateOrderError) {
      return;
    }
    const token = ++this.checkToken;
    this.checkState = 'checking';
    this.importErrors = [];
    this.lines = [];
    this.error = '';
    this.dialogRef.disableClose = true;
    this.api.quotationExcelValidate(this.data.projectId, this.file, {
      ...this.mapping,
      quotedDateColumn: '',
      validUntilColumn: '',
      defaultQuotedDate: this.quotedDate,
      defaultValidUntil: this.validUntil,
      currencyId: this.currencyId,
    }).subscribe({
      next: (result) => {
        if (token !== this.checkToken) {
          return;
        }
        this.dialogRef.disableClose = this.isImporting;
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
        this.isVerifyStep = true;
      },
      error: (error: HttpErrorResponse) => {
        if (token !== this.checkToken) {
          return;
        }
        this.dialogRef.disableClose = this.isImporting;
        this.checkState = 'failed';
        this.importErrors = error.error?.errors ?? [];
        this.error = error.error?.message || 'The spreadsheet could not be checked.';
      },
    });
  }

  startImport() {
    const items = this.lineItems();
    if (this.isImporting || this.checkState !== 'passed' || items.length === 0) {
      if (this.checkState === 'passed' && items.length === 0) {
        this.lineSubmitted = true;
        this.error = 'Enter a unit price or total price for at least one row.';
      }
      return;
    }
    this.lineSubmitted = true;
    this.error = '';
    if (this.revealInvalidDates() || this.dateOrderError) {
      return;
    }
    if (this.lines.some((line) => this.isPriced(line) && (this.lineInvalid(line, 'dsn') || this.lineInvalid(line, 'part') || this.lineInvalid(line, 'qty') || this.lineInvalid(line, 'moq') || this.lineInvalid(line, 'price')))) {
      this.error = 'DSN number, part number, and quantity are required. Check the highlighted rows.';
      return;
    }
    if (!this.supplierReady()) {
      this.isVerifyStep = false;
      return;
    }
    this.dialogRef.disableClose = true;
    this.withSupplier()
      .pipe(
        switchMap(() => this.api.importQuotation(this.data.projectId, {
          supplierId: this.supplierId!,
          supplierReference: this.supplierQuotationRef.trim(),
          currencyId: this.currencyId,
          quotedDate: this.quotedDate || null,
          validUntil: this.validUntil || null,
          items,
        }, this.storedFile())),
        whileBusy((busy) => (this.isImporting = busy)),
      )
      .subscribe({
        next: () => {
          this.dialogRef.close(true);
        },
        error: (error: HttpErrorResponse) => {
          this.dialogRef.disableClose = false;
          if (this.creatingSupplier) {
            this.error = error.error?.message || 'The supplier could not be created.';
            return;
          }
          const issues = (error.error?.errors as ActivityImportIssue[] | undefined) ?? [];
          if (issues.length) {
            this.isVerifyStep = false;
            this.checkState = 'failed';
            this.importErrors = issues;
            this.lines = [];
            this.error = issues.map((issue) => `Row ${issue.row}: [${issue.field}] ${issue.message}`).join(' ');
            return;
          }
          const body = error.error;
          this.error = (typeof body === 'string' ? body : body?.message) || 'The quotation could not be imported.';
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

  onOriginalChoice(checked: boolean) {
    this.spreadsheetIsOriginal = checked;
    if (checked) {
      this.originalFile = null;
    }
  }

  resetCheck() {
    this.invalidatePreview();
  }

  private invalidatePreview() {
    this.checkToken++;
    this.checkState = 'idle';
    this.importErrors = [];
    this.lines = [];
    this.previewQuery = '';
    this.isVerifyStep = false;
    this.lineSubmitted = false;
    if (!this.isImporting) {
      this.dialogRef.disableClose = false;
    }
  }

  get filteredLines(): ImportLine[] {
    const query = this.previewQuery.trim().toLowerCase();
    if (!query) {
      return this.lines;
    }
    return this.lines.filter((line) => this.lineSearchText(line).includes(query));
  }

  private lineSearchText(line: ImportLine): string {
    const currency = this.currencies.find((item) => item.id === line.currencyId);
    return [line.dsnNo, line.partNo, line.description, line.condition, line.leadTime, line.quantity, line.unitPrice, line.totalPrice, currency?.code, currency?.symbol]
      .filter((value) => !!value)
      .join(' ')
      .toLowerCase();
  }

  private toLine(source: {
    row?: number;
    dsnNo?: string | null;
    partNo?: string | null;
    description?: string | null;
    condition?: string | null;
    leadTime?: string | null;
    minimumQty?: number | null;
    unitPrice?: number | null;
    quantity?: number | null;
    totalPrice?: number | null;
    priceWarning?: boolean;
    unitExplicit?: boolean;
    totalExplicit?: boolean;
    activityId?: number | null;
    currencyId?: number | null;
    currencyExplicit?: boolean;
  }, fallbackRow: number): ImportLine {
    const unitText = source.unitPrice == null ? '' : String(source.unitPrice);
    const totalText = source.totalPrice == null ? '' : String(source.totalPrice);
    const line: ImportLine = {
      row: source.row ?? fallbackRow,
      dsnNo: source.dsnNo ?? '',
      partNo: source.partNo ?? '',
      description: source.description ?? '',
      condition: source.condition ?? '',
      leadTime: source.leadTime ?? '',
      minimumQty: source.minimumQty ? String(source.minimumQty) : '',
      unitPrice: unitText,
      quantity: String(source.quantity ?? 1),
      totalPrice: totalText,
      unitExplicit: source.unitExplicit ?? !!unitText,
      totalExplicit: source.totalExplicit ?? !!totalText,
      activityId: source.activityId ?? directActivityId(source.dsnNo ?? '', source.partNo ?? '', this.activities),
      priceWarning: !!source.priceWarning,
      currencyId: source.currencyId ?? this.currencyId,
      currencyExplicit: !!source.currencyExplicit,
    };
    return line;
  }

  private linkLine(line: ImportLine) {
    line.activityId = directActivityId(line.dsnNo, line.partNo, this.activities);
  }

  private recalculate(line: ImportLine) {
    const quantity = this.effectiveQuantity(line);
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

  private storedFile(): File | null {
    return this.spreadsheetIsOriginal ? this.file : this.originalFile;
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
        condition: line.condition.trim(),
        leadTime: line.leadTime.trim(),
        minimumQty: this.optionalQuantity(line.minimumQty),
        unitPrice: this.money(line.unitPrice),
        quantity: this.quantity(line),
        totalPrice: this.money(line.totalPrice),
        activityId: line.activityId,
        activityGroupId: null,
        currencyId: line.currencyId ?? this.currencyId,
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

  private effectiveQuantity(line: ImportLine): number {
    const minimum = Number(line.minimumQty);
    if (line.minimumQty.trim() && Number.isInteger(minimum) && minimum > 0) {
      return minimum;
    }
    const requested = Number(line.quantity);
    return Number.isInteger(requested) && requested > 0 ? requested : 1;
  }

  private revealInvalidDates(): boolean {
    const invalid = this.dateFields?.some((field) => field.invalid) ?? false;
    this.dateFields?.forEach((field) => {
      if (field.invalid) {
        field.reveal();
      }
    });
    return invalid;
  }

  get dateOrderError(): string {
    if (this.quotedDate && this.validUntil && this.validUntil < this.quotedDate) {
      return 'Valid until cannot be earlier than the quoted date.';
    }
    return '';
  }

  private validQuantity(value: string): boolean {
    if (!value.trim()) {
      return true;
    }
    const parsed = Number(value);
    return Number.isInteger(parsed) && parsed >= 1;
  }

  private optionalQuantity(value: string): number | null {
    return value.trim() && this.validQuantity(value) ? Number(value) : null;
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
      conditionColumn: '',
      leadTimeColumn: '',
      minimumQtyColumn: '',
      quotedDateColumn: '',
      validUntilColumn: '',
      unitPriceColumn: '',
      quantityColumn: '',
      totalPriceColumn: '',
      currencyColumn: '',
      defaultDsnNo: '',
      defaultPartNo: '',
      defaultDescription: '',
      defaultCondition: '',
      defaultLeadTime: '',
      defaultMinimumQty: '',
      defaultQuotedDate: '',
      defaultValidUntil: '',
      defaultUnitPrice: '',
      defaultQuantity: '',
      defaultTotalPrice: '',
    };
  }
}
