import { Component, OnDestroy, OnInit, inject, viewChild } from '@angular/core';
import { Subscription } from 'rxjs';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { MatDialog } from '@angular/material/dialog';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MaterialModule } from '../../app/material-module';
import { whileBusy } from '../../core/while-busy';
import { ActivityType, Project, ProjectApiService } from '../../core/services/project-api.service';
import { AddProjectDialog } from '../add-project-dialog/add-project-dialog';
import { CreateQuotationDialogComponent } from '../add-quotation-dialog/add-quotation-dialog';
import { ImportQuotationDialogComponent } from '../import-quotation-dialog/import-quotation-dialog';
import { ConfirmDialog } from '../confirm-dialog/confirm-dialog';
import { ProjectActivitiesTabComponent } from './tabs/project-activities-tab/project-activities-tab';
import { ProjectPurchasesTabComponent } from './tabs/project-purchases-tab/project-purchases-tab';
import { ProjectQuotationsTabComponent } from './tabs/project-quotations-tab/project-quotations-tab';
import { QuotationSync } from '../quotation-sync';

export type ProjectTab = 'activities' | 'quotations' | 'purchases';

const PROJECT_TABS: ProjectTab[] = ['activities', 'quotations', 'purchases'];

@Component({
  selector: 'app-project',
  standalone: true,
  imports: [
    RouterModule,
    MaterialModule,
    ProjectActivitiesTabComponent,
    ProjectQuotationsTabComponent,
    ProjectPurchasesTabComponent,
  ],
  templateUrl: './project.html',
  styleUrl: './project.css',
})
export class ProjectDetailsComponent implements OnInit, OnDestroy {
  private api = inject(ProjectApiService);
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private dialog = inject(MatDialog);
  private snackBar = inject(MatSnackBar);
  private quotationSync = inject(QuotationSync);
  private syncSub?: Subscription;
  private activitiesTab = viewChild(ProjectActivitiesTabComponent);
  private quotationsTab = viewChild(ProjectQuotationsTabComponent);

  project: Project | null = null;
  types: ActivityType[] = [];
  tab: ProjectTab = 'activities';
  loading = true;
  isDeleting = false;
  activityFilter = '';
  quotationCount = 0;
  error = '';

  get activityCount(): number {
    return this.project?.activities?.length ?? 0;
  }

  ngOnInit() {
    this.api.activityTypes().subscribe({
      next: (types) => {
        this.types = types;
      },
    });

    this.route.queryParamMap.subscribe((params) => {
      this.tab = this.readTab(params.get('tab'));
    });

    this.syncSub = this.quotationSync.changes$.subscribe((projectId) => {
      if (this.project?.id === projectId) {
        this.load(projectId, true);
      }
    });

    this.route.paramMap.subscribe((params) => {
      const id = Number(params.get('id'));
      if (!this.route.snapshot.queryParamMap.get('tab')) {
        this.router.navigate([], {
          relativeTo: this.route,
          queryParams: { tab: 'activities' },
          queryParamsHandling: 'merge',
          replaceUrl: true,
        });
      }
      this.load(id);
    });
  }

  ngOnDestroy() {
    this.syncSub?.unsubscribe();
  }

  selectTab(tab: ProjectTab) {
    if (tab === this.tab) {
      return;
    }

    this.router.navigate([], {
      relativeTo: this.route,
      queryParams: { tab },
      queryParamsHandling: 'merge',
    });
  }

  load(id: number, silent = false) {
    if (!silent) {
      this.loading = true;
    }
    this.error = '';
    this.api.get(id, 'dsnNo', 'asc').subscribe({
      next: (project) => {
        this.project = project;
        this.loading = false;
      },
      error: () => {
        this.error = 'This project could not be loaded.';
        this.loading = false;
      },
    });
  }

  reload() {
    if (this.project) {
      this.load(this.project.id);
    }
  }

  onActivitiesChange() {
    if (this.project) {
      this.load(this.project.id, true);
    }
  }

  openEditProject() {
    if (!this.project || this.isDeleting) {
      return;
    }

    const projectId = this.project.id;
    const ref = this.dialog.open(AddProjectDialog, {
      disableClose: true,
      width: '520px',
      maxWidth: 'calc(100vw - 32px)',
      panelClass: 'add-project-dialog-panel',
      backdropClass: 'add-project-dialog-backdrop',
      autoFocus: 'first-tabbable',
      data: { project: this.project },
    });

    ref.afterClosed().subscribe((saved) => {
      if (saved) {
        this.load(projectId);
      }
    });
  }

  openDeleteProject() {
    if (!this.project || this.isDeleting) {
      return;
    }

    const projectId = this.project.id;
    const ref = this.dialog.open(ConfirmDialog, {
      disableClose: true,
      width: '480px',
      maxWidth: 'calc(100vw - 32px)',
      panelClass: 'add-project-dialog-panel',
      backdropClass: 'add-project-dialog-backdrop',
      data: {
        title: 'Delete Project',
        message: 'Are you sure you want to delete this project? All associated activities and alternative parts will be permanently removed.',
        confirmLabel: 'Delete',
      },
    });

    ref.afterClosed().subscribe((confirmed) => {
      if (!confirmed || this.isDeleting) {
        return;
      }

      this.api.deleteProject(projectId).pipe(whileBusy((busy) => (this.isDeleting = busy))).subscribe({
        next: () => {
          this.snackBar.open('Project deleted.', undefined, { duration: 3000 });
          this.router.navigate(['/projects']);
        },
        error: () => {
          this.error = 'This project could not be deleted.';
        },
      });
    });
  }

  openImport() {
    this.activitiesTab()?.openImport();
  }

  openNewGroup() {
    this.activitiesTab()?.openNewGroup();
  }

  openAddActivity() {
    this.activitiesTab()?.openAddActivity();
  }

  openQuotation() {
    if (!this.project || this.isDeleting) {
      return;
    }

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
        projectId: this.project.id,
        activities: this.project.activities ?? [],
        groups: this.activitiesTab()?.groups ?? [],
      },
    });
    this.afterQuotationSaved(ref);
  }

  openImportQuotation() {
    if (!this.project || this.isDeleting) {
      return;
    }

    const ref = this.dialog.open(ImportQuotationDialogComponent, {
      disableClose: true,
      width: '90vw',
      maxWidth: '1200px',
      minWidth: 'min(850px, 96vw)',
      maxHeight: '90vh',
      panelClass: ['add-project-dialog-panel', 'quotation-dialog-panel', 'app-dialog-pane'],
      backdropClass: 'add-project-dialog-backdrop',
      autoFocus: 'first-tabbable',
      data: {
        projectId: this.project.id,
        activities: this.project.activities ?? [],
        groups: this.activitiesTab()?.groups ?? [],
      },
    });
    this.afterQuotationSaved(ref);
  }

  private afterQuotationSaved(ref: { afterClosed: () => { subscribe: (next: (saved: boolean) => void) => void } }) {
    ref.afterClosed().subscribe((saved) => {
      if (saved && this.project) {
        this.snackBar.open('Quotation saved.', undefined, { duration: 3000 });
        this.quotationSync.notify(this.project.id);
      }
    });
  }

  private readTab(value: string | null): ProjectTab {
    return PROJECT_TABS.includes(value as ProjectTab) ? (value as ProjectTab) : 'activities';
  }
}
