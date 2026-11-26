import { Component, OnInit, inject } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { MatDialog } from '@angular/material/dialog';
import { MatSnackBar } from '@angular/material/snack-bar';
import { forkJoin } from 'rxjs';
import { MaterialModule } from '../../app/material-module';
import { whileBusy } from '../../core/while-busy';
import { ReturnNavigation } from '../../core/return-navigation';
import { PartAlternate, PartQuotationLine, PartRecord, ProjectApiService } from '../../core/services/project-api.service';
import { ConfirmDialog } from '../../projects/confirm-dialog/confirm-dialog';
import { formatMoney } from '../../projects/money-format';
import { quotationValidity } from '../../projects/quotation-terms';
import { PartDialog } from '../part-dialog/part-dialog';
import { PartAlternateDialog } from '../part-alternate-dialog/part-alternate-dialog';

@Component({
  selector: 'app-part',
  standalone: true,
  imports: [MaterialModule, RouterLink],
  templateUrl: './part.html',
  styleUrl: './part.css',
})
export class PartDetailsComponent implements OnInit {
  private api = inject(ProjectApiService);
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private dialog = inject(MatDialog);
  private snackBar = inject(MatSnackBar);
  private returns = inject(ReturnNavigation);

  part: PartRecord | null = null;
  alternates: PartAlternate[] = [];
  quotations: PartQuotationLine[] = [];
  loading = true;
  relatedLoading = false;
  deleting = false;
  removingAlternateId: number | null = null;
  error = '';
  relatedError = '';

  ngOnInit() {
    this.route.paramMap.subscribe((params) => {
      const id = Number(params.get('id'));
      if (!id) {
        this.router.navigate(['/parts']);
        return;
      }
      this.load(id);
    });
  }

