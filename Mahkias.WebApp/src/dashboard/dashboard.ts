import { Component } from '@angular/core';
import { MaterialModule } from '../app/material-module';
import { GoogleAuthService } from '../auth/google-auth.service';

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [MaterialModule],
  templateUrl: './dashboard.html',
  styleUrl: './dashboard.css',
})
export class DashboardComponent {
  userName = '';

  readonly modules = [
    { icon: 'folder', title: 'Projects', description: 'Track jobs, deadlines, and status.' },
    { icon: 'inventory_2', title: 'Parts', description: 'Catalogue parts and manufacturers.' },
    { icon: 'request_quote', title: 'Quotations', description: 'Price requests and supplier quotes.' },
    { icon: 'shopping_cart', title: 'Orders', description: 'Purchase orders and fulfilment.' },
  ];

  constructor(googleAuth: GoogleAuthService) {
    this.userName = googleAuth.user?.given_name || googleAuth.user?.name || '';
  }
}
