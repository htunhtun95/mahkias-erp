import { HttpErrorResponse } from '@angular/common/http';
import { AfterViewInit, Component, ViewChild, inject } from '@angular/core';
import { MAT_DIALOG_DATA, MatDialog, MatDialogRef } from '@angular/material/dialog';
import { MaterialModule } from '../../app/material-module';
import { closeOnEscape } from '../../core/close-on-escape';
import { DocumentDraft, DocumentKind, DocumentManagerComponent, ManagedDocument } from '../document-manager/document-manager';

export interface DocumentLibraryData {
  title: string;
  note: string;
  kind: DocumentKind;
  projectId: number;
  ownerId: number | null;
  documents: ManagedDocument[];
  drafts?: DocumentDraft[] | null;
  accept: string;
  supportingOnly?: boolean;
  sortBase?: number;
}

export interface DocumentLibraryResult {
  saved: boolean;
  drafts?: DocumentDraft[];
}

export const documentAccept = '.pdf,.doc,.docx,.xls,.xlsx,.xlsm,.csv,.png,.jpg,.jpeg,.gif,.webp,.bmp,.tif,.tiff';

export function openDocumentLibrary(dialog: MatDialog, data: DocumentLibraryData) {
  return dialog.open<DocumentLibraryDialog, DocumentLibraryData, DocumentLibraryResult>(DocumentLibraryDialog, {
    disableClose: true,
    width: '720px',
    maxWidth: 'calc(100vw - 32px)',
    maxHeight: 'calc(100vh - 32px)',
    panelClass: ['add-project-dialog-panel', 'app-dialog-pane'],
    backdropClass: 'add-project-dialog-backdrop',
    autoFocus: 'first-tabbable',
    data,
  });
}

@Component({
  selector: 'app-document-library-dialog',
  standalone: true,
  imports: [MaterialModule, DocumentManagerComponent],
  templateUrl: './document-library-dialog.html',
  styleUrl: './document-library-dialog.css',
})
export class DocumentLibraryDialog implements AfterViewInit {
  private dialogRef = inject(MatDialogRef<DocumentLibraryDialog, DocumentLibraryResult>);
  readonly data = inject<DocumentLibraryData>(MAT_DIALOG_DATA);
  @ViewChild('library') library?: DocumentManagerComponent;

  saving = false;
  error = '';

  constructor() {
    closeOnEscape(this.dialogRef, () => this.cancel());
  }

  ngAfterViewInit() {
    if (this.data.drafts?.length) {
      this.library?.restore(this.data.drafts);
    }
  }

  cancel() {
    if (!this.saving) {
      this.dialogRef.close({ saved: false });
    }
  }

  save() {
    if (this.saving || !this.library?.hasChanges) {
      return;
    }
    const ownerId = this.data.ownerId;
    if (!ownerId) {
      this.dialogRef.close({ saved: true, drafts: this.library.capture() });
      return;
    }
    this.saving = true;
    this.error = '';
    this.dialogRef.disableClose = true;
    this.library.sync(this.data.kind, this.data.projectId, ownerId).subscribe({
      next: () => {
        this.saving = false;
        this.dialogRef.close({ saved: true });
      },
      error: (error: HttpErrorResponse) => {
        this.saving = false;
        this.dialogRef.disableClose = false;
        this.error = error.error?.message || 'The documents could not be saved.';
      },
    });
  }
}
