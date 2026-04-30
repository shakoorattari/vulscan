import { Routes } from '@angular/router';
import { authGuard } from './core/guards/auth.guard';

export const routes: Routes = [
  {
    path: 'login',
    loadComponent: () => import('./features/login/login.component').then((m) => m.LoginComponent),
  },
  {
    path: '',
    loadComponent: () =>
      import('./shared/components/layout/layout.component').then((m) => m.LayoutComponent),
    canActivate: [authGuard],
    children: [
      { path: '', redirectTo: 'dashboard', pathMatch: 'full' },
      {
        path: 'dashboard',
        loadComponent: () =>
          import('./features/dashboard/dashboard.component').then((m) => m.DashboardComponent),
      },
      {
        path: 'scans',
        loadComponent: () =>
          import('./features/scans/scans.component').then((m) => m.ScansComponent),
      },
      {
        path: 'scans/:scanId/report',
        loadComponent: () =>
          import('./features/scans/scan-report.component').then((m) => m.ScanReportComponent),
      },
      {
        path: 'packages',
        loadComponent: () =>
          import('./features/packages/packages.component').then((m) => m.PackagesComponent),
      },
      {
        path: 'reports',
        loadComponent: () =>
          import('./features/reports/reports.component').then((m) => m.ReportsComponent),
      },
      {
        path: 'reports/projects/:projectId',
        loadComponent: () =>
          import('./features/reports/project-detail.component').then((m) => m.ProjectDetailComponent),
      },
      {
        path: 'reports/vulnerabilities/:cveId',
        loadComponent: () =>
          import('./features/reports/vulnerability-detail.component').then(
            (m) => m.VulnerabilityDetailComponent,
          ),
      },
    ],
  },
  { path: '**', redirectTo: '' },
];
