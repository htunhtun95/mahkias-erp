import { Component, EventEmitter, Input, Output } from '@angular/core';
import { MaterialModule } from '../../app/material-module';

export interface StoredDriveFile {
  fileName: string;
  fileType: string;
  webViewLink: string;
}

@Component({
  selector: 'app-drive-upload-card',
  standalone: true,
  imports: [MaterialModule],
  templateUrl: './drive-upload-card.html',
  styleUrl: './drive-upload-card.css',
})
export class DriveUploadCardComponent {
  @Input() title = 'Attachment File';
  @Input() hint = '';
  @Input() emptyLabel = 'Drag a file here, or browse';
  @Input() accept = '';
  @Input() disabled = false;
  @Input() file: File | null = null;
  @Output() fileChange = new EventEmitter<File | null>();
  @Input() saved: StoredDriveFile[] = [];
  @Output() removeSaved = new EventEmitter<void>();

  dragging = false;

  onDragOver(event: DragEvent) {
    event.preventDefault();
    event.stopPropagation();
    if (!this.disabled) {
      this.dragging = true;
    }
  }

  onDragLeave(event: DragEvent) {
    event.preventDefault();
    event.stopPropagation();
    const next = event.relatedTarget as Node | null;
    if (next && (event.currentTarget as HTMLElement).contains(next)) {
      return;
    }
    this.dragging = false;
  }

  onDrop(event: DragEvent) {
    event.preventDefault();
    event.stopPropagation();
    this.dragging = false;
    if (this.disabled) {
      return;
    }
    this.take(Array.from(event.dataTransfer?.files ?? []));
  }

  browse(picker: HTMLInputElement) {
    if (!this.disabled) {
      picker.click();
    }
  }

  onBrowse(event: Event) {
    const input = event.target as HTMLInputElement;
    this.take(Array.from(input.files ?? []));
    input.value = '';
  }

  remove() {
    if (this.disabled) {
      return;
    }
    this.fileChange.emit(null);
  }

  fileSize(bytes: number): string {
    if (bytes < 1024) {
      return `${bytes} B`;
    }
    if (bytes < 1024 * 1024) {
      return `${(bytes / 1024).toFixed(1)} KB`;
    }
    return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
  }

  fileKind(file: File): string {
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

  private take(incoming: File[]) {
    const accepted = incoming.find((file) => this.accepted(file));
    if (!accepted) {
      return;
    }
    this.fileChange.emit(accepted);
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
}
