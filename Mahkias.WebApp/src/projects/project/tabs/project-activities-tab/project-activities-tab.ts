import { HttpErrorResponse } from '@angular/common/http';
import { Component, ElementRef, EventEmitter, Input, OnChanges, OnDestroy, Output, SimpleChanges, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatDialog } from '@angular/material/dialog';
import { MatSnackBar } from '@angular/material/snack-bar';
import { Subscription } from 'rxjs';
import { MaterialModule } from '../../../../app/material-module';
import { AutoGrowDirective } from '../../../../core/auto-grow';
import { blockEnterSubmit } from '../../../../core/block-enter-submit';
import { whileBusy } from '../../../../core/while-busy';
import { Activity, ActivityGroup, ActivityLabel, ActivityQuotationLink, ActivityType, Project, ProjectApiService, PurchaseOrderTraceLine } from '../../../../core/services/project-api.service';
import { QuotationDataDialog, QuotationDataRow, QuotationDataState } from './quotation-data-dialog';
import { formatMoney } from '../../../money-format';
import { PurchaseOrderSync } from '../../../purchase-order-sync';
import { QuotationSync } from '../../../quotation-sync';
import { ActivityGroupDialog } from './activity-group-dialog';
import { AssignGroupDialog } from './assign-group-dialog';
import { AssignLabelDialog } from './assign-label-dialog';
import { GroupDetailDialog } from './group-detail-dialog';
import { AddActivityDialog } from '../../../add-activity-dialog/add-activity-dialog';
import { ConfirmDialog } from '../../../confirm-dialog/confirm-dialog';
import { EditActivityDialog } from '../../../edit-activity-dialog/edit-activity-dialog';
import { ImportActivityDialog } from '../../../import-activity-dialog/import-activity-dialog';
import {
  activityHasLabel,
  activitySharesType,
  applyLabelFilter as filterActivitiesByLabel,
  directLabels as labelsOnActivity,
  GroupBlock,
  groupFullySelected,
  groupPartlySelected,
  activityLabelIds,
  groupLabelIds,
  labelHasMembers,
  labelSelectionMode as selectionUsesLabels,
  labelToolbarActions,
  labelsForType,
  LabelRemovalPlan,
  planLabelRemoval as buildLabelRemoval,
  planOpenLabelRemoval as buildOpenLabelRemoval,
  scopedLabels,
  selectionNote as describeSelection,
  selectionType as sharedSelectionType,
  syncGroupSelection,
  TypeSection,
} from './activity-structure';

@Component({
  selector: 'app-project-activities-tab',
  standalone: true,
  imports: [FormsModule, MaterialModule, AutoGrowDirective],
  templateUrl: './project-activities-tab.html',
  styleUrl: './project-activities-tab.css',
})
export class ProjectActivitiesTabComponent implements OnChanges, OnDestroy {
  private api = inject(ProjectApiService);
  private dialog = inject(MatDialog);
  private snackBar = inject(MatSnackBar);
  private quotationSync = inject(QuotationSync);
  private purchaseOrderSync = inject(PurchaseOrderSync);
  private host = inject(ElementRef<HTMLElement>);
  private syncSub: Subscription;
  private poSyncSub: Subscription;
  private linkRequest = 0;
  private poRequest = 0;
  private seenProjectId = 0;
  private poLinesLoaded = false;
  private detailState: QuotationDataState | null = null;
  private linksVersion = 0;
  private sectionsKey = Number.NaN;
  private sectionsCache: TypeSection[] = [];
  private labelCounts = new Map<string, number>();
  private lowestByActivity = new Map<number, ActivityQuotationLink>();
  private quoteCountByActivity = new Map<number, number>();

  @Input({ required: true }) project!: Project;
  @Input() types: ActivityType[] = [];
  @Input() activityFilter = '';
  @Input() quoteFilter: 'all' | 'quoted' | 'unquoted' = 'all';
  @Output() reload = new EventEmitter<void>();
  @Output() activitiesChange = new EventEmitter<void>();
  @Output() failed = new EventEmitter<string>();

  onFieldEnter(event: Event) {
    blockEnterSubmit(event);
  }
  isSaving = false;
  deletingId: number | null = null;
  groups: ActivityGroup[] = [];
  labels: ActivityLabel[] = [];
  private labelSelection = new Map<number, number | null>();
  expandedGroupIds = new Set<number>();
  poLines: PurchaseOrderTraceLine[] = [];
  poLinesLoading = false;
  linksLoading = false;
  quotationLinks = new Map<number, ActivityQuotationLink[]>();
  selectedIds = new Set<number>();
  selectedGroupIds = new Set<number>();
  selectionError = '';
  batchSubmitted = false;
  batchError = '';
  readonly searchThreshold = 10;
  private editing = new Set<string>();
  private drafts = new Map<number, ActivityDraft>();

  constructor() {
    this.syncSub = this.quotationSync.changes$.subscribe((projectId) => {
      if (projectId === this.project?.id) {
        this.loadQuotationLinks();
      }
    });
    this.poSyncSub = this.purchaseOrderSync.changes$.subscribe((projectId) => {
      if (projectId === this.project?.id) {
        this.loadPurchaseOrderLines();
      }
    });
  }

  ngOnDestroy() {
    this.syncSub.unsubscribe();
    this.poSyncSub.unsubscribe();
  }

  get hasActivities(): boolean {
    return (this.project?.activities?.length ?? 0) > 0;
  }

  ngOnChanges(changes: SimpleChanges) {
    const project = changes['project']?.currentValue as Project | undefined;
    if (!project?.id) {
      return;
    }
    const idChanged = project.id !== this.seenProjectId;
    this.seenProjectId = project.id;
    this.pruneSelection();
    if (idChanged) {
      this.poLines = [];
      this.poLinesLoaded = false;
      this.poLinesLoading = false;
      this.loadGroups();
      this.loadQuotationLinks();
      return;
    }
    this.loadQuotationLinks();
    this.loadLabels();
    if (this.poLinesLoaded || this.detailState) {
      this.loadPurchaseOrderLines();
    }
  }

  ordersFor(activityId: number): PurchaseOrderTraceLine[] {
    return this.poLines.filter((line) => line.activityId === activityId);
  }

  private ordersForGroup(activities: Activity[]): PurchaseOrderTraceLine[] {
    const ids = new Set(activities.map((activity) => activity.id));
    return this.poLines
      .filter((line) => line.activityId != null && ids.has(line.activityId))
      .sort((left, right) =>
        (left.dsnNo ?? '').localeCompare(right.dsnNo ?? '', undefined, { numeric: true, sensitivity: 'base' })
        || left.code.localeCompare(right.code, undefined, { numeric: true, sensitivity: 'base' })
        || left.lineId - right.lineId);
  }

