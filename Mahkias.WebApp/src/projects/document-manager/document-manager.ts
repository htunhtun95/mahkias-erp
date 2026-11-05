import { Component, Input, OnChanges } from '@angular/core';
import { CdkDragDrop, DragDropModule, moveItemInArray } from '@angular/cdk/drag-drop';
import { concatMap, from, Observable, of, switchMap, tap, toArray } from 'rxjs';
import { MaterialModule } from '../../app/material-module';
import { ProjectApiService } from '../../core/services/project-api.service';

export interface ManagedDocument {
  documentId: number;
  fileName: string;
  fileType: string;
  fileSize?: number;
  webViewLink?: string | null;
  isOriginal?: boolean;
  sortOrder?: number;
}

export interface DocumentDraft {
  key: string;
  documentId: number | null;
  fileName: string;
  fileType: string;
  fileSize: number;
  webViewLink: string;
  isOriginal: boolean;
  sortOrder: number;
  pendingFile: File | null;
  removed: boolean;
}

export type DocumentKind = 'quotation' | 'purchase-order' | 'project';

type DocumentEntry = DocumentDraft;

export function formatFileSize(bytes: number): string {
  if (!bytes) {
    return '';
  }
  if (bytes < 1024) {
    return `${bytes} B`;
  }
  if (bytes < 1024 * 1024) {
    return `${(bytes / 1024).toFixed(1)} KB`;
  }
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
}

@Component({
  selector: 'app-document-manager',
  standalone: true,
  imports: [MaterialModule, DragDropModule],
  templateUrl: './document-manager.html',
  styleUrl: './document-manager.css',
})
export class DocumentManagerComponent implements OnChanges {
  @Input() documents: ManagedDocument[] = [];
  @Input() accept = '';
  @Input() disabled = false;
  @Input() supportingOnly = false;
  @Input() lockOriginal = true;
  @Input() sortBase = 0;
  @Input() title = 'Documents';
  @Input() hint = 'PDF, Excel, Word, or image. Add more than one file and set the order.';

  entries: DocumentEntry[] = [];
  dragging = false;
  private removed: DocumentEntry[] = [];
  private touched = false;
  private seeded = false;

  constructor(private api: ProjectApiService) {}

  ngOnChanges() {
    if (this.touched || this.seeded) {
      return;
    }
    if (!this.documents?.length) {
      return;
    }
    this.entries = this.documents.map((document, index) => ({
      key: `saved-${document.documentId}`,
      documentId: document.documentId,
      fileName: document.fileName,
      fileType: document.fileType,
      fileSize: document.fileSize ?? 0,
      webViewLink: document.webViewLink ?? '',
      isOriginal: !!document.isOriginal,
      sortOrder: document.sortOrder ?? index,
      pendingFile: null,
      removed: false,
    }));
    this.removed = [];
    this.pinOriginal();
    this.seeded = true;
  }

  readonly canSort = (index: number) => !this.locksOriginal || index > 0;

  get locksOriginal(): boolean {
    return this.lockOriginal && !this.supportingOnly;
  }

  get visible(): DocumentEntry[] {
    return this.entries;
  }

  get hasOriginal(): boolean {
    return this.entries.some((entry) => entry.isOriginal);
  }

  onDragOver(event: DragEvent) {
    event.preventDefault();
    if (!this.disabled) {
      this.dragging = true;
    }
  }

  onDragLeave(event: DragEvent) {
    event.preventDefault();
    this.dragging = false;
  }

  onDrop(event: DragEvent) {
    event.preventDefault();
    this.dragging = false;
    if (!this.disabled) {
      this.addFiles(Array.from(event.dataTransfer?.files ?? []));
    }
  }

  browse(picker: HTMLInputElement) {
    if (!this.disabled) {
      picker.click();
    }
  }

  onBrowse(event: Event) {
    const input = event.target as HTMLInputElement;
    this.addFiles(Array.from(input.files ?? []));
    input.value = '';
  }

