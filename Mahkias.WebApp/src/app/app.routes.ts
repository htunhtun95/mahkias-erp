import { Routes } from '@angular/router';
import { AuthGuard } from '../auth/auth-guard';
import { DashboardComponent } from '../dashboard/dashboard';
import { LayoutComponent } from '../layout/layout';

export const routes: Routes = [
  {
    path: 'login',
    loadComponent: () => import('../login/login').then((m) => m.LoginComponent),
  },
  {
    path: '',
    canActivate: [AuthGuard],
    component: LayoutComponent,
    children: [
      { path: '', pathMatch: 'full', redirectTo: 'dashboard' },
      { path: 'dashboard', component: DashboardComponent },
      {
        path: 'projects',
        loadComponent: () => import('../projects/projects').then((m) => m.ProjectsComponent),
      },
      {
        path: 'projects/:id',
        loadComponent: () => import('../projects/project/project').then((m) => m.ProjectDetailsComponent),
      },
      {
        path: 'suppliers',
        loadComponent: () => import('../suppliers/suppliers').then((module) => module.SuppliersComponent),
      },
      {
        path: 'suppliers/:id',
        loadComponent: () => import('../suppliers/supplier/supplier').then((m) => m.SupplierDetailsComponent),
      },
    ],
  },
  { path: '**', redirectTo: 'dashboard' },
];
