import { Activity, ActivityGroup, ActivityLabel } from '../../../../core/services/project-api.service';

export interface GroupBlock {
  group: ActivityGroup;
  activities: Activity[];
}

export interface TypeSection {
  typeId: number | null;
  typeName: string;
  activities: Activity[];
  groups: GroupBlock[];
  shownCount: number;
}

export interface LabelRemovalPlan {
  activityIds: number[];
  groupIds: number[];
  keepers: Activity[];
}

export function labelHasMembers(label: ActivityLabel): boolean {
  return (label.activityIds?.length ?? 0) > 0 || (label.activityGroupIds?.length ?? 0) > 0;
}

export function labelsForType(labels: ActivityLabel[], typeId: number | null): ActivityLabel[] {
  if (typeId == null) {
    return [];
  }
  return labels
    .filter((label) => label.typeId === typeId && labelHasMembers(label))
    .sort(byCreatedAt);
}

function byCreatedAt(left: ActivityLabel, right: ActivityLabel): number {
  const leftTime = labelCreatedAt(left);
  const rightTime = labelCreatedAt(right);
  if (leftTime !== rightTime) {
    return leftTime - rightTime;
  }
  return left.id - right.id;
}

function labelCreatedAt(label: ActivityLabel): number {
  const parsed = Date.parse(label.createdAt ?? '');
  return Number.isFinite(parsed) ? parsed : Number.MAX_SAFE_INTEGER;
}

export function directLabels(labels: ActivityLabel[], activity: Activity): ActivityLabel[] {
  return labels
    .filter((label) => label.typeId === activity.typeId && (label.activityIds ?? []).includes(activity.id))
    .sort((left, right) => left.name.localeCompare(right.name));
}

export function activityHasLabel(activity: Activity, label: ActivityLabel): boolean {
  if ((label.activityIds ?? []).includes(activity.id)) {
    return true;
  }
  return activity.activityGroupId != null && (label.activityGroupIds ?? []).includes(activity.activityGroupId);
}

export function scopedLabels(labels: ActivityLabel[], activeLabelId: number | null): ActivityLabel[] {
  if (activeLabelId == null) {
    return labels;
  }
  return labels.filter((label) => label.id === activeLabelId);
}

export function activitySharesType(activities: Activity[]): boolean {
  const typeIds = new Set(activities.map((activity) => activity.typeId));
  return activities.length > 0 && typeIds.size === 1 && activities.every((activity) => !!activity.typeId);
}

export function selectionType(activities: Activity[], groups: ActivityGroup[]): number | null | undefined {
  const types = new Set<number | null>();
  for (const activity of activities) {
    types.add(activity.typeId ?? null);
  }
  for (const group of groups) {
    types.add(group.typeId ?? null);
  }
  if (types.size !== 1) {
    return undefined;
  }
  return [...types][0];
}

export function activityLabelIds(labels: ActivityLabel[], activity: Activity): number[] {
  const ids = labels
    .filter((label) => label.typeId === activity.typeId && activityHasLabel(activity, label))
    .map((label) => label.id);
  return [...new Set(ids)].sort((left, right) => left - right);
}

export function groupLabelIds(labels: ActivityLabel[], group: ActivityGroup): number[] {
  if (group.typeId == null) {
    return [];
  }
  return labels
    .filter((label) => label.typeId === group.typeId && (label.activityGroupIds ?? []).includes(group.id))
    .map((label) => label.id)
    .sort((left, right) => left - right);
}

export function labelToolbarActions(activeLabelId: number | null, memberships: number[][]): { assign: boolean; remove: boolean } {
  if (memberships.length === 0) {
    return { assign: false, remove: false };
  }
  const onActiveLabel = activeLabelId != null && memberships.some((ids) => ids.includes(activeLabelId));
  const onAnyLabel = memberships.some((ids) => ids.length > 0);
  return {
    assign: true,
    remove: activeLabelId != null ? onActiveLabel : onAnyLabel,
  };
}

