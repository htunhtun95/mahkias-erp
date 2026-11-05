import { HttpErrorResponse } from '@angular/common/http';
import { Component, EventEmitter, Input, OnChanges, OnDestroy, Output, SimpleChanges, inject } from '@angular/core';
import { RouterModule } from '@angular/router';
import { MatDialog } from '@angular/material/dialog';
import { MatSnackBar } from '@angular/material/snack-bar';
import { Subscription } from 'rxjs';
import { MaterialModule } from '../../../../app/material-module';
import { Project, ProjectApiService, QuotationLine, QuotationListItem } from '../../../../core/services/project-api.service';
import { CreateQuotationDialogComponent } from '../../../add-quotation-dialog/add-quotation-dialog';
import { ConfirmDialog } from '../../../confirm-dialog/confirm-dialog';
import { PurchaseOrderDialogComponent } from '../../../purchase-order-dialog/purchase-order-dialog';
import { PurchaseOrderSync } from '../../../purchase-order-sync';
import { documentAccept, openDocumentLibrary } from '../../../document-library-dialog/document-library-dialog';
import { QuotationDetailDialog } from './quotation-detail-dialog';
import { QuotationSync } from '../../../quotation-sync';
import { formatMoney } from '../../../money-format';
import { quotationQuantity, quotationValidity } from '../../../quotation-terms';

@Component({
  selector: 'app-project-quotations-tab',
  standalone: true,
  imports: [MaterialModule, RouterModule],
  templateUrl: './project-quotations-tab.html',
  styleUrl: './project-quotations-tab.css',
})
export class ProjectQuotationsTabComponent implements OnChanges, OnDestroy {
  private api = inject(ProjectApiService);
  private dialog = inject(MatDialog);
  private snackBar = inject(MatSnackBar);
  private sync = inject(QuotationSync);
  private purchaseSync = inject(PurchaseOrderSync);
  private syncSub: Subscription;

  @Input() project: Project | null = null;
  @Output() countChange = new EventEmitter<number>();

  quotations: QuotationListItem[] = [];
  loading = false;
  error = '';
  expanded = new Set<number>();
  lines = new Map<number, QuotationLine[]>();
  selectedLineIds = new Set<number>();
  loadingLine = new Set<number>();
  deletingId: number | null = null;

  constructor() {
    this.syncSub = this.sync.changes$.subscribe((projectId) => {
      if (projectId === this.project?.id) {
        this.load();
      }
    });
  }

  ngOnChanges(changes: SimpleChanges) {
    const nextId = changes['project']?.currentValue?.id;
    const previousId = changes['project']?.previousValue?.id;
    if (nextId !== previousId) {
      this.expanded.clear();
      this.lines.clear();
      this.selectedLineIds.clear();
      this.load();
    }
  }

  ngOnDestroy() {
    this.syncSub.unsubscribe();
  }

  load() {
    if (!this.project) {
      this.quotations = [];
      this.countChange.emit(0);
      return;
    }
    this.loading = true;
    this.error = '';
    this.api.quotations(this.project.id).subscribe({
      next: (quotations) => {
        this.quotations = quotations;
        this.loading = false;
        this.countChange.emit(quotations.length);
        const ids = new Set(quotations.map((quotation) => quotation.id));
        for (const id of [...this.expanded]) {
          if (!ids.has(id)) {
            this.expanded.delete(id);
            this.lines.delete(id);
          } else {
            this.fetchLines(id);
          }
        }
      },
      error: () => {
        this.loading = false;
        this.error = 'Quotations could not be loaded.';
        this.countChange.emit(0);
      },
    });
  }

  toggle(quotation: QuotationListItem) {
    if (this.expanded.has(quotation.id)) {
      this.expanded.delete(quotation.id);
      return;
    }
    this.expanded.add(quotation.id);
    if (!this.lines.has(quotation.id)) {
      this.fetchLines(quotation.id);
    }
  }

