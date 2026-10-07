import { HttpErrorResponse } from '@angular/common/http';
import { Component, EventEmitter, Input, OnChanges, OnDestroy, Output, SimpleChanges, inject } from '@angular/core';
import { MatDialog } from '@angular/material/dialog';
import { MatSnackBar } from '@angular/material/snack-bar';
import { Subscription } from 'rxjs';
import { MaterialModule } from '../../../../app/material-module';
import { Project, ProjectApiService, QuotationLine, QuotationListItem } from '../../../../core/services/project-api.service';
import { CreateQuotationDialogComponent } from '../../../add-quotation-dialog/add-quotation-dialog';
import { ConfirmDialog } from '../../../confirm-dialog/confirm-dialog';
import { QuotationSync } from '../../../quotation-sync';

@Component({
  selector: 'app-project-quotations-tab',
  standalone: true,
  imports: [MaterialModule],
  templateUrl: './project-quotations-tab.html',
  styleUrl: './project-quotations-tab.css',
})
export class ProjectQuotationsTabComponent implements OnChanges, OnDestroy {
  private api = inject(ProjectApiService);
  private dialog = inject(MatDialog);
  private snackBar = inject(MatSnackBar);
  private sync = inject(QuotationSync);
  private syncSub: Subscription;

  @Input() project: Project | null = null;
  @Output() countChange = new EventEmitter<number>();

  quotations: QuotationListItem[] = [];
  loading = false;
  error = '';
  expanded = new Set<number>();
  lines = new Map<number, QuotationLine[]>();
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

  isExpanded(id: number): boolean {
    return this.expanded.has(id);
  }

  lineItems(id: number): QuotationLine[] {
    return this.lines.get(id) ?? [];
  }

  edit(quotation: QuotationListItem) {
    if (!this.project || this.deletingId != null) {
      return;
    }
    this.api.quotation(quotation.id).subscribe({
      next: (detail) => {
        const ref = this.dialog.open(CreateQuotationDialogComponent, {
          disableClose: true,
          width: '90vw',
          maxWidth: '1200px',
          minWidth: 'min(850px, 96vw)',
          maxHeight: '90vh',
          panelClass: ['add-project-dialog-panel', 'quotation-dialog-panel', 'app-dialog-pane'],
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
    return this.money(amount);
  }

  money(value?: number | null): string {
    if (value == null) {
      return '—';
    }
    return value.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 4 });
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
