import { Injectable } from '@angular/core';
import { Subject } from 'rxjs';

@Injectable({ providedIn: 'root' })
export class ProcurementTrace {
  private readonly activityRequests = new Subject<{ projectId: number; activityId: number }>();
  private readonly orderRequests = new Subject<{ projectId: number; purchaseOrderId: number }>();

  readonly activities$ = this.activityRequests.asObservable();
  readonly orders$ = this.orderRequests.asObservable();

  showActivity(projectId: number, activityId: number) {
    this.activityRequests.next({ projectId, activityId });
  }

  showOrder(projectId: number, purchaseOrderId: number) {
    this.orderRequests.next({ projectId, purchaseOrderId });
  }
}
