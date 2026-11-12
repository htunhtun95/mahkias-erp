import { Component, inject } from '@angular/core';
import { MAT_DIALOG_DATA, MatDialog, MatDialogRef } from '@angular/material/dialog';
import { forkJoin } from 'rxjs';
import { MaterialModule } from '../../../../app/material-module';
import { Activity, ActivityQuotationLink, ProjectApiService, PurchaseOrderListItem, PurchaseOrderTraceLine, Supplier } from '../../../../core/services/project-api.service';
import { ProcurementTrace } from '../../../procurement-trace';
import { closeOnEscape } from '../../../../core/close-on-escape';
import { formatMoney } from '../../../money-format';
import { leadTimeLabel, leadTimeText } from '../../../lead-time';
import { pricedQuantity, quotationQuantity, quotationValidity } from '../../../quotation-terms';
import { PurchaseOrderDialogComponent, PurchaseOrderPresetLine } from '../../../purchase-order-dialog/purchase-order-dialog';
import { PurchaseOrderSync } from '../../../purchase-order-sync';

export type QuotationViewKind = 'item' | 'group';

export interface QuotationDataRow {
  dsnNo: string | null;
  partNo: string | null;
  link: ActivityQuotationLink | null;
}


export interface QuotationDataState {
  kind: QuotationViewKind;
  projectId?: number;
  activityId?: number;
  groupId?: number;
  title: string;
  subtitle: string;
  requiredQuantity?: number | null;
  serialNo?: string | null;
  tsn?: string | null;
  tso?: string | null;
  csn?: string | null;
  cso?: string | null;
  rows: QuotationDataRow[];
  activities?: Activity[];
  orders?: PurchaseOrderTraceLine[];
  ordersLoading?: boolean;
}

@Component({
  selector: 'app-quotation-data-dialog',
  standalone: true,
  imports: [MaterialModule],
  templateUrl: './quotation-data-dialog.html',
  styleUrl: './quotation-data-dialog.css',
})
export class QuotationDataDialog {
  readonly leadTimeLabel = leadTimeLabel;
  private dialogRef = inject(MatDialogRef<QuotationDataDialog>);
  private dialog = inject(MatDialog);
  private api = inject(ProjectApiService);
  private purchaseSync = inject(PurchaseOrderSync);
  private trace = inject(ProcurementTrace);
  data = inject<QuotationDataState>(MAT_DIALOG_DATA);
  private selectedKeys = new Set<string>();
  private suppliers: Supplier[] = [];
  orderChoices: PurchaseOrderListItem[] = [];

  constructor() {
    closeOnEscape(this.dialogRef, () => this.close());
    this.loadChoices();
  }

  get isGroup(): boolean {
    return this.data.kind === 'group';
  }

  get rows(): QuotationDataRow[] {
    return this.data.rows ?? [];
  }

  get orders(): PurchaseOrderTraceLine[] {
    return this.data.orders ?? [];
  }

  get heading(): string {
    return this.isGroup ? 'Quotes' : 'Quoted options';
  }

  get emptyMessage(): string {
    return this.isGroup ? 'No activities in this group.' : 'No quotations for this activity.';
  }

  get quoteReceived(): number {
    const required = Number(this.data.requiredQuantity ?? 0);
    if (!Number.isFinite(required) || required <= 0) {
      return 0;
    }
    const quoted = this.pricedRows().reduce((sum, link) => sum + (Number(link.quantity) || 0), 0);
    if (quoted >= required) {
      return 100;
    }
    return Math.max(0, Math.min(100, Math.round((100 * quoted) / required)));
  }

  get quotationStatus(): string {
    if (!this.pricedRows().length) {
      return 'Not quoted';
    }
    return this.quoteReceived >= 100 ? 'Quoted' : 'Partial';
  }

  close() {
    this.dialogRef.close(false);
  }

  isSelected(row: QuotationDataRow): boolean {
    const key = this.rowKey(row);
    return !!key && this.selectedKeys.has(key);
  }