  revealActivity(activityId: number) {
    const activity = this.project?.activities?.find((item) => item.id === activityId);
    if (!activity) {
      return;
    }
    if (activity.activityGroupId) {
      this.expandedGroupIds.add(activity.activityGroupId);
    }
    this.openActivityDetails(activity);
    setTimeout(() => {
      this.host.nativeElement.querySelector(`[data-activity-id="${activityId}"]`)?.scrollIntoView({ block: 'center' });
    });
  }

  toggleGroup(groupId: number) {
    if (this.expandedGroupIds.has(groupId)) {
      this.expandedGroupIds.delete(groupId);
      return;
    }
    this.expandedGroupIds.add(groupId);
  }

  isGroupOpen(groupId: number): boolean {
    return this.expandedGroupIds.has(groupId);
  }

  lowestLink(activity: Activity): ActivityQuotationLink | null {
    return this.lowestByActivity.get(activity.id) ?? null;
  }

  groupLowestLink(activities: Activity[]): ActivityQuotationLink | null {
    let best: ActivityQuotationLink | null = null;
    for (const activity of activities) {
      const link = this.lowestLink(activity);
      if (!link) {
        continue;
      }
      if (!best || this.comparablePrice(link) < this.comparablePrice(best)) {
        best = link;
      }
    }
    return best;
  }

  quoteCount(activity: Activity): number {
    return this.quoteCountByActivity.get(activity.id) ?? 0;
  }

  groupQuoteCount(activities: Activity[]): number {
    const ids = new Set<number>();
    for (const activity of activities) {
      for (const link of this.quotesFor(activity.id)) {
        ids.add(link.quotationId);
      }
    }
    return ids.size;
  }

  priceText(link: ActivityQuotationLink): string {
    if (link.unitPrice != null) {
      return formatMoney(link.unitPrice, link.currencySymbol, link.currencyCode);
    }
    return formatMoney(link.baseUnitPrice, '$', 'USD');
  }

  openActivityDetails(activity: Activity) {
    const state: QuotationDataState = {
      kind: 'item',
      activityId: activity.id,
      projectId: this.project.id,
      title: 'Activity details',
      subtitle: this.itemSubtitle(activity),
      requiredQuantity: activity.quantity,
      rows: this.itemQuoteRows(activity),
      orders: [],
    };
    this.ensurePurchaseOrders();
    state.orders = this.ordersFor(activity.id);
    state.ordersLoading = this.poLinesLoading;
    this.openQuotationView(state);
  }

  openGroupQuotations(group: ActivityGroup, activities: Activity[]) {
    const state: QuotationDataState = {
      kind: 'group',
      groupId: group.id,
      projectId: this.project.id,
      title: 'Group details',
      subtitle: group.name,
      rows: this.groupQuoteRows(activities),
      orders: [],
    };
    this.ensurePurchaseOrders();
    state.orders = this.ordersForGroup(activities);
    state.ordersLoading = this.poLinesLoading;
    this.openQuotationView(state);
  }

  private openQuotationView(state: QuotationDataState) {
    this.detailState = state;
    const ref = this.dialog.open(QuotationDataDialog, {
      disableClose: true,
      width: '100vw',
      maxWidth: '100vw',
      minWidth: '100vw',
      height: '100vh',
      maxHeight: '100vh',
      panelClass: ['add-project-dialog-panel', 'quotation-dialog-panel', 'po-new-panel', 'app-dialog-pane'],
      backdropClass: 'add-project-dialog-backdrop',
      autoFocus: 'first-tabbable',
      data: state,
    });
    ref.afterClosed().subscribe(() => {
      if (this.detailState === state) {
        this.detailState = null;
      }
    });
  }

  private itemSubtitle(activity: Activity): string {
    const part = activity.mainPartNo || activity.partNo || '—';
    return `DSN ${activity.dsnNo || '—'} · ${part}`;
  }

  private itemQuoteRows(activity: Activity): QuotationDataRow[] {
    return this.quotesFor(activity.id).map((link) => ({
      dsnNo: activity.dsnNo,
      partNo: activity.mainPartNo || activity.partNo || null,
      link,
    }));
  }

  private groupQuoteRows(activities: Activity[]): QuotationDataRow[] {
    return [...activities]
      .sort((left, right) => (left.dsnNo ?? '').localeCompare(right.dsnNo ?? '', undefined, { numeric: true, sensitivity: 'base' }))
      .map((activity) => ({
        dsnNo: activity.dsnNo,
        partNo: this.lowestLink(activity)?.partNo || activity.mainPartNo || activity.partNo || null,
        link: this.lowestLink(activity),
      }));
  }

  get activityGroups(): TypeSection[] {
    const key = this.viewKey();
    if (key !== this.sectionsKey) {
      this.sectionsKey = key;
      const visible = this.visibleProjectActivities();
      this.sectionsCache = this.composeActivityGroups(visible);
      this.labelCounts = this.countLabels(visible);
    }
    return this.sectionsCache;
  }

  private viewKey(): number {
    let hash = 2166136261;
    const mix = (value: number) => {
      hash ^= value | 0;
      hash = Math.imul(hash, 16777619);
    };
    const fold = (value: string | null | undefined) => {
      const text = value ?? '';
      let sum = text.length;
      for (let index = 0; index < text.length; index++) {
        sum = (sum + text.charCodeAt(index)) | 0;
      }
      mix(sum);
    };
    mix(this.project?.id ?? 0);
    mix(this.linksVersion);
    mix(this.types.length);
    mix(this.groups.length);
    mix(this.labels.length);
    fold(this.activityFilter);
    fold(this.quoteFilter);
    for (const [typeId, labelId] of this.labelSelection) {
      mix(typeId);
      mix(labelId ?? 0);
    }
    const activities = this.project?.activities ?? [];
    mix(activities.length);
    for (const activity of activities) {
      mix(activity.id);
      mix(activity.activityGroupId ?? 0);
      mix(activity.typeId ?? 0);
      mix(activity.quantity ?? 0);
      fold(activity.dsnNo);
      fold(activity.partNo);
    }
    for (const group of this.groups) {
      mix(group.id);
      mix(group.typeId ?? 0);
      fold(group.name);
    }
    for (const label of this.labels) {
      mix(label.id);
      mix(label.activityIds.length);
      mix(label.activityGroupIds.length);
    }
    return hash;
  }

