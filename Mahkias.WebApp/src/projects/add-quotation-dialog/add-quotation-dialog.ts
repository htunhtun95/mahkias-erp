import { AfterViewInit, Component, ElementRef, OnDestroy, QueryList, ViewChild, ViewChildren, inject } from '@angular/core';
import { TextFieldModule } from '@angular/cdk/text-field';
import { FormsModule, NgModel } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { MAT_DIALOG_DATA, MatDialog, MatDialogRef } from '@angular/material/dialog';
import { Observable, of, switchMap, tap } from 'rxjs';
import { MaterialModule } from '../../app/material-module';
import { DocumentManagerComponent, syncDocumentDrafts } from '../document-manager/document-manager';
import { documentAccept as acceptedDocuments } from '../document-library-dialog/document-library-dialog';
import { blockEnterSubmit } from '../../core/block-enter-submit';
import { closeOnEscape } from '../../core/close-on-escape';
import { leadTimeDays, leadTimeInvalid, leadTimeText, LeadTimeDaysDirective } from '../lead-time';
import { WholeQuantityDirective } from '../whole-quantity';
import { SheetCellFocusDirective } from '../sheet-cell-focus';
import { whileBusy } from '../../core/while-busy';
import {
  Activity,
  ActivityImportIssue,
  ActivityLabel,
  Currency,
  ProjectApiService,
  QuotationDetail,
  QuotationDocument,
  QuotationItemInput,
  QuotationLine,
  Supplier,
} from '../../core/services/project-api.service';
import { ActivitySuggestion, directActivityId } from '../quotation-map';
import { duplicateSupplierReferenceMessage, hasDuplicateSupplierReference } from '../../suppliers/supplier-reference';
import { activityHasLabel } from '../project/tabs/project-activities-tab/activity-structure';
import { QuotationDateFieldComponent } from '../quotation-date-field/quotation-date-field';
import { dateInputValue, pricedQuantity, quotationQuantity } from '../quotation-terms';
import { LookupComboboxComponent } from '../lookup-combobox/lookup-combobox';
import { LookupValue, canonicalCondition, canonicalUnit } from '../quotation-lookups';
import { QuotationChargeRow, QuotationChargesComponent, readQuotationCharges } from '../quotation-charges/quotation-charges';
import { ActivitySuggestComponent } from '../activity-suggest/activity-suggest';
import { QuotationItemDetails, QuotationItemDetailsDialog } from './quotation-item-details-dialog';

interface QuoteRow {
  key: string;
  manual: boolean;
  activityId: number | null;
  dsnNo: string;
  partNo: string;
  description: string;
  condition: string;
  serialNo: string;
  tsn: string;
  tso: string;
  csn: string;
  cso: string;
  usageOwned: boolean;
  usageEdited: {
    serialNo: boolean;
    tsn: boolean;
    tso: boolean;
    csn: boolean;
    cso: boolean;
  };
  unitCode: string;
  leadTime: string;
  minimumQty: string;
  unitPrice: string;
  quantity: string;
  totalPrice: string;
  currencyId: number | null;
  currencyExplicit: boolean;
  typeId: number | null;
  typeName: string;
  unitExplicit: boolean;
  totalExplicit: boolean;
}

@Component({
  selector: 'app-create-quotation-dialog',
  standalone: true,
  imports: [FormsModule, MaterialModule, TextFieldModule, QuotationDateFieldComponent, LookupComboboxComponent, QuotationChargesComponent, SheetCellFocusDirective, DocumentManagerComponent, LeadTimeDaysDirective, WholeQuantityDirective, ActivitySuggestComponent],
  templateUrl: './add-quotation-dialog.html',
  styleUrl: './add-quotation-dialog.css',
})
export class CreateQuotationDialogComponent implements AfterViewInit, OnDestroy {
  @ViewChild('supplierInput') private supplierInput?: ElementRef<HTMLInputElement>;
  @ViewChild('supplierField') private supplierField?: NgModel;
  @ViewChild('newSupplierField') private newSupplierField?: NgModel;
  @ViewChildren(QuotationDateFieldComponent) private dateFields?: QueryList<QuotationDateFieldComponent>;
  @ViewChild('library') private library?: DocumentManagerComponent;
  @ViewChild('quoteSheet') private quoteSheet?: ElementRef<HTMLTableElement>;

  private api = inject(ProjectApiService);
  private dialog = inject(MatDialog);
  private dialogRef = inject(MatDialogRef<CreateQuotationDialogComponent>);
  data = inject<{ projectId: number; activities: Activity[]; quotation?: QuotationDetail }>(MAT_DIALOG_DATA);
  quotationId = this.data.quotation?.id ?? null;
  readonly creating = this.quotationId == null;
  savedDocuments: QuotationDocument[] = [];
  readonly documentAccept = acceptedDocuments;
  quotationCode = this.data.quotation?.code ?? '';
  rowFilter = '';
  typeFilter: number | null = null;
  labelId: number | null = null;
  private labels: ActivityLabel[] = [];
  currencies: Currency[] = [];
  units: LookupValue[] = [];
  conditions: LookupValue[] = [];
  currencyId: number | null = this.data.quotation?.currencyId ?? null;