  toggleRow(row: QuotationDataRow, checked: boolean) {
    const key = this.rowKey(row);
    if (!key) {
      return;
    }
    if (checked) {
      this.selectedKeys.add(key);
    } else {
      this.selectedKeys.delete(key);
    }
  }

  toggleAll(checked: boolean) {
    this.selectedKeys.clear();
    if (checked) {
      for (const row of this.selectableRows) {
        const key = this.rowKey(row);
        if (key) {
          this.selectedKeys.add(key);
        }
      }
    }
  }

  clearSelection() {
    this.selectedKeys.clear();
  }

  get selectedCount(): number {
    return this.selectedRows.length;
  }

  get selectionNote(): string {
    const count = this.selectedCount;
    return `${count} ${count === 1 ? 'item' : 'items'} selected`;
  }

  get supplierConflict(): boolean {
    const ids = new Set<number>();
    const names = new Set<string>();
    for (const row of this.selectedRows) {
      const link = row.link;
      if (!link) {
        continue;
      }
      names.add((link.supplierName ?? '').trim().toLowerCase());
      const supplierId = this.supplierIdFor(link);
      if (supplierId) {
        ids.add(supplierId);
      }
    }
    return ids.size > 1 || names.size > 1;
  }

  get selectionMessage(): string {
    return this.supplierConflict ? 'Select lines from one supplier only.' : '';
  }

  get selectedSupplierId(): number | null {
    if (!this.selectedRows.length || this.supplierConflict) {
      return null;
    }
    const link = this.selectedRows[0].link;
    return link ? this.supplierIdFor(link) : null;
  }

  get activeOrders(): PurchaseOrderListItem[] {
    const supplierId = this.selectedSupplierId;
    if (!supplierId) {
      return [];
    }
    return this.orderChoices.filter((order) => order.supplierId === supplierId && !this.isCancelled(order));
  }

  get canCreate(): boolean {
    return !!this.data.projectId && this.selectedCount > 0 && !this.supplierConflict && this.selectedSupplierId != null;
  }

  get allSelected(): boolean {
    return this.selectableRows.length > 0 && this.selectableRows.every((row) => this.isSelected(row));
  }

  get someSelected(): boolean {
    return this.selectedCount > 0 && !this.allSelected;
  }

  get selectableRows(): QuotationDataRow[] {
    return this.rows.filter((row) => !!row.link);
  }

  choosePurchaseOrder(select: HTMLSelectElement) {
    const value = select.value;
    select.value = '';
    if (value === 'new') {
      this.createPurchaseOrder();
      return;
    }
    const orderId = Number(value);
    if (Number.isInteger(orderId) && orderId > 0) {
      this.addToExistingPurchaseOrder(orderId);
    }
  }

  createPurchaseOrder() {
    if (!this.canCreate || !this.data.projectId) {
      return;
    }
    this.openPurchaseOrder({
      projectId: this.data.projectId,
      activities: this.data.activities ?? [],
      supplierId: this.selectedSupplierId,
      lines: this.presetLines(),
    });
  }

  addToExistingPurchaseOrder(orderId: number) {
    if (!this.canCreate || !this.data.projectId || !this.activeOrders.some((order) => order.id === orderId)) {
      return;
    }
    this.openPurchaseOrder({
      projectId: this.data.projectId,
      activities: this.data.activities ?? [],
      purchaseOrderId: orderId,
      lines: this.presetLines(),
    });
  }

  openOrder(line: PurchaseOrderTraceLine) {
    if (!this.data.projectId) {
      return;
    }
    this.trace.showOrder(this.data.projectId, line.purchaseOrderId);
    this.close();
  }

  money(amount?: number | null, symbol?: string | null, code?: string | null): string {
    return formatMoney(amount, symbol, code);
  }

  when(value?: string | null): string {
    if (!value) {
      return '—';
    }
    const date = new Date(value);
    if (Number.isNaN(date.getTime())) {
      return '—';
    }
    return date.toLocaleDateString('en-US', { month: 'short', day: 'numeric', year: 'numeric' });
  }

