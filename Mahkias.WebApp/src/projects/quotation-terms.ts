export interface QuotationTerm {
  days: number;
  expired: boolean;
  label: string;
}

export interface MinimumOrderNote {
  text: string;
  short: boolean;
}

export function quotationValidity(validUntil?: string | null, today = new Date()): QuotationTerm | null {
  const end = calendarDate(validUntil);
  if (!end) {
    return null;
  }

  const start = new Date(today.getFullYear(), today.getMonth(), today.getDate());
  const days = Math.round((end.getTime() - start.getTime()) / 86400000);
  if (days < 0) {
    const overdue = Math.abs(days);
    return { days, expired: true, label: overdue === 1 ? 'Expired 1 day ago' : `Expired ${overdue} days ago` };
  }
  if (days === 0) {
    return { days: 0, expired: false, label: 'Expires today' };
  }
  return { days, expired: false, label: days === 1 ? '1 day left' : `${days} days left` };
}

export function minimumOrderNote(
  quantity: number | null | undefined,
  minimumQty: number | null | undefined,
  requiredQuantity?: number | null,
): MinimumOrderNote | null {
  const minimum = Number(minimumQty);
  if (!Number.isFinite(minimum) || minimum <= 0) {
    return null;
  }

  const required = requiredQuantity != null && Number.isFinite(Number(requiredQuantity)) && Number(requiredQuantity) > 0
    ? Number(requiredQuantity)
    : Number(quantity ?? 0);
  if (minimum > required) {
    return { text: `Req ${formatCount(required)} · MOQ ${formatCount(minimum)}`, short: true };
  }
  return { text: `MOQ ${formatCount(minimum)}`, short: false };
}

export function dateInputValue(value?: string | null): string {
  const match = /^(\d{4}-\d{2}-\d{2})/.exec(value ?? '');
  return match ? match[1] : '';
}

function calendarDate(value?: string | null): Date | null {
  const match = /^(\d{4})-(\d{2})-(\d{2})/.exec(value ?? '');
  if (!match) {
    return null;
  }
  const date = new Date(Number(match[1]), Number(match[2]) - 1, Number(match[3]));
  return Number.isNaN(date.getTime()) ? null : date;
}

function formatCount(value: number): string {
  return Number.isInteger(value) ? String(value) : String(value);
}
