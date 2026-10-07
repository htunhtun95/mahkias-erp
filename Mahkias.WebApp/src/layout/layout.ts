import { CommonModule } from '@angular/common';
import { Component } from '@angular/core';
import { Router, RouterModule } from '@angular/router';
import { MaterialModule } from '../app/material-module';
import { GoogleAuthService } from '../auth/google-auth.service';

@Component({
  selector: 'app-layout',
  standalone: true,
  imports: [CommonModule, RouterModule, MaterialModule],
  templateUrl: './layout.html',
  styleUrl: './layout.css',
})
export class LayoutComponent {
  user: any;

  constructor(
    private googleAuth: GoogleAuthService,
    private router: Router,
  ) {
    this.user = googleAuth.user;
  }

  logout() {
    this.googleAuth.logout();
    this.router.navigate(['/login']);
  }
}
