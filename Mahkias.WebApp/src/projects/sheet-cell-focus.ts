import { Directive } from '@angular/core';

/** Focusing the field when a full-screen grid cell is clicked outside the field itself. */
@Directive({
  selector: 'table.sheet, table.import-sheet',
  host: {
    '(click)': 'focusField($event)',
  },
})
export class SheetCellFocusDirective {
  focusField(event: Event) {
    const target = event.target;
    if (!(target instanceof Element) || !target.closest('.po-new-panel')) {
      return;
    }
    const cell = target.closest('td');
    if (!cell || !cell.closest('tbody')) {
      return;
    }
    if (target.closest('button, a, input, textarea, select')) {
      return;
    }
    const field = cell.querySelector('input, textarea, select');
    if (!(field instanceof HTMLElement) || field.hasAttribute('disabled')) {
      return;
    }
    field.focus();
  }
}
