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
import { ImportPurchaseOrderDialogComponent } from '../import-purchase-order-dialog/import-purchase-order-dialog';
import { ImportQuotationDialogComponent } from '../import-quotation-dialog/import-quotation-dialog';
import { PurchaseOrderDialogComponent } from '../purchase-order-dialog/purchase-order-dialog';
import { ConfirmDialog } from '../confirm-dialog/confirm-dialog';
import { ProjectActivitiesTabComponent } from './tabs/project-activities-tab/project-activities-tab';
import { ProjectDocumentsTabComponent } from './tabs/project-documents-tab/project-documents-tab';
import { ProjectPurchasesTabComponent } from './tabs/project-purchases-tab/project-purchases-tab';
import { ProjectQuotationsTabComponent } from './tabs/project-quotations-tab/project-quotations-tab';
import { ProcurementTrace } from '../procurement-trace';
import { PurchaseOrderSync } from '../purchase-order-sync';
import { QuotationSync } from '../quotation-sync';

export type ProjectTab = 'activities' | 'procurement' | 'documents';
export type ProcurementSection = 'quotations' | 'purchases';

const PROJECT_TABS: ProjectTab[] = ['activities', 'procurement', 'documents'];
const PROCUREMENT_SECTIONS: ProcurementSection[] = ['quotations', 'purchases'];

