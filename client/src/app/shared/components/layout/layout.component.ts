import { CommonModule } from '@angular/common';
import { Component, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatListModule } from '@angular/material/list';
import { MatMenuModule } from '@angular/material/menu';
import { MatSidenavModule } from '@angular/material/sidenav';
import { MatToolbarModule } from '@angular/material/toolbar';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthService } from '../../../core/auth/auth.service';

@Component({
  selector: 'app-layout',
  standalone: true,
  imports: [
    CommonModule,
    RouterOutlet,
    RouterLink,
    RouterLinkActive,
    MatToolbarModule,
    MatSidenavModule,
    MatListModule,
    MatIconModule,
    MatButtonModule,
    MatMenuModule,
  ],
  template: `
    <mat-sidenav-container class="layout-container">
      <!-- Sidebar -->
      <mat-sidenav mode="side" [opened]="sidenavOpen()" class="sidenav">
        <div class="sidenav-header">
          <mat-icon class="logo-icon">shield</mat-icon>
          <span class="logo-text">Vulscan</span>
        </div>

        <mat-nav-list>
          <a mat-list-item routerLink="/dashboard" routerLinkActive="active-link">
            <mat-icon matListItemIcon>dashboard</mat-icon>
            <span matListItemTitle>Dashboard</span>
          </a>
          <a mat-list-item routerLink="/scans" routerLinkActive="active-link">
            <mat-icon matListItemIcon>radar</mat-icon>
            <span matListItemTitle>Scans</span>
          </a>
          <a mat-list-item routerLink="/reports" routerLinkActive="active-link">
            <mat-icon matListItemIcon>assessment</mat-icon>
            <span matListItemTitle>Reports</span>
          </a>
        </mat-nav-list>
      </mat-sidenav>

      <!-- Main content -->
      <mat-sidenav-content class="main-content">
        <mat-toolbar color="primary" class="top-toolbar">
          <button mat-icon-button (click)="toggleSidenav()">
            <mat-icon>menu</mat-icon>
          </button>
          <span class="toolbar-title">Vulscan — Vulnerability Scanner</span>
          <span class="spacer"></span>
          <button mat-icon-button [matMenuTriggerFor]="userMenu">
            <mat-icon>account_circle</mat-icon>
          </button>
          <mat-menu #userMenu="matMenu">
            <div class="user-info">
              <strong>{{ auth.user()?.username }}</strong>
              <small>{{ auth.user()?.role }}</small>
            </div>
            <mat-divider></mat-divider>
            <button mat-menu-item (click)="auth.logout()">
              <mat-icon>logout</mat-icon>
              <span>Logout</span>
            </button>
          </mat-menu>
        </mat-toolbar>

        <div class="content-area">
          <router-outlet />
        </div>
      </mat-sidenav-content>
    </mat-sidenav-container>
  `,
  styles: `
    .layout-container {
      height: 100vh;
    }

    .sidenav {
      width: 240px;
      background: #1a237e;
    }

    .sidenav-header {
      display: flex;
      align-items: center;
      padding: 16px;
      gap: 8px;
      color: white;
      border-bottom: 1px solid rgba(255, 255, 255, 0.12);
    }

    .logo-icon {
      font-size: 32px;
      width: 32px;
      height: 32px;
      color: #4fc3f7;
    }

    .logo-text {
      font-size: 20px;
      font-weight: 600;
      letter-spacing: 1px;
    }

    .sidenav ::ng-deep .mat-mdc-list-item {
      color: rgba(255, 255, 255, 0.8);
    }

    .sidenav ::ng-deep .active-link {
      background: rgba(255, 255, 255, 0.12);
      color: white;
    }

    .sidenav ::ng-deep .mat-icon {
      color: rgba(255, 255, 255, 0.8);
    }

    .top-toolbar {
      position: sticky;
      top: 0;
      z-index: 10;
    }

    .toolbar-title {
      margin-left: 8px;
      font-size: 16px;
      font-weight: 500;
    }

    .spacer {
      flex: 1;
    }

    .content-area {
      padding: 24px;
      background: #f5f5f5;
      min-height: calc(100vh - 64px);
    }

    .user-info {
      padding: 12px 16px;
      display: flex;
      flex-direction: column;
      gap: 2px;
    }

    .user-info small {
      color: rgba(0, 0, 0, 0.54);
    }
  `,
})
export class LayoutComponent {
  readonly sidenavOpen = signal(true);

  constructor(public readonly auth: AuthService) {}

  toggleSidenav(): void {
    this.sidenavOpen.update((v) => !v);
  }
}