  private composeActivityGroups(visible: Activity[]): TypeSection[] {
    const ungrouped = visible.filter((activity) => !activity.activityGroupId);
    const order = this.types.length
      ? this.types.map((type) => type.id)
      : [...new Set(visible.map((activity) => activity.typeId).filter((id): id is number => !!id))];

    const sections: TypeSection[] = [];
    for (const typeId of order) {
      const items = ungrouped.filter((activity) => activity.typeId === typeId);
      const blocks = this.groupBlocks(typeId);
      if (!items.length && !blocks.length) {
        continue;
      }
      const filtered = this.applyLabelFilter(typeId, items, blocks);
      const type = this.types.find((item) => item.id === typeId);
      sections.push({
        typeId,
        typeName: type?.name || items[0]?.type || blocks[0]?.activities[0]?.type || 'Activities',
        activities: filtered.activities,
        groups: filtered.groups.map((block) => ({
          ...block,
          quoteCount: this.groupQuoteCount(block.activities),
          lowest: this.groupLowestLink(block.activities),
        })),
        shownCount: filtered.activities.length + filtered.groups.reduce((total, block) => total + block.activities.length, 0),
      });
    }

    const untyped = ungrouped.filter((activity) => !activity.typeId);
    if (untyped.length) {
      sections.push({ typeId: null, typeName: 'Untyped', activities: untyped, groups: [], shownCount: untyped.length });
    }

    return sections;
  }

  get batchEditing(): boolean {
    return this.editing.size > 0;
  }

  isBatchEditing(typeId: number | null): boolean {
    return this.editing.has(this.typeKey(typeId));
  }

  visibleActivities(group: TypeSection): Activity[] {
    return [
      ...group.activities,
      ...group.groups.flatMap((block) => block.activities),
    ];
  }

  isRowEditable(group: TypeSection, activity: Activity): boolean {
    return this.isBatchEditing(group.typeId) && this.visibleActivities(group).some((item) => item.id === activity.id);
  }

  toggleBatch(group: TypeSection) {
    const key = this.typeKey(group.typeId);
    if (this.editing.has(key)) {
      this.editing.delete(key);
      for (const activity of this.project?.activities ?? []) {
        if ((activity.typeId ?? null) === group.typeId) {
          this.drafts.delete(activity.id);
        }
      }
      if (!this.batchEditing) {
        this.batchError = '';
        this.batchSubmitted = false;
      }
      return;
    }

    this.editing.add(key);
    for (const activity of this.visibleActivities(group)) {
      this.ensureDraft(activity);
    }
  }

  cancelBatch() {
    this.editing.clear();
    this.drafts.clear();
    this.batchError = '';
    this.batchSubmitted = false;
  }

  draft(activity: Activity): ActivityDraft {
    return this.ensureDraft(activity);
  }

  setQuantity(activity: Activity, value: number | string | null) {
    const draft = this.ensureDraft(activity);
    if (value == null || value === '') {
      draft.quantity = null;
      return;
    }
    draft.quantity = Number(value);
  }

  fieldInvalid(activity: Activity, field: 'dsnNo' | 'partNo' | 'quantity'): boolean {
    if (!this.batchSubmitted) {
      return false;
    }
    const draft = this.drafts.get(activity.id);
    if (!draft) {
      return false;
    }
    if (field === 'dsnNo') {
      return !draft.dsnNo.trim();
    }
    if (field === 'partNo') {
      return !draft.partNo.trim();
    }
    return !this.wholeQuantity(draft.quantity);
  }

  saveBatch() {
    if (this.isSaving) {
      return;
    }

    this.batchSubmitted = true;
    this.batchError = '';
    const changed = this.changedDrafts();
    const invalid = changed.some(
      (item) => !item.draft.dsnNo.trim() || !item.draft.partNo.trim() || !this.wholeQuantity(item.draft.quantity),
    );
    if (invalid) {
      this.batchError = 'DSN no, part number, and a whole-number quantity are required on the highlighted rows.';
      return;
    }
    if (changed.length === 0) {
      this.batchSubmitted = false;
      return;
    }

    this.api
      .updateActivities(
        changed.map(({ activity, draft }) => ({
          id: activity.id,
          projectId: activity.projectId,
          dsnNo: draft.dsnNo.trim(),
          partNo: draft.partNo,
          description: draft.description.trim(),
          quantity: draft.quantity,
          budget: activity.budget,
          typeId: activity.typeId,
        })),
      )
      .pipe(whileBusy((busy) => (this.isSaving = busy)))
      .subscribe({
        next: () => {
          this.cancelBatch();
          this.snackBar.open('Activities updated.', undefined, { duration: 3000 });
          this.reload.emit();
        },
        error: (err: HttpErrorResponse) => {
          const message = typeof err.error === 'string' ? err.error : err.error?.message;
          this.batchError = message || 'The activities could not be updated.';
        },
      });
  }

  openImport() {
    const projectId = this.project.id;
    const ref = this.dialog.open(ImportActivityDialog, {
      disableClose: true,
      width: '1080px',
      maxWidth: 'calc(100vw - 32px)',
      maxHeight: '92vh',
      panelClass: 'activity-dialog-panel',
      backdropClass: 'add-project-dialog-backdrop',
      data: { projectId, types: this.types },
    });

    ref.afterClosed().subscribe((saved) => {
      if (saved) {
        this.reload.emit();
      }
    });
  }

  openAddActivity() {
    const projectId = this.project.id;
    const ref = this.dialog.open(AddActivityDialog, {
      disableClose: true,
      width: '1440px',
      maxWidth: 'calc(100vw - 32px)',
      maxHeight: '92vh',
      panelClass: 'activity-dialog-panel',
      backdropClass: 'add-project-dialog-backdrop',
      data: { projectId, types: this.types },
    });

    ref.afterClosed().subscribe((saved) => {
      if (saved) {
        this.reload.emit();
      }
    });
  }

  openEditActivity(activity: Activity) {
    const ref = this.dialog.open(EditActivityDialog, {
      disableClose: true,
      width: '680px',
      maxWidth: 'calc(100vw - 32px)',
      panelClass: 'add-project-dialog-panel',
      backdropClass: 'add-project-dialog-backdrop',
      data: {
        activity,
        types: this.types,
      },
    });

    ref.afterClosed().subscribe((updated: Activity | false | undefined) => {
      if (!updated || !this.project.activities) {
        return;
      }

      this.project.activities = this.project.activities.map((item) => (item.id === updated.id ? updated : item));
      const draft = this.drafts.get(updated.id);
      if (draft) {
        draft.dsnNo = updated.dsnNo ?? '';
        draft.partNo = updated.partNo ?? '';
        draft.description = updated.description ?? '';
        draft.quantity = updated.quantity == null ? null : Number(updated.quantity);
      }
      this.activitiesChange.emit();
    });
  }