  manageDocuments(quotation: QuotationListItem) {
    if (!this.project || this.deletingId != null) {
      return;
    }
    this.api.quotationDocuments(this.project.id, quotation.id).subscribe({
      next: (documents) => {
        openDocumentLibrary(this.dialog, {
          title: 'Manage documents',
          note: `Files for ${quotation.code}. Choose the original, then set the order.`,
          kind: 'quotation',
          projectId: this.project!.id,
          ownerId: quotation.id,
          documents: documents ?? [],
          accept: documentAccept,
        });
      },
    });
  }

  openDetails(quotation: QuotationListItem) {
    if (!this.project || this.deletingId != null) {
      return;
    }
    this.dialog.open(QuotationDetailDialog, {
      disableClose: true,
      width: '100vw',
      maxWidth: '100vw',
      minWidth: '100vw',
      height: '100vh',
      maxHeight: '100vh',
      panelClass: ['add-project-dialog-panel', 'quotation-dialog-panel', 'po-new-panel', 'app-dialog-pane'],
      backdropClass: 'add-project-dialog-backdrop',
      autoFocus: 'first-tabbable',
      data: {
        projectId: this.project.id,
        quotation,
      },
    });
  }

  isExpanded(id: number): boolean {
    return this.expanded.has(id);
  }

  lineItems(id: number): QuotationLine[] {
    return this.lines.get(id) ?? [];
  }

  lineSelected(id: number): boolean {
    return this.selectedLineIds.has(id);
  }

  allLinesSelected(quotationId: number): boolean {
    const lines = this.lineItems(quotationId);
    return lines.length > 0 && lines.every((line) => this.selectedLineIds.has(line.id));
  }

  someLinesSelected(quotationId: number): boolean {
    const lines = this.lineItems(quotationId);
    const selected = lines.filter((line) => this.selectedLineIds.has(line.id)).length;
    return selected > 0 && selected < lines.length;
  }

  toggleLine(id: number, checked: boolean) {
    if (checked) {
      this.selectedLineIds.add(id);
    } else {
      this.selectedLineIds.delete(id);
    }
  }

  toggleAllLines(quotationId: number, checked: boolean) {
    for (const line of this.lineItems(quotationId)) {
      this.toggleLine(line.id, checked);
    }
  }

  get selectedCount(): number {
    return this.selectedLineIds.size;
  }

  get selectionNote(): string {
    const count = this.selectedCount;
    return `${count} ${count === 1 ? 'item' : 'items'} selected`;
  }

  clearSelection() {
    this.selectedLineIds.clear();
  }

