import { Injectable } from '@angular/core';
import { Subject } from 'rxjs';

@Injectable({ providedIn: 'root' })
export class PurchaseOrderSync {
  private readonly bus = new Subject<number>();
  readonly changes$ = this.bus.asObservable();

  notify(projectId: number) {
    this.bus.next(projectId);
  }
}
