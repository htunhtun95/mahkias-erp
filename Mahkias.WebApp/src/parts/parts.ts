import { Component, OnInit, inject } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { Router, RouterModule } from '@angular/router';
import { MatDialog } from '@angular/material/dialog';
import { PageEvent } from '@angular/material/paginator';
import { MatSnackBar } from '@angular/material/snack-bar';
import { Sort } from '@angular/material/sort';
import { MaterialModule } from '../app/material-module';
import { whileBusy } from '../core/while-busy';
import { PartRecord, ProjectApiService } from '../core/services/project-api.service';
import { ConfirmDialog } from '../projects/confirm-dialog/confirm-dialog';
import { formatProjectListDate } from '../projects/projects';
import { PartDialog } from './part-dialog/part-dialog';

@Component({
  selector: 'app-parts',
  standalone: true,
  imports: [RouterModule, MaterialModule],
  templateUrl: './parts.html',
  styleUrl: './parts.css',
})
export class PartsComponent implements OnInit {
  private api = inject(ProjectApiService);
  private dialog = inject(MatDialog);
  private router = inject(Router);
  private snackBar = inject(MatSnackBar);

  parts: PartRecord[] = [];
  searchTerm = '';
  sortBy: 'partNo' | 'createdAt' = 'partNo';
  sortDirection: 'asc' | 'desc' = 'asc';
  loading = true;
  deletingId: number | null = null;
  error = '';
  pageIndex = 0;
  pageSize = 10;
  private searchTimer: ReturnType<typeof setTimeout> | undefined;

  ngOnInit() {
    this.load();
  }

  get isDeleting() {
    return this.deletingId != null;
  }

  get page() {
    const start = this.pageIndex * this.pageSize;
    return this.ordered.slice(start, start + this.pageSize);
  }

  onSort(event: Sort) {
    this.sortBy = event.active === 'createdAt' ? 'createdAt' : 'partNo';
    this.sortDirection = event.direction === 'desc' ? 'desc' : 'asc';
    this.pageIndex = 0;
  }

  onSearch(value: string) {
    this.searchTerm = value;
    this.pageIndex = 0;
    clearTimeout(this.searchTimer);
    this.searchTimer = setTimeout(() => this.load(), 250);
  }

  onPage(event: PageEvent) {
    this.pageIndex = event.pageIndex;
    this.pageSize = event.pageSize;
  }

  load() {
    this.loading = this.parts.length === 0;
    this.error = '';
    this.api.parts(this.searchTerm).subscribe({
      next: (parts) => {
        this.parts = parts ?? [];
        const lastPage = Math.max(0, Math.ceil(this.parts.length / this.pageSize) - 1);
        if (this.pageIndex > lastPage) {
          this.pageIndex = lastPage;
        }
        this.loading = false;
      },
      error: () => {
        this.error = 'Parts could not be loaded.';
        this.loading = false;
      },
    });
  }

  listDate(part: PartRecord) {
    return formatProjectListDate(part.createdAt);
  }

  descriptionText(part: PartRecord) {
    const text = (part.descriptions ?? []).map((item) => item.description?.trim()).filter((item) => !!item).join(' · ');
    return text || '—';
  }

  openAdd() {
    const ref = this.dialog.open(PartDialog, this.dialogConfig());
    ref.afterClosed().subscribe((saved) => {
      if (saved && typeof saved === 'object') {
        this.snackBar.open('Part saved.', undefined, { duration: 3000 });
        this.router.navigate(['/parts', saved.id]);
      }
    });
  }

  openEdit(event: Event, part: PartRecord) {
    event.stopPropagation();
    if (this.isDeleting) {
      return;
    }
    const ref = this.dialog.open(PartDialog, this.dialogConfig({ part }));
    ref.afterClosed().subscribe((saved) => {
      if (saved) {
        this.snackBar.open('Part saved.', undefined, { duration: 3000 });
        this.load();
      }
    });
  }

  openDetails(part: PartRecord, event?: Event) {
    if (this.isDeleting || this.eventFromActions(event)) {
      return;
    }
    this.router.navigate(['/parts', part.id]);
  }

  openDelete(event: Event, part: PartRecord) {
    event.stopPropagation();
    if (this.isDeleting) {
      return;
    }
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
      if (!confirmed || this.isDeleting) {
        return;
      }
      this.api.deletePart(part.id).pipe(whileBusy((busy) => (this.deletingId = busy ? part.id : null))).subscribe({
        next: () => {
          this.snackBar.open('Part deleted.', undefined, { duration: 3000 });
          this.load();
        },
        error: (error: HttpErrorResponse) => {
          this.error = error.status === 404 ? 'This part was not found.' : 'This part could not be deleted.';
        },
      });
    });
  }

  private get ordered() {
    const direction = this.sortDirection === 'asc' ? 1 : -1;
    return [...this.parts].sort((left, right) => {
      const compared = this.sortBy === 'createdAt'
        ? this.createdTime(left) - this.createdTime(right)
        : left.partNo.localeCompare(right.partNo, undefined, { sensitivity: 'base', numeric: true });
      return compared === 0 ? left.id - right.id : compared * direction;
    });
  }

  private createdTime(part: PartRecord) {
    const time = Date.parse(part.createdAt ?? '');
    return Number.isNaN(time) ? 0 : time;
  }

  private dialogConfig(data?: { part: PartRecord }) {
    return {
      disableClose: true,
      width: '560px',
      maxWidth: 'calc(100vw - 32px)',
      panelClass: 'add-project-dialog-panel',
      backdropClass: 'add-project-dialog-backdrop',
      autoFocus: 'first-tabbable' as const,
      data,
    };
  }

  private eventFromActions(event?: Event) {
    return !!event && !!(event.target as HTMLElement).closest('.row-actions');
  }
}
