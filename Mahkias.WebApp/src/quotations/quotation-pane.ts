import { MatDialog, MatDialogRef } from '@angular/material/dialog';
import { Activity, QuotationDetail } from '../core/services/project-api.service';
import { CreateQuotationDialogComponent } from '../projects/add-quotation-dialog/add-quotation-dialog';
import { ImportQuotationDialogComponent } from '../projects/import-quotation-dialog/import-quotation-dialog';

const pane = {
  disableClose: true,
  width: '100vw',
  maxWidth: '100vw',
  minWidth: '100vw',
  height: '100vh',
  maxHeight: '100vh',
  panelClass: ['add-project-dialog-panel', 'quotation-dialog-panel', 'po-new-panel', 'app-dialog-pane'],
  autoFocus: 'first-tabbable' as const,
};

export function openQuotationEditor(
  dialog: MatDialog,
  data: {
    projectId: number | null;
    activities: Activity[];
    quotation?: QuotationDetail;
    projectLabel?: string | null;
    lockProject?: boolean;
  },
): MatDialogRef<CreateQuotationDialogComponent, boolean> {
  return dialog.open(CreateQuotationDialogComponent, {
    ...pane,
    backdropClass: 'quotation-create-backdrop',
    data,
  });
}

export function openQuotationImport(dialog: MatDialog): MatDialogRef<ImportQuotationDialogComponent, boolean> {
  return dialog.open(ImportQuotationDialogComponent, {
    ...pane,
    backdropClass: 'add-project-dialog-backdrop',
    data: { projectId: null, activities: [] },
  });
}
