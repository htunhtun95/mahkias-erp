import { Injectable, inject } from '@angular/core';
import { Subject } from 'rxjs';
import { ProjectApiService } from '../core/services/project-api.service';

@Injectable({ providedIn: 'root' })
export class QuotationSync {
  private readonly api = inject(ProjectApiService);
  private readonly bus = new Subject<number>();
  readonly changes$ = this.bus.asObservable();

  notify(projectId: number) {
    this.api.forgetProject(projectId);
    this.bus.next(projectId);
  }
}
