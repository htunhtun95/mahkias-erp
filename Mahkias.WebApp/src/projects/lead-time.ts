import { Directive, HostListener, Input, inject } from '@angular/core';
import { NgControl } from '@angular/forms';

const maxLeadDays = 2147483647;

export function leadTimeDays(value: string | number | null | undefined): number | null {
  if (value == null) {
    return null;
  }
  const text = String(value).trim();
  if (!text) {
    return null;
  }
  const match = text.match(/^(\d+)(?:\.0+)?(?:\s*(?:days?|d))?$/i);
  if (!match) {
    return null;
  }
  const days = Number(match[1]);
  return Number.isSafeInteger(days) && days <= maxLeadDays ? days : null;
}

export function leadTimeInvalid(value: string | number | null | undefined): boolean {
  if (value == null || String(value).trim() === '') {
    return false;
  }
  return leadTimeDays(value) == null;
}

export function leadTimeText(value: string | number | null | undefined): string {
  const days = leadTimeDays(value);
  return days == null ? '' : String(days);
}

export function leadTimeLabel(value: string | number | null | undefined): string {
  const days = leadTimeDays(value);
  if (days == null) {
    return '—';
  }
  return days === 1 ? '1 day' : `${days} days`;
}

@Directive({
  selector: 'input[leadTimeDays]',
  standalone: true,
})
export class LeadTimeDaysDirective {
  private readonly control = inject(NgControl, { optional: true, self: true });

  @Input() leadTimeDays: boolean | '' = '';

  @HostListener('beforeinput', ['$event'])
  onBeforeInput(event: InputEvent) {
    if (!this.enabled) {
      return;
    }
    if (!event.inputType || event.inputType.startsWith('delete') || event.inputType.startsWith('history')) {
      return;
    }
    if (event.inputType === 'insertFromPaste' || event.inputType === 'insertFromDrop') {
      return;
    }
    if (event.data != null && /\D/.test(event.data)) {
      event.preventDefault();
    }
  }

  @HostListener('paste', ['$event'])
  onPaste(event: ClipboardEvent) {
    if (!this.enabled) {
      return;
    }
    event.preventDefault();
    const input = event.target as HTMLInputElement;
    const digits = (event.clipboardData?.getData('text') ?? '').replace(/\D/g, '');
    const start = input.selectionStart ?? input.value.length;
    const end = input.selectionEnd ?? input.value.length;
    this.commit(input, input.value.slice(0, start) + digits + input.value.slice(end), start + digits.length);
  }

  @HostListener('drop', ['$event'])
  onDrop(event: DragEvent) {
    if (!this.enabled) {
      return;
    }
    event.preventDefault();
    const input = event.target as HTMLInputElement;
    const digits = (event.dataTransfer?.getData('text') ?? '').replace(/\D/g, '');
    const start = input.selectionStart ?? input.value.length;
    const end = input.selectionEnd ?? input.value.length;
    this.commit(input, input.value.slice(0, start) + digits + input.value.slice(end), start + digits.length);
  }

  @HostListener('input', ['$event'])
  onInput(event: Event) {
    if (!this.enabled) {
      return;
    }
    const input = event.target as HTMLInputElement | null;
    if (!input || !/\D/.test(input.value)) {
      return;
    }
    const digits = input.value.replace(/\D/g, '');
    this.commit(input, digits, digits.length);
  }

  private commit(input: HTMLInputElement, value: string, cursor: number) {
    input.value = value;
    const place = Math.min(cursor, value.length);
    input.setSelectionRange(place, place);
    this.control?.control?.setValue(value);
  }

  private get enabled(): boolean {
    return this.leadTimeDays !== false;
  }
}