  supplierQuotationRef = this.data.quotation?.supplierReference ?? '';
  quotedDate = dateInputValue(this.data.quotation?.quotedDate);
  validUntil = dateInputValue(this.data.quotation?.validUntil);
  supplierQuery = this.data.quotation?.supplierName ?? '';
  supplierId: number | null = this.data.quotation?.supplierId ?? null;
  charges: QuotationChargeRow[] = (this.data.quotation?.charges ?? []).map((charge) => ({
    key: crypto.randomUUID(),
    name: charge.name ?? '',
    amount: charge.amount == null ? '' : String(charge.amount),
  }));
  suppliers: Supplier[] = [];
  supplierOpen = false;
  creatingSupplier = false;
  newSupplierName = '';
  newSupplierReference = '';
  referenceError = '';
  rows: QuoteRow[] = this.creating ? [] : this.initialRows();
  preparing = this.creating;
  submitted = false;
  isSaving = false;
  error = '';
  private prepareTimer?: ReturnType<typeof setTimeout>;
  private readonly minColumnWidth = 48;
  columnsResized = false;
  resizedWidths: number[] = [];
  private resizeDrag: { index: number; startX: number; startWidth: number } | null = null;

  ngAfterViewInit() {
    if (!this.preparing) {
      return;
    }
    this.prepareTimer = setTimeout(() => {
      this.prepareTimer = undefined;
      this.rows = this.initialRows();
      this.syncRowLookups();
      this.preparing = false;
    });
  }

  ngOnDestroy() {
    clearTimeout(this.prepareTimer);
    this.endColumnResize();
  }

  resizedTableWidth(): number | null {
    if (!this.columnsResized) {
      return null;
    }
    return this.resizedWidths.reduce((sum, width) => sum + width, 0);
  }

  createColumnWidth(index: number): number | null {
    return this.columnsResized ? this.resizedWidths[index] : null;
  }

  startColumnResize(event: MouseEvent, index: number) {
    if (event.button !== 0) {
      return;
    }
    event.preventDefault();
    event.stopPropagation();
    const headers = this.quoteSheet?.nativeElement.querySelectorAll('thead th');
    if (!headers?.length) {
      return;
    }
    if (!this.columnsResized) {
      this.resizedWidths = [...headers].map((header) => Math.round(header.getBoundingClientRect().width));
      this.columnsResized = true;
    }
    this.resizeDrag = {
      index,
      startX: event.clientX,
      startWidth: this.resizedWidths[index] ?? this.minColumnWidth,
    };
    document.body.style.cursor = 'col-resize';
    document.body.style.userSelect = 'none';
    document.addEventListener('mousemove', this.onColumnResizeMove);
    document.addEventListener('mouseup', this.endColumnResize);
  }

  private onColumnResizeMove = (event: MouseEvent) => {
    if (!this.resizeDrag) {
      return;
    }
    const next = Math.max(this.minColumnWidth, Math.round(this.resizeDrag.startWidth + event.clientX - this.resizeDrag.startX));
    this.resizedWidths[this.resizeDrag.index] = next;
  };

  private endColumnResize = () => {
    if (!this.resizeDrag) {
      return;
    }
    this.resizeDrag = null;
    document.body.style.cursor = '';
    document.body.style.userSelect = '';
    document.removeEventListener('mousemove', this.onColumnResizeMove);
    document.removeEventListener('mouseup', this.endColumnResize);
  };

  constructor() {
    closeOnEscape(this.dialogRef, () => this.cancel());
    this.api.suppliers().subscribe({
      next: (suppliers) => {
        this.suppliers = suppliers;
      },
    });
    this.api.units().subscribe({
      next: (units) => {
        this.units = units ?? [];
        for (const row of this.rows) {
          if (row.unitCode.trim()) {
            row.unitCode = canonicalUnit(row.unitCode, this.units);
          }
        }
      },
    });
    this.api.conditions().subscribe({
      next: (conditions) => {
        this.conditions = conditions ?? [];
        for (const row of this.rows) {
          if (row.condition.trim()) {
            row.condition = canonicalCondition(row.condition, this.conditions);
          }
        }
      },
    });
    this.api.currencies().subscribe({
      next: (currencies) => {
        this.currencies = currencies;
        const base = currencies.find((currency) => currency.isDefault) ?? currencies.find((currency) => currency.code === 'USD');
        this.currencyId = base?.id ?? this.currencyId;
        for (const row of this.rows) {
          if (row.currencyId == null) {
            row.currencyId = this.currencyId;
          }
        }
      },
    });
    this.api.activityLabels(this.data.projectId).subscribe({
      next: (labels) => {
        this.labels = labels ?? [];
        if (this.labelId != null && !this.quoteLabels.some((label) => label.id === this.labelId)) {
          this.labelId = null;
        }
      },
    });
    if (this.quotationId) {
      this.loadSavedDocuments();
    }
  }

  get activities(): Activity[] {
    return this.data.activities ?? [];
  }

