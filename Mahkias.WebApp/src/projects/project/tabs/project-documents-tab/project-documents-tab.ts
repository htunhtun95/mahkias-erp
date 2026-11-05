import { Component, Input, OnChanges, inject } from '@angular/core';
import { MatDialog } from '@angular/material/dialog';
import { MaterialModule } from '../../../../app/material-module';
import { Project, ProjectApiService, ProjectDocument } from '../../../../core/services/project-api.service';
import { formatFileSize } from '../../../document-manager/document-manager';
import { documentAccept, openDocumentLibrary } from '../../../document-library-dialog/document-library-dialog';

@Component({
  selector: 'app-project-documents-tab',
  standalone: true,
  imports: [MaterialModule],
  templateUrl: './project-documents-tab.html',
  styleUrl: './project-documents-tab.css',
})
export class ProjectDocumentsTabComponent implements OnChanges {
  private api = inject(ProjectApiService);
  private dialog = inject(MatDialog);

  @Input() project: Project | null = null;
  @Input() disabled = false;

  documents: ProjectDocument[] = [];
  loading = false;
  error = '';
  private loadedFor: number | null = null;

  ngOnChanges() {
    const id = this.project?.id;
    if (!id || id === this.loadedFor) {
      return;
    }
    this.load(id);
  }

  fileSize = formatFileSize;

  added(value?: string | null): string {
    if (!value) {
      return '—';
    }
    const date = new Date(value);
    if (Number.isNaN(date.getTime())) {
      return '—';
    }
    return date.toLocaleDateString(undefined, { day: 'numeric', month: 'short', year: 'numeric' });
  }

  manage() {
    const project = this.project;
    if (!project || this.disabled) {
      return;
    }
    openDocumentLibrary(this.dialog, {
      title: 'Manage documents',
      note: 'Drag any file to set the order for this project.',
      kind: 'project',
      projectId: project.id,
      ownerId: project.id,
      documents: this.documents,
      accept: documentAccept,
    }).afterClosed().subscribe((result) => {
      if (result?.saved) {
        this.load(project.id, true);
      }
    });
  }

  private load(projectId: number, refresh = false) {
    if (!refresh) {
      this.loadedFor = projectId;
    }
    this.loading = true;
    this.error = '';
    this.api.projectDocuments(projectId).subscribe({
      next: (documents) => {
        if (this.project?.id !== projectId) {
          return;
        }
        this.documents = documents ?? [];
        this.loading = false;
      },
      error: () => {
        if (this.project?.id === projectId) {
          this.loading = false;
          this.error = 'Documents could not be loaded.';
        }
      },
    });
  }
}
