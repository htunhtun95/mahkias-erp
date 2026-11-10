import { MatDialogRef } from '@angular/material/dialog';

const closers: Array<() => void> = [];
let listening = false;

function installEscapeListener(): void {
  if (listening || typeof document === 'undefined') {
    return;
  }
  listening = true;
  document.addEventListener('keydown', (event) => {
    if (event.key !== 'Escape') {
      return;
    }
    const close = closers[closers.length - 1];
    if (!close) {
      return;
    }
    const panes = document.querySelectorAll('.cdk-overlay-pane').length;
    setTimeout(() => {
      const open = [...document.querySelectorAll('.cdk-overlay-pane')];
      if (open.length < panes) {
        return;
      }
      const top = open[open.length - 1];
      if (!top?.classList.contains('po-new-panel')) {
        return;
      }
      close();
    });
  }, true);
}

/** Closes the top full-screen dialog on Escape. A nested overlay, such as a date picker, keeps the dialog open. */
export function closeOnEscape<T>(dialogRef: MatDialogRef<T>, close: () => void): void {
  installEscapeListener();
  closers.push(close);
  dialogRef.afterClosed().subscribe(() => {
    const index = closers.lastIndexOf(close);
    if (index >= 0) {
      closers.splice(index, 1);
    }
  });
}