  deleteActivity(activity: Activity) {
    if (this.deletingId != null || this.isSaving) {
      return;
    }

    const ref = this.dialog.open(ConfirmDialog, {
      disableClose: true,
      width: '440px',
      maxWidth: 'calc(100vw - 32px)',
      panelClass: 'add-project-dialog-panel',
      backdropClass: 'add-project-dialog-backdrop',
      data: {
        title: 'Delete activity',
        message: 'Are you sure you want to delete this activity?',
        confirmLabel: 'Delete',
      },
    });

    ref.afterClosed().subscribe((confirmed) => {
      if (!confirmed || this.deletingId != null) {
        return;
      }

      this.api.deleteActivity(activity.id).pipe(whileBusy((busy) => {
        this.deletingId = busy ? activity.id : null;
      })).subscribe({
        next: () => {
          this.project.activities = (this.project.activities ?? []).filter((item) => item.id !== activity.id);
          this.drafts.delete(activity.id);
          if (this.selectedIds.delete(activity.id)) {
            this.syncSelectionError();
          }
          this.activitiesChange.emit();
        },
        error: () => this.failed.emit('The activity could not be deleted.'),
      });
    });
  }

  editGroup(group: ActivityGroup) {
    this.openGroupDialog(group);
  }

  ungroup(group: ActivityGroup) {
    if (this.members(group.id).length === 0) {
      return;
    }
    this.confirmUngroup(
      'Ungroup Activities',
      'Ungroup these activities? They will return to the activity list, and this group will be deleted.',
      () => {
        this.api.deleteActivityGroup(this.project.id, group.id).subscribe({
          next: () => this.reload.emit(),
          error: () => this.failed.emit('The group could not be ungrouped.'),
        });
      },
    );
  }

  ungroupActivity(activity: Activity) {
    this.confirmUngroup(
      'Remove from Group',
      'Remove this activity from its group? It will return to the general list. An empty group is deleted.',
      () => this.unassignActivities([activity.id]),
      'Remove from Group',
    );
  }

  directLabels(activity: Activity): ActivityLabel[] {
    return labelsOnActivity(this.labels, activity);
  }

  removeFromLabel(activity: Activity) {
    const labels = this.labelTargets(activity);
    if (!labels.length) {
      return;
    }
    const names = labels.map((label) => label.name).join(', ');
    this.confirmUngroup(
      'Remove from Label',
      `Remove this activity from ${names}? It stays on the project and returns to the full list for this type.`,
      () => this.clearLabels(activity, labels),
      'Remove from Label',
    );
  }

  deleteGroup(group: ActivityGroup) {
    const ref = this.dialog.open(ConfirmDialog, {
      disableClose: true,
      width: '440px',
      maxWidth: 'calc(100vw - 32px)',
      panelClass: 'add-project-dialog-panel',
      backdropClass: 'add-project-dialog-backdrop',
      data: {
        title: 'Delete Group',
        message: 'Delete this group? Activities in it stay on the project as standalone activities.',
        confirmLabel: 'Delete',
      },
    });
    ref.afterClosed().subscribe((confirmed) => {
      if (!confirmed) {
        return;
      }
      this.api.deleteActivityGroup(this.project.id, group.id).subscribe({
        next: () => {
          this.reload.emit();
        },
        error: () => this.failed.emit('The group could not be deleted.'),
      });
    });
  }

  isSelected(activity: Activity): boolean {
    return this.selectedIds.has(activity.id);
  }

  get selectingGrouped(): boolean {
    return this.selectedActivities().some((activity) => !!activity.activityGroupId);
  }

  get labelSelectionMode(): boolean {
    return selectionUsesLabels(this.selectedGroupIds.size, this.selectedActivities());
  }

  get selectionNote(): string {
    return describeSelection(this.selectedIds.size, this.selectedGroupIds.size);
  }

  isSelectionDisabled(activity: Activity): boolean {
    if (this.isSaving || this.deletingId != null) {
      return true;
    }
    const locked = this.selectionType();
    if (locked === undefined || this.isSelected(activity)) {
      return false;
    }
    return (activity.typeId ?? null) !== locked;
  }

  isGroupSelected(group: ActivityGroup): boolean {
    return this.selectedGroupIds.has(group.id);
  }

  isGroupChecked(group: ActivityGroup): boolean {
    return groupFullySelected(this.members(group.id), this.selectedIds);
  }

  isGroupIndeterminate(group: ActivityGroup): boolean {
    return groupPartlySelected(this.members(group.id), this.selectedIds);
  }

  isGroupSelectionDisabled(group: ActivityGroup): boolean {
    if (this.isSaving || this.deletingId != null || group.typeId == null) {
      return true;
    }
    const locked = this.selectionType();
    if (locked === undefined || this.isGroupChecked(group) || this.isGroupIndeterminate(group)) {
      return false;
    }
    return group.typeId !== locked;
  }

  toggleGroupSelected(group: ActivityGroup, checked: boolean) {
    if (checked && this.isGroupSelectionDisabled(group)) {
      return;
    }
    for (const activity of this.members(group.id)) {
      if (checked) {
        if (!this.isSelectionDisabled(activity)) {
          this.selectedIds.add(activity.id);
        }
      } else {
        this.selectedIds.delete(activity.id);
      }
    }
    this.syncParentGroup(group.id);
    this.syncSelectionError();
  }

  toggleSelected(activity: Activity, checked: boolean) {
    if (checked && this.isSelectionDisabled(activity)) {
      return;
    }
    if (checked) {
      this.selectedIds.add(activity.id);
    } else {
      this.selectedIds.delete(activity.id);
    }
    if (activity.activityGroupId != null) {
      this.syncParentGroup(activity.activityGroupId);
    }
    this.syncSelectionError();
  }

  private syncParentGroup(groupId: number) {
    syncGroupSelection(groupId, this.members(groupId), this.selectedIds, this.selectedGroupIds);
  }

  clearSelection() {
    this.selectedIds.clear();
    this.selectedGroupIds.clear();
    this.syncSelectionError();
  }

  removeSelectedFromGroups() {
    const ids = this.selectedActivities()
      .filter((activity) => !!activity.activityGroupId)
      .map((activity) => activity.id);
    if (ids.length === 0) {
      return;
    }
    this.unassignActivities(ids);
  }

