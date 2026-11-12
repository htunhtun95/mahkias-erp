import { Component, Input, OnChanges, inject } from '@angular/core';
import { MaterialModule } from '../../../../app/material-module';
import { Project, ProjectApiService, ProjectHistoryEntry } from '../../../../core/services/project-api.service';

@Component({
  selector: 'app-project-history-tab',
  standalone: true,
  imports: [MaterialModule],
  templateUrl: './project-history-tab.html',
  styleUrl: './project-history-tab.css',
})
export class ProjectHistoryTabComponent implements OnChanges {
  private api = inject(ProjectApiService);

  @Input() project: Project | null = null;

  entries: ProjectHistoryEntry[] = [];
  loading = false;
  error = '';

  ngOnChanges() {
    const id = this.project?.id;
    if (!id) {
      this.entries = [];
      return;
    }
    this.load(id);
  }

  track(entry: ProjectHistoryEntry): string {
    return `${entry.source}-${entry.id}`;
  }

  label(action: string): string {
    switch (action) {
      case 'Created':
        return 'Created';
      case 'Updated':
        return 'Updated';
      case 'Deleted':
        return 'Deleted';
      case 'ActivityAdded':
        return 'Activity added';
      case 'ActivityUpdated':
        return 'Activity updated';
      case 'ActivityRemoved':
        return 'Activity removed';
      case 'QuotationCreated':
        return 'Quotation created';
      case 'QuotationUpdated':
        return 'Quotation updated';
      case 'QuotationDeleted':
        return 'Quotation deleted';
      case 'DocumentUploaded':
        return 'Document uploaded';
      case 'DocumentUpdated':
        return 'Document updated';
      case 'DocumentRemoved':
        return 'Document removed';
      case 'DocumentReordered':
        return 'Documents reordered';
      default:
        return action || 'Updated';
    }
  }

  actor(entry: ProjectHistoryEntry): string {
    const name = entry.actorName?.trim();
    const email = entry.actorEmail?.trim();
    if (name && email) {
      return `${name} · ${email}`;
    }
    return name || email || '';
  }

  when(value?: string | null): string {
    if (!value) {
      return '';
    }
    const date = new Date(value);
    if (Number.isNaN(date.getTime())) {
      return '';
    }
    return date.toLocaleString(undefined, {
      day: 'numeric',
      month: 'short',
      year: 'numeric',
      hour: 'numeric',
      minute: '2-digit',
    });
  }

  private load(projectId: number) {
    this.loading = true;
    this.error = '';
    this.api.projectHistory(projectId).subscribe({
      next: (entries) => {
        if (this.project?.id !== projectId) {
          return;
        }
        this.entries = (entries ?? []).filter((entry) => entry.source !== 'PurchaseOrder');
        this.loading = false;
      },
      error: () => {
        if (this.project?.id !== projectId) {
          return;
        }
        this.entries = [];
        this.loading = false;
        this.error = 'History could not be loaded.';
      },
    });
  }
}
