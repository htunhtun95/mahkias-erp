import { TextFieldModule } from '@angular/cdk/text-field';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, ElementRef, OnInit, ViewChild, inject } from '@angular/core';
import { FormsModule, NgModel } from '@angular/forms';
import { MAT_DIALOG_DATA, MatDialog, MatDialogRef } from '@angular/material/dialog';
import { forkJoin, of, switchMap } from 'rxjs';
import { MaterialModule } from '../../app/material-module';
import {
  Activity,
  Currency,
  ExcelColumn,
  LookupValue,
  ProjectApiService,
  PurchaseOrderExcelMappingInput,
  PurchaseOrderItemInput,
  Supplier,
} from '../../core/services/project-api.service';
import { DocumentDraft, documentSummary, syncDocumentDrafts } from '../document-manager/document-manager';
import { openDocumentLibrary } from '../document-library-dialog/document-library-dialog';
import { DriveUploadCardComponent } from '../drive-upload-card/drive-upload-card';
import { formatMoney } from '../money-format';
import { LookupComboboxComponent } from '../lookup-combobox/lookup-combobox';
import { QuotationChargeRow, QuotationChargesComponent, quotationChargeTotal, readQuotationCharges } from '../quotation-charges/quotation-charges';
import { QuotationDateFieldComponent } from '../quotation-date-field/quotation-date-field';
import { canonicalCondition, canonicalUnit } from '../quotation-lookups';
import { dateInputValue } from '../quotation-terms';
import { closeOnEscape } from '../../core/close-on-escape';
import { SheetCellFocusDirective } from '../sheet-cell-focus';

interface ImportPurchaseOrderData {
  projectId: number;
  activities?: Activity[];
}

interface PreviewLine {
  row: number;
  activityId: number | null;
  activityWarning: string;
  dsnNo: string;
  partNo: string;
  description: string;
  condition: string;
  unit: string;
  leadTime: string;
  quantity: string;
  unitPrice: string;
  totalPrice: string;
  unitExplicit: boolean;
  totalExplicit: boolean;
  priceWarning: boolean;
}

@Component({
  selector: 'app-import-purchase-order-dialog',
  standalone: true,
  imports: [FormsModule, TextFieldModule, MaterialModule, QuotationDateFieldComponent, QuotationChargesComponent, DriveUploadCardComponent, LookupComboboxComponent, SheetCellFocusDirective],
  templateUrl: './import-purchase-order-dialog.html',
  styleUrls: ['../purchase-order-dialog/purchase-order-dialog.css'],
})
export class ImportPurchaseOrderDialogComponent implements OnInit {
  private dialog = inject(MatDialog);
  documentDrafts: DocumentDraft[] | null = null;
  private api = inject(ProjectApiService);
  private dialogRef = inject(MatDialogRef<ImportPurchaseOrderDialogComponent, boolean>);
  readonly data = inject<ImportPurchaseOrderData>(MAT_DIALOG_DATA);