  assignSelected() {
    if (this.selectedIds.size === 0 || this.selectingGrouped) {
      return;
    }
    const selected = this.selectedActivities();
    if (!activitySharesType(selected)) {
      this.selectionError = 'Activities in a group must share the same type.';
      this.failed.emit(this.selectionError);
      return;
    }
    this.syncSelectionError();
    const ref = this.dialog.open(AssignGroupDialog, {
      disableClose: true,
      width: '90vw',
      maxWidth: '520px',
      minWidth: 'min(440px, 96vw)',
      maxHeight: '90vh',
      panelClass: ['add-project-dialog-panel', 'app-dialog-pane'],
      backdropClass: 'add-project-dialog-backdrop',
      data: { groups: this.compatibleGroups(selected[0].typeId) },
    });
    ref.afterClosed().subscribe((result: number | { name: string } | false) => {
      if (!result) {
        return;
      }
      const ids = [...this.selectedIds];
      const apply = (groupId: number) => {
        this.api.assignActivities(this.project.id, ids, groupId).subscribe({
          next: () => {
            this.selectedIds.clear();
            this.reload.emit();
          },
          error: (error: HttpErrorResponse) => this.failed.emit(error.error?.message || 'The activities could not be added to the group.'),
        });
      };
      if (typeof result === 'number') {
        apply(result);
        return;
      }
      this.api.createActivityGroup(this.project.id, result.name, '').subscribe({
        next: (group) => apply(group.id),
        error: () => this.failed.emit('The group could not be created.'),
      });
    });
  }

  openGroupDetail(groupId: number | null | undefined) {
    const group = this.groups.find((item) => item.id === groupId);
    if (!group) {
      return;
    }
    const ref = this.dialog.open(GroupDetailDialog, {
      disableClose: true,
      width: '90vw',
      maxWidth: '960px',
      minWidth: 'min(560px, 96vw)',
      maxHeight: '90vh',
      panelClass: ['add-project-dialog-panel', 'app-dialog-pane'],
      backdropClass: 'add-project-dialog-backdrop',
      data: {
        group,
        activities: (this.project.activities ?? []).filter((activity) => activity.activityGroupId === group.id),
        canDelete: group.typeId == null || !(this.project.activities ?? []).some((activity) => activity.activityGroupId === group.id),
      },
    });
    ref.afterClosed().subscribe((action) => {
      if (action === 'edit') {
        this.editGroup(group);
      }
      if (action === 'ungroup') {
        this.ungroup(group);
      }
      if (action === 'delete') {
        this.deleteGroup(group);
      }
    });
  }

  assignToGroup(activity: Activity) {
    if (!activitySharesType([activity])) {
      this.failed.emit('Activities in a group must share the same type.');
      return;
    }
    const ref = this.dialog.open(AssignGroupDialog, {
      disableClose: true,
      width: '90vw',
      maxWidth: '520px',
      minWidth: 'min(440px, 96vw)',
      maxHeight: '90vh',
      panelClass: ['add-project-dialog-panel', 'app-dialog-pane'],
      backdropClass: 'add-project-dialog-backdrop',
      data: { groups: this.compatibleGroups(activity.typeId) },
    });
    ref.afterClosed().subscribe((result: number | { name: string } | false) => {
      if (!result) {
        return;
      }
      const apply = (groupId: number) => {
        this.api.assignActivityGroup(this.project.id, activity.id, groupId).subscribe({
          next: () => this.reload.emit(),
          error: (error: HttpErrorResponse) => this.failed.emit(error.error?.message || 'The activity could not be added to the group.'),
        });
      };
      if (typeof result === 'number') {
        apply(result);
        return;
      }
      this.api.createActivityGroup(this.project.id, result.name, '').subscribe({
        next: (group) => apply(group.id),
        error: () => this.failed.emit('The group could not be created.'),
      });
    });
  }

  removeFromGroup(activity: Activity) {
    this.api.assignActivityGroup(this.project.id, activity.id, null).subscribe({
      next: () => this.reload.emit(),
      error: () => this.failed.emit('The activity could not be removed from the group.'),
    });
  }

  labelsFor(typeId: number | null): ActivityLabel[] {
    return labelsForType(this.labels, typeId);
  }

  isLabelSelected(typeId: number | null, labelId: number | null): boolean {
    if (typeId == null) {
      return labelId == null;
    }
    return (this.labelSelection.get(typeId) ?? null) === labelId;
  }

  selectLabel(typeId: number | null, labelId: number | null) {
    if (typeId == null) {
      return;
    }
    if (labelId == null) {
      this.labelSelection.delete(typeId);
      return;
    }
    this.labelSelection.set(typeId, labelId);
  }

  labelMatchCount(typeId: number | null, label: ActivityLabel | null): number {
    if (typeId == null) {
      return 0;
    }
    this.activityGroups;
    return this.labelCounts.get(label ? `${typeId}:${label.id}` : `${typeId}:`) ?? 0;
  }

  private countLabels(visible: Activity[]): Map<string, number> {
    const counts = new Map<string, number>();
    const labelsByType = new Map<number, ActivityLabel[]>();
    for (const label of this.labels) {
      if (label.typeId == null) {
        continue;
      }
      const list = labelsByType.get(label.typeId) ?? [];
      list.push(label);
      labelsByType.set(label.typeId, list);
    }
    for (const activity of visible) {
      if (activity.typeId == null) {
        continue;
      }
      const allKey = `${activity.typeId}:`;
      counts.set(allKey, (counts.get(allKey) ?? 0) + 1);
      for (const label of labelsByType.get(activity.typeId) ?? []) {
        if (activityHasLabel(activity, label)) {
          const key = `${activity.typeId}:${label.id}`;
          counts.set(key, (counts.get(key) ?? 0) + 1);
        }
      }
    }
    return counts;
  }

  get labelActions(): { assign: boolean; remove: boolean } {
    const typeId = this.selectionType();
    const activeLabelId = typeof typeId === 'number' ? (this.labelSelection.get(typeId) ?? null) : null;
    const memberships = [
      ...this.selectedActivities().map((activity) => activityLabelIds(this.labels, activity)),
      ...this.selectedGroups().map((group) => groupLabelIds(this.labels, group)),
    ];
    return labelToolbarActions(activeLabelId, memberships);
  }

  assignSelectedLabels() {
    const typeId = this.selectionType();
    if (typeId == null) {
      this.selectionError = 'A label can only be attached within one activity type.';
      this.failed.emit(this.selectionError);
      return;
    }
    this.syncSelectionError();
    this.openLabelDialog(typeId, [...this.selectedIds], [...this.selectedGroupIds]);
  }