  get quoteLabels(): ActivityLabel[] {
    return this.labels
      .filter((label) => this.activities.some((activity) => activityHasLabel(activity, label)))
      .sort((left, right) => {
        const leftTime = labelTime(left);
        const rightTime = labelTime(right);
        if (leftTime !== rightTime) {
          return leftTime - rightTime;
        }
        return left.id - right.id;
      });
  }

  selectLabel(id: number | null) {
    this.labelId = id;
  }

  private labelCountKey = Number.NaN;
  private labelCountAll = 0;
  private labelCountById = new Map<number, number>();
  private visibleKey = Number.NaN;
  private visibleCache: QuoteRow[] = [];
  private indexedActivities: Activity[] | null = null;
  private activityById = new Map<number, Activity>();

  labelCount(label: ActivityLabel | null): number {
    const key = this.rowLinkKey();
    if (key !== this.labelCountKey) {
      this.labelCountKey = key;
      this.recountLabels();
    }
    return label ? (this.labelCountById.get(label.id) ?? 0) : this.labelCountAll;
  }

  get visibleRows(): QuoteRow[] {
    const key = this.visibleRowsKey();
    if (key === this.visibleKey) {
      return this.visibleCache;
    }
    this.visibleKey = key;
    const query = this.rowFilter.trim().toLowerCase();
    const label = this.labels.find((item) => item.id === this.labelId) ?? null;
    const typeId = this.typeFilter;
    this.visibleCache = this.rows.filter((row) => {
      const matchesQuery = !query || row.dsnNo.toLowerCase().includes(query) || row.partNo.toLowerCase().includes(query);
      const matchesType = typeId == null || row.typeId === typeId;
      return matchesQuery && matchesType && this.matchesLabel(row, label);
    });
    return this.visibleCache;
  }

  private rowLinkKey(): number {
    let hash = this.rows.length;
    for (const row of this.rows) {
      hash = Math.imul(hash, 33) ^ (row.activityId ?? 0);
    }
    for (const label of this.labels) {
      hash = Math.imul(hash, 33) ^ label.id ^ label.activityIds.length;
    }
    return hash;
  }

  private visibleRowsKey(): number {
    let hash = (this.labelId ?? 0) ^ (this.typeFilter ?? 0) ^ this.rowLinkKey();
    const query = this.rowFilter;
    for (let index = 0; index < query.length; index++) {
      hash = Math.imul(hash, 33) ^ query.charCodeAt(index);
    }
    for (const row of this.rows) {
      hash = Math.imul(hash, 33) ^ row.dsnNo.length ^ row.partNo.length;
      for (let index = 0; index < row.dsnNo.length; index++) {
        hash = (hash + row.dsnNo.charCodeAt(index)) | 0;
      }
      for (let index = 0; index < row.partNo.length; index++) {
        hash = (hash + row.partNo.charCodeAt(index)) | 0;
      }
    }
    return hash;
  }

  private recountLabels() {
    const counts = new Map<number, number>();
    let all = 0;
    for (const row of this.rows) {
      all += 1;
      for (const label of this.labels) {
        if (this.matchesLabel(row, label)) {
          counts.set(label.id, (counts.get(label.id) ?? 0) + 1);
        }
      }
    }
    this.labelCountAll = all;
    this.labelCountById = counts;
  }

  onDsn(row: QuoteRow) {
    this.linkRow(row);
  }

  addItems() {
    if (this.isSaving || this.creating) {
      return;
    }
    this.rowFilter = '';
    this.typeFilter = null;
    this.labelId = null;
    this.rows = [...this.rows, ...Array.from({ length: 5 }, () => this.blankRow())];
  }

  setDsn(row: QuoteRow, value: string) {
    row.dsnNo = value;
    this.onDsn(row);
  }

  setPart(row: QuoteRow, value: string) {
    row.partNo = value;
    this.onPart(row);
  }

  chooseActivity(row: QuoteRow, suggestion: ActivitySuggestion) {
    const activity = this.activityFor(suggestion.activityId);
    row.activityId = suggestion.activityId;
    row.dsnNo = suggestion.dsnNo;
    row.partNo = suggestion.partNo;
    row.typeId = activity?.typeId ?? null;
    row.typeName = row.typeId == null ? '' : (activity?.type?.trim() || 'Type');
    if (!activity) {
      return;
    }
    row.description = (activity.description ?? '').trim();
    row.condition = this.lookupOrDefault(activity.condition, 'condition');
    row.unitCode = this.lookupOrDefault(activity.unit, 'unit');
    row.quantity = this.activityQuantity(activity);
    row.usageOwned = false;
    if (!row.usageEdited.serialNo) {
      row.serialNo = (activity.serialNo ?? '').trim();
    }
    if (!row.usageEdited.tsn) {
      row.tsn = (activity.tsn ?? '').trim();
    }
    if (!row.usageEdited.tso) {
      row.tso = (activity.tso ?? '').trim();
    }
    if (!row.usageEdited.csn) {
      row.csn = (activity.csn ?? '').trim();
    }
    if (!row.usageEdited.cso) {
      row.cso = (activity.cso ?? '').trim();
    }
    this.recalculate(row);
  }

