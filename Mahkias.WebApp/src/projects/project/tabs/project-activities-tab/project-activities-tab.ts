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
import { Activity, ActivityGroup, ActivityQuotationLink, ActivityType, Project, ProjectApiService } from '../../../../core/services/project-api.service';
import { ActivityDetailDialog, ActivityDetailState } from './activity-detail-dialog';
import { QuotationSync } from '../../../quotation-sync';
import { ActivityGroupDialog } from './activity-group-dialog';
import { AssignGroupDialog } from './assign-group-dialog';
import { GroupDetailDialog } from './group-detail-dialog';
import { AddActivityDialog } from '../../../add-activity-dialog/add-activity-dialog';
import { ConfirmDialog } from '../../../confirm-dialog/confirm-dialog';
import { EditActivityDialog } from '../../../edit-activity-dialog/edit-activity-dialog';
import { ImportActivityDialog } from '../../../import-activity-dialog/import-activity-dialog';

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
  private detailState: ActivityDetailState | null = null;

  @Input({ required: true }) project!: Project;
  @Input() types: ActivityType[] = [];
  @Input() activityFilter = '';
  @Output() reload = new EventEmitter<void>();
  @Output() activitiesChange = new EventEmitter<void>();
  @Output() failed = new EventEmitter<string>();

  onFieldEnter(event: Event) {
    blockEnterSubmit(event);
  }
  isSaving = false;
  deletingId: number | null = null;
  groups: ActivityGroup[] = [];
  expandedGroupIds = new Set<number>();
  quotationLinks = new Map<number, ActivityQuotationLink[]>();
  selectedIds = new Set<number>();
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
      this.syncDetailActivity();
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

  groupTypeName(group: ActivityGroup): string {
    if (group.typeId == null) {
      return '';
    }
    return this.types.find((type) => type.id === group.typeId)?.name ?? '';
  }

  openDetails(activity: Activity) {
    const state: ActivityDetailState = {
      activity,
      links: this.quotesFor(activity.id),
    };
    this.detailState = state;
    const ref = this.dialog.open(ActivityDetailDialog, {
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
      const type = this.types.find((item) => item.id === typeId);
      sections.push({
        typeId,
        typeName: type?.name || items[0]?.type || blocks[0]?.activities[0]?.type || 'Activities',
        activities: items,
        groups: blocks,
      });
    }

    const untyped = ungrouped.filter((activity) => !activity.typeId);
    if (untyped.length) {
      sections.push({ typeId: null, typeName: 'Untyped', activities: untyped, groups: [] });
    }

    return sections;
  }

  get globalGroups(): GroupBlock[] {
    const query = this.activeFilter;
    return this.groups
      .map((group) => ({
        group,
        activities: this.visibleMembers(group.id),
        total: this.members(group.id).length,
      }))
      .filter((block) => block.total === 0 || block.group.typeId == null)
      .filter((block) => {
        if (!query) {
          return true;
        }
        const name = (block.group.name ?? '').toLowerCase().includes(query);
        return name || block.activities.length > 0;
      })
      .map((block) => ({ group: block.group, activities: block.activities }));
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
      width: '1080px',
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
      data: { activity, types: this.types },
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

  openNewGroup() {
    this.openGroupDialog();
  }

  editGroup(group: ActivityGroup) {
    this.openGroupDialog(group);
  }

  hasMembers(groupId: number): boolean {
    return this.members(groupId).length > 0;
  }

  ungroup(group: ActivityGroup) {
    const ids = this.members(group.id).map((activity) => activity.id);
    if (ids.length === 0) {
      return;
    }
    this.confirmRemoveFromGroup(() => this.unassignActivities(ids));
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
        message: 'Delete this group? Activities in it stay on the project and become unassigned.',
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

  isSelectionDisabled(activity: Activity): boolean {
    if (this.isSaving || this.deletingId != null) {
      return true;
    }
    if (this.isSelected(activity) || this.selectedIds.size === 0) {
      return false;
    }
    const locked = this.selectedActivities()[0]?.typeId ?? null;
    return (activity.typeId ?? null) !== locked;
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
    this.syncSelectionError();
  }

  clearSelection() {
    this.selectedIds.clear();
    this.syncSelectionError();
  }

  assignSelected() {
    if (this.selectedIds.size === 0) {
      return;
    }
    const selected = this.selectedActivities();
    if (!this.sameType(selected)) {
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
    if (!this.sameType([activity])) {
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

  private loadGroups() {
    this.api.activityGroups(this.project.id).subscribe({
      next: (groups) => {
        this.groups = groups;
      },
    });
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
      if (link.unitPrice == null) {
        continue;
      }
      const rows = grouped.get(link.activityId) ?? [];
      rows.push(link);
      grouped.set(link.activityId, rows);
    }
    for (const [activityId, rows] of grouped) {
      rows.sort((left, right) => (left.unitPrice! - right.unitPrice!) || (left.quotationId - right.quotationId) || ((left.itemId ?? 0) - (right.itemId ?? 0)));
      rows.forEach((row, index) => {
        row.lowest = index === 0;
      });
      grouped.set(activityId, rows);
    }
    return grouped;
  }

  private syncDetailActivity() {
    if (!this.detailState) {
      return;
    }
    const current = (this.project.activities ?? []).find((activity) => activity.id === this.detailState!.activity.id);
    if (current) {
      this.detailState.activity = current;
    }
  }

  private publishDetail() {
    if (!this.detailState) {
      return;
    }
    this.syncDetailActivity();
    this.detailState.links = this.quotesFor(this.detailState.activity.id);
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
    if (removed) {
      this.syncSelectionError();
    }
  }

  private syncSelectionError() {
    this.selectionError = '';
    this.failed.emit('');
  }

  private confirmRemoveFromGroup(onConfirm: () => void) {
    const ref = this.dialog.open(ConfirmDialog, {
      disableClose: true,
      width: '440px',
      maxWidth: 'calc(100vw - 32px)',
      panelClass: 'add-project-dialog-panel',
      backdropClass: 'add-project-dialog-backdrop',
      data: {
        title: 'Ungroup Activities',
        message: 'Ungroup these activities? They will return to the unassigned list, and the group container will remain in Unassigned / Global Groups.',
        confirmLabel: 'Ungroup',
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

  private sameType(activities: Activity[]): boolean {
    const typeIds = new Set(activities.map((activity) => activity.typeId));
    return activities.length > 0 && typeIds.size === 1 && activities.every((activity) => !!activity.typeId);
  }

  private compatibleGroups(typeId: number | null | undefined): ActivityGroup[] {
    return this.groups.filter((group) => group.typeId == null || group.typeId === typeId);
  }

  private visibleProjectActivities(): Activity[] {
    const query = this.activeFilter;
    return [...(this.project?.activities ?? [])]
      .filter((activity) => !query || this.matchesFilter(activity, query))
      .sort((left, right) => compareDsnNo(left.dsnNo, right.dsnNo));
  }

  private members(groupId: number): Activity[] {
    return (this.project.activities ?? []).filter((activity) => activity.activityGroupId === groupId);
  }

  private visibleMembers(groupId: number): Activity[] {
    const query = this.activeFilter;
    return this.members(groupId)
      .filter((activity) => !query || this.matchesFilter(activity, query))
      .sort((left, right) => compareDsnNo(left.dsnNo, right.dsnNo));
  }

  private groupBlocks(typeId: number): GroupBlock[] {
    return this.groups
      .filter((group) => group.typeId === typeId)
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

export interface GroupBlock {
  group: ActivityGroup;
  activities: Activity[];
}

export interface TypeSection {
  typeId: number | null;
  typeName: string;
  activities: Activity[];
  groups: GroupBlock[];
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
