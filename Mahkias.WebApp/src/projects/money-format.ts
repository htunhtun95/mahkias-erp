export function formatMoney(value?: number | null, symbol?: string | null, code?: string | null): string {
  if (value == null || !Number.isFinite(Number(value))) {
    return '—';
  }

  const amount = Number(value).toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
  const mark = (symbol ?? '').trim();
  if (mark) {
    return mark.length > 1 ? `${mark} ${amount}` : `${mark}${amount}`;
  }

  return `${amount} ${(code || 'USD').trim()}`;
}