  suppliers: Supplier[] = [];
  currencies: Currency[] = [];
  conditions: LookupValue[] = [];
  units: LookupValue[] = [];
  supplierId: number | null = null;
  supplierQuery = '';
  supplierOpen = false;
  @ViewChild('supplierInput') supplierInput?: ElementRef<HTMLInputElement>;
  currencyId: number | null = null;
  statuses: LookupValue[] = [];
  statusCode = 'Draft';
  poDate = todayIso();
  code = '';
  shipToAddress = '';
  charges: QuotationChargeRow[] = [];
  readonly documentAccept = '.pdf,.doc,.docx,.xls,.xlsx,.xlsm,.csv,.png,.jpg,.jpeg,.gif,.webp,.bmp,.tif,.tiff';
  file: File | null = null;
  originalFile: File | null = null;
  originalDragging = false;
  spreadsheetIsOriginal = true;
  isVerifyStep = false;
  previewQuery = '';
  columns: ExcelColumn[] = [];
  mapping: PurchaseOrderExcelMappingInput = blankMapping();
  readonly fields = [
    { key: 'dsnNoColumn', def: 'defaultDsnNo', label: 'DSN No', required: true },
    { key: 'partNoColumn', def: 'defaultPartNo', label: 'Part No', required: true },
    { key: 'descriptionColumn', def: 'defaultDescription', label: 'Description', required: false },
    { key: 'conditionColumn', def: 'defaultCondition', label: 'Condition', required: false },
    { key: 'unitColumn', def: 'defaultUnit', label: 'Unit', required: false },
    { key: 'leadTimeColumn', def: 'defaultLeadTime', label: 'Lead Time', required: false },
    { key: 'unitPriceColumn', def: 'defaultUnitPrice', label: 'Unit Price', required: false },
    { key: 'quantityColumn', def: 'defaultQuantity', label: 'Quantity', required: false },
    { key: 'totalPriceColumn', def: 'defaultTotalPrice', label: 'Total Price', required: false },
  ] as const;
  lines: PreviewLine[] = [];
  errors: { row: number; field: string; message: string }[] = [];
  checking = false;
  reading = false;
  saving = false;
  checkState: 'idle' | 'checking' | 'passed' | 'failed' = 'idle';
  error = '';
  private mappingToken = 0;

  ngOnInit() {
    closeOnEscape(this.dialogRef, () => this.close());
    forkJoin({
      suppliers: this.api.suppliers(),
      currencies: this.api.currencies(),
      units: this.api.units(),
      conditions: this.api.conditions(),
      statuses: this.api.purchaseOrderStatuses(),
    }).subscribe({
      next: (result) => {
        this.suppliers = result.suppliers;
        this.currencies = result.currencies;
        this.units = result.units;
        this.conditions = result.conditions;
        this.statuses = result.statuses ?? [];
        this.statusCode = 'Draft';
        this.currencyId = result.currencies.find((currency) => currency.isDefault)?.id ?? result.currencies[0]?.id ?? null;
        for (const line of this.lines) {
          this.linkLine(line);
        }
      },
      error: () => {
        this.error = 'Purchase order details could not be loaded.';
      },
    });
  }

  get symbol(): string {
    return this.currencies.find((currency) => currency.id === this.currencyId)?.symbol || '$';
  }

  get currencyCode(): string {
    return this.currencies.find((currency) => currency.id === this.currencyId)?.code || 'USD';
  }

  get activities(): Activity[] {
    return this.data.activities ?? [];
  }

  get conditionNames(): string[] {
    return this.conditions.map((condition) => condition.name);
  }

  get unitNames(): string[] {
    return this.units.map((unit) => unit.name);
  }

  get subtotal(): number {
    return roundMoney(this.lines.reduce((sum, line) => sum + (this.moneyValue(line.totalPrice) ?? 0), 0));
  }

  get chargesAmount(): number {
    return roundMoney(quotationChargeTotal(this.charges));
  }

  get grandTotal(): number {
    return roundMoney(this.subtotal + this.chargesAmount);
  }

  money(value: number): string {
    return formatMoney(value, this.symbol, this.currencyCode);
  }

  mappingNoted = false;

  noteMap(key: string) {
    if (key === 'dsnNoColumn' || key === 'partNoColumn' || key === 'defaultDsnNo' || key === 'defaultPartNo') {
      this.mappingNoted = true;
    }
  }

  get canCheck(): boolean {
    return !!this.file && this.columns.length > 0 && !this.requiredMissing && !!this.code.trim() && !!this.supplierId && !readQuotationCharges(this.charges).error && !this.checking && !this.reading && !this.saving;
  }

  get canImport(): boolean {
    return this.isVerifyStep && this.lines.length > 0 && this.previewIssues.length === 0 && !!this.supplierId && !!this.code.trim() && !readQuotationCharges(this.charges).error && !this.saving;
  }

  get activityIssueCount(): number {
    return this.lines.filter((line) => !!line.activityWarning).length;
  }

  get issueBanner(): string {
    if (this.activityIssueCount === 1) {
      return '1 row does not match an activity on this project.';
    }
    if (this.activityIssueCount > 1) {
      return `${this.activityIssueCount} rows do not match an activity on this project.`;
    }
    return 'Fix the highlighted cells before importing.';
  }