  openDetails(row: QuoteRow) {
    if (this.isSaving) {
      return;
    }
    this.dialog.open(QuotationItemDetailsDialog, {
      disableClose: false,
      width: '680px',
      maxWidth: '92vw',
      autoFocus: 'first-tabbable',
      panelClass: ['quote-item-details-panel', 'quotation-item-details'],
      backdropClass: 'add-project-dialog-backdrop',
      data: {
        dsnNo: row.dsnNo,
        partNo: row.partNo,
        minimumQty: row.minimumQty,
        serialNo: row.serialNo,
        tsn: row.tsn,
        tso: row.tso,
        csn: row.csn,
        cso: row.cso,
      } satisfies QuotationItemDetails,
    }).afterClosed().subscribe((result) => {
      if (!result) {
        return;
      }
      row.minimumQty = result.minimumQty;
      row.serialNo = result.serialNo;
      row.tsn = result.tsn;
      row.tso = result.tso;
      row.csn = result.csn;
      row.cso = result.cso;
      row.usageEdited = { serialNo: true, tsn: true, tso: true, csn: true, cso: true };
      this.onMinimum(row);
    });
  }

  onPart(row: QuoteRow) {
    this.linkRow(row);
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
    this.creatingSupplier = true;
    this.newSupplierName = this.supplierQuery.trim();
    this.newSupplierReference = '';
    this.supplierId = null;
    this.supplierOpen = false;
  }

  cancelNewSupplier() {
    this.creatingSupplier = false;
    this.newSupplierName = '';
    this.newSupplierReference = '';
    this.supplierQuery = '';
    this.supplierId = null;
    this.supplierOpen = false;
  }

  clearSupplier() {
    if (this.isSaving) {
      return;
    }
    this.creatingSupplier = false;
    this.newSupplierName = '';
    this.newSupplierReference = '';
    this.supplierQuery = '';
    this.supplierId = null;
    this.supplierOpen = true;
    setTimeout(() => {
      this.supplierField?.control.markAsDirty();
      this.supplierInput?.nativeElement.focus();
    });
  }

  closeSuppliers() {
    setTimeout(() => {
      if (document.activeElement === this.supplierInput?.nativeElement) {
        return;
      }
      this.supplierOpen = false;
    }, 150);
  }

  onUnit(row: QuoteRow) {
    row.unitExplicit = !!row.unitPrice.trim();
    if (!row.unitExplicit) {
      if (!row.totalExplicit) {
        row.totalPrice = '';
      }
      return;
    }
    this.recalculate(row);
  }

  onTotal(row: QuoteRow) {
    row.totalExplicit = !!row.totalPrice.trim();
    if (!row.totalExplicit) {
      if (!row.unitExplicit) {
        row.unitPrice = '';
      }
      return;
    }
    this.recalculate(row);
  }

  onQuantity(row: QuoteRow) {
    this.recalculate(row);
  }

  onMinimum(row: QuoteRow) {
    this.recalculate(row);
  }

  onQuantityBlur(row: QuoteRow) {
    if (!row.quantity.trim()) {
      row.quantity = '1';
    }
    this.recalculate(row);
  }

  private seen = new Set<string>();

  private fieldStarted(field?: NgModel): boolean {
    return !!field?.touched || !!field?.dirty;
  }

  get supplierErrorVisible(): boolean {
    return !this.supplierId && this.fieldStarted(this.supplierField);
  }

  get newSupplierErrorVisible(): boolean {
    return !this.newSupplierName.trim() && this.fieldStarted(this.newSupplierField);
  }

  touch(row: QuoteRow, field: string) {
    this.seen.add(`${row.key}:${field}`);
  }

  interacted(row: QuoteRow, field: string): boolean {
    return this.seen.has(`${row.key}:${field}`);
  }

  get validationMessage(): string {
    if (this.creatingSupplier && !this.newSupplierName.trim()) {
      return 'Supplier name is required.';
    }
    if (!this.creatingSupplier && !this.supplierId) {
      return 'Choose a supplier.';
    }
    if (this.datesInvalid) {
      return 'Enter a valid date.';
    }
    if (this.dateOrderError) {
      return this.dateOrderError;
    }
    const filled = this.rows.filter((row) => this.isPriced(row));
    if (!filled.length) {
      return 'Enter a unit price or total price for at least one activity.';
    }
    if (filled.some((row) => !row.dsnNo.trim() || !row.partNo.trim() || !this.validQuantity(row.quantity) || !this.validOptionalQuantity(row.minimumQty) || this.priceBroken(row))) {
      return 'DSN number, part number, and quantity are required. Check the highlighted rows.';
    }
    if (filled.some((row) => leadTimeInvalid(row.leadTime))) {
      return 'Lead time must be a whole number of days, or left blank.';
    }
    const charges = readQuotationCharges(this.charges).error;
    if (charges) {
      return charges;
    }
    return '';
  }

  get canSave(): boolean {
    return !this.validationMessage;
  }

