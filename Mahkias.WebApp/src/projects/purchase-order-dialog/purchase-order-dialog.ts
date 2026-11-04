import { TextFieldModule } from '@angular/cdk/text-field';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, ElementRef, OnInit, ViewChild, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { catchError, forkJoin, of, switchMap } from 'rxjs';
import { MaterialModule } from '../../app/material-module';
import {
  Activity,
  ActivityQuotationLink,
  Currency,
  LookupValue,
  ProjectApiService,
  PurchaseOrderDocument,
  PurchaseOrderHistoryEntry,
  PurchaseOrderItemInput,
  PurchaseOrderLine,
  Supplier,
} from '../../core/services/project-api.service';
import { DriveUploadCardComponent, StoredDriveFile } from '../drive-upload-card/drive-upload-card';
import { formatMoney } from '../money-format';
import { LookupComboboxComponent } from '../lookup-combobox/lookup-combobox';
import { QuotationChargeRow, QuotationChargesComponent, quotationChargeTotal, readQuotationCharges } from '../quotation-charges/quotation-charges';
import { QuotationDateFieldComponent } from '../quotation-date-field/quotation-date-field';
import { canonicalCondition, canonicalUnit } from '../quotation-lookups';
import { dateInputValue } from '../quotation-terms';
import { ActivitySuggestion, directActivityId } from '../quotation-map';
import { ActivitySuggestComponent } from '../activity-suggest/activity-suggest';

export interface PurchaseOrderPresetLine {
  activityId: number;
  quotationId?: number | null;
  quotationCode?: string | null;
  dsnNo?: string | null;
  partNo?: string | null;
  description?: string | null;
  condition?: string | null;
  unit?: string | null;
  leadTime?: string | null;
  quantity?: number | null;
  unitPrice?: number | null;
  totalPrice?: number | null;
  isAlternativePart?: boolean;
}

export interface PurchaseOrderDialogData {
  projectId: number;
  activities: Activity[];
  purchaseOrderId?: number;
  supplierId?: number | null;
  lines?: PurchaseOrderPresetLine[];
}

interface PoLine {
  key: string;
  activityId: number | null;
  quotationId: number | null;
  quotationCode: string;
  fromQuotation: boolean;
  manual: boolean;
  dsnNo: string;
  partNo: string;
  description: string;
  condition: string;
  unit: string;
  leadTime: string;
  quantity: string;
  unitPrice: string;
  totalPrice: string;
  edited: PoEdits;
}

interface PoEdits {
  description: boolean;
  quantity: boolean;
  condition: boolean;
  unit: boolean;
  leadTime: boolean;
  dsn: boolean;
}

@Component({
  selector: 'app-purchase-order-dialog',
  standalone: true,
  imports: [
    FormsModule,
    TextFieldModule,
    MaterialModule,
    QuotationDateFieldComponent,
    QuotationChargesComponent,
    DriveUploadCardComponent,
    LookupComboboxComponent,
    ActivitySuggestComponent,
  ],
  templateUrl: './purchase-order-dialog.html',
  styleUrl: './purchase-order-dialog.css',
})
export class PurchaseOrderDialogComponent implements OnInit {
  private api = inject(ProjectApiService);
  private dialogRef = inject(MatDialogRef<PurchaseOrderDialogComponent, boolean>);
  readonly data = inject<PurchaseOrderDialogData>(MAT_DIALOG_DATA);

  suppliers: Supplier[] = [];
  currencies: Currency[] = [];
  conditions: LookupValue[] = [];
  units: LookupValue[] = [];
  statuses: LookupValue[] = [];
  statusCode = 'Draft';
  originalStatus = 'Draft';
  history: PurchaseOrderHistoryEntry[] = [];
  conditionOptions: string[] = [];
  unitOptions: string[] = [];
  readonly documentAccept = '.pdf,.doc,.docx,.xls,.xlsx,.xlsm,.csv,.png,.jpg,.jpeg,.gif,.webp,.bmp,.tif,.tiff';
  documents: PurchaseOrderDocument[] = [];
  storedCleared = false;
  links: ActivityQuotationLink[] = [];
  private applying = false;
  rows: PoLine[] = [];
  charges: QuotationChargeRow[] = [];
  code = '';
  supplierId: number | null = null;
  supplierQuery = '';
  supplierOpen = false;
  @ViewChild('supplierInput') supplierInput?: ElementRef<HTMLInputElement>;
  currencyId: number | null = null;
  poDate = todayIso();
  shipToAddress = '';
  file: File | null = null;
  search = '';
  loading = true;
  saving = false;
  submitted = false;
  error = '';

