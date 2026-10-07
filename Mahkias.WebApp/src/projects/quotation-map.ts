import { Activity, ActivityGroup, QuotationItemInput } from '../core/services/project-api.service';

export interface AllocationSlice {
  activityId: number;
  dsnNo: string;
  quantity: number;
  required: number;
}

export interface AllocatedLine {
  dsnNo: string;
  partNo: string;
  activityIds: number[];
  manual: boolean;
  allocations: AllocationSlice[];
  groupId: number | null;
}

export interface PartSuggestion {
  kind: 'part' | 'group';
  label: string;
  value: string;
  groupId: number | null;
}

export function partSuggestions(query: string, activities: Activity[], groups: ActivityGroup[]): PartSuggestion[] {
  const text = (query ?? '').trim().toLowerCase();
  const parts = new Map<string, string>();
  for (const activity of activities) {
    for (const value of [activity.mainPartNo, activity.partNo, ...(activity.alternativePartNos ?? [])]) {
      const part = (value ?? '').trim();
      const key = part.toLowerCase();
      if (part && !parts.has(key)) {
        parts.set(key, part);
      }
    }
  }
  const partHits = [...parts.values()]
    .filter((part) => !text || part.toLowerCase().includes(text))
    .sort((left, right) => left.localeCompare(right))
    .slice(0, 8)
    .map((part) => ({ kind: 'part' as const, label: part, value: part, groupId: null }));
  const groupHits = (groups ?? [])
    .filter((group) => (group.name ?? '').trim() && (!text || group.name.toLowerCase().includes(text)))
    .slice(0, 8)
    .map((group) => ({
      kind: 'group' as const,
      label: `Group: ${group.name.trim()}`,
      value: group.name.trim(),
      groupId: group.id,
    }));
  return [...partHits, ...groupHits];
}

export function applyAllocation(line: AllocatedLine, quantity: number, activities: Activity[], edited: 'dsn' | 'part' | 'qty' | 'manual' | 'group'): void {
  const quoted = quantity >= 1 ? quantity : 0;
  if (!(line.partNo ?? '').trim() && !(line.dsnNo ?? '').trim()) {
    line.activityIds = [];
    line.allocations = [];
    line.manual = false;
    line.groupId = null;
    return;
  }
  if (edited === 'dsn' || edited === 'part' || edited === 'group') {
    line.manual = false;
  }

  const dsnHits = activitiesMatchingDsn(line.dsnNo, activities);
  if (edited === 'dsn' && dsnHits.length > 0) {
    line.groupId = null;
  }
  if (line.groupId != null && !line.manual) {
    const pool = sortByDsn(activities.filter((activity) => activity.activityGroupId === line.groupId));
    line.allocations = allocateQuantity(quoted, pool);
    line.activityIds = line.allocations.map((slice) => slice.activityId);
    if (pool.length === 1 && line.allocations.length === 1 && !(line.dsnNo ?? '').trim()) {
      line.dsnNo = line.allocations[0].dsnNo;
    }
    return;
  }

  const candidates = matchCandidates(line.dsnNo, line.partNo, activities);
  const pool = line.manual
    ? sortByDsn(activities.filter((activity) => line.activityIds.includes(activity.id)))
    : candidates;
  line.allocations = allocateQuantity(quoted, pool);
  if (!line.manual) {
    line.activityIds = line.allocations.map((slice) => slice.activityId);
    if (candidates.length === 1 && line.allocations.length === 1 && (edited === 'part' || !(line.dsnNo ?? '').trim())) {
      line.dsnNo = line.allocations[0].dsnNo;
    }
  } else if (line.activityIds.length === 1) {
    const selected = activities.find((activity) => activity.id === line.activityIds[0]);
    if (selected) {
      line.dsnNo = (selected.dsnNo ?? '').trim();
    }
  }
}

