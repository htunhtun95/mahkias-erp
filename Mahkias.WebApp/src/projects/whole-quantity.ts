import { Directive, HostListener, inject } from '@angular/core';
import { NgControl } from '@angular/forms';

@Directive({
  selector: 'input[wholeQuantity]',
  standalone: true,
})
export class WholeQuantityDirective {
  private readonly control = inject(NgControl, { optional: true, self: true });

  @HostListener('beforeinput', ['$event'])
  onBeforeInput(event: InputEvent) {
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
    event.preventDefault();
    const input = event.target as HTMLInputElement;
    const digits = (event.clipboardData?.getData('text') ?? '').replace(/\D/g, '');
    const start = input.selectionStart ?? input.value.length;
    const end = input.selectionEnd ?? input.value.length;
    this.commit(input, input.value.slice(0, start) + digits + input.value.slice(end), start + digits.length);
  }

  @HostListener('drop', ['$event'])
  onDrop(event: DragEvent) {
    event.preventDefault();
    const input = event.target as HTMLInputElement;
    const digits = (event.dataTransfer?.getData('text') ?? '').replace(/\D/g, '');
    const start = input.selectionStart ?? input.value.length;
    const end = input.selectionEnd ?? input.value.length;
    this.commit(input, input.value.slice(0, start) + digits + input.value.slice(end), start + digits.length);
  }

  @HostListener('input', ['$event'])
  onInput(event: Event) {
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
}