  drop(event: CdkDragDrop<DocumentEntry[]>) {
    if (this.disabled || event.previousIndex === event.currentIndex) {
      return;
    }
    if (this.locksOriginal && (event.previousIndex === 0 || event.currentIndex === 0)) {
      return;
    }
    moveItemInArray(this.entries, event.previousIndex, event.currentIndex);
    this.pinOriginal();
    this.touched = true;
  }

  markOriginal(entry: DocumentEntry) {
    if (!this.locksOriginal || this.disabled || entry.isOriginal) {
      return;
    }
    for (const item of this.entries) {
      item.isOriginal = item === entry;
    }
    const index = this.entries.indexOf(entry);
    if (index > 0) {
      this.entries.splice(index, 1);
      this.entries.unshift(entry);
    }
    this.renumber();
    this.touched = true;
  }

  remove(entry: DocumentEntry) {
    if (this.disabled) {
      return;
    }
    const index = this.entries.indexOf(entry);
    if (index < 0) {
      return;
    }
    this.entries.splice(index, 1);
    entry.removed = true;
    entry.isOriginal = false;
    if (entry.documentId) {
      this.removed.push(entry);
    }
    this.pinOriginal();
    this.touched = true;
  }

  get hasChanges(): boolean {
    return this.touched;
  }

  replace(documents: ManagedDocument[]) {
    this.restore(documents.map((document, index) => ({
      key: `saved-${document.documentId}`,
      documentId: document.documentId,
      fileName: document.fileName,
      fileType: document.fileType,
      fileSize: document.fileSize ?? 0,
      webViewLink: document.webViewLink ?? '',
      isOriginal: !!document.isOriginal,
      sortOrder: document.sortOrder ?? index,
      pendingFile: null,
      removed: false,
    })));
    this.touched = false;
  }

  capture(): DocumentDraft[] {
    return [...this.entries, ...this.removed].map((entry) => ({ ...entry }));
  }

  restore(drafts: DocumentDraft[]) {
    const copies = drafts.map((entry) => ({ ...entry }));
    this.removed = copies.filter((entry) => entry.removed);
    this.entries = copies.filter((entry) => !entry.removed);
    this.pinOriginal();
    this.seeded = true;
    this.touched = copies.some((entry) => entry.pendingFile || entry.removed);
  }

  sync(kind: DocumentKind, projectId: number, ownerId: number): Observable<unknown> {
    return syncDocumentDrafts(this.api, kind, projectId, ownerId, this.capture(), this.supportingOnly, this.sortBase);
  }

  fileSize(bytes: number): string {
    return formatFileSize(bytes);
  }

  private addFiles(files: File[]) {
    const accepted = files.filter((file) => this.accepted(file));
    if (!accepted.length) {
      return;
    }
    const makeOriginal = this.locksOriginal && !this.entries.some((entry) => entry.isOriginal);
    accepted.forEach((file, index) => {
      const entry: DocumentEntry = {
        key: `new-${crypto.randomUUID()}`,
        documentId: null,
        fileName: file.name,
        fileType: this.fileKind(file),
        fileSize: file.size,
        webViewLink: '',
        isOriginal: makeOriginal && index === 0,
        sortOrder: 0,
        pendingFile: file,
        removed: false,
      };
      if (entry.isOriginal) {
        this.entries.unshift(entry);
      } else {
        this.entries.push(entry);
      }
    });
    this.pinOriginal();
    this.touched = true;
    this.seeded = true;
  }

  private pinOriginal() {
    if (!this.locksOriginal) {
      if (this.supportingOnly) {
        for (const entry of this.entries) {
          entry.isOriginal = false;
        }
      }
      this.renumber();
      return;
    }
    let original = this.entries.find((entry) => entry.isOriginal);
    if (!original && this.entries.length) {
      original = this.entries[0];
      original.isOriginal = true;
    }
    for (const entry of this.entries) {
      entry.isOriginal = entry === original;
    }
    if (original) {
      const index = this.entries.indexOf(original);
      if (index > 0) {
        this.entries.splice(index, 1);
        this.entries.unshift(original);
      }
    }
    this.renumber();
  }

  private renumber() {
    this.visible.forEach((entry, index) => {
      entry.sortOrder = this.sortBase + index;
    });
  }