  removeSelectedFromLabels() {
    const label = this.activeFilterLabel();
    const plan = label ? this.planLabelRemoval(label) : this.planOpenLabelRemoval();
    if (!plan.activityIds.length && !plan.groupIds.length) {
      return;
    }
    const target = label ? label.name : 'their labels';
    this.confirmUngroup(
      'Remove from Label',
      `Remove the selection from ${target}? Activities stay in their group or on the type list.`,
      () => this.applyLabelRemoval(label, plan),
      'Remove from Label',
    );
  }

  private activeFilterLabel(): ActivityLabel | null {
    const typeId = this.selectionType();
    if (typeof typeId !== 'number') {
      return null;
    }
    const labelId = this.labelSelection.get(typeId) ?? null;
    if (labelId == null) {
      return null;
    }
    return this.labels.find((label) => label.id === labelId && label.typeId === typeId) ?? null;
  }

  private planOpenLabelRemoval(): LabelRemovalPlan {
    return buildOpenLabelRemoval(
      this.selectedActivities(),
      this.selectedGroups(),
      (activity) => this.directLabels(activity),
      (group) => this.groupLabelTargets(group),
    );
  }

  private planLabelRemoval(label: ActivityLabel): LabelRemovalPlan {
    return buildLabelRemoval(
      label,
      this.selectedActivities(),
      this.selectedGroups(),
      this.selectedIds,
      this.selectedGroupIds,
      (groupId) => this.members(groupId),
    );
  }

  private applyLabelRemoval(label: ActivityLabel | null, plan: LabelRemovalPlan) {
    const remove = () => {
      this.api.removeLabelMembers(this.project.id, plan.activityIds, plan.groupIds, label?.id ?? null).subscribe({
        next: () => {
          this.clearSelection();
          this.loadLabels();
        },
        error: () => this.failed.emit('The selection could not be removed from the label.'),
      });
    };
    this.keepLabelOn(label, plan.keepers, remove);
  }

  private keepLabelOn(label: ActivityLabel | null, keepers: Activity[], done: () => void) {
    if (!label || keepers.length === 0) {
      done();
      return;
    }
    const batches = new Map<string, { ids: number[]; labelIds: number[] }>();
    for (const activity of keepers) {
      const labelIds = [...new Set([...this.directLabels(activity).map((item) => item.id), label.id])].sort((left, right) => left - right);
      const key = labelIds.join(',');
      const batch = batches.get(key) ?? { ids: [], labelIds };
      batch.ids.push(activity.id);
      batches.set(key, batch);
    }
    const queue = [...batches.values()];
    const next = () => {
      const batch = queue.shift();
      if (!batch) {
        done();
        return;
      }
      this.api.setActivityLabels(this.project.id, batch.ids, batch.labelIds).subscribe({
        next: () => next(),
        error: () => this.failed.emit('The selection could not be removed from the label.'),
      });
    };
    next();
  }

  groupLabelTargets(group: ActivityGroup): ActivityLabel[] {
    const labels = this.labels
      .filter((label) => label.typeId === group.typeId && (label.activityGroupIds ?? []).includes(group.id))
      .sort((left, right) => left.name.localeCompare(right.name));
    const activeId = group.typeId == null ? null : (this.labelSelection.get(group.typeId) ?? null);
    return scopedLabels(labels, activeId);
  }

  removeGroupFromLabel(group: ActivityGroup) {
    const labels = this.groupLabelTargets(group);
    if (!labels.length) {
      return;
    }
    const names = labels.map((label) => label.name).join(', ');
    const labelId = labels.length === 1 ? labels[0].id : null;
    this.confirmUngroup(
      'Remove from Label',
      `Remove ${group.name} from ${names}? The group stays under its activity type.`,
      () => {
        this.api.removeLabelMembers(this.project.id, [], [group.id], labels.length === 1 ? labelId : null).subscribe({
          next: () => this.loadLabels(),
          error: () => this.failed.emit('The group could not be removed from the label.'),
        });
      },
      'Remove from Label',
    );
  }

  private openLabelDialog(typeId: number | null | undefined, activityIds: number[], groupIds: number[]) {
    if (typeId == null) {
      this.failed.emit('A label can only be attached within one activity type.');
      return;
    }
    const ref = this.dialog.open(AssignLabelDialog, {
      disableClose: true,
      width: '90vw',
      maxWidth: '520px',
      minWidth: 'min(440px, 96vw)',
      maxHeight: '90vh',
      panelClass: ['add-project-dialog-panel', 'app-dialog-pane'],
      backdropClass: 'add-project-dialog-backdrop',
      data: { labels: this.labelsFor(typeId), activityIds, groupIds },
    });
    ref.afterClosed().subscribe((result: { labelIds: number[]; name: string } | false) => {
      if (!result) {
        return;
      }
      const apply = (labelIds: number[]) => {
        this.saveLabelMembers(activityIds, groupIds, labelIds);
      };
      const name = result.name?.trim();
      if (!name) {
        apply(result.labelIds);
        return;
      }
      this.api.createActivityLabel(this.project.id, name, typeId).subscribe({
        next: (label) => apply([...result.labelIds, label.id]),
        error: (error: HttpErrorResponse) => this.failed.emit(error.error?.message || 'The label could not be created.'),
      });
    });
  }

  private saveLabelMembers(activityIds: number[], groupIds: number[], labelIds: number[]) {
    const finish = () => {
      this.clearSelection();
      this.loadLabels();
    };
    const saveGroups = () => {
      if (!groupIds.length) {
        finish();
        return;
      }
      this.api.setGroupLabels(this.project.id, groupIds, labelIds).subscribe({
        next: finish,
        error: (error: HttpErrorResponse) => this.failed.emit(error.error?.message || 'The labels could not be saved.'),
      });
    };
    if (!activityIds.length) {
      saveGroups();
      return;
    }
    this.api.setActivityLabels(this.project.id, activityIds, labelIds).subscribe({
      next: saveGroups,
      error: (error: HttpErrorResponse) => this.failed.emit(error.error?.message || 'The labels could not be saved.'),
    });
  }

  private selectedGroups(): ActivityGroup[] {
    return this.groups.filter((group) => this.selectedGroupIds.has(group.id));
  }

  private selectionType(): number | null | undefined {
    return sharedSelectionType(this.selectedActivities(), this.selectedGroups());
  }

  private loadGroups() {
    this.api.activityGroups(this.project.id).subscribe({
      next: (groups) => {
        this.groups = groups;
        const live = new Set(groups.map((group) => group.id));
        for (const id of [...this.selectedGroupIds]) {
          if (!live.has(id)) {
            this.selectedGroupIds.delete(id);
          }
        }
        for (const group of groups) {
          this.syncParentGroup(group.id);
        }
        this.loadLabels();
      },
      error: () => this.loadLabels(),
    });
  }

