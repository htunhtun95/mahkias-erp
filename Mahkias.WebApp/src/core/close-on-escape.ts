import { MatDialogRef } from '@angular/material/dialog';

/** Closes a dialog on Escape. A nested overlay, such as a date picker, keeps the dialog open. */
export function closeOnEscape<T>(dialogRef: MatDialogRef<T>, close: () => void): void {
  dialogRef.keydownEvents().subscribe((event) => {
    if (event.key !== 'Escape') {
      return;
    }
    const panes = document.querySelectorAll('.cdk-overlay-pane').length;
    setTimeout(() => {
      if (document.querySelectorAll('.cdk-overlay-pane').length < panes) {
        return;
      }
      close();
    });
  });
}
