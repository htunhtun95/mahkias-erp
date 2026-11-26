import { Component, OnDestroy, OnInit, inject } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { MatDialog } from '@angular/material/dialog';
import { MatSnackBar } from '@angular/material/snack-bar';
import { Subscription, forkJoin, of } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { MaterialModule } from '../../app/material-module';
import { ReturnNavigation } from '../../core/return-navigation';
import { whileBusy } from '../../core/while-busy';
import { Activity, ProjectApiService, QuotationCharge, QuotationDetail, QuotationDocument, QuotationLine } from '../../core/services/project-api.service';
import { ConfirmDialog } from '../../projects/confirm-dialog/confirm-dialog';
import { documentAccept, openDocumentLibrary } from '../../projects/document-library-dialog/document-library-dialog';
import { formatFileSize } from '../../projects/document-manager/document-manager';
import { leadTimeLabel } from '../../projects/lead-time';
import { formatMoney } from '../../projects/money-format';
import { quotationQuantity, quotationValidity } from '../../projects/quotation-terms';
import { usageDetails } from '../../projects/usage-fields';
import { attachmentIcon, attachmentTone } from '../../projects/attachment-file';
import { openQuotationEditor } from '../quotation-pane';

@Component({
  selector: 'app-quotation-details',
  standalone: true,
  imports: [RouterModule, MaterialModule],
  templateUrl: './quotation.html',
  styleUrls: ['./quotation.css', '../../projects/attachment-files.css'],
})
export class QuotationDetailsComponent implements OnInit, OnDestroy {
  readonly leadTimeLabel = leadTimeLabel;
  readonly usageDetails = usageDetails;
  readonly fileSize = formatFileSize;
  readonly fileIcon = attachmentIcon;
  readonly fileTone = attachmentTone;

  private api = inject(ProjectApiService);
  private dialog = inject(MatDialog);
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private returns = inject(ReturnNavigation);
  private snackBar = inject(MatSnackBar);
  private subscription: Subscription | null = null;

  loading = true;
  deleting = false;
  error = '';
  detail: QuotationDetail | null = null;
  documents: QuotationDocument[] = [];

  ngOnInit() {
    this.subscription = this.route.paramMap.subscribe((params) => {
      const id = Number(params.get('id'));
      if (!id) {
        this.loading = false;
        this.error = 'Quotation details could not be loaded.';
        return;
      }
      this.load(id);
    });
  }

  ngOnDestroy() {
    this.subscription?.unsubscribe();
  }

  get lines(): QuotationLine[] {
    return this.detail?.items ?? [];
  }

  get charges(): QuotationCharge[] {
    return this.detail?.charges ?? [];
  }

  get linked(): boolean {
    return (this.detail?.projectIds?.length ?? 0) > 0;
  }

  get projectLinks(): { id: number; label: string }[] {
    const ids = this.detail?.projectIds ?? [];
    const label = (this.detail?.projectLabel ?? '').trim();
    const labels = label.split(',').map((part) => part.trim()).filter((part) => part.length > 0);
    if (ids.length === 1) {
      return [{ id: ids[0], label: label || labels[0] || 'Project' }];
    }
    return ids.map((id, index) => ({
      id,
      label: labels[index] || 'Project',
    }));
  }

  get chargesTotal(): number {
    return roundMoney(this.charges.reduce((sum, charge) => sum + (Number(charge.amount) || 0), 0));
  }

  get subtotal(): number {
    const base = this.lines.reduce((sum, line) => sum + (Number(line.baseTotalPrice) || 0), 0);
    const rate = Number(this.detail?.exchangeRate) || 1;
    return roundMoney(base / rate);
  }

  get grandTotal(): number {
    return roundMoney(this.subtotal + this.chargesTotal);
  }

  get validityLabel(): string {
    return quotationValidity(this.detail?.validUntil)?.label ?? '';
  }

  get validityExpired(): boolean {
    return quotationValidity(this.detail?.validUntil)?.expired ?? false;
  }

  goBack() {
    const previous = this.returns.previous();
    if (previous) {
      this.router.navigateByUrl(previous);
      return;
    }
    this.router.navigate(['/quotations']);
  }

  money(value?: number | null, symbol?: string | null, code?: string | null): string {
    return formatMoney(value, symbol, code);
  }

