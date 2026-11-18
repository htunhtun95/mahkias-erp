import { LookupValue } from '../core/services/project-api.service';

export type { LookupValue };

export function canonicalUnit(value: string, units: LookupValue[]): string {
  const text = value.trim();
  if (!text) {
    return '';
  }
  const match = units.find((unit) => sameText(unit.name, text) || sameText(unit.code, text));
  return match?.name || text;
}

export function canonicalCondition(value: string, conditions: LookupValue[]): string {
  const text = value.trim();
  if (!text) {
    return '';
  }
  const match = conditions.find((condition) => sameText(condition.name, text) || sameText(condition.code, text));
  return match?.name || text;
}

function sameText(left: string | null | undefined, right: string | null | undefined): boolean {
  const expected = (left ?? '').trim().toLowerCase();
  const actual = (right ?? '').trim().toLowerCase();
  return expected.length > 0 && expected === actual;
}