  get visibleValidationMessage(): string {
    if (!this.validationMessage) {
      return '';
    }
    if (this.creatingSupplier && !this.newSupplierName.trim()) {
      return this.newSupplierErrorVisible ? this.validationMessage : '';
    }
    if (!this.creatingSupplier && !this.supplierId) {
      return this.supplierErrorVisible ? this.validationMessage : '';
    }
    if (this.datesInvalid || this.dateOrderError || this.seen.size || readQuotationCharges(this.charges).error || this.rows.some((row) => this.isPriced(row) && leadTimeInvalid(row.leadTime))) {
      return this.validationMessage;
    }
    return '';
  }

  invalid(row: QuoteRow, field: 'dsn' | 'part' | 'price' | 'qty'): boolean {
    if (!this.isPriced(row)) {
      return false;
    }
    if (field === 'dsn') {
      return !row.dsnNo.trim();
    }
    if (field === 'part') {
      return !row.partNo.trim();
    }
    if (field === 'qty') {
      return !this.validQuantity(row.quantity);
    }
    if (!row.unitPrice.trim() && !row.totalPrice.trim()) {
      return false;
    }
    if (row.unitPrice.trim() && this.money(row.unitPrice) == null) {
      return true;
    }
    return !!row.totalPrice.trim() && this.money(row.totalPrice) == null;
  }

  leadBroken(row: QuoteRow): boolean {
    return leadTimeInvalid(row.leadTime);
  }

  onRowCurrency(row: QuoteRow) {
    row.currencyExplicit = true;
  }

  currencyMark(currencyId: number | null): string {
    const currency = this.currencies.find((item) => item.id === currencyId);
    return currency?.symbol || currency?.code || '$';
  }

  get chargeSymbol(): string {
    return this.currencyMark(this.currencyId);
  }

  get conditionNames(): string[] {
    return this.conditions.map((condition) => condition.name);
  }

  get unitNames(): string[] {
    return this.units.map((unit) => unit.name);
  }

  normalizeUnit(row: QuoteRow) {
    row.unitCode = canonicalUnit(row.unitCode, this.units);
  }

  normalizeCondition(row: QuoteRow) {
    row.condition = canonicalCondition(row.condition, this.conditions);
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
    this.submitted = true;
    this.error = '';
    const datesInvalid = this.revealInvalidDates();
    if (!this.supplierReady()) {
      return;
    }
    if (datesInvalid) {
      return;
    }
    for (const row of this.rows) {
      if (!row.quantity.trim()) {
        row.quantity = '1';
      }
      this.recalculate(row);
    }
    const filled = this.rows.filter((row) => this.isPriced(row));
    if (filled.length === 0) {
      this.error = 'Enter a unit price or total price for at least one activity.';
      return;
    }
    if (filled.some((row) => !row.dsnNo.trim() || !row.partNo.trim() || !this.validQuantity(row.quantity) || !this.validOptionalQuantity(row.minimumQty) || this.invalid(row, 'price'))) {
      this.error = 'DSN number, part number, and quantity are required. Check the highlighted rows.';
      return;
    }
    if (filled.some((row) => leadTimeInvalid(row.leadTime))) {
      this.error = 'Lead time must be a whole number of days, or left blank.';
      return;
    }
    if (this.dateOrderError) {
      return;
    }
    const charges = readQuotationCharges(this.charges);
    if (charges.error) {
      this.error = charges.error;
      return;
    }

    const items: QuotationItemInput[] = filled.map((row) => ({
      dsnNo: row.dsnNo.trim(),
      partNo: row.partNo.trim(),
      description: row.description.trim(),
      condition: row.condition.trim(),
      serialNo: row.serialNo.trim(),
      tsn: row.tsn.trim(),
      tso: row.tso.trim(),
      csn: row.csn.trim(),
      cso: row.cso.trim(),
      unit: row.unitCode.trim(),
      leadTime: leadTimeDays(row.leadTime),
      minimumQty: this.optionalQuantity(row.minimumQty),
      unitPrice: this.money(row.unitPrice),
      quantity: this.quantity(row),
      totalPrice: this.money(row.totalPrice),
      activityId: row.activityId,
      activityGroupId: null,
      currencyId: row.currencyId ?? this.currencyId,
    }));

    this.dialogRef.disableClose = true;
    let quotationSaved = false;
    this.withSupplier()
      .pipe(
        switchMap(() => {
          const body = {
            supplierId: this.supplierId!,
            supplierReference: this.supplierQuotationRef.trim(),
            currencyId: this.currencyId,
            quotedDate: this.quotedDate || null,
            validUntil: this.validUntil || null,
            items,
            charges: charges.charges,
          };
          const saved = !this.quotationId
            ? this.api.importQuotation(this.data.projectId, body, null)
            : this.api.updateQuotation(this.quotationId, {
              ...body,
              projectIds: [this.data.projectId],
            });
          return saved.pipe(
            switchMap((result) => {
              quotationSaved = true;
              const id = this.quotationId || result?.id;
              if (id) {
                this.quotationId = id;
              }
              const drafts = this.library?.capture() ?? [];
              if (!id || !this.library?.hasChanges) {
                return of(result);
              }
              return syncDocumentDrafts(this.api, 'quotation', this.data.projectId, id, drafts, false);
            }),
          );
        }),
        whileBusy((busy) => (this.isSaving = busy)),
      )
      .subscribe({
        next: () => {
          this.dialogRef.close(true);
        },
        error: (error: HttpErrorResponse) => {
          if (quotationSaved) {
            this.loadSavedDocuments();
            this.error = this.driveError(error) || 'The quotation was saved, but the documents could not be updated.';
            return;
          }
          if (this.creatingSupplier) {
            this.referenceError = error.error?.message || 'The supplier could not be created.';
            this.error = this.referenceError;
            return;
          }
          const issues = (error.error?.errors as ActivityImportIssue[] | undefined) ?? [];
          this.error = issues.length
            ? issues.map((issue) => `Row ${issue.row}: [${issue.field}] ${issue.message}`).join(' ')
            : error.error?.message || 'The quotation could not be saved.';
        },
      });
  }