  summaryMoney(value?: number | null): string {
    return formatMoney(value, this.detail?.currencySymbol, this.detail?.currencyCode);
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

  quantity(line: QuotationLine) {
    return quotationQuantity(line.quantity, line.minimumQty);
  }

  fileKind(fileName: string): string {
    const kind = extension(fileName);
    return kind ? kind.toUpperCase() : 'File';
  }

  edit() {
    const detail = this.detail;
    if (!detail || this.deleting) {
      return;
    }
    const projectId = detail.projectIds?.[0] ?? null;
    const project = projectId ? this.api.get(projectId).pipe(catchError(() => of(null))) : of(null);
    project.subscribe({
      next: (loaded) => {
        const activities = (loaded?.activities ?? []) as Activity[];
        const ref = openQuotationEditor(this.dialog, {
          projectId,
          activities,
          quotation: detail,
          projectLabel: detail.projectLabel,
        });
        ref.afterClosed().subscribe((saved) => {
          if (!saved) {
            return;
          }
          if (projectId) {
            this.api.forgetProject(projectId);
          }
          this.snackBar.open('Quotation saved.', undefined, { duration: 3000 });
          this.load(detail.id);
        });
      },
    });
  }

  manageDocuments() {
    const detail = this.detail;
    const projectId = detail?.projectIds?.[0];
    if (!detail || !projectId || this.deleting) {
      return;
    }
    openDocumentLibrary(this.dialog, {
      title: 'Manage Documents',
      note: `Files for ${detail.code}. Choose the original, then set the order.`,
      kind: 'quotation',
      projectId,
      ownerId: detail.id,
      documents: this.documents,
      accept: documentAccept,
    }).afterClosed().subscribe((result) => {
      if (result?.saved) {
        this.loadDocuments(detail);
      }
    });
  }

  remove() {
    const detail = this.detail;
    if (!detail || this.deleting) {
      return;
    }
    const ref = this.dialog.open(ConfirmDialog, {
      disableClose: true,
      width: '480px',
      maxWidth: 'calc(100vw - 32px)',
      panelClass: 'add-project-dialog-panel',
      backdropClass: 'add-project-dialog-backdrop',
      data: {
        title: 'Delete Quotation',
        message: 'Are you sure you want to delete this quotation? Its line items will be permanently removed.',
        confirmLabel: 'Delete',
        cancelLabel: 'Cancel',
      },
    });
    ref.afterClosed().subscribe((confirmed) => {
      if (!confirmed || this.deleting || !this.detail) {
        return;
      }
      const projectId = this.detail.projectIds?.[0];
      this.api.deleteQuotation(this.detail.id).pipe(whileBusy((busy) => (this.deleting = busy))).subscribe({
        next: () => {
          if (projectId) {
            this.api.forgetProject(projectId);
          }
          this.snackBar.open('Quotation deleted.', undefined, { duration: 3000 });
          this.router.navigate(['/quotations']);
        },
        error: (error: HttpErrorResponse) => {
          this.error = error.error?.message || 'This quotation could not be deleted.';
        },
      });
    });
  }

  private load(id: number) {
    this.loading = this.detail?.id !== id;
    this.error = '';
    this.api.quotation(id).subscribe({
      next: (detail) => {
        this.detail = detail;
        this.loading = false;
        this.loadDocuments(detail);
      },
      error: () => {
        this.detail = null;
        this.documents = [];
        this.loading = false;
        this.error = 'Quotation details could not be loaded.';
      },
    });
  }

  private loadDocuments(detail: QuotationDetail) {
    const projectIds = detail.projectIds ?? [];
    if (projectIds.length === 0) {
      this.documents = [];
      return;
    }
    forkJoin(projectIds.map((projectId) => this.api.quotationDocuments(projectId, detail.id).pipe(
      catchError(() => of([] as QuotationDocument[])),
    ))).subscribe((groups) => {
      const seen = new Set<number>();
      this.documents = groups.flat().filter((document) => {
        if (seen.has(document.documentId)) {
          return false;
        }
        seen.add(document.documentId);
        return true;
      });
    });
  }
}

function roundMoney(value: number): number {
  return Math.round(value * 100) / 100;
}

function extension(fileName: string): string {
  const index = fileName.lastIndexOf('.');
  return index >= 0 ? fileName.slice(index + 1).toLowerCase() : '';
}
