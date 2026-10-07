import { Component, OnInit, inject } from '@angular/core';
import { Router, RouterModule } from '@angular/router';
import { MatDialog } from '@angular/material/dialog';
import { MatSnackBar } from '@angular/material/snack-bar';
import { Sort } from '@angular/material/sort';
import { MaterialModule } from '../app/material-module';
import { whileBusy } from '../core/while-busy';
import { Project, ProjectApiService } from '../core/services/project-api.service';
import { AddProjectDialog } from './add-project-dialog/add-project-dialog';
import { ConfirmDialog } from './confirm-dialog/confirm-dialog';

@Component({
  selector: 'app-projects',
  standalone: true,
  imports: [RouterModule, MaterialModule],
  templateUrl: './projects.html',
  styleUrl: './projects.css',
})
export class ProjectsComponent implements OnInit {
  private api = inject(ProjectApiService);
  private dialog = inject(MatDialog);
  private router = inject(Router);
  private snackBar = inject(MatSnackBar);

  projects: Project[] = [];
  searchTerm = '';
  sortBy = 'createdAt';
  sortDirection: 'asc' | 'desc' = 'desc';
  loading = true;
  deletingId: number | null = null;
  private hasLoaded = false;
  error = '';
  private searchTimer: ReturnType<typeof setTimeout> | undefined;

  ngOnInit() {
    this.load();
  }

  onSearch(value: string) {
    this.searchTerm = value;
    clearTimeout(this.searchTimer);
    this.searchTimer = setTimeout(() => this.load(), 250);
  }

  load() {
    if (!this.hasLoaded) {
      this.loading = true;
    }
    this.error = '';
    this.api.list(this.searchTerm, this.sortBy, this.sortDirection).subscribe({
      next: (projects) => {
        this.projects = projects;
        this.hasLoaded = true;
        this.loading = false;
      },
      error: () => {
        this.error = 'Projects could not be loaded.';
        this.loading = false;
      },
    });
  }

  openAddProject() {
    const ref = this.dialog.open(AddProjectDialog, {
      disableClose: true,
      width: '520px',
      maxWidth: 'calc(100vw - 32px)',
      panelClass: 'add-project-dialog-panel',
      backdropClass: 'add-project-dialog-backdrop',
      autoFocus: 'first-tabbable',
    });
    ref.afterClosed().subscribe((saved: Project | boolean | undefined) => {
      if (saved && typeof saved === 'object') {
        this.router.navigate(['/projects', saved.id]);
      }
    });
  }

  onSort(event: Sort) {
    this.sortBy = event.active || 'createdAt';
    this.sortDirection = event.direction === 'asc' ? 'asc' : 'desc';
    this.load();
  }

  /**
   * Projects list only. Prefer Modified, and fall back to Created when Modified is null.
   * The date header sorts by modifiedAt. SQL keeps null ModifiedAt as null (nulls first
   * on ASC, nulls last on DESC in SQL Server) and does not substitute CreatedAt.
   */
  listDate(project: Project) {
    return formatProjectListDate(project.modifiedAt || project.createdAt);
  }

  get isDeleting() {
    return this.deletingId != null;
  }

  openProject(project: Project, event?: Event) {
    if (this.isDeleting || this.eventFromActions(event)) {
      return;
    }
    this.router.navigate(['/projects', project.id]);
  }

  onRowKey(event: Event, project: Project) {
    if (this.eventFromActions(event)) {
      return;
    }
    event.preventDefault();
    this.openProject(project);
  }

  openEditProject(event: Event, project: Project) {
    event.stopPropagation();
    if (this.isDeleting) {
      return;
    }
    const ref = this.dialog.open(AddProjectDialog, {
      disableClose: true,
      width: '520px',
      maxWidth: 'calc(100vw - 32px)',
      panelClass: 'add-project-dialog-panel',
      backdropClass: 'add-project-dialog-backdrop',
      autoFocus: 'first-tabbable',
      data: { project },
    });
    ref.afterClosed().subscribe((saved) => {
      if (saved) {
        this.load();
      }
    });
  }

  openDeleteProject(event: Event, project: Project) {
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
        title: 'Delete Project',
        message:
          'Are you sure you want to delete this project? All associated activities and alternative parts will be permanently removed.',
        confirmLabel: 'Delete',
      },
    });

    ref.afterClosed().subscribe((confirmed) => {
      if (!confirmed || this.isDeleting) {
        return;
      }

      this.api.deleteProject(project.id).pipe(whileBusy((busy) => {
        this.deletingId = busy ? project.id : null;
      })).subscribe({
        next: () => {
          this.snackBar.open('Project deleted.', undefined, { duration: 3000 });
          this.load();
        },
        error: () => {
          this.error = 'This project could not be deleted.';
        },
      });
    });
  }

  private eventFromActions(event?: Event) {
    const target = event?.target;
    return target instanceof Element && !!target.closest('.row-actions');
  }
}

export function formatAuditDate(value: string | null | undefined) {
  if (!value) {
    return '—';
  }
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) {
    return '—';
  }
  return new Intl.DateTimeFormat('en-GB', {
    day: '2-digit',
    month: 'short',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  }).format(date);
}

/** Compact projects-list date. Today / Yesterday, otherwise "Oct 5, 2026". */
export function formatProjectListDate(value: string | null | undefined) {
  if (!value) {
    return '—';
  }
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) {
    return '—';
  }

  const today = startOfLocalDay(new Date());
  const day = startOfLocalDay(date);
  const diffDays = Math.round((today.getTime() - day.getTime()) / 86_400_000);
  if (diffDays === 0) {
    return 'Today';
  }
  if (diffDays === 1) {
    return 'Yesterday';
  }

  return new Intl.DateTimeFormat('en-US', {
    month: 'short',
    day: 'numeric',
    year: 'numeric',
  }).format(date);
}

function startOfLocalDay(date: Date) {
  return new Date(date.getFullYear(), date.getMonth(), date.getDate());
}