  private loadSavedDocuments() {
    if (!this.quotationId) {
      return;
    }
    this.api.quotationDocuments(this.data.projectId, this.quotationId).subscribe({
      next: (documents) => {
        this.savedDocuments = documents ?? [];
        this.library?.replace(this.savedDocuments);
      },
    });
  }

  private driveError(error: HttpErrorResponse): string {
    const body = error.error;
    return typeof body === 'string' ? body : body?.message || '';
  }

  get dateOrderError(): string {
    if (this.quotedDate && this.validUntil && this.validUntil < this.quotedDate) {
      return 'Valid until cannot be earlier than the quoted date.';
    }
    return '';
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

  private get datesInvalid(): boolean {
    return this.dateFields?.some((field) => field.invalid) ?? false;
  }

  private priceBroken(row: QuoteRow): boolean {
    if (row.unitPrice.trim() && this.money(row.unitPrice) == null) {
      return true;
    }
    return !!row.totalPrice.trim() && this.money(row.totalPrice) == null;
  }

  private supplierReady(): boolean {
    if (this.creatingSupplier) {
      if (!this.newSupplierName.trim()) {
        this.error = 'Supplier name is required.';
        return false;
      }
      const reference = this.newSupplierReference.trim();
      if (reference && hasDuplicateSupplierReference(reference, this.suppliers)) {
        this.referenceError = duplicateSupplierReferenceMessage(reference);
        this.error = this.referenceError;
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

  private syncRowLookups() {
    for (const row of this.rows) {
      if (row.unitCode.trim()) {
        row.unitCode = canonicalUnit(row.unitCode, this.units);
      }
      if (row.condition.trim()) {
        row.condition = canonicalCondition(row.condition, this.conditions);
      }
      if (row.currencyId == null) {
        row.currencyId = this.currencyId;
      }
    }
  }

  private initialRows(): QuoteRow[] {
    const items = [...(this.data.quotation?.items ?? [])];
    const used = new Set<number>();
    const quoted: QuoteRow[] = [];
    const available: QuoteRow[] = [];
    for (const activity of this.sortedActivities()) {
      const item = this.takeItem(activity, items, used);
      const row = this.rowFromActivity(activity, item);
      (item ? quoted : available).push(row);
    }
    items.forEach((item, index) => {
      if (!used.has(index)) {
        quoted.push(this.rowFromItem(item, index));
      }
    });
    return this.creating ? [...available, ...quoted] : quoted;
  }

  private sortedActivities(): Activity[] {
    return [...this.activities].sort((left, right) => {
      const leftKey = this.dsnKey(left.dsnNo);
      const rightKey = this.dsnKey(right.dsnNo);
      if (leftKey.numeric !== rightKey.numeric) {
        return leftKey.numeric - rightKey.numeric;
      }
      if (leftKey.text !== rightKey.text) {
        return leftKey.text < rightKey.text ? -1 : 1;
      }
      return left.id - right.id;
    });
  }

  private takeItem(activity: Activity, items: QuotationLine[], used: Set<number>): QuotationLine | null {
    const byId = items.findIndex((item, index) => !used.has(index) && item.activityId === activity.id);
    if (byId >= 0) {
      used.add(byId);
      return items[byId];
    }
    const byIdentity = items.findIndex((item, index) => !used.has(index) && this.sameText(item.dsnNo, activity.dsnNo) && this.sameActivityPart(item.partNo, activity));
    if (byIdentity >= 0) {
      used.add(byIdentity);
      return items[byIdentity];
    }
    return null;
  }

  private rowFromActivity(activity: Activity, item: QuotationLine | null): QuoteRow {
    const row = this.priceRow({
      key: `activity-${activity.id}`,
      manual: false,
      activityId: activity.id,
      dsnNo: item?.dsnNo?.trim() || (activity.dsnNo ?? '').trim(),
      partNo: item?.partNo?.trim() || (activity.mainPartNo || activity.partNo || '').trim(),
      description: item?.description?.trim() || (activity.description ?? '').trim(),
      condition: item ? (item.condition?.trim() || '') : this.lookupOrDefault(activity.condition, 'condition'),
      serialNo: item ? (item.serialNo ?? '').trim() : (activity.serialNo ?? '').trim(),
      tsn: item ? (item.tsn ?? '').trim() : (activity.tsn ?? '').trim(),
      tso: item ? (item.tso ?? '').trim() : (activity.tso ?? '').trim(),
      csn: item ? (item.csn ?? '').trim() : (activity.csn ?? '').trim(),
      cso: item ? (item.cso ?? '').trim() : (activity.cso ?? '').trim(),
      usageOwned: item != null,
      usageEdited: { serialNo: false, tsn: false, tso: false, csn: false, cso: false },
      unitCode: item ? (item.unit?.trim() || '') : this.lookupOrDefault(activity.unit, 'unit'),
      leadTime: leadTimeText(item?.leadTime),
      minimumQty: item?.minimumQty ? String(item.minimumQty) : '',
      quantity: item ? String(item.quantity || 1) : this.activityQuantity(activity),
      unitPrice: item?.unitPrice == null ? '' : String(item.unitPrice),
      totalPrice: item?.totalPrice == null ? '' : String(item.totalPrice),
      currencyId: item?.currencyId ?? null,
      currencyExplicit: item?.currencyId != null,
      typeId: activity.typeId ?? null,
      typeName: activity.typeId == null ? '' : (activity.type?.trim() || 'Type'),
    });
    return row;
  }

  private lookupOrDefault(value: string | null | undefined, kind: 'unit' | 'condition'): string {
    const canonical = kind === 'unit'
      ? canonicalUnit(value ?? '', this.units)
      : canonicalCondition(value ?? '', this.conditions);
    return canonical.trim() || (kind === 'unit' ? 'EA' : 'NE');
  }

  private rowFromItem(item: QuotationLine, index: number): QuoteRow {
    const activityId = item.activityId ?? directActivityId(item.dsnNo ?? '', item.partNo ?? '', this.activities);
    const activity = this.activityFor(activityId);
    return this.priceRow({
      key: `item-${item.id ?? index}`,
      manual: false,
      activityId,
      dsnNo: item.dsnNo ?? '',
      partNo: item.partNo ?? '',
      description: item.description ?? '',
      condition: item.condition ?? '',
      serialNo: (item.serialNo ?? '').trim(),
      tsn: (item.tsn ?? '').trim(),
      tso: (item.tso ?? '').trim(),
      csn: (item.csn ?? '').trim(),
      cso: (item.cso ?? '').trim(),
      usageOwned: true,
      usageEdited: { serialNo: false, tsn: false, tso: false, csn: false, cso: false },
      unitCode: item.unit?.trim() || '',
      leadTime: leadTimeText(item.leadTime),
      minimumQty: item.minimumQty ? String(item.minimumQty) : '',
      quantity: String(item.quantity || 1),
      unitPrice: item.unitPrice == null ? '' : String(item.unitPrice),
      totalPrice: item.totalPrice == null ? '' : String(item.totalPrice),
      currencyId: item.currencyId ?? null,
      currencyExplicit: item.currencyId != null,
      typeId: activity?.typeId ?? null,
      typeName: activity?.typeId == null ? '' : (activity.type?.trim() || 'Type'),
    });
  }

  private blankRow(): QuoteRow {
    return this.priceRow({
      key: `manual-${crypto.randomUUID()}`,
      manual: true,
      activityId: null,
      dsnNo: '',
      partNo: '',
      description: '',
      condition: this.lookupOrDefault('', 'condition'),
      serialNo: '',
      tsn: '',
      tso: '',
      csn: '',
      cso: '',
      usageOwned: false,
      usageEdited: { serialNo: false, tsn: false, tso: false, csn: false, cso: false },
      unitCode: this.lookupOrDefault('', 'unit'),
      leadTime: '',
      minimumQty: '',
      quantity: '1',
      unitPrice: '',
      totalPrice: '',
      currencyId: this.currencyId,
      currencyExplicit: false,
      typeId: null,
      typeName: '',
    });
  }

  private priceRow(row: Omit<QuoteRow, 'unitExplicit' | 'totalExplicit'>): QuoteRow {
    const priced: QuoteRow = {
      ...row,
      unitExplicit: !!row.unitPrice.trim(),
      totalExplicit: !!row.totalPrice.trim(),
    };
    if (priced.unitExplicit && priced.totalExplicit && this.pricesMatch(priced)) {
      priced.totalExplicit = false;
    }
    this.recalculate(priced);
    return priced;
  }

  private pricesMatch(row: QuoteRow): boolean {
    const unit = this.money(row.unitPrice);
    const total = this.money(row.totalPrice);
    if (unit == null || total == null) {
      return false;
    }
    return Math.abs(unit * this.effectiveQuantity(row) - total) <= 0.01;
  }

  private matchesLabel(row: QuoteRow, label: ActivityLabel | null): boolean {
    if (!label) {
      return true;
    }
    const activity = this.activityFor(row.activityId);
    return !!activity && activityHasLabel(activity, label);
  }

  private activityFor(activityId: number | null): Activity | undefined {
    if (this.indexedActivities !== this.activities) {
      this.indexedActivities = this.activities;
      this.activityById = new Map(this.activities.map((activity) => [activity.id, activity]));
    }
    return activityId == null ? undefined : this.activityById.get(activityId);
  }

  private linkRow(row: QuoteRow) {
    row.activityId = directActivityId(row.dsnNo, row.partNo, this.activities);
    const activity = this.activityFor(row.activityId);
    row.typeId = activity?.typeId ?? null;
    row.typeName = row.typeId == null ? '' : (activity?.type?.trim() || 'Type');
    if (!row.usageOwned) {
      if (!row.usageEdited.serialNo) {
        row.serialNo = (activity?.serialNo ?? '').trim();
      }
      if (!row.usageEdited.tsn) {
        row.tsn = (activity?.tsn ?? '').trim();
      }
      if (!row.usageEdited.tso) {
        row.tso = (activity?.tso ?? '').trim();
      }
      if (!row.usageEdited.csn) {
        row.csn = (activity?.csn ?? '').trim();
      }
      if (!row.usageEdited.cso) {
        row.cso = (activity?.cso ?? '').trim();
      }
    }
  }

  get activityTypeOptions(): { id: number; name: string }[] {
    const names = new Map<number, string>();
    for (const row of this.rows) {
      if (row.typeId != null && !names.has(row.typeId)) {
        names.set(row.typeId, row.typeName || 'Type');
      }
    }
    return [...names.entries()]
      .map(([id, name]) => ({ id, name }))
      .sort((left, right) => left.name.localeCompare(right.name));
  }

  private recalculate(row: QuoteRow) {
    const quantity = this.effectiveQuantity(row);
    if (row.unitExplicit && !row.totalExplicit) {
      const unit = this.money(row.unitPrice);
      row.totalPrice = unit == null ? '' : this.formatMoney(unit * quantity);
      return;
    }
    if (row.totalExplicit && !row.unitExplicit) {
      const total = this.money(row.totalPrice);
      row.unitPrice = total == null ? '' : this.formatMoney(total / quantity);
    }
  }

  private isPriced(row: QuoteRow): boolean {
    return !!row.unitPrice.trim() || !!row.totalPrice.trim();
  }

  private activityQuantity(activity: Activity): string {
    if (activity.quantity == null || !Number.isFinite(Number(activity.quantity)) || Number(activity.quantity) <= 0) {
      return '1';
    }
    const rounded = Math.round(Number(activity.quantity));
    return String(rounded < 1 ? 1 : rounded);
  }

  private sameText(left: string | null | undefined, right: string | null | undefined): boolean {
    const value = (left ?? '').trim().toLowerCase();
    return !!value && value === (right ?? '').trim().toLowerCase();
  }

  private sameActivityPart(partNo: string | null | undefined, activity: Activity): boolean {
    const part = (partNo ?? '').trim().toLowerCase();
    if (!part) {
      return false;
    }
    return [activity.partNo, activity.mainPartNo ?? '', ...(activity.alternativePartNos ?? [])]
      .some((value) => (value ?? '').trim().toLowerCase() === part);
  }

  private dsnKey(dsnNo: string | null | undefined): { numeric: number; text: string } {
    const text = (dsnNo ?? '').trim().toLowerCase();
    if (/^\d+$/.test(text)) {
      return { numeric: Number(text), text };
    }
    return { numeric: Number.MAX_SAFE_INTEGER, text };
  }

  private quantity(row: QuoteRow): number {
    return this.validQuantity(row.quantity) ? Number(row.quantity) : 1;
  }

  moqNote(row: QuoteRow): { text: string; short: boolean } | null {
    const requested = Number(row.quantity);
    const minimum = Number(row.minimumQty);
    const shown = quotationQuantity(
      Number.isInteger(requested) ? requested : null,
      row.minimumQty.trim() && Number.isInteger(minimum) && minimum > 0 ? minimum : null,
    );
    return shown.note ? { text: `MOQ ${shown.value}`, short: shown.short } : null;
  }

  private effectiveQuantity(row: QuoteRow): number {
    const minimum = Number(row.minimumQty);
    const requested = Number(row.quantity);
    return pricedQuantity(
      Number.isInteger(requested) && requested > 0 ? requested : null,
      row.minimumQty.trim() && Number.isInteger(minimum) && minimum > 0 ? minimum : null,
      1,
    );
  }

  private validQuantity(value: string): boolean {
    if (!value.trim()) {
      return true;
    }
    const parsed = Number(value);
    return Number.isInteger(parsed) && parsed >= 1;
  }

  validOptionalQuantity(value: string): boolean {
    return !value.trim() || this.validQuantity(value);
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

}

function labelTime(label: ActivityLabel): number {
  const parsed = Date.parse(label.createdAt ?? '');
  return Number.isFinite(parsed) ? parsed : Number.MAX_SAFE_INTEGER;
}