@Component({
  selector: 'app-project',
  standalone: true,
  imports: [
    RouterModule,
    MaterialModule,
    ProjectActivitiesTabComponent,
    ProjectDocumentsTabComponent,
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
  private purchaseSync = inject(PurchaseOrderSync);
  private trace = inject(ProcurementTrace);
  private syncSub?: Subscription;
  private traceSub?: Subscription;
  private activitiesTab = viewChild(ProjectActivitiesTabComponent);
  private documentsTab = viewChild(ProjectDocumentsTabComponent);
  private purchasesTab = viewChild(ProjectPurchasesTabComponent);
  private quotationsTab = viewChild(ProjectQuotationsTabComponent);

  project: Project | null = null;
  types: ActivityType[] = [];
  tab: ProjectTab = 'activities';
  section: ProcurementSection = 'quotations';
  loading = true;
  isDeleting = false;
  openingQuotation = false;
  activityFilter = '';
  quoteFilter: 'all' | 'quoted' | 'unquoted' = 'all';
  quotationCount = 0;
  purchaseCount = 0;
  procurementMounted = false;
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
      const rawTab = params.get('tab');
      if (rawTab === 'quotations' || rawTab === 'purchases') {
        this.router.navigate([], {
          relativeTo: this.route,
          queryParams: { tab: 'procurement', section: rawTab },
          replaceUrl: true,
        });
        return;
      }

      const tab = this.readTab(rawTab);
      if (tab === 'procurement') {
        this.procurementMounted = true;
        const rawSection = params.get('section');
        const section = PROCUREMENT_SECTIONS.includes(rawSection as ProcurementSection)
          ? (rawSection as ProcurementSection)
          : null;
        if (!section) {
          this.router.navigate([], {
            relativeTo: this.route,
            queryParams: { tab: 'procurement', section: 'quotations' },
            queryParamsHandling: 'merge',
            replaceUrl: true,
          });
          return;
        }

        this.tab = 'procurement';
        this.section = section;
        return;
      }

      if (tab === 'documents') {
        this.tab = 'documents';
        if (params.get('section')) {
          this.router.navigate([], {
            relativeTo: this.route,
            queryParams: { section: null },
            queryParamsHandling: 'merge',
            replaceUrl: true,
          });
        }
        return;
      }

      this.tab = 'activities';
      if (params.get('section')) {
        this.router.navigate([], {
          relativeTo: this.route,
          queryParams: { section: null },
          queryParamsHandling: 'merge',
          replaceUrl: true,
        });
      }
    });

    this.syncSub = this.quotationSync.changes$.subscribe((projectId) => {
      if (this.project?.id === projectId) {
        this.load(projectId, true);
      }
    });

    this.traceSub = new Subscription();
    this.traceSub.add(this.trace.activities$.subscribe(({ projectId, activityId }) => {
      if (this.project?.id !== projectId) {
        return;
      }
      this.selectTab('activities');
      setTimeout(() => this.activitiesTab()?.revealActivity(activityId));
    }));
    this.traceSub.add(this.trace.orders$.subscribe(({ projectId, purchaseOrderId }) => {
      if (this.project?.id !== projectId) {
        return;
      }
      this.selectSection('purchases');
      setTimeout(() => this.purchasesTab()?.reveal(purchaseOrderId));
    }));

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
    this.traceSub?.unsubscribe();
  }

  selectTab(tab: ProjectTab) {
    if (tab === this.tab) {
      return;
    }

    this.router.navigate([], {
      relativeTo: this.route,
      queryParams: tab === 'procurement'
        ? { tab, section: this.section }
        : { tab, section: null },
      queryParamsHandling: 'merge',
    });
  }

  manageDocuments() {
    this.documentsTab()?.manage();
  }

  selectSection(section: ProcurementSection) {
    if (this.tab === 'procurement' && section === this.section) {
      return;
    }

    this.router.navigate([], {
      relativeTo: this.route,
      queryParams: { tab: 'procurement', section },
      queryParamsHandling: 'merge',
    });
  }

  load(id: number, silent = false) {
    this.api.forgetProject(id);
    if (!silent) {
      this.loading = true;
    }
    this.error = '';
    this.api.get(id, 'dsnNo', 'asc').subscribe({
      next: (project) => {
        this.project = project;
        this.loading = false;
        this.warmProcurement(project.id);
      },
      error: () => {
        this.error = 'This project could not be loaded.';
        this.loading = false;
      },
    });
  }

  private warmProcurement(projectId: number) {
    this.api.quotations(projectId).subscribe({
      next: (items) => {
        if (this.project?.id === projectId) {
          this.quotationCount = items.length;
        }
      },
    });
    this.api.purchaseOrders(projectId).subscribe({
      next: (items) => {
        if (this.project?.id === projectId) {
          this.purchaseCount = items.length;
        }
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

  openAddActivity() {
    this.activitiesTab()?.openAddActivity();
  }

  openQuotation() {
    if (!this.project || this.isDeleting || this.openingQuotation) {
      return;
    }

    this.openingQuotation = true;
    try {
      const ref = this.dialog.open(CreateQuotationDialogComponent, {
        disableClose: true,
        width: '100vw',
        maxWidth: '100vw',
        minWidth: '100vw',
        height: '100vh',
        maxHeight: '100vh',
        panelClass: ['add-project-dialog-panel', 'quotation-dialog-panel', 'po-new-panel', 'app-dialog-pane'],
        backdropClass: 'quotation-create-backdrop',
        autoFocus: 'first-tabbable',
        data: {
          projectId: this.project.id,
          activities: this.project.activities ?? [],
          groups: this.activitiesTab()?.groups ?? [],
        },
      });
      const finishOpening = () => {
        this.openingQuotation = false;
      };
      ref.afterOpened().subscribe(finishOpening);
      ref.afterClosed().subscribe(finishOpening);
      this.afterQuotationSaved(ref);
    } catch {
      this.openingQuotation = false;
    }
  }

  openImportQuotation() {
    if (!this.project || this.isDeleting) {
      return;
    }

    const ref = this.dialog.open(ImportQuotationDialogComponent, {
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
        groups: this.activitiesTab()?.groups ?? [],
      },
    });
    this.afterQuotationSaved(ref);
  }

  openPurchaseOrder() {
    if (!this.project || this.isDeleting) {
      return;
    }

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
      },
    });
    this.afterPurchaseSaved(ref);
  }

  openImportPurchaseOrder() {
    if (!this.project || this.isDeleting) {
      return;
    }

    const ref = this.dialog.open(ImportPurchaseOrderDialogComponent, {
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
      },
    });
    this.afterPurchaseSaved(ref);
  }

  private afterPurchaseSaved(ref: { afterClosed: () => { subscribe: (next: (saved: boolean) => void) => void } }) {
    ref.afterClosed().subscribe((saved) => {
      if (saved && this.project) {
        this.snackBar.open('Purchase order saved.', undefined, { duration: 3000 });
        this.purchaseSync.notify(this.project.id);
      }
    });
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