export function labelSelectionMode(groupCount: number, activities: Activity[]): boolean {
  if (groupCount > 0) {
    return true;
  }
  return activities.some((activity) => !!activity.activityGroupId) && activities.some((activity) => !activity.activityGroupId);
}

export function selectionNote(activityCount: number, groupCount: number): string {
  const parts: string[] = [];
  if (activityCount) {
    parts.push(`${activityCount} ${activityCount === 1 ? 'activity' : 'activities'}`);
  }
  if (groupCount) {
    parts.push(`${groupCount} ${groupCount === 1 ? 'group' : 'groups'}`);
  }
  return parts.join(' and ') + ' selected';
}

export function groupFullySelected(children: Activity[], selectedIds: Set<number>): boolean {
  return children.length > 0 && children.every((activity) => selectedIds.has(activity.id));
}

export function groupPartlySelected(children: Activity[], selectedIds: Set<number>): boolean {
  const selected = children.filter((activity) => selectedIds.has(activity.id)).length;
  return selected > 0 && selected < children.length;
}

export function syncGroupSelection(groupId: number, children: Activity[], selectedIds: Set<number>, selectedGroupIds: Set<number>) {
  if (groupFullySelected(children, selectedIds)) {
    selectedGroupIds.add(groupId);
  } else {
    selectedGroupIds.delete(groupId);
  }
}

export function applyLabelFilter(
  label: ActivityLabel | null,
  activities: Activity[],
  groups: GroupBlock[],
): { activities: Activity[]; groups: GroupBlock[] } {
  if (!label) {
    return { activities, groups };
  }
  const activityIds = new Set(label.activityIds ?? []);
  const groupIds = new Set(label.activityGroupIds ?? []);
  return {
    activities: activities.filter((activity) => activityIds.has(activity.id)),
    groups: groups
      .map((block) => groupIds.has(block.group.id)
        ? block
        : { ...block, activities: block.activities.filter((activity) => activityIds.has(activity.id)) })
      .filter((block) => block.activities.length > 0),
  };
}

export function planOpenLabelRemoval(
  activities: Activity[],
  groups: ActivityGroup[],
  activityLabels: (activity: Activity) => ActivityLabel[],
  groupLabels: (group: ActivityGroup) => ActivityLabel[],
): LabelRemovalPlan {
  return {
    activityIds: activities.filter((activity) => activityLabels(activity).length > 0).map((activity) => activity.id),
    groupIds: groups.filter((group) => groupLabels(group).length > 0).map((group) => group.id),
    keepers: [],
  };
}

export function planLabelRemoval(
  label: ActivityLabel,
  activities: Activity[],
  groups: ActivityGroup[],
  selectedActivityIds: Set<number>,
  selectedGroupIds: Set<number>,
  members: (groupId: number) => Activity[],
): LabelRemovalPlan {
  const activityIds = new Set<number>();
  const groupIds = new Set<number>();
  const direct = new Set(label.activityIds ?? []);
  const labeledGroups = new Set(label.activityGroupIds ?? []);

  for (const activity of activities) {
    if (direct.has(activity.id)) {
      activityIds.add(activity.id);
    }
    const groupId = activity.activityGroupId;
    if (groupId != null && labeledGroups.has(groupId) && !selectedGroupIds.has(groupId)) {
      groupIds.add(groupId);
    }
  }

  for (const group of groups) {
    if (labeledGroups.has(group.id)) {
      groupIds.add(group.id);
    }
    for (const activity of members(group.id)) {
      if (direct.has(activity.id)) {
        activityIds.add(activity.id);
      }
    }
  }

  const keepers: Activity[] = [];
  for (const groupId of groupIds) {
    if (selectedGroupIds.has(groupId)) {
      continue;
    }
    for (const activity of members(groupId)) {
      if (!selectedActivityIds.has(activity.id) && !direct.has(activity.id)) {
        keepers.push(activity);
      }
    }
  }

  return { activityIds: [...activityIds], groupIds: [...groupIds], keepers };
}