  quotedUnit(link: ActivityQuotationLink): string {
    if (link.unitPrice != null) {
      return formatMoney(link.unitPrice, link.currencySymbol, link.currencyCode);
    }
    return formatMoney(link.baseUnitPrice, '$', 'USD');
  }

  quotedTotal(link: ActivityQuotationLink): string {
    if (link.totalPrice != null) {
      return formatMoney(link.totalPrice, link.currencySymbol, link.currencyCode);
    }
    return formatMoney(link.baseTotalPrice, '$', 'USD');
  }

  private pricedRows(): ActivityQuotationLink[] {
    return this.rows
      .map((row) => row.link)
      .filter((link): link is ActivityQuotationLink => !!link && (link.unitPrice != null || link.totalPrice != null));
  }

  validity(validUntil?: string | null) {
    return quotationValidity(validUntil);
  }

  quantityDisplay(link: ActivityQuotationLink | null | undefined) {
    if (!link) {
      return quotationQuantity(null, null);
    }
    return quotationQuantity(link.quantity, link.minimumQty);
  }

  private get selectedRows(): QuotationDataRow[] {
    return this.selectableRows.filter((row) => this.isSelected(row));
  }

  private loadChoices() {
    const projectId = this.data.projectId;
    if (!projectId) {
      return;
    }
    forkJoin({
      orders: this.api.purchaseOrders(projectId),
      suppliers: this.api.suppliers(),
    }).subscribe({
      next: (result) => {
        this.orderChoices = result.orders ?? [];
        this.suppliers = result.suppliers ?? [];
      },
    });
  }

  private openPurchaseOrder(data: {
    projectId: number;
    activities: Activity[];
    purchaseOrderId?: number;
    supplierId?: number | null;
    lines: PurchaseOrderPresetLine[];
  }) {
    const ref = this.dialog.open(PurchaseOrderDialogComponent, {
      disableClose: true,
      width: '100vw',
      maxWidth: '100vw',
      minWidth: '100vw',
      height: '100vh',
      maxHeight: '100vh',
      panelClass: ['add-project-dialog-panel', 'quotation-dialog-panel', 'po-new-panel', 'app-dialog-pane'],
      backdropClass: 'add-project-dialog-backdrop',
      autoFocus: 'first-tabbable',
      data,
    });
    ref.afterClosed().subscribe((saved) => {
      if (!saved || !this.data.projectId) {
        return;
      }
      this.clearSelection();
      this.purchaseSync.notify(this.data.projectId);
      this.loadChoices();
    });
  }

  private presetLines(): PurchaseOrderPresetLine[] {
    return this.selectedRows.flatMap((row) => {
      const link = row.link;
      if (!link) {
        return [];
      }
      return [{
        activityId: link.activityId,
        quotationId: link.quotationId,
        quotationCode: link.quotationRef,
        dsnNo: row.dsnNo,
        partNo: link.partNo,
        isAlternativePart: !!link.alternativePart,
        description: link.description,
        condition: link.condition,
        unit: link.unit,
        leadTime: leadTimeText(link.leadTime),
        quantity: orderedQuantity(link),
        unitPrice: link.unitPrice,
        totalPrice: link.totalPrice,
      }];
    });
  }

  private supplierIdFor(link: ActivityQuotationLink): number | null {
    if (link.supplierId && link.supplierId > 0) {
      return link.supplierId;
    }
    const name = (link.supplierName ?? '').trim().toLowerCase();
    if (!name) {
      return null;
    }
    const matches = this.suppliers.filter((supplier) => supplier.name.trim().toLowerCase() === name);
    return matches.length === 1 ? matches[0].id : null;
  }

  private isCancelled(order: PurchaseOrderListItem): boolean {
    return (order.statusCode ?? '').trim().toLowerCase() === 'cancelled';
  }

  private rowKey(row: QuotationDataRow): string {
    const link = row.link;
    if (!link) {
      return '';
    }
    return `${link.activityId}:${link.itemId ?? link.quotationId}`;
  }
}

function orderedQuantity(link: ActivityQuotationLink): number {
  return pricedQuantity(link.quantity, link.minimumQty);
}