  private loadLabels() {
    this.api.activityLabels(this.project.id).subscribe({
      next: (labels) => {
        this.labels = (labels ?? []).filter((label) => labelHasMembers(label));
        for (const [typeId, labelId] of [...this.labelSelection]) {
          if (labelId != null && !this.labels.some((label) => label.id === labelId && label.typeId === typeId)) {
            this.labelSelection.delete(typeId);
          }
        }
      },
      error: () => this.failed.emit('Labels could not be loaded.'),
    });
  }

  private applyLabelFilter(typeId: number, activities: Activity[], groups: GroupBlock[]): { activities: Activity[]; groups: GroupBlock[] } {
    const labelId = this.labelSelection.get(typeId) ?? null;
    const label = labelId == null ? null : this.labels.find((item) => item.id === labelId && item.typeId === typeId) ?? null;
    return filterActivitiesByLabel(label ?? null, activities, groups);
  }

  private loadPurchaseOrderLines() {
    if (!this.project?.id) {
      this.poLines = [];
      this.poLinesLoading = false;
      return;
    }
    const request = ++this.poRequest;
    const projectId = this.project.id;
    this.poLinesLoading = true;
    if (this.detailState) {
      this.detailState.ordersLoading = true;
    }
    this.api.purchaseOrderLines(projectId).subscribe({
      next: (lines) => {
        if (request !== this.poRequest || projectId !== this.project?.id) {
          return;
        }
        this.poLines = lines ?? [];
        this.poLinesLoaded = true;
        this.poLinesLoading = false;
        this.publishDetail();
      },
      error: () => {
        if (request !== this.poRequest || projectId !== this.project?.id) {
          return;
        }
        this.poLines = [];
        this.poLinesLoaded = true;
        this.poLinesLoading = false;
        this.publishDetail();
      },
    });
  }

  private loadQuotationLinks() {
    if (!this.project?.id) {
      return;
    }
    const request = ++this.linkRequest;
    const projectId = this.project.id;
    this.linksLoading = true;
    this.api.quotationLinks(projectId).subscribe({
      next: (links) => {
        if (request !== this.linkRequest || projectId !== this.project?.id) {
          return;
        }
        this.quotationLinks = this.groupQuotes(links);
        this.linksLoading = false;
        this.publishDetail();
      },
      error: () => {
        if (request !== this.linkRequest || projectId !== this.project?.id) {
          return;
        }
        this.linksLoading = false;
      },
    });
  }

  private quotesFor(activityId: number): ActivityQuotationLink[] {
    return this.quotationLinks.get(activityId) ?? [];
  }

  private groupQuotes(links: ActivityQuotationLink[]): Map<number, ActivityQuotationLink[]> {
    const grouped = new Map<number, ActivityQuotationLink[]>();
    this.lowestByActivity = new Map<number, ActivityQuotationLink>();
    this.quoteCountByActivity = new Map<number, number>();
    for (const link of links) {
      if (link.unitPrice == null && link.baseUnitPrice == null) {
        continue;
      }
      const rows = grouped.get(link.activityId) ?? [];
      rows.push(link);
      grouped.set(link.activityId, rows);
    }
    for (const [activityId, rows] of grouped) {
      rows.sort((left, right) => (this.comparablePrice(left) - this.comparablePrice(right)) || (left.quotationId - right.quotationId) || ((left.itemId ?? 0) - (right.itemId ?? 0)));
      const quotations = new Set<number>();
      rows.forEach((row, index) => {
        row.lowest = index === 0;
        quotations.add(row.quotationId);
      });
      grouped.set(activityId, rows);
      if (rows[0]) {
        this.lowestByActivity.set(activityId, rows[0]);
      }
      this.quoteCountByActivity.set(activityId, quotations.size);
    }
    this.linksVersion++;
    return grouped;
  }

  private comparablePrice(link: ActivityQuotationLink): number {
    const base = link.baseUnitPrice ?? link.unitPrice;
    return base == null ? Number.POSITIVE_INFINITY : Number(base);
  }

  private publishDetail() {
    const state = this.detailState;
    if (!state) {
      return;
    }
    if (state.kind === 'item' && state.activityId) {
      const activity = (this.project.activities ?? []).find((item) => item.id === state.activityId);
      if (!activity) {
        return;
      }
      state.subtitle = this.itemSubtitle(activity);
      state.requiredQuantity = activity.quantity;
      state.rows = this.itemQuoteRows(activity);
      state.orders = this.ordersFor(activity.id);
      state.ordersLoading = this.poLinesLoading;
      return;
    }
    if (state.kind === 'group' && state.groupId) {
      const group = this.groups.find((item) => item.id === state.groupId);
      if (group) {
        state.subtitle = group.name;
      }
      const activities = (this.project.activities ?? []).filter((item) => item.activityGroupId === state.groupId);
      state.projectId = this.project.id;
      state.rows = this.groupQuoteRows(activities);
      state.orders = this.ordersForGroup(activities);
      state.ordersLoading = this.poLinesLoading;
    }
  }

  private ensurePurchaseOrders() {
    if (!this.poLinesLoaded && !this.poLinesLoading) {
      this.loadPurchaseOrderLines();
    }
  }

  private openGroupDialog(group?: ActivityGroup) {
    const ref = this.dialog.open(ActivityGroupDialog, {
      disableClose: true,
      width: '520px',
      maxWidth: 'calc(100vw - 32px)',
      panelClass: 'add-project-dialog-panel',
      backdropClass: 'add-project-dialog-backdrop',
      data: { group },
    });
    ref.afterClosed().subscribe((saved) => {
      if (!saved?.name) {
        return;
      }
      const request = group
        ? this.api.updateActivityGroup(this.project.id, group.id, saved.name, saved.description ?? '')
        : this.api.createActivityGroup(this.project.id, saved.name, saved.description ?? '');
      request.subscribe({
        next: () => this.loadGroups(),
        error: () => this.failed.emit('The group could not be saved.'),
      });
    });
  }

  private pruneSelection() {
    const live = new Set((this.project?.activities ?? []).map((activity) => activity.id));
    let removed = false;
    for (const id of [...this.selectedIds]) {
      if (!live.has(id)) {
        this.selectedIds.delete(id);
        removed = true;
      }
    }
    for (const group of this.groups) {
      this.syncParentGroup(group.id);
    }
    if (removed) {
      this.syncSelectionError();
    }
  }

  private syncSelectionError() {
    this.selectionError = '';
    this.failed.emit('');
  }

