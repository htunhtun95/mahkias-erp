import { HttpErrorResponse } from '@angular/common/http';
import { Component, EventEmitter, Input, OnChanges, OnDestroy, Output, SimpleChanges, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatDialog } from '@angular/material/dialog';
import { MatSnackBar } from '@angular/material/snack-bar';
import { Subscription } from 'rxjs';
import { MaterialModule } from '../../../../app/material-module';
import { AutoGrowDirective } from '../../../../core/auto-grow';
import { blockEnterSubmit } from '../../../../core/block-enter-submit';
import { whileBusy } from '../../../../core/while-busy';
import { Activity, ActivityGroup, ActivityLabel, ActivityQuotationLink, ActivityType, Project, ProjectApiService } from '../../../../core/services/project-api.service';
import { QuotationDataDialog, QuotationDataRow, QuotationDataState } from './quotation-data-dialog';
import { formatMoney } from '../../../money-format';
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
  private syncSub: Subscription;
  private linkRequest = 0;
  private detailState: QuotationDataState | null = null;

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
  }

  ngOnDestroy() {
    this.syncSub.unsubscribe();
  }

  get hasActivities(): boolean {
    return (this.project?.activities?.length ?? 0) > 0;
  }

  ngOnChanges(changes: SimpleChanges) {
    if (changes['project']?.currentValue?.id) {
      this.pruneSelection();
      this.loadGroups();
      this.loadQuotationLinks();
    }
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
    return this.quotesFor(activity.id).find((link) => link.lowest) ?? null;
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

  priceText(link: ActivityQuotationLink): string {
    if (link.unitPrice != null) {
      return formatMoney(link.unitPrice, link.currencySymbol, link.currencyCode);
    }
    return formatMoney(link.baseUnitPrice, '$', 'USD');
  }

  openItemQuotations(activity: Activity) {
    const state: QuotationDataState = {
      kind: 'item',
      activityId: activity.id,
      title: 'Quotations',
      subtitle: this.itemSubtitle(activity),
      requiredQuantity: activity.quantity,
      rows: this.itemQuoteRows(activity),
    };
    this.openQuotationView(state);
  }

  openGroupQuotations(group: ActivityGroup, activities: Activity[]) {
    const state: QuotationDataState = {
      kind: 'group',
      groupId: group.id,
      title: 'Quotations',
      subtitle: group.name,
      rows: this.groupQuoteRows(activities),
    };
    this.openQuotationView(state);
  }

  private openQuotationView(state: QuotationDataState) {
    this.detailState = state;
    const ref = this.dialog.open(QuotationDataDialog, {
      disableClose: true,
      width: '95vw',
      maxWidth: '1200px',
      maxHeight: '90vh',
      panelClass: ['add-project-dialog-panel', 'app-dialog-pane'],
      backdropClass: 'add-project-dialog-backdrop',
      autoFocus: 'first-tabbable',
      data: state,
    });
    ref.afterClosed().subscribe(() => {
      if (this.detailState === state) {
        this.detailState = null;
      }
    });
    this.loadQuotationLinks();
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
    const visible = this.visibleProjectActivities();
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
        groups: filtered.groups,
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
      width: '840px',
      maxWidth: 'calc(100vw - 32px)',
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
      width: '1200px',
      maxWidth: 'calc(100vw - 32px)',
      maxHeight: '85vh',
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
      width: '520px',
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
    return this.visibleProjectActivities().filter((activity) => {
      if (activity.typeId !== typeId) {
        return false;
      }
      return label == null || activityHasLabel(activity, label);
    }).length;
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

  private loadQuotationLinks() {
    if (!this.project?.id) {
      return;
    }
    const request = ++this.linkRequest;
    const projectId = this.project.id;
    this.api.quotationLinks(projectId).subscribe({
      next: (links) => {
        if (request !== this.linkRequest || projectId !== this.project?.id) {
          return;
        }
        this.quotationLinks = this.groupQuotes(links);
        this.publishDetail();
      },
    });
  }

  private quotesFor(activityId: number): ActivityQuotationLink[] {
    return this.quotationLinks.get(activityId) ?? [];
  }

  private groupQuotes(links: ActivityQuotationLink[]): Map<number, ActivityQuotationLink[]> {
    const grouped = new Map<number, ActivityQuotationLink[]>();
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
      rows.forEach((row, index) => {
        row.lowest = index === 0;
      });
      grouped.set(activityId, rows);
    }
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
      return;
    }
    if (state.kind === 'group' && state.groupId) {
      const group = this.groups.find((item) => item.id === state.groupId);
      if (group) {
        state.subtitle = group.name;
      }
      const activities = (this.project.activities ?? []).filter((item) => item.activityGroupId === state.groupId);
      state.rows = this.groupQuoteRows(activities);
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

  private members(groupId: number): Activity[] {
    return (this.project.activities ?? []).filter((activity) => activity.activityGroupId === groupId);
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
