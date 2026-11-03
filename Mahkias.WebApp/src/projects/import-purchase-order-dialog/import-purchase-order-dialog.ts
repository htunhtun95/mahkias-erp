import { HttpErrorResponse } from '@angular/common/http';
import { Component, OnInit, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { forkJoin } from 'rxjs';
import { MaterialModule } from '../../app/material-module';
import {
  Currency,
  ProjectApiService,
  PurchaseOrderExcelMappingInput,
  PurchaseOrderItemInput,
  Supplier,
} from '../../core/services/project-api.service';
import { DriveUploadCardComponent } from '../drive-upload-card/drive-upload-card';
import { QuotationChargeRow, QuotationChargesComponent, readQuotationCharges } from '../quotation-charges/quotation-charges';
import { QuotationDateFieldComponent } from '../quotation-date-field/quotation-date-field';
import { dateInputValue } from '../quotation-terms';

interface ImportPurchaseOrderData {
  projectId: number;
}

@Component({
  selector: 'app-import-purchase-order-dialog',
  standalone: true,
  imports: [FormsModule, MaterialModule, QuotationDateFieldComponent, QuotationChargesComponent, DriveUploadCardComponent],
  templateUrl: './import-purchase-order-dialog.html',
  styleUrls: ['../purchase-order-dialog/purchase-order-dialog.css'],
})
export class ImportPurchaseOrderDialogComponent implements OnInit {
  private api = inject(ProjectApiService);
  private dialogRef = inject(MatDialogRef<ImportPurchaseOrderDialogComponent, boolean>);
  readonly data = inject<ImportPurchaseOrderData>(MAT_DIALOG_DATA);

  suppliers: Supplier[] = [];
  currencies: Currency[] = [];
  supplierId: number | null = null;
  currencyId: number | null = null;
  poDate = todayIso();
  code = '';
  shipToAddress = '';
  charges: QuotationChargeRow[] = [];
  readonly documentAccept = '.pdf,.doc,.docx,.xls,.xlsx,.xlsm,.csv,.png,.jpg,.jpeg,.gif,.webp,.bmp,.tif,.tiff';
  file: File | null = null;
  originalFile: File | null = null;
  originalDragging = false;
  spreadsheetIsOriginal = true;
  step: 'file' | 'map' | 'verify' = 'file';
  mapping: PurchaseOrderExcelMappingInput = {};
  items: PurchaseOrderItemInput[] = [];
  errors: { row: number; field: string; message: string }[] = [];
  checking = false;
  saving = false;
  checkState: 'idle' | 'checked' = 'idle';
  error = '';

  ngOnInit() {
    forkJoin({
      suppliers: this.api.suppliers(),
      currencies: this.api.currencies(),
    }).subscribe({
      next: (result) => {
        this.suppliers = result.suppliers;
        this.currencies = result.currencies;
        this.currencyId = result.currencies.find((currency) => currency.isDefault)?.id ?? result.currencies[0]?.id ?? null;
      },
      error: () => {
        this.error = 'Purchase order details could not be loaded.';
      },
    });
  }

  get symbol(): string {
    return this.currencies.find((currency) => currency.id === this.currencyId)?.symbol || '$';
  }

  get canImport(): boolean {
    return this.checkState === 'checked' && this.items.length > 0 && this.errors.length === 0 && !!this.supplierId && !this.saving;
  }

  close() {
    if (!this.saving) {
      this.dialogRef.close(false);
    }
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
    this.file = file;
    this.checkState = 'idle';
    this.items = [];
    this.errors = [];
    this.step = 'file';
  }

  readColumns() {
    if (!this.file || this.checking) {
      this.error = 'Choose a spreadsheet first.';
      return;
    }
    this.checking = true;
    this.error = '';
    this.api.quotationExcelMapping(this.data.projectId, this.file).subscribe({
      next: (result) => {
        const suggested = result.suggested ?? {};
        this.mapping = {
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
        this.checking = false;
        this.step = 'map';
      },
      error: (error: HttpErrorResponse) => {
        this.checking = false;
        this.error = error.error?.message || 'The spreadsheet columns could not be read.';
      },
    });
  }

  check() {
    if (!this.file || this.checking) {
      return;
    }
    this.checking = true;
    this.error = '';
    this.api.purchaseOrderExcelValidate(this.data.projectId, this.file, this.mapping).subscribe({
      next: (result) => {
        this.items = result.items ?? [];
        this.errors = result.errors ?? [];
        this.checkState = 'checked';
        this.checking = false;
        this.step = 'verify';
        if (!this.items.length && !this.errors.length) {
          this.error = 'No purchase order lines were found in the spreadsheet.';
        }
      },
      error: (error: HttpErrorResponse) => {
        this.checking = false;
        this.checkState = 'idle';
        this.error = error.error?.message || 'The spreadsheet could not be checked.';
      },
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
      projectIds: [this.data.projectId],
      items: this.items,
      charges: charges.charges,
    }, this.referenceFile()).subscribe({
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
