export interface SplitPartNumber {
  value: string;
  main: boolean;
}

const orPatterns = ['(  or  )', '( or )', '(or)', ' or '];

/** Mirrors dbo.SplitAlternativePartNumbers. */
export function splitAlternativePartNumbers(partNo: string | null | undefined): SplitPartNumber[] {
  if (partNo == null) {
    return [];
  }

  const collapsed = partNo.replace(/\r/g, ' ').replace(/\n/g, ' ').trim();
  if (!collapsed) {
    return [];
  }

  let work = replaceOr(partNo);
  let slashAt = 0;
  while (slashAt <= work.length) {
    const slash = work.indexOf('/', slashAt);
    if (slash < 0) {
      break;
    }

    let tokenEnd = work.length;
    const nextSlash = work.indexOf('/', slash + 1);
    const nextBar = work.indexOf('|', slash + 1);
    if (nextSlash >= 0 && nextSlash < tokenEnd) {
      tokenEnd = nextSlash;
    }
    if (nextBar >= 0 && nextBar < tokenEnd) {
      tokenEnd = nextBar;
    }

    const right = work.slice(slash + 1, tokenEnd).trim();
    let leftBound = -1;
    let scan = 0;
    while (scan <= work.length) {
      const bar = work.indexOf('|', scan);
      if (bar < 0 || bar >= slash) {
        break;
      }
      leftBound = bar;
      scan = bar + 1;
    }
    const left = work.slice(leftBound + 1, slash).trim();
    if (isFullPartNumber(left) && isFullPartNumber(right)) {
      work = `${work.slice(0, slash)}|${work.slice(slash + 1)}`;
    }
    slashAt = slash + 1;
  }

  return work
    .split('|')
    .map(trimPiece)
    .filter((value) => !!value)
    .map((value, index) => ({ value, main: index === 0 }));
}

export function retainsSelectedPart(value: string | null | undefined, selectedPartNo: string | null | undefined): boolean {
  return keepsSelectedMain(value, selectedPartNo);
}

/** The selected part stays the existing main part only while that number is still the first piece. */
export function keepsSelectedMain(value: string | null | undefined, selectedPartNo: string | null | undefined): boolean {
  const selected = (selectedPartNo ?? '').trim();
  if (!selected) {
    return false;
  }
  const pieces = splitAlternativePartNumbers(value);
  if (!pieces.length || pieces[0].value.trim().toLowerCase() !== selected.toLowerCase()) {
    return false;
  }
  const normalized = String(value ?? '').replace(/\r/g, ' ').replace(/\n/g, ' ').trim().toLowerCase();
  return normalized.startsWith(selected.toLowerCase());
}

export interface PartPickState {
  partNo: string;
  partId: number | null;
  pickedPartNo: string | null;
}

/** Clearing the input drops the typed number and restores the selection from when the row opened. Any other edit keeps the picked main part only when it is still the first piece. */
export function applyPartInput(current: PartPickState, value: string, initial: PartPickState): PartPickState {
  if (!value.trim()) {
    return {
      partNo: '',
      partId: initial.partId,
      pickedPartNo: initial.pickedPartNo,
    };
  }
  if (keepsSelectedMain(value, current.pickedPartNo)) {
    return {
      partNo: value,
      partId: current.partId,
      pickedPartNo: current.pickedPartNo,
    };
  }
  return {
    partNo: value,
    partId: null,
    pickedPartNo: null,
  };
}

export function applyPartPick(partId: number, partNo: string | null | undefined): PartPickState {
  const selected = partNo ?? '';
  return {
    partNo: selected,
    partId,
    pickedPartNo: selected,
  };
}

export function partNumberPreview(partNo: string | null | undefined): string {
  const raw = partNo ?? '';
  if (!/\bor\b|\//i.test(raw)) {
    return '';
  }

  const parts = splitAlternativePartNumbers(raw);
  if (parts.length <= 1) {
    return 'Kept as one part number.';
  }

  const alternates = parts.slice(1).map((part) => part.value).join(', ');
  return `Primary ${parts[0].value}. Alternate ${alternates}.`;
}

function replaceOr(value: string): string {
  let work = value;
  for (const pattern of orPatterns) {
    const needle = pattern.toLowerCase();
    let lower = work.toLowerCase();
    let index = lower.indexOf(needle);
    while (index >= 0) {
      work = `${work.slice(0, index)}|${work.slice(index + pattern.length)}`;
      lower = `${lower.slice(0, index)}|${lower.slice(index + pattern.length)}`;
      index = lower.indexOf(needle);
    }
  }
  return work;
}

function isFullPartNumber(value: string): boolean {
  return value.length > 3 && !/[\t /]/.test(value) && /[^0-9]/.test(value);
}

function trimPiece(value: string): string {
  return value.replace(/^[\t\r\n]+|[\t\r\n]+$/g, '').trim();
}
