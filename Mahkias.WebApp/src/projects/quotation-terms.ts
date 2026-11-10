export interface QuotationTerm {
  days: number;
  expired: boolean;
  label: string;
}

export interface QuantityDisplay {
  value: string;
  note: string | null;
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

export function quotationQuantity(
  quantity: number | null | undefined,
  minimumQty: number | null | undefined,
): QuantityDisplay {
  const requested = Number(quantity);
  const hasRequested = Number.isFinite(requested);
  const minimum = Number(minimumQty);
  const hasMinimum = Number.isFinite(minimum) && minimum > 0;
  if (!hasMinimum) {
    return { value: hasRequested ? formatCount(requested) : '—', note: null, short: false };
  }

  const required = hasRequested ? requested : 0;
  return {
    value: formatCount(minimum),
    note: `Req: ${formatCount(required)}`,
    short: minimum > required,
  };
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
