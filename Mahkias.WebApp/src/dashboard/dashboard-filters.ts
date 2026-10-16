export type CreatedRange = 'all' | 'today' | 'week' | 'month' | 'year' | 'custom';

export interface DashboardStatusOption {
  id: string;
  label: string;
}

export interface DashboardDataset {
  id: string;
  label: string;
  statuses: DashboardStatusOption[];
}

export interface TrackedRecord {
  dataset: string;
  status: string;
  createdAt: string | null;
}

export interface DashboardFilterState {
  dataset: string;
  status: string;
  created: CreatedRange;
  createdFrom: string;
  createdTo: string;
}

export const DASHBOARD_DATASETS: DashboardDataset[] = [
  {
    id: 'quotations',
    label: 'Quotations',
    statuses: [
      { id: 'pending', label: 'Pending' },
      { id: 'processing', label: 'Processing' },
    ],
  },
];

export function createDashboardFilters(): DashboardFilterState {
  return {
    dataset: 'all',
    status: 'all',
    created: 'all',
    createdFrom: '',
    createdTo: '',
  };
}

export function statusOptionsFor(dataset: string): DashboardStatusOption[] {
  const sets = dataset === 'all' ? DASHBOARD_DATASETS : DASHBOARD_DATASETS.filter((item) => item.id === dataset);
  const seen = new Set<string>();
  const options: DashboardStatusOption[] = [];
  for (const set of sets) {
    for (const status of set.statuses) {
      if (seen.has(status.id)) {
        continue;
      }
      seen.add(status.id);
      options.push(status);
    }
  }
  return options;
}

export function applyDashboardFilters<T extends TrackedRecord>(records: T[], filters: DashboardFilterState): T[] {
  return records.filter((record) => {
    if (filters.dataset !== 'all' && record.dataset !== filters.dataset) {
      return false;
    }
    if (filters.status !== 'all' && record.status !== filters.status) {
      return false;
    }
    return matchesCreatedAt(record.createdAt, filters.created, filters.createdFrom, filters.createdTo);
  });
}

export function matchesCreatedAt(createdAt: string | null | undefined, range: CreatedRange, from = '', to = ''): boolean {
  if (range === 'all') {
    return true;
  }

  const created = startOfDay(createdAt);
  if (created == null) {
    return false;
  }

  if (range === 'today') {
    return created >= (startOfDay(new Date()) ?? created);
  }

  if (range === 'week') {
    const start = new Date();
    start.setDate(start.getDate() - 7);
    return created >= (startOfDay(start) ?? created);
  }

  if (range === 'month') {
    const start = new Date();
    start.setMonth(start.getMonth() - 1);
    return created >= (startOfDay(start) ?? created);
  }

  if (range === 'year') {
    const start = new Date();
    start.setFullYear(start.getFullYear() - 1);
    return created >= (startOfDay(start) ?? created);
  }

  const fromDay = startOfDay(from ? `${from}T00:00:00` : null);
  const toDay = startOfDay(to ? `${to}T00:00:00` : null);
  if (fromDay != null && created < fromDay) {
    return false;
  }
  if (toDay != null && created > toDay) {
    return false;
  }
  return true;
}

function startOfDay(value?: string | Date | null): number | null {
  if (!value) {
    return null;
  }
  const date = value instanceof Date ? value : new Date(value);
  if (Number.isNaN(date.getTime())) {
    return null;
  }
  return new Date(date.getFullYear(), date.getMonth(), date.getDate()).getTime();
}