  createPurchaseOrder() {
    if (!this.project || this.deletingId != null) {
      return;
    }
    const lines = [];
    const supplierIds = new Set<number>();
    for (const quotation of this.quotations) {
      for (const line of this.lineItems(quotation.id)) {
        if (!this.selectedLineIds.has(line.id)) {
          continue;
        }
        if (!line.activityId) {
          this.error = 'Every selected line needs an activity before it can go on a purchase order.';
          return;
        }
        supplierIds.add(quotation.supplierId);
        lines.push({
          activityId: line.activityId,
          quotationId: quotation.id,
          quotationCode: quotation.code,
          dsnNo: line.dsnNo,
          partNo: line.partNo,
          isAlternativePart: !!line.isAlternativePart,
          description: line.description,
          condition: line.condition,
          unit: line.unit,
          leadTime: line.leadTime,
          quantity: quotedQuantity(line),
          unitPrice: line.unitPrice,
          totalPrice: line.totalPrice,
        });
      }
    }
    if (!lines.length) {
      this.error = 'Select one or more quotation lines first.';
      return;
    }
    if (supplierIds.size !== 1) {
      this.error = 'Select lines from one supplier only.';
      return;
    }
    this.error = '';
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
      data: {
        projectId: this.project.id,
        activities: this.project.activities ?? [],
        supplierId: [...supplierIds][0],
        lines,
      },
    });
    ref.afterClosed().subscribe((saved) => {
      if (saved && this.project) {
        this.selectedLineIds.clear();
        this.snackBar.open('Purchase order saved.', undefined, { duration: 3000 });
        this.purchaseSync.notify(this.project.id);
      }
    });
  }

  edit(quotation: QuotationListItem) {
    if (!this.project || this.deletingId != null) {
      return;
    }
    this.api.quotation(quotation.id).subscribe({
      next: (detail) => {
        const ref = this.dialog.open(CreateQuotationDialogComponent, {
          disableClose: true,
          width: '100vw',
          maxWidth: '100vw',
          minWidth: '100vw',
          height: '100vh',
          maxHeight: '100vh',
          panelClass: ['add-project-dialog-panel', 'quotation-dialog-panel', 'po-new-panel', 'app-dialog-pane'],
          backdropClass: 'add-project-dialog-backdrop',
          autoFocus: 'first-tabbable',
          data: {
            projectId: this.project!.id,
            activities: this.project!.activities ?? [],
            quotation: detail,
          },
        });
        ref.afterClosed().subscribe((saved) => {
          if (saved && this.project) {
            this.snackBar.open('Quotation saved.', undefined, { duration: 3000 });
            this.sync.notify(this.project.id);
          }
        });
      },
      error: () => {
        this.error = 'This quotation could not be opened.';
      },
    });
  }

  remove(quotation: QuotationListItem) {
    if (!this.project || this.deletingId != null) {
      return;
    }
    const ref = this.dialog.open(ConfirmDialog, {
      disableClose: true,
      width: '420px',
      panelClass: 'add-project-dialog-panel',
      backdropClass: 'add-project-dialog-backdrop',
      data: {
        title: 'Delete quotation',
        message: 'Are you sure you want to delete this quotation? Its line items will be permanently removed.',
        confirmLabel: 'Delete',
        cancelLabel: 'Cancel',
      },
    });
    ref.afterClosed().subscribe((confirmed) => {
      if (!confirmed || !this.project) {
        return;
      }
      this.deletingId = quotation.id;
      this.api.deleteQuotation(quotation.id).subscribe({
        next: () => {
          this.deletingId = null;
          this.snackBar.open('Quotation deleted.', undefined, { duration: 3000 });
          this.sync.notify(this.project!.id);
        },
        error: (error: HttpErrorResponse) => {
          this.deletingId = null;
          this.error = error.error?.message || 'The quotation could not be deleted.';
        },
      });
    });
  }

  get lineCount(): number {
    return this.quotations.reduce((total, quotation) => total + (quotation.itemCount || 0), 0);
  }

  get quotedAmount(): string {
    const amount = this.quotations.reduce((total, quotation) => total + (quotation.totalPrice || 0), 0);
    return this.money(amount, '$', 'USD');
  }

  money(value?: number | null, symbol?: string | null, code?: string | null): string {
    return formatMoney(value, symbol, code);
  }

  validity(quotedDate?: string | null, validUntil?: string | null) {
    if (!quotedDate || !validUntil) {
      return null;
    }
    return quotationValidity(validUntil);
  }

  quantityDisplay(quantity?: number | null, minimumQty?: number | null) {
    return quotationQuantity(quantity, minimumQty);
  }

  when(value?: string | null): string {
    if (!value) {
      return '';
    }
    const date = new Date(value);
    if (Number.isNaN(date.getTime())) {
      return '';
    }
    return date.toLocaleDateString('en-US', { month: 'short', day: 'numeric', year: 'numeric' });
  }

  private fetchLines(id: number) {
    this.loadingLine.add(id);
    this.api.quotation(id).subscribe({
      next: (detail) => {
        this.lines.set(id, detail.items ?? []);
        this.loadingLine.delete(id);
      },
      error: () => {
        this.loadingLine.delete(id);
        this.lines.set(id, []);
      },
    });
  }
}

function quotedQuantity(line: QuotationLine): number {
  const minimum = Number(line.minimumQty);
  if (Number.isFinite(minimum) && minimum > 0) {
    return minimum;
  }
  const quantity = Number(line.quantity);
  return Number.isFinite(quantity) && quantity > 0 ? quantity : 0;
}