  labelTargets(activity: Activity): ActivityLabel[] {
    const activeId = activity.typeId == null ? null : (this.labelSelection.get(activity.typeId) ?? null);
    return scopedLabels(this.directLabels(activity), activeId);
  }

  private clearLabels(activity: Activity, labels: ActivityLabel[]) {
    const request = labels.length === 1
      ? this.api.removeActivityLabel(this.project.id, activity.id, labels[0].id)
      : this.api.removeActivityLabels(this.project.id, activity.id);
    request.subscribe({
      next: () => this.loadLabels(),
      error: () => this.failed.emit('The activity could not be removed from the label.'),
    });
  }

  private confirmUngroup(title: string, message: string, onConfirm: () => void, confirmLabel = 'Ungroup') {
    const ref = this.dialog.open(ConfirmDialog, {
      disableClose: true,
      width: '440px',
      maxWidth: 'calc(100vw - 32px)',
      panelClass: 'add-project-dialog-panel',
      backdropClass: 'add-project-dialog-backdrop',
      data: {
        title,
        message,
        confirmLabel,
      },
    });
    ref.afterClosed().subscribe((confirmed) => {
      if (confirmed) {
        onConfirm();
      }
    });
  }

  private unassignActivities(ids: number[]) {
    this.api.assignActivities(this.project.id, ids, null).subscribe({
      next: () => {
        ids.forEach((id) => this.selectedIds.delete(id));
        this.syncSelectionError();
        this.reload.emit();
      },
      error: () => this.failed.emit('The activities could not be removed from their groups.'),
    });
  }

  private selectedActivities(): Activity[] {
    return (this.project.activities ?? []).filter((activity) => this.selectedIds.has(activity.id));
  }

  private compatibleGroups(typeId: number | null | undefined): ActivityGroup[] {
    return this.groups.filter((group) => group.typeId == null || group.typeId === typeId);
  }

  private visibleProjectActivities(): Activity[] {
    const query = this.activeFilter;
    return [...(this.project?.activities ?? [])]
      .filter((activity) => !query || this.matchesFilter(activity, query))
      .filter((activity) => this.matchesQuote(activity))
      .sort((left, right) => compareDsnNo(left.dsnNo, right.dsnNo));
  }

  private memberIndexSource: Activity[] | null = null;
  private membersByGroup = new Map<number, Activity[]>();

  private members(groupId: number): Activity[] {
    const activities = this.project?.activities ?? [];
    if (this.memberIndexSource !== activities) {
      const grouped = new Map<number, Activity[]>();
      for (const activity of activities) {
        if (!activity.activityGroupId) {
          continue;
        }
        const list = grouped.get(activity.activityGroupId) ?? [];
        list.push(activity);
        grouped.set(activity.activityGroupId, list);
      }
      this.memberIndexSource = activities;
      this.membersByGroup = grouped;
    }
    return this.membersByGroup.get(groupId) ?? [];
  }

  private visibleMembers(groupId: number): Activity[] {
    const query = this.activeFilter;
    return this.members(groupId)
      .filter((activity) => !query || this.matchesFilter(activity, query))
      .filter((activity) => this.matchesQuote(activity))
      .sort((left, right) => compareDsnNo(left.dsnNo, right.dsnNo));
  }

  private matchesQuote(activity: Activity): boolean {
    const quoted = this.quotesFor(activity.id).length > 0;
    if (this.quoteFilter === 'quoted') {
      return quoted;
    }
    if (this.quoteFilter === 'unquoted') {
      return !quoted;
    }
    return true;
  }

  private groupBlocks(typeId: number): GroupBlock[] {
    return this.groups
      .filter((group) => {
        if (group.typeId === typeId) {
          return true;
        }
        return group.typeId == null && this.members(group.id).some((activity) => activity.typeId === typeId);
      })
      .map((group) => ({ group, activities: this.visibleMembers(group.id) }))
      .filter((block) => block.activities.length > 0)
      .sort((left, right) => left.group.name.localeCompare(right.group.name));
  }

  private typeKey(typeId: number | null): string {
    return typeId == null ? 'untyped' : String(typeId);
  }

  private ensureDraft(activity: Activity): ActivityDraft {
    const existing = this.drafts.get(activity.id);
    if (existing) {
      return existing;
    }
    const draft: ActivityDraft = {
      dsnNo: activity.dsnNo ?? '',
      partNo: activity.partNo ?? '',
      description: activity.description ?? '',
      quantity: activity.quantity == null ? null : Number(activity.quantity),
    };
    this.drafts.set(activity.id, draft);
    return draft;
  }

  private changedDrafts(): Array<{ activity: Activity; draft: ActivityDraft }> {
    const editingIds = new Set<number>();
    for (const group of this.activityGroups) {
      if (!this.isBatchEditing(group.typeId)) {
        continue;
      }
      for (const activity of this.visibleActivities(group)) {
        editingIds.add(activity.id);
      }
    }

    const changed: Array<{ activity: Activity; draft: ActivityDraft }> = [];
    for (const activity of this.project?.activities ?? []) {
      if (!editingIds.has(activity.id)) {
        continue;
      }
      const draft = this.drafts.get(activity.id);
      if (!draft || !this.draftChanged(activity, draft)) {
        continue;
      }
      changed.push({ activity, draft });
    }
    return changed;
  }

  private draftChanged(activity: Activity, draft: ActivityDraft): boolean {
    const quantity = activity.quantity == null ? null : Number(activity.quantity);
    return (activity.dsnNo ?? '') !== draft.dsnNo.trim()
      || (activity.partNo ?? '') !== draft.partNo
      || (activity.description ?? '') !== draft.description.trim()
      || quantity !== draft.quantity;
  }

  private wholeQuantity(value: number | null): boolean {
    return value != null && Number.isInteger(value) && value >= 0;
  }

  private get activeFilter(): string {
    if ((this.project?.activities?.length ?? 0) <= this.searchThreshold) {
      return '';
    }
    return this.activityFilter.trim().toLowerCase();
  }

  private matchesFilter(activity: Activity, query: string): boolean {
    const fields = [
      activity.dsnNo,
      activity.partNo,
      activity.mainPartNo,
      ...(activity.alternativePartNos ?? []),
      activity.description,
    ];
    return fields.some((value) => (value ?? '').toLowerCase().includes(query));
  }

}

interface ActivityDraft {
  dsnNo: string;
  partNo: string;
  description: string;
  quantity: number | null;
}

export function compareDsnNo(left: string | null | undefined, right: string | null | undefined) {
  const dsnA = left || '';
  const dsnB = right || '';
  return dsnA.localeCompare(dsnB, undefined, {
    numeric: true,
    sensitivity: 'base',
  });
}
