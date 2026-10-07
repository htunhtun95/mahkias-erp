import { Component } from '@angular/core';

@Component({
  selector: 'app-project-purchases-tab',
  standalone: true,
  templateUrl: './project-purchases-tab.html',
  styleUrl: './project-purchases-tab.css',
})
export class ProjectPurchasesTabComponent {
  readonly trackers = [
    { label: 'PO Sent', value: '0', tone: 'sent' },
    { label: 'In Transit', value: '0', tone: 'transit' },
    { label: 'Received', value: '0', tone: 'received' },
  ];
}
