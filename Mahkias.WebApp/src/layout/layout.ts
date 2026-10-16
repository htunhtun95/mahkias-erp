import { CommonModule } from '@angular/common';
import { Component } from '@angular/core';
import { Router, RouterModule } from '@angular/router';
import { MaterialModule } from '../app/material-module';
import { GoogleAuthService } from '../auth/google-auth.service';

interface NavItem {
  id: string;
  label: string;
  icon: string;
  link: string;
  exact?: boolean;
}

@Component({
  selector: 'app-layout',
  standalone: true,
  imports: [CommonModule, RouterModule, MaterialModule],
  templateUrl: './layout.html',
  styleUrl: './layout.css',
})
export class LayoutComponent {
  user: any;
  collapsed = false;

  readonly nav: NavItem[] = [
    { id: 'dashboard', label: 'Dashboard', icon: 'space_dashboard', link: '/dashboard', exact: true },
    { id: 'projects', label: 'Projects', icon: 'folder', link: '/projects' },
    { id: 'suppliers', label: 'Suppliers', icon: 'local_shipping', link: '/suppliers' },
  ];

  constructor(
    private googleAuth: GoogleAuthService,
    private router: Router,
  ) {
    this.user = googleAuth.user;
    this.collapsed = localStorage.getItem('sidebar_collapsed') === '1';
  }

  logout() {
    this.googleAuth.logout();
    this.router.navigate(['/login']);
  }

  toggleCollapsed() {
    this.collapsed = !this.collapsed;
    localStorage.setItem('sidebar_collapsed', this.collapsed ? '1' : '0');
  }
}