export function excessQuantity(quantity: number, allocations: AllocationSlice[]): number {
  const required = allocations.reduce((sum, slice) => sum + slice.required, 0);
  return quantity > required && allocations.length > 0 ? quantity - required : 0;
}

export function excessLabel(quantity: number, allocations: AllocationSlice[]): string {
  const excess = excessQuantity(quantity, allocations);
  return excess > 0 ? `Excess Qty (+${excess})` : '';
}

export function allocationSummary(allocations: AllocationSlice[]): string {
  if (allocations.length < 2) {
    return '';
  }
  return allocations.map((slice) => `${slice.dsnNo || 'DSN'} × ${slice.quantity}`).join(', ');
}

export function allocatedItems(line: {
  dsnNo: string;
  partNo: string;
  description: string;
  unitPrice: number | null;
  quantity: number;
  totalPrice: number | null;
  allocations: AllocationSlice[];
}): QuotationItemInput[] {
  const partNo = line.partNo.trim();
  const description = line.description.trim();
  const slices = line.allocations.filter((slice) => slice.quantity > 0);
  if (slices.length === 0) {
    return [{
      dsnNo: line.dsnNo.trim(),
      partNo,
      description,
      unitPrice: line.unitPrice,
      quantity: line.quantity,
      totalPrice: line.totalPrice,
      activityId: null,
      activityGroupId: null,
    }];
  }
  if (slices.length === 1) {
    return [{
      dsnNo: slices[0].dsnNo || line.dsnNo.trim(),
      partNo,
      description,
      unitPrice: line.unitPrice,
      quantity: slices[0].quantity,
      totalPrice: line.totalPrice,
      activityId: slices[0].activityId,
      activityGroupId: null,
    }];
  }

  let remaining = line.totalPrice ?? 0;
  return slices.map((slice, index) => {
    const last = index === slices.length - 1;
    let totalPrice = line.totalPrice;
    if (line.totalPrice != null && line.quantity > 0) {
      totalPrice = last ? round4(remaining) : round4((line.totalPrice * slice.quantity) / line.quantity);
      if (!last) {
        remaining = round4(remaining - (totalPrice ?? 0));
      }
    } else if (line.unitPrice != null) {
      totalPrice = round4(line.unitPrice * slice.quantity);
    }
    return {
      dsnNo: slice.dsnNo,
      partNo,
      description,
      unitPrice: line.unitPrice,
      quantity: slice.quantity,
      totalPrice,
      activityId: slice.activityId,
      activityGroupId: null,
    };
  });
}

export function directActivityId(dsnNo: string, partNo: string, activities: Activity[]): number | null {
  const dsnHits = activitiesMatchingDsn(dsnNo, activities);
  const altHits = activitiesMatchingAlternative(partNo, activities);
  if (dsnHits.length === 1) {
    return dsnHits[0].id;
  }
  if (dsnHits.length > 1) {
    const narrowedAlt = dsnHits.filter((activity) => altHits.some((alt) => alt.id === activity.id));
    if (narrowedAlt.length === 1) {
      return narrowedAlt[0].id;
    }
    const part = (partNo ?? '').trim().toLowerCase();
    const narrowed = dsnHits.filter((activity) => partMatches(activity, part));
    return narrowed.length === 1 ? narrowed[0].id : null;
  }
  if (altHits.length === 1) {
    return altHits[0].id;
  }
  const partHits = activitiesMatchingPart(partNo, activities);
  return partHits.length === 1 ? partHits[0].id : null;
}

function activitiesMatchingAlternative(partNo: string, activities: Activity[]): Activity[] {
  const part = (partNo ?? '').trim().toLowerCase();
  if (!part) {
    return [];
  }
  return activities.filter((activity) => (activity.alternativePartNos ?? []).some((value) => (value ?? '').trim().toLowerCase() === part)
    && (activity.mainPartNo ?? '').trim().toLowerCase() !== part);
}

export function activitiesMatchingPart(partNo: string, activities: Activity[]): Activity[] {
  const part = (partNo ?? '').trim().toLowerCase();
  if (!part) {
    return [];
  }
  return activities.filter((activity) => partMatches(activity, part));
}