  private accepted(file: File): boolean {
    if (!this.accept.trim()) {
      return true;
    }
    const name = file.name.toLowerCase();
    return this.accept
      .split(',')
      .map((item) => item.trim().toLowerCase())
      .filter((item) => item.startsWith('.'))
      .some((extension) => name.endsWith(extension));
  }

  private fileKind(file: File): string {
    const extension = file.name.includes('.') ? file.name.split('.').pop()?.toLowerCase() ?? '' : '';
    const kinds: Record<string, string> = {
      pdf: 'PDF',
      doc: 'Word',
      docx: 'Word',
      xls: 'Excel',
      xlsx: 'Excel',
      xlsm: 'Excel',
      csv: 'CSV',
      png: 'Image',
      jpg: 'Image',
      jpeg: 'Image',
      gif: 'Image',
      webp: 'Image',
      bmp: 'Image',
      tif: 'Image',
      tiff: 'Image',
    };
    return kinds[extension] ?? (extension ? extension.toUpperCase() : 'File');
  }
}

export function documentSummary(documents: { fileName: string; isOriginal?: boolean; removed?: boolean }[], supportingOnly = false): string {
  const files = documents.filter((document) => !document.removed);
  if (!files.length) {
    return supportingOnly ? 'No supporting files.' : 'No files attached.';
  }
  const count = files.length === 1 ? '1 file' : `${files.length} files`;
  if (supportingOnly) {
    return count;
  }
  const original = files.find((document) => document.isOriginal);
  return original ? `${count} · Original: ${original.fileName}` : count;
}

export function syncDocumentDrafts(
  api: ProjectApiService,
  kind: DocumentKind,
  projectId: number,
  ownerId: number,
  entries: DocumentDraft[],
  supportingOnly: boolean,
  sortBase = 0,
): Observable<unknown> {
  const active = entries.filter((entry) => !entry.removed);
  const classify = kind !== 'project' && !supportingOnly;
  if (classify) {
    const original = active.find((entry) => entry.isOriginal) ?? active[0];
    if (original) {
      const index = active.indexOf(original);
      if (index > 0) {
        active.splice(index, 1);
        active.unshift(original);
      }
      active.forEach((entry) => {
        entry.isOriginal = entry === original;
      });
    }
  } else {
    active.forEach((entry) => {
      entry.isOriginal = false;
    });
  }
  active.forEach((entry, index) => {
    entry.sortOrder = sortBase + index;
  });
  const removals = entries.filter((entry) => entry.removed && entry.documentId);
  return from(removals).pipe(
    concatMap((entry) => removeStoredDocument(api, kind, projectId, ownerId, entry.documentId!)),
    toArray(),
    switchMap(() => from(active.filter((entry) => entry.pendingFile)).pipe(
      concatMap((entry) => api.uploadOwnedDocument(kind, projectId, ownerId, entry.pendingFile!, entry.isOriginal, entry.sortOrder).pipe(
        tap((document) => {
          entry.documentId = document.documentId;
          entry.fileName = document.fileName;
          entry.fileType = document.fileType;
          entry.webViewLink = document.webViewLink ?? '';
          entry.fileSize = document.fileSize || entry.fileSize;
          entry.pendingFile = null;
        }),
      )),
      toArray(),
    )),
    switchMap(() => {
      const saved = active.filter((entry) => entry.documentId);
      if (!saved.length) {
        return of(null);
      }
      return api.orderDocuments(kind, projectId, ownerId, {
        documents: saved.map((entry) => ({ documentId: entry.documentId!, sortOrder: entry.sortOrder })),
        originalDocumentId: classify ? saved.find((entry) => entry.isOriginal)?.documentId ?? null : null,
      });
    }),
  );
}

function removeStoredDocument(api: ProjectApiService, kind: DocumentKind, projectId: number, ownerId: number, documentId: number) {
  if (kind === 'project') {
    return api.removeProjectDocument(projectId, documentId);
  }
  return kind === 'quotation'
    ? api.removeQuotationDocument(projectId, ownerId, documentId)
    : api.removePurchaseOrderDocument(projectId, ownerId, documentId);
}