  goBack() {
    const previous = this.returns.previous();
    if (previous && !previous.split(/[?#]/)[0].startsWith('/parts/')) {
      this.router.navigateByUrl(previous);
      return;
    }
    this.router.navigate(['/parts']);
  }

  openEdit() {
    if (!this.part || this.deleting) {
      return;
    }
    const ref = this.dialog.open(PartDialog, {
      disableClose: true,
      width: '560px',
      maxWidth: 'calc(100vw - 32px)',
      panelClass: 'add-project-dialog-panel',
      backdropClass: 'add-project-dialog-backdrop',
      autoFocus: 'first-tabbable',
      data: { part: this.part },
    });
    ref.afterClosed().subscribe((saved) => {
      if (saved && this.part) {
        this.snackBar.open('Part saved.', undefined, { duration: 3000 });
        this.load(this.part.id);
      }
    });
  }

  openDelete() {
    if (!this.part || this.deleting) {
      return;
    }
    const part = this.part;
    const ref = this.dialog.open(ConfirmDialog, {
      disableClose: true,
      width: '480px',
      maxWidth: 'calc(100vw - 32px)',
      panelClass: 'add-project-dialog-panel',
      backdropClass: 'add-project-dialog-backdrop',
      data: {
        title: 'Delete Part',
        message: 'Are you sure you want to delete this part and its descriptions?',
        confirmLabel: 'Delete',
      },
    });
    ref.afterClosed().subscribe((confirmed) => {
      if (!confirmed || this.deleting) {
        return;
      }
      this.api.deletePart(part.id).pipe(whileBusy((busy) => (this.deleting = busy))).subscribe({
        next: () => {
          this.snackBar.open('Part deleted.', undefined, { duration: 3000 });
          this.router.navigate(['/parts']);
        },
        error: (error: HttpErrorResponse) => {
          this.error = error.status === 404 ? 'This part was not found.' : 'This part could not be deleted.';
        },
      });
    });
  }

  openAlternate(alternate?: PartAlternate) {
    if (!this.part || this.deleting) {
      return;
    }
    const partId = this.part.id;
    const ref = this.dialog.open(PartAlternateDialog, {
      disableClose: true,
      width: '560px',
      maxWidth: 'calc(100vw - 32px)',
      panelClass: 'add-project-dialog-panel',
      backdropClass: 'add-project-dialog-backdrop',
      autoFocus: 'first-tabbable',
      data: { partId, alternate },
    });
    ref.afterClosed().subscribe((saved) => {
      if (!saved || this.part?.id !== partId) {
        return;
      }
      this.snackBar.open(alternate ? 'Alternate part saved.' : 'Alternate part added.', undefined, { duration: 3000 });
      this.loadRelated(partId);
    });
  }

  removeAlternate(event: Event, alternate: PartAlternate) {
    event.stopPropagation();
    event.preventDefault();
    if (!this.part || this.removingAlternateId) {
      return;
    }
    const partId = this.part.id;
    const ref = this.dialog.open(ConfirmDialog, {
      disableClose: true,
      width: '480px',
      maxWidth: 'calc(100vw - 32px)',
      panelClass: 'add-project-dialog-panel',
      backdropClass: 'add-project-dialog-backdrop',
      data: {
        title: 'Remove Alternate Part',
        message: 'Remove this alternate from the part? The part record itself stays in the catalog.',
        confirmLabel: 'Remove',
      },
    });
    ref.afterClosed().subscribe((confirmed) => {
      if (!confirmed || this.removingAlternateId || this.part?.id !== partId) {
        return;
      }
      this.api.removePartAlternate(partId, alternate.id).pipe(whileBusy((busy) => {
        this.removingAlternateId = busy ? alternate.id : null;
      })).subscribe({
        next: () => {
          this.snackBar.open('Alternate part removed.', undefined, { duration: 3000 });
          this.loadRelated(partId);
        },
        error: (error: HttpErrorResponse) => {
          this.relatedError = error.status === 404
            ? 'That alternate part was not found.'
            : 'The alternate part could not be removed.';
        },
      });
    });
  }

  openQuotation(line: PartQuotationLine, event?: Event) {
    if (event && this.blocked(event)) {
      return;
    }
    this.router.navigate(['/quotations', line.quotationId]);
  }

  descriptions(alternate: PartAlternate) {
    return alternate.descriptions.map((item) => item.description).filter(Boolean).join(', ');
  }

  quantity(line: PartQuotationLine) {
    const unit = line.unit?.trim();
    return unit ? `${line.quantity} ${unit}` : `${line.quantity}`;
  }

  money(line: PartQuotationLine) {
    return formatMoney(line.unitPrice, line.currencySymbol, line.currencyCode);
  }

  when(value?: string | null) {
    if (!value) {
      return '—';
    }
    const date = new Date(value);
    if (Number.isNaN(date.getTime())) {
      return '—';
    }
    return date.toLocaleDateString('en-US', { month: 'short', day: 'numeric', year: 'numeric' });
  }

  validity(line: PartQuotationLine) {
    return quotationValidity(line.validUntil);
  }

  private load(id: number) {
    this.loading = this.part?.id !== id;
    this.error = '';
    this.relatedError = '';
    if (this.part?.id !== id) {
      this.alternates = [];
      this.quotations = [];
    }
    this.api.part(id).subscribe({
      next: (part) => {
        this.part = part;
        this.loading = false;
        this.loadRelated(id);
      },
      error: (error: HttpErrorResponse) => {
        this.part = null;
        this.alternates = [];
        this.quotations = [];
        this.loading = false;
        this.error = error.status === 404 ? 'This part was not found.' : 'The part could not be loaded.';
      },
    });
  }

  private loadRelated(id: number) {
    this.relatedLoading = true;
    this.relatedError = '';
    forkJoin({
      alternates: this.api.partAlternates(id),
      quotations: this.api.partQuotations(id),
    }).subscribe({
      next: (result) => {
        if (this.part?.id !== id) {
          return;
        }
        this.alternates = result.alternates;
        this.quotations = result.quotations;
        this.relatedLoading = false;
      },
      error: () => {
        if (this.part?.id !== id) {
          return;
        }
        this.relatedLoading = false;
        this.relatedError = 'Related quotations and alternate parts could not be loaded.';
      },
    });
  }

  private blocked(event: Event) {
    const target = event.target as HTMLElement | null;
    return !!target?.closest('a, button');
  }
}