export function activityOptionLabel(activity: Activity, activities: Activity[]): string {
  const base = activityBaseLabel(activity);
  const duplicated = activities.filter((item) => activityBaseLabel(item) === base).length > 1;
  return duplicated ? `${base} · #${activity.id}` : base;
}

export function samePartNo(left: string, right: string): boolean {
  const part = (left ?? '').trim().toLowerCase();
  return !!part && part === (right ?? '').trim().toLowerCase();
}

function matchCandidates(dsnNo: string, partNo: string, activities: Activity[]): Activity[] {
  const dsnHits = activitiesMatchingDsn(dsnNo, activities);
  if (dsnHits.length > 0) {
    return sortByDsn(dsnHits);
  }
  return sortByDsn(activitiesMatchingPart(partNo, activities));
}

function allocateQuantity(quantity: number, pool: Activity[]): AllocationSlice[] {
  if (quantity < 1 || pool.length === 0) {
    return [];
  }
  if (pool.length === 1) {
    const activity = pool[0];
    return [{
      activityId: activity.id,
      dsnNo: (activity.dsnNo ?? '').trim(),
      quantity,
      required: requiredQuantity(activity),
    }];
  }

  const slices: AllocationSlice[] = [];
  let remaining = quantity;
  for (let index = 0; index < pool.length; index++) {
    const activity = pool[index];
    const required = requiredQuantity(activity);
    const last = index === pool.length - 1;
    const take = last ? remaining : Math.min(required, remaining);
    if (take > 0) {
      slices.push({
        activityId: activity.id,
        dsnNo: (activity.dsnNo ?? '').trim(),
        quantity: take,
        required,
      });
    }
    remaining -= take;
    if (remaining <= 0 && !last) {
      break;
    }
  }
  return slices;
}

function requiredQuantity(activity: Activity): number {
  if (activity.quantity == null || !Number.isFinite(Number(activity.quantity))) {
    return 1;
  }
  const rounded = Math.round(Number(activity.quantity));
  return rounded < 0 ? 0 : rounded;
}

function sortByDsn(activities: Activity[]): Activity[] {
  return [...activities].sort((left, right) => {
    const leftKey = dsnKey(left.dsnNo);
    const rightKey = dsnKey(right.dsnNo);
    if (leftKey.numeric !== rightKey.numeric) {
      return leftKey.numeric - rightKey.numeric;
    }
    if (leftKey.text !== rightKey.text) {
      return leftKey.text < rightKey.text ? -1 : 1;
    }
    return left.id - right.id;
  });
}

function dsnKey(dsnNo: string | null | undefined): { numeric: number; text: string } {
  const text = (dsnNo ?? '').trim().toLowerCase();
  if (/^\d+$/.test(text)) {
    return { numeric: Number(text), text };
  }
  return { numeric: Number.MAX_SAFE_INTEGER, text };
}

function activitiesMatchingDsn(dsnNo: string, activities: Activity[]): Activity[] {
  const dsn = (dsnNo ?? '').trim().toLowerCase();
  if (!dsn) {
    return [];
  }
  return activities.filter((activity) => (activity.dsnNo ?? '').trim().toLowerCase() === dsn);
}

function activityBaseLabel(activity: Activity): string {
  const dsn = (activity.dsnNo ?? '').trim();
  const part = (activity.mainPartNo || activity.partNo || '').trim();
  if (dsn && part) {
    return `${dsn} · ${part}`;
  }
  return dsn || part || `#${activity.id}`;
}

function partMatches(activity: Activity, part: string): boolean {
  const values = [activity.partNo, activity.mainPartNo ?? '', ...(activity.alternativePartNos ?? [])];
  return values.some((value) => (value ?? '').trim().toLowerCase() === part);
}

function round4(value: number): number {
  return Math.round(value * 10000) / 10000;
}