  ngOnInit() {
    const id = this.data.purchaseOrderId;
    forkJoin({
      suppliers: this.api.suppliers(),
      currencies: this.api.currencies(),
      units: this.api.units(),
      conditions: this.api.conditions(),
      statuses: this.api.purchaseOrderStatuses(),
      detail: id ? this.api.purchaseOrder(id) : of(null),
      documents: id ? this.api.purchaseOrderDocuments(this.data.projectId, id) : of([]),
      links: this.api.quotationLinks(this.data.projectId).pipe(catchError(() => of([] as ActivityQuotationLink[]))),
    }).subscribe({
      next: (result) => {
        this.suppliers = result.suppliers;
        this.currencies = result.currencies;
        this.conditions = result.conditions;
        this.units = result.units;
        this.statuses = result.statuses ?? [];
        this.conditionOptions = names(result.conditions);
        this.unitOptions = names(result.units);
        this.documents = result.documents;
        this.links = result.links ?? [];
        const fallbackCurrency = result.currencies.find((currency) => currency.isDefault)?.id ?? result.currencies[0]?.id ?? null;
        if (result.detail) {
          this.code = result.detail.code;
          this.supplierId = result.detail.supplierId;
          this.supplierQuery = result.suppliers.find((supplier) => supplier.id === result.detail?.supplierId)?.name ?? '';
          this.currencyId = result.detail.currencyId || fallbackCurrency;
          this.poDate = dateInputValue(result.detail.poDate);
          this.shipToAddress = result.detail.shipToAddress ?? '';
          this.statusCode = result.detail.statusCode || 'Draft';
          this.originalStatus = this.statusCode;
          this.history = result.detail.history ?? [];
          this.rows = result.detail.items.map((item) => this.lineFromDetail(item));
          this.charges = (result.detail.charges ?? []).map((charge) => ({
            key: crypto.randomUUID(),
            name: charge.name,
            amount: charge.amount == null ? '' : String(charge.amount),
          }));
        } else {
          this.supplierId = this.data.supplierId ?? null;
          this.supplierQuery = result.suppliers.find((supplier) => supplier.id === this.supplierId)?.name ?? '';
          this.currencyId = fallbackCurrency;
          this.rows = this.data.lines?.length
            ? this.data.lines.map((line) => this.lineFromPreset(line))
            : this.manualRows();
        }
        this.loading = false;
      },
      error: () => {
        this.loading = false;
        this.error = 'Purchase order details could not be loaded.';
      },
    });
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

  clearSupplier() {
    if (this.loading || this.saving) {
      return;
    }
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

  get title(): string {
    return this.data.purchaseOrderId ? 'Edit purchase order' : 'New purchase order';
  }

  get storedFiles(): StoredDriveFile[] {
    if (this.storedCleared || this.file) {
      return [];
    }
    return this.documents
      .filter((document) => !!document.webViewLink)
      .map((document) => ({
        fileName: document.fileName,
        fileType: document.fileType,
        webViewLink: document.webViewLink,
      }));
  }

  get symbol(): string {
    return this.currencies.find((currency) => currency.id === this.currencyId)?.symbol || '$';
  }

  get currencyCode(): string {
    return this.currencies.find((currency) => currency.id === this.currencyId)?.code || 'USD';
  }

  get subtotal(): number {
    return roundMoney(this.rows.reduce((sum, row) => sum + (lineAmount(row) ?? 0), 0));
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

  get visibleRows(): PoLine[] {
    const term = this.search.trim().toLowerCase();
    if (!term) {
      return this.rows;
    }
    return this.rows.filter((row) =>
      [row.dsnNo, row.partNo, row.description, row.quotationCode].some((value) => value.toLowerCase().includes(term)),
    );
  }

  onAttachment(file: File | null) {
    this.file = file;
  }

  clearStoredAttachment() {
    if (!this.saving) {
      this.file = null;
      this.storedCleared = true;
    }
  }

  close() {
    if (!this.saving) {
      this.dialogRef.close(false);
    }
  }

  addLine() {
    this.rows = [...this.rows, this.blankLine()];
    this.search = '';
  }

  removeLine(row: PoLine) {
    if (this.saving) {
      return;
    }
    this.rows = this.rows.filter((item) => item.key !== row.key);
  }

  normalizeCondition(row: PoLine) {
    row.condition = canonicalCondition(row.condition, this.conditions);
  }

  normalizeUnit(row: PoLine) {
    row.unit = canonicalUnit(row.unit, this.units);
  }

  setDsn(row: PoLine, value: string) {
    row.dsnNo = value;
    this.onDsn(row);
  }

  setPart(row: PoLine, value: string) {
    row.partNo = value;
    this.onPart(row);
  }

  chooseActivity(row: PoLine, suggestion: ActivitySuggestion) {
    const activity = (this.data.activities ?? []).find((item) => item.id === suggestion.activityId);
    this.applying = true;
    row.activityId = suggestion.activityId;
    row.dsnNo = suggestion.dsnNo;
    row.partNo = suggestion.partNo;
    row.edited = blankEdits();
    if (activity) {
      const details = this.activityDetails(activity);
      row.description = details.description;
      row.quantity = activityQuantity(activity) || '1';
      row.condition = this.whenMissing(canonicalCondition(details.condition, this.conditions), 'NE');
      row.unit = this.whenMissing(canonicalUnit(details.unit, this.units), 'EA');
      row.leadTime = details.leadTime;
      if (row.unitPrice.trim()) {
        this.syncFromUnit(row);
      }
    }
    this.applying = false;
  }

  onDsn(row: PoLine) {
    this.mark(row, 'dsn');
    this.onIdentity(row);
  }

  onPart(row: PoLine) {
    this.onIdentity(row);
  }

  onDescription(row: PoLine) {
    this.mark(row, 'description');
  }

  onQuantity(row: PoLine) {
    this.mark(row, 'quantity');
    this.syncFromUnit(row);
  }

  onLead(row: PoLine) {
    this.mark(row, 'leadTime');
  }

  onCondition(row: PoLine, value: string) {
    row.condition = value;
    this.mark(row, 'condition');
  }

  onUnit(row: PoLine, value: string) {
    row.unit = value;
    this.mark(row, 'unit');
  }

  onIdentity(row: PoLine) {
    if (row.fromQuotation) {
      return;
    }
    const part = row.partNo;
    const nextId = directActivityId(row.dsnNo, row.partNo, this.data.activities ?? []);
    row.partNo = part;
    row.activityId = nextId;
    const activity = nextId ? (this.data.activities ?? []).find((item) => item.id === nextId) : undefined;
    if (!activity) {
      return;
    }
    const details = this.activityDetails(activity);
    this.applying = true;
    this.fill(row, 'description', details.description);
    this.fill(row, 'quantity', activityQuantity(activity) || '1');
    this.fill(row, 'condition', this.whenMissing(canonicalCondition(details.condition, this.conditions), 'NE'));
    this.fill(row, 'unit', this.whenMissing(canonicalUnit(details.unit, this.units), 'EA'));
    this.fill(row, 'leadTime', details.leadTime);
    if (!row.edited.dsn && !row.dsnNo.trim() && activity.dsnNo?.trim()) {
      row.dsnNo = activity.dsnNo.trim();
    }
    this.applying = false;
  }

  invalid(row: PoLine, field: 'dsn' | 'part' | 'qty' | 'price'): boolean {
    if (!this.submitted) {
      return false;
    }
    const problem = lineProblem(row);
    if (problem === 'skip' || problem == null) {
      return false;
    }
    if (field === 'price') {
      return problem === 'price';
    }
    return problem === field;
  }

  private mark(row: PoLine, field: keyof PoEdits) {
    if (!this.applying) {
      row.edited[field] = true;
    }
  }

  private whenMissing(value: string, fallback: string): string {
    return value.trim() || fallback;
  }

  private fill(row: PoLine, field: keyof PoEdits, value: string) {
    const next = value.trim();
    if (row.edited[field] || !next) {
      return;
    }
    if (field === 'description') {
      row.description = next;
    } else if (field === 'quantity') {
      row.quantity = next;
    } else if (field === 'condition') {
      row.condition = next;
    } else if (field === 'unit') {
      row.unit = next;
    } else if (field === 'leadTime') {
      row.leadTime = next;
    }
  }

  private activityDetails(activity: Activity): { description: string; condition: string; unit: string; leadTime: string } {
    const links = this.links.filter((link) => link.activityId === activity.id);
    return {
      description: (activity.description ?? '').trim() || agreed(links, (link) => link.description),
      condition: agreed(links, (link) => link.condition),
      unit: agreed(links, (link) => link.unit),
      leadTime: agreed(links, (link) => link.leadTime),
    };
  }

  syncFromUnit(row: PoLine) {
    const quantity = numeric(row.quantity);
    const unitPrice = numeric(row.unitPrice);
    if (quantity != null && quantity > 0 && unitPrice != null) {
      row.totalPrice = (quantity * unitPrice).toFixed(2);
    }
  }

  syncFromTotal(row: PoLine) {
    const quantity = numeric(row.quantity);
    const total = numeric(row.totalPrice);
    if (quantity != null && quantity > 0 && total != null) {
      row.unitPrice = (total / quantity).toFixed(2);
    }
  }

  get willRevise(): boolean {
    return !!this.data.purchaseOrderId && this.originalStatus === 'Sent' && this.statusCode === 'Sent';
  }

  historyLabel(action: string): string {
    switch (action) {
      case 'StatusChanged':
        return 'Status changed';
      case 'Created':
        return 'Created';
      case 'Edited':
        return 'Edited';
      case 'Revised':
        return 'Revised';
      case 'Deleted':
        return 'Deleted';
      default:
        return action || 'Updated';
    }
  }

  historyActor(entry: PurchaseOrderHistoryEntry): string {
    const name = entry.actorName?.trim();
    const email = entry.actorEmail?.trim();
    if (name && email) {
      return `${name} · ${email}`;
    }
    return name || email || 'Unknown user';
  }

  whenStamp(value?: string | null): string {
    if (!value) {
      return '';
    }
    const date = new Date(value);
    if (Number.isNaN(date.getTime())) {
      return '';
    }
    return date.toLocaleString(undefined, { year: 'numeric', month: 'short', day: 'numeric', hour: 'numeric', minute: '2-digit' });
  }

  save() {
    if (this.saving) {
      return;
    }
    const code = this.code.trim();
    this.code = code;
    if (!code) {
      this.error = 'Enter a PO number.';
      return;
    }
    if (!this.supplierId) {
      this.error = 'Choose a supplier.';
      return;
    }
    this.submitted = true;
    const items: PurchaseOrderItemInput[] = [];
    for (const row of this.rows) {
      const problem = lineProblem(row);
      if (problem === 'skip') {
        continue;
      }
      if (problem) {
        this.error = lineMessage(problem);
        return;
      }
      if (!row.quantity.trim()) {
        row.quantity = '1';
      }
      const quantity = numeric(row.quantity) as number;
      const unitPrice = numeric(row.unitPrice) as number;
      const totalPrice = numeric(row.totalPrice) ?? unitPrice * quantity;
      items.push({
        activityId: row.activityId,
        quotationId: row.activityId ? row.quotationId : null,
        dsnNo: row.dsnNo.trim(),
        partNo: row.partNo.trim(),
        description: row.description.trim(),
        condition: textOrNull(canonicalCondition(row.condition, this.conditions)),
        unit: textOrNull(canonicalUnit(row.unit, this.units)),
        leadTime: row.leadTime,
        quantity,
        unitPrice: roundMoney(unitPrice as number),
        totalPrice: roundMoney(totalPrice as number),
      });
    }
    if (!items.length) {
      this.error = 'Add at least one purchase order line with a unit price.';
      return;
    }
    const charges = readQuotationCharges(this.charges);
    if (charges.error) {
      this.error = charges.error;
      return;
    }
    const body = {
      code,
      supplierId: this.supplierId,
      poDate: isoDate(this.poDate),
      shipToAddress: this.shipToAddress.trim(),
      currencyId: this.currencyId,
      status: this.statusCode,
      projectIds: [this.data.projectId],
      items,
      charges: charges.charges,
    };
    this.saving = true;
    this.error = '';
    const file = this.file;
    const clearStored = this.storedCleared && !file;
    const currentFile = this.documents[0];
    let orderSaved = false;
    const request = file
      ? this.data.purchaseOrderId
        ? this.api.savePurchaseOrder(this.data.projectId, this.data.purchaseOrderId, body, file)
        : this.api.importPurchaseOrder(this.data.projectId, body, file)
      : this.data.purchaseOrderId
        ? this.api.updatePurchaseOrder(this.data.purchaseOrderId, body)
        : this.api.createPurchaseOrder(body);
    request.pipe(
      switchMap((saved) => {
        orderSaved = true;
        const id = this.data.purchaseOrderId || saved?.id;
        if (clearStored && id && currentFile) {
          return this.api.removePurchaseOrderDocument(this.data.projectId, id, currentFile.documentId);
        }
        return of(saved);
      }),
    ).subscribe({
      next: () => {
        this.saving = false;
        this.dialogRef.close(true);
      },
      error: (error: HttpErrorResponse) => {
        this.saving = false;
        if (orderSaved) {
          this.storedCleared = false;
          this.error = error.error?.message || 'The purchase order was saved, but the file could not be updated.';
          return;
        }
        this.error = error.error?.message || 'The purchase order could not be saved.';
      },
    });
  }

  private manualRows(): PoLine[] {
    const activities = this.data.activities ?? [];
    if (activities.length > 10) {
      return Array.from({ length: 5 }, () => this.blankLine());
    }
    return activities.map((activity) => this.lineFromActivity(activity));
  }

  private lineFromActivity(activity: Activity): PoLine {
    const details = this.activityDetails(activity);
    return {
      key: crypto.randomUUID(),
      activityId: activity.id,
      quotationId: null,
      quotationCode: '',
      fromQuotation: false,
      manual: false,
      edited: blankEdits(),
      dsnNo: (activity.dsnNo ?? '').trim(),
      partNo: (activity.mainPartNo || activity.partNo || '').trim(),
      description: (activity.description ?? '').trim(),
      condition: this.whenMissing(canonicalCondition(details.condition, this.conditions), 'NE'),
      unit: this.whenMissing(canonicalUnit(details.unit, this.units), 'EA'),
      leadTime: '',
      quantity: activityQuantity(activity) || '1',
      unitPrice: '',
      totalPrice: '',
    };
  }

  private lineFromPreset(line: PurchaseOrderPresetLine): PoLine {
    const quantity = line.quantity && line.quantity > 0 ? String(line.quantity) : '1';
    const unitPrice = line.unitPrice == null ? '' : String(line.unitPrice);
    const quantityValue = numeric(quantity);
    const unitValue = numeric(unitPrice);
    const totalPrice = quantityValue != null && quantityValue > 0 && unitValue != null
      ? (quantityValue * unitValue).toFixed(2)
      : line.totalPrice == null ? '' : String(line.totalPrice);
    return {
      key: crypto.randomUUID(),
      activityId: line.activityId,
      quotationId: line.quotationId ?? null,
      quotationCode: line.quotationCode ?? '',
      fromQuotation: true,
      manual: false,
      edited: blankEdits(),
      dsnNo: line.dsnNo ?? '',
      partNo: quotedPart(line, this.data.activities ?? []),
      description: line.description ?? '',
      condition: this.whenMissing(canonicalCondition(line.condition ?? '', this.conditions), 'NE'),
      unit: this.whenMissing(canonicalUnit(line.unit ?? '', this.units), 'EA'),
      leadTime: line.leadTime ?? '',
      quantity,
      unitPrice,
      totalPrice,
    };
  }

  private lineFromDetail(line: PurchaseOrderLine): PoLine {
    const fromQuotation = line.quotationId != null;
    const activityId = line.activityId && line.activityId > 0 ? line.activityId : null;
    return {
      key: crypto.randomUUID(),
      activityId,
      quotationId: line.quotationId ?? null,
      quotationCode: line.quotationCode ?? '',
      fromQuotation,
      manual: !fromQuotation && !activityId,
      edited: blankEdits(),
      dsnNo: line.dsnNo ?? '',
      partNo: line.partNo ?? '',
      description: line.description ?? '',
      condition: canonicalCondition(line.condition ?? '', this.conditions),
      unit: canonicalUnit(line.unit ?? '', this.units),
      leadTime: line.leadTime ?? '',
      quantity: line.quantity == null ? '1' : String(line.quantity),
      unitPrice: String(line.unitPrice ?? ''),
      totalPrice: String(line.totalPrice ?? ''),
    };
  }

  private blankLine(): PoLine {
    return {
      key: crypto.randomUUID(),
      activityId: null,
      quotationId: null,
      quotationCode: '',
      fromQuotation: false,
      manual: true,
      edited: blankEdits(),
      dsnNo: '',
      partNo: '',
      description: '',
      condition: 'NE',
      unit: 'EA',
      leadTime: '',
      quantity: '1',
      unitPrice: '',
      totalPrice: '',
    };
  }
}

function quotedPart(line: PurchaseOrderPresetLine, activities: Activity[]): string {
  const stored = (line.partNo ?? '').trim();
  if (!line.isAlternativePart) {
    return stored;
  }
  const activity = activities.find((item) => item.id === line.activityId);
  const alternatives = (activity?.alternativePartNos ?? [])
    .map((value) => (value ?? '').trim())
    .filter((value) => value.length > 0);
  const exact = alternatives.find((value) => value.toLowerCase() === stored.toLowerCase());
  if (exact) {
    return exact;
  }
  const main = (activity?.mainPartNo ?? '').trim().toLowerCase();
  if (stored && stored.toLowerCase() !== main) {
    return stored;
  }
  return alternatives.length === 1 ? alternatives[0] : stored;
}

function blankEdits(): PoEdits {
  return { description: false, quantity: false, condition: false, unit: false, leadTime: false, dsn: false };
}

function agreed(links: ActivityQuotationLink[], pick: (link: ActivityQuotationLink) => string | null | undefined): string {
  const values: string[] = [];
  for (const link of links) {
    const value = (pick(link) ?? '').trim();
    if (value && !values.some((item) => item.toLowerCase() === value.toLowerCase())) {
      values.push(value);
    }
  }
  return values.length === 1 ? values[0] : '';
}

function lineAmount(row: PoLine): number | null {
  if (skippable(row)) {
    return null;
  }
  const unitPrice = numeric(row.unitPrice);
  const quantity = row.quantity.trim() ? numeric(row.quantity) : 1;
  const totalPrice = numeric(row.totalPrice);
  if (unitPrice == null || quantity == null || quantity <= 0 || (row.totalPrice.trim() && totalPrice == null)) {
    return null;
  }
  return roundMoney(row.totalPrice.trim() && totalPrice != null ? totalPrice : unitPrice * quantity);
}

function lineProblem(row: PoLine): 'dsn' | 'part' | 'qty' | 'price' | 'skip' | null {
  if (skippable(row)) {
    return 'skip';
  }
  if (!row.fromQuotation && !row.activityId) {
    if (!row.dsnNo.trim()) {
      return 'dsn';
    }
    if (!row.partNo.trim()) {
      return 'part';
    }
  }
  const quantity = numeric(row.quantity);
  const unitPrice = numeric(row.unitPrice);
  const totalPrice = numeric(row.totalPrice);
  if (row.quantity.trim() && (quantity == null || quantity <= 0)) {
    return 'qty';
  }
  if (unitPrice == null || (row.totalPrice.trim() && totalPrice == null)) {
    return 'price';
  }
  return null;
}

function lineMessage(problem: 'dsn' | 'part' | 'qty' | 'price'): string {
  if (problem === 'dsn') {
    return 'Enter a DSN No for each line that is not linked to an activity.';
  }
  if (problem === 'part') {
    return 'Enter a Part No for each line that is not linked to an activity.';
  }
  if (problem === 'qty') {
    return 'Each purchase order line needs a quantity greater than zero.';
  }
  return 'Enter a valid unit price of zero or more.';
}

function skippable(row: PoLine): boolean {
  return !row.unitPrice.trim();
}

function activityQuantity(activity: Activity): string {
  const value = Number(activity.quantity);
  if (!Number.isFinite(value) || value <= 0) {
    return '';
  }
  const rounded = Math.round(value * 100) / 100;
  return Number.isInteger(rounded) ? String(rounded) : String(rounded);
}

function textOrNull(value: string): string | null {
  const text = value.trim();
  return text || null;
}

function roundMoney(value: number): number {
  return Math.round(value * 100) / 100;
}

function names(values: LookupValue[]): string[] {
  return values.map((value) => value.name).filter((name) => !!name);
}

function numeric(value: string): number | null {
  const text = value.trim().replace(/[$,\s]/g, '');
  if (!text || /[a-z]/i.test(text)) {
    return null;
  }
  const parsed = Number(text);
  return Number.isFinite(parsed) && parsed >= 0 ? parsed : null;
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