  get previewIssues(): { row: number; message: string }[] {
    return this.lines.flatMap((line) => {
      const messages: string[] = [];
      if (line.activityWarning) {
        messages.push(line.activityWarning);
      }
      if (!this.validQuantity(line.quantity)) {
        messages.push('Quantity must be a whole number greater than zero.');
      }
      if (this.moneyValue(line.unitPrice) == null) {
        messages.push('Unit price must be zero or more.');
      }
      if (line.totalPrice.trim() && this.moneyValue(line.totalPrice) == null) {
        messages.push('Total price must be zero or more.');
      }
      return messages.map((message) => ({ row: line.row, message }));
    });
  }

  get dsnMissing(): boolean {
    return !this.mapping.dsnNoColumn?.trim() && !this.mapping.defaultDsnNo?.trim();
  }

  get partMissing(): boolean {
    return !this.mapping.partNoColumn?.trim() && !this.mapping.defaultPartNo?.trim();
  }

  get requiredMissing(): boolean {
    return this.dsnMissing || this.partMissing;
  }

  get filteredSuppliers(): Supplier[] {
    const query = this.supplierQuery.trim().toLowerCase();
    if (!query) {
      return this.suppliers;
    }
    return this.suppliers.filter((supplier) =>
      supplier.name.toLowerCase().includes(query) || (supplier.reference ?? '').toLowerCase().includes(query),
    );
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

  clearSupplier(field?: NgModel) {
    if (this.saving) {
      return;
    }
    field?.control.markAsDirty();
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

  get filteredLines(): PreviewLine[] {
    const query = this.previewQuery.trim().toLowerCase();
    if (!query) {
      return this.lines;
    }
    return this.lines.filter((line) =>
      [line.dsnNo, line.partNo, line.description, line.condition, line.unit, line.leadTime]
        .some((value) => value.toLowerCase().includes(query)),
    );
  }

  lineInvalid(line: PreviewLine, field: 'dsn' | 'part' | 'qty' | 'price'): boolean {
    if (field === 'dsn' || field === 'part') {
      return !!line.activityWarning;
    }
    if (field === 'qty') {
      return !this.validQuantity(line.quantity);
    }
    if (this.moneyValue(line.unitPrice) == null) {
      return true;
    }
    return !!line.totalPrice.trim() && this.moneyValue(line.totalPrice) == null;
  }

  onDsn(line: PreviewLine) {
    this.linkLine(line);
  }

  onPart(line: PreviewLine) {
    this.linkLine(line);
  }

  onUnitPrice(line: PreviewLine) {
    line.unitExplicit = !!line.unitPrice.trim();
    if (!line.unitExplicit && !line.totalExplicit) {
      line.totalPrice = '';
      line.priceWarning = false;
      return;
    }
    this.recalculate(line);
  }

  onTotalPrice(line: PreviewLine) {
    line.totalExplicit = !!line.totalPrice.trim();
    if (!line.totalExplicit && !line.unitExplicit) {
      line.unitPrice = '';
      line.priceWarning = false;
      return;
    }
    this.recalculate(line);
  }

  onQuantity(line: PreviewLine) {
    this.recalculate(line);
  }

  onQuantityBlur(line: PreviewLine) {
    if (!line.quantity.trim()) {
      line.quantity = '1';
    }
    this.recalculate(line);
  }

  normalizeUnit(line: PreviewLine) {
    line.unit = canonicalUnit(line.unit, this.units);
  }

  normalizeCondition(line: PreviewLine) {
    line.condition = canonicalCondition(line.condition, this.conditions);
  }

  close() {
    if (!this.saving) {
      this.dialogRef.close(false);
    }
  }

  get supportingLabel(): string {
    return documentSummary(this.documentDrafts ?? [], true);
  }

  openDocuments() {
    openDocumentLibrary(this.dialog, {
      title: 'Supporting documents',
      note: 'Extra files are stored when the purchase order is imported. The spreadsheet above is still the file that is read.',
      kind: 'purchase-order',
      projectId: this.data.projectId,
      ownerId: null,
      documents: [],
      drafts: this.documentDrafts,
      accept: this.documentAccept,
      supportingOnly: true,
      sortBase: 100,
    }).afterClosed().subscribe((result) => {
      if (result?.saved) {
        this.documentDrafts = result.drafts ?? [];
      }
    });
  }

  onOriginalChoice(checked: boolean) {
    this.spreadsheetIsOriginal = checked;
    if (checked) {
      this.originalFile = null;
    }
  }

  onOriginalFile(event: Event) {
    const input = event.target as HTMLInputElement;
    this.originalFile = input.files?.[0] ?? null;
    input.value = '';
  }

  onOriginalDragOver(event: DragEvent) {
    event.preventDefault();
    event.stopPropagation();
    if (!this.saving && !this.checking) {
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
    if (this.saving || this.checking) {
      return;
    }
    const file = Array.from(event.dataTransfer?.files ?? []).find((item) => this.originalAccepted(item));
    if (file) {
      this.originalFile = file;
    }
  }

  clearOriginal() {
    if (!this.saving && !this.checking) {
      this.originalFile = null;
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

  onFile(file: File | null) {
    if (file === this.file) {
      return;
    }
    this.file = file;
    const token = ++this.mappingToken;
    this.resetCheck();
    this.columns = [];
    this.mapping = blankMapping();
    this.error = '';
    this.reading = false;
    if (!file) {
      return;
    }
    this.reading = true;
    this.api.quotationExcelMapping(this.data.projectId, file).subscribe({
      next: (result) => {
        if (token !== this.mappingToken || this.file !== file) {
          return;
        }
        const suggested = result.suggested ?? {};
        this.reading = false;
        this.columns = result.columns ?? [];
        this.mapping = {
          ...blankMapping(),
          dsnNoColumn: suggested.dsnNo ?? '',
          partNoColumn: suggested.partNo ?? '',
          descriptionColumn: suggested.description ?? '',
          conditionColumn: suggested.condition ?? '',
          unitColumn: suggested.unit ?? '',
          leadTimeColumn: suggested.leadTime ?? '',
          quantityColumn: suggested.quantity ?? '',
          unitPriceColumn: suggested.unitPrice ?? '',
          totalPriceColumn: suggested.totalPrice ?? '',
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

  dirtyMapping() {
    this.resetCheck();
  }

  showMapping() {
    if (!this.saving) {
      this.isVerifyStep = false;
    }
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

  check() {
    if (!this.canCheck || !this.file) {
      return;
    }
    const code = this.code.trim();
    this.code = code;
    if (!code) {
      this.error = 'Enter a PO number.';
      return;
    }
    if (code.length > 50) {
      this.error = 'PO number must be 50 characters or fewer.';
      return;
    }
    if (!this.supplierId) {
      this.error = 'Select a supplier.';
      return;
    }
    this.checking = true;
    this.checkState = 'checking';
    this.error = '';
    this.errors = [];
    this.lines = [];
    this.api.purchaseOrderExcelValidate(this.data.projectId, this.file, this.mapping).subscribe({
      next: (result) => {
        this.checking = false;
        this.errors = result.errors ?? [];
        this.lines = (result.items ?? []).map((item, index) => this.previewLine(item, index));
        if (!this.lines.length) {
          this.checkState = 'failed';
          if (!this.errors.length) {
            this.error = 'No purchase order lines were found in the spreadsheet.';
          }
          return;
        }
        this.checkState = this.previewIssues.length ? 'failed' : 'passed';
        this.previewQuery = '';
        this.isVerifyStep = true;
      },
      error: (error: HttpErrorResponse) => {
        this.checking = false;
        this.checkState = 'failed';
        this.error = error.error?.message || 'The spreadsheet could not be checked.';
      },
    });
  }

  private resetCheck() {
    this.checkState = 'idle';
    this.lines = [];
    this.errors = [];
    this.previewQuery = '';
    this.isVerifyStep = false;
  }

  private previewLine(item: PurchaseOrderItemInput & { row?: number }, index: number): PreviewLine {
    const unitText = item.unitPrice == null ? '' : String(item.unitPrice);
    const totalText = item.totalPrice == null ? '' : String(item.totalPrice);
    const line: PreviewLine = {
      row: item.row || index + 1,
      activityId: item.activityId ?? null,
      activityWarning: '',
      dsnNo: item.dsnNo ?? '',
      partNo: item.partNo ?? '',
      description: item.description ?? '',
      condition: item.condition ?? '',
      unit: item.unit ?? '',
      leadTime: item.leadTime ?? '',
      quantity: item.quantity == null ? '' : String(item.quantity),
      unitPrice: unitText,
      totalPrice: totalText,
      unitExplicit: !!unitText,
      totalExplicit: !!totalText,
      priceWarning: false,
    };
    this.linkLine(line);
    this.notePrice(line);
    return line;
  }

  private linkLine(line: PreviewLine) {
    const matches = matchActivities(this.activities, line.dsnNo, line.partNo);
    if (matches.length === 1) {
      line.activityId = matches[0].id;
      line.activityWarning = '';
      return;
    }
    line.activityId = null;
    line.activityWarning = matches.length > 1
      ? 'More than one activity matches this DSN and part.'
      : 'No activity on this project matches this DSN and part.';
  }

  private recalculate(line: PreviewLine) {
    const quantity = this.quantity(line);
    if (line.unitExplicit && !line.totalExplicit) {
      const unit = this.moneyValue(line.unitPrice);
      line.totalPrice = unit == null || quantity == null ? '' : this.formatAmount(unit * quantity);
    } else if (line.totalExplicit && !line.unitExplicit) {
      const total = this.moneyValue(line.totalPrice);
      line.unitPrice = total == null || quantity == null ? '' : this.formatAmount(total / quantity);
    }
    this.notePrice(line);
  }

  private notePrice(line: PreviewLine) {
    const quantity = this.quantity(line);
    const unit = this.moneyValue(line.unitPrice);
    const total = this.moneyValue(line.totalPrice);
    line.priceWarning = !!(line.unitExplicit && line.totalExplicit && quantity != null && unit != null && total != null
      && Math.abs(roundMoney(unit * quantity) - roundMoney(total)) > 0.01);
  }

  private validQuantity(value: string): boolean {
    if (!value.trim()) {
      return false;
    }
    const parsed = Number(value);
    return Number.isInteger(parsed) && parsed >= 1;
  }

  private quantity(line: PreviewLine): number | null {
    return this.validQuantity(line.quantity) ? Number(line.quantity) : null;
  }

  private moneyValue(value: string): number | null {
    const text = value.trim().replace(/^\$/, '');
    if (!text || /[a-z]/i.test(text)) {
      return null;
    }
    const parsed = Number(text);
    return Number.isFinite(parsed) && parsed >= 0 ? parsed : null;
  }

  private formatAmount(value: number): string {
    return String(Math.round(value * 10000) / 10000);
  }

  private lineItems(): PurchaseOrderItemInput[] {
    return this.lines.map((line) => {
      const quantity = this.quantity(line) ?? 0;
      const unitPrice = this.moneyValue(line.unitPrice) ?? 0;
      const totalPrice = this.moneyValue(line.totalPrice) ?? roundMoney(unitPrice * quantity);
      return {
        activityId: line.activityId,
        dsnNo: line.dsnNo.trim(),
        partNo: line.partNo.trim(),
        description: line.description.trim(),
        condition: line.condition.trim(),
        unit: line.unit.trim(),
        leadTime: line.leadTime.trim(),
        quantity,
        unitPrice,
        totalPrice,
      };
    });
  }

  startImport() {
    if (!this.canImport || !this.supplierId) {
      return;
    }
    const code = this.code.trim();
    this.code = code;
    if (!code) {
      this.error = 'Enter a PO number.';
      return;
    }
    if (code.length > 50) {
      this.error = 'PO number must be 50 characters or fewer.';
      return;
    }
    if (this.previewIssues.length) {
      this.error = this.activityIssueCount
        ? 'Each row must match an activity on this project before it can be imported.'
        : 'Fix the highlighted cells before importing.';
      return;
    }
    const charges = readQuotationCharges(this.charges);
    if (charges.error) {
      this.error = charges.error;
      return;
    }
    this.saving = true;
    this.error = '';
    this.api.importPurchaseOrder(this.data.projectId, {
      code,
      supplierId: this.supplierId,
      poDate: isoDate(this.poDate),
      shipToAddress: this.shipToAddress.trim(),
      currencyId: this.currencyId,
      status: this.statusCode || 'Draft',
      projectIds: [this.data.projectId],
      items: this.lineItems(),
      charges: charges.charges,
    }, this.referenceFile()).pipe(
      switchMap((saved) => {
        const id = saved?.id;
        const drafts = this.documentDrafts;
        if (!id || !drafts?.some((draft) => draft.pendingFile && !draft.removed)) {
          return of(saved);
        }
        return syncDocumentDrafts(this.api, 'purchase-order', this.data.projectId, id, drafts, true, 100);
      }),
    ).subscribe({
      next: () => {
        this.saving = false;
        this.dialogRef.close(true);
      },
      error: (error: HttpErrorResponse) => {
        this.saving = false;
        this.error = error.error?.message || 'The purchase order could not be imported.';
      },
    });
  }

  private referenceFile(): File | null {
    return this.spreadsheetIsOriginal ? this.file : this.originalFile;
  }
}

function matchActivities(activities: Activity[], dsnNo: string, partNo: string): Activity[] {
  const dsn = dsnNo.trim().toLowerCase();
  let matches = dsn
    ? activities.filter((activity) => (activity.dsnNo ?? '').trim().toLowerCase() === dsn)
    : [];
  if (matches.length > 1 && partNo.trim()) {
    const narrowed = matches.filter((activity) => partMatches(activity, partNo));
    if (narrowed.length) {
      matches = narrowed;
    }
  }
  if (!matches.length && partNo.trim()) {
    matches = activities.filter((activity) => partMatches(activity, partNo));
  }
  return matches;
}

function partMatches(activity: Activity, importedPart: string): boolean {
  const part = activityPart(activity);
  if (sameText(part, importedPart)) {
    return true;
  }
  const wanted = importedPart.trim();
  if (wanted.length < 3) {
    return false;
  }
  return part.split(/\s+or\s+|[,;]/i).some((token) => sameText(token, wanted));
}

function activityPart(activity: Activity): string {
  return (activity.partNo || activity.mainPartNo || '').trim();
}

function sameText(left: string, right: string): boolean {
  return left.trim().toLowerCase() === right.trim().toLowerCase();
}

function blankMapping(): PurchaseOrderExcelMappingInput {
  return {
    dsnNoColumn: '',
    partNoColumn: '',
    descriptionColumn: '',
    conditionColumn: '',
    unitColumn: '',
    leadTimeColumn: '',
    quantityColumn: '',
    unitPriceColumn: '',
    totalPriceColumn: '',
    defaultDsnNo: '',
    defaultPartNo: '',
    defaultDescription: '',
    defaultCondition: '',
    defaultUnit: '',
    defaultLeadTime: '',
    defaultQuantity: '',
    defaultUnitPrice: '',
    defaultTotalPrice: '',
  };
}

function roundMoney(value: number): number {
  return Math.round(value * 100) / 100;
}

function todayIso(): string {
  return isoDate(new Date()) ?? '';
}

function isoDate(value: string | Date | null): string | null {
  if (!value) {
    return null;
  }
  if (value instanceof Date) {
    if (Number.isNaN(value.getTime())) {
      return null;
    }
    const month = String(value.getMonth() + 1).padStart(2, '0');
    const day = String(value.getDate()).padStart(2, '0');
    return `${value.getFullYear()}-${month}-${day}`;
  }
  return dateInputValue(value) || null;
}
