export interface UsageField {
  label: string;
  value: string;
}

export function usageDetails(source: {
  serialNo?: string | null;
  tsn?: string | null;
  tso?: string | null;
  csn?: string | null;
  cso?: string | null;
} | null | undefined): UsageField[] {
  return [
    { label: 'SerialNo', value: source?.serialNo },
    { label: 'TSN', value: source?.tsn },
    { label: 'TSO', value: source?.tso },
    { label: 'CSN', value: source?.csn },
    { label: 'CSO', value: source?.cso },
  ]
    .map((field) => ({ label: field.label, value: (field.value ?? '').trim() }))
    .filter((field) => field.value.length > 0);
}
