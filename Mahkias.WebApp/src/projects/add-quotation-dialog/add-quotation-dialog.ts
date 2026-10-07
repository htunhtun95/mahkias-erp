import { Component, ElementRef, ViewChild, inject } from '@angular/core';
import { TextFieldModule } from '@angular/cdk/text-field';
import { FormsModule } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { Observable, of, switchMap, tap } from 'rxjs';
import { MaterialModule } from '../../app/material-module';
import { blockEnterSubmit } from '../../core/block-enter-submit';
import { whileBusy } from '../../core/while-busy';
import {
  Activity,
  ActivityImportIssue,
  ProjectApiService,
  QuotationDetail,
  QuotationItemInput,
  QuotationLine,
  Supplier,
} from '../../core/services/project-api.service';
import { directActivityId } from '../quotation-map';

interface QuoteRow {
  key: string;
  activityId: number | null;
  dsnNo: string;
  partNo: string;
  description: string;
  unitPrice: string;
  quantity: string;
  totalPrice: string;
  unitExplicit: boolean;
  totalExplicit: boolean;
}

@Component({
  selector: 'app-create-quotation-dialog',
  standalone: true,
  imports: [FormsModule, MaterialModule, TextFieldModule],
  templateUrl: './add-quotation-dialog.html',
  styleUrl: './add-quotation-dialog.css',
})
export class CreateQuotationDialogComponent {
  @ViewChild('supplierInput') private supplierInput?: ElementRef<HTMLInputElement>;

  private api = inject(ProjectApiService);
  private dialogRef = inject(MatDialogRef<CreateQuotationDialogComponent>);
  data = inject<{ projectId: number; activities: Activity[]; quotation?: QuotationDetail }>(MAT_DIALOG_DATA);
  quotationId = this.data.quotation?.id ?? null;
  quotationCode = this.data.quotation?.code ?? '';
  rowFilter = '';

  supplierQuotationRef = this.data.quotation?.supplierReference ?? '';
  supplierQuery = this.data.quotation?.supplierName ?? '';
  supplierId: number | null = this.data.quotation?.supplierId ?? null;
  suppliers: Supplier[] = [];
  supplierOpen = false;
  creatingSupplier = false;
  newSupplierName = '';
  newSupplierReference = '';
  rows: QuoteRow[] = this.initialRows();
  submitted = false;
  isSaving = false;
  error = '';

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

  get visibleRows(): QuoteRow[] {
    const query = this.rowFilter.trim().toLowerCase();
    if (!query) {
      return this.rows;
    }
    return this.rows.filter((row) => row.dsnNo.toLowerCase().includes(query) || row.partNo.toLowerCase().includes(query));
  }

  onDsn(row: QuoteRow) {
    this.linkRow(row);
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

  onQuantityBlur(row: QuoteRow) {
    if (!row.quantity.trim()) {
      row.quantity = '1';
    }
    this.recalculate(row);
  }

  invalid(row: QuoteRow, field: 'dsn' | 'part' | 'price' | 'qty'): boolean {
    if (!this.submitted || !this.isPriced(row)) {
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
    if (!this.supplierReady()) {
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
    if (filled.some((row) => !row.dsnNo.trim() || !row.partNo.trim() || !this.validQuantity(row.quantity) || this.invalid(row, 'price'))) {
      this.error = 'DSN number, part number, and quantity are required. Check the highlighted rows.';
      return;
    }

    const items: QuotationItemInput[] = filled.map((row) => ({
      dsnNo: row.dsnNo.trim(),
      partNo: row.partNo.trim(),
      description: row.description.trim(),
      unitPrice: this.money(row.unitPrice),
      quantity: this.quantity(row),
      totalPrice: this.money(row.totalPrice),
      activityId: row.activityId,
      activityGroupId: null,
    }));

    this.dialogRef.disableClose = true;
    this.withSupplier()
      .pipe(
        switchMap(() => {
          const body = {
            supplierId: this.supplierId!,
            supplierReference: this.supplierQuotationRef.trim(),
            projectIds: [this.data.projectId],
            items,
          };
          return this.quotationId
            ? this.api.updateQuotation(this.quotationId, body)
            : this.api.createQuotation(body);
        }),
        whileBusy((busy) => (this.isSaving = busy)),
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
          this.error = issues.length
            ? issues.map((issue) => `Row ${issue.row}: [${issue.field}] ${issue.message}`).join(' ')
            : error.error?.message || 'The quotation could not be saved.';
        },
      });
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

  private initialRows(): QuoteRow[] {
    const items = [...(this.data.quotation?.items ?? [])];
    const used = new Set<number>();
    const rows = this.sortedActivities().map((activity) => this.rowFromActivity(activity, this.takeItem(activity, items, used)));
    items.forEach((item, index) => {
      if (!used.has(index)) {
        rows.push(this.rowFromItem(item, index));
      }
    });
    return rows;
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
      activityId: activity.id,
      dsnNo: item?.dsnNo?.trim() || (activity.dsnNo ?? '').trim(),
      partNo: item?.partNo?.trim() || (activity.mainPartNo || activity.partNo || '').trim(),
      description: item?.description?.trim() || (activity.description ?? '').trim(),
      quantity: item ? String(item.quantity || 1) : this.activityQuantity(activity),
      unitPrice: item?.unitPrice == null ? '' : String(item.unitPrice),
      totalPrice: item?.totalPrice == null ? '' : String(item.totalPrice),
    });
    return row;
  }

  private rowFromItem(item: QuotationLine, index: number): QuoteRow {
    return this.priceRow({
      key: `item-${item.id ?? index}`,
      activityId: item.activityId ?? directActivityId(item.dsnNo ?? '', item.partNo ?? '', this.activities),
      dsnNo: item.dsnNo ?? '',
      partNo: item.partNo ?? '',
      description: item.description ?? '',
      quantity: String(item.quantity || 1),
      unitPrice: item.unitPrice == null ? '' : String(item.unitPrice),
      totalPrice: item.totalPrice == null ? '' : String(item.totalPrice),
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
    return Math.abs(unit * this.quantity(row) - total) <= 0.01;
  }

  private linkRow(row: QuoteRow) {
    row.activityId = directActivityId(row.dsnNo, row.partNo, this.activities);
  }

  private recalculate(row: QuoteRow) {
    const quantity = this.quantity(row);
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

  private validQuantity(value: string): boolean {
    if (!value.trim()) {
      return true;
    }
    const parsed = Number(value);
    return Number.isInteger(parsed) && parsed >= 1;
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
