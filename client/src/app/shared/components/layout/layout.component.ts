import { CommonModule } from '@angular/common';
import { Component, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatMenuModule } from '@angular/material/menu';
import { MatSidenavModule } from '@angular/material/sidenav';
import { MatTooltipModule } from '@angular/material/tooltip';
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
    MatSidenavModule,
    MatIconModule,
    MatButtonModule,
    MatMenuModule,
    MatTooltipModule,
  ],
  template: `
    <mat-sidenav-container class="layout-container">
      <!-- Sidebar -->
      <mat-sidenav mode="side" [opened]="sidenavOpen()" class="sidenav">
        <div class="brand">
          <div class="brand-mark">
            <mat-icon>shield</mat-icon>
          </div>
          <div class="brand-text">
            <span class="brand-name">Vulscan</span>
            <span class="brand-tagline">Vulnerability Intelligence</span>
          </div>
        </div>

        <nav class="nav-list">
          <a class="nav-item" routerLink="/dashboard" routerLinkActive="active">
            <span class="nav-indicator"></span>
            <mat-icon>space_dashboard</mat-icon>
            <span class="nav-label">Dashboard</span>
          </a>
          <a class="nav-item" routerLink="/scans" routerLinkActive="active">
            <span class="nav-indicator"></span>
            <mat-icon>radar</mat-icon>
            <span class="nav-label">Scans</span>
          </a>
          <a class="nav-item" routerLink="/discovery" routerLinkActive="active">
            <span class="nav-indicator"></span>
            <mat-icon>travel_explore</mat-icon>
            <span class="nav-label">Discovery</span>
          </a>
          <a class="nav-item" routerLink="/packages" routerLinkActive="active">
            <span class="nav-indicator"></span>
            <mat-icon>inventory_2</mat-icon>
            <span class="nav-label">Packages</span>
          </a>
          <a class="nav-item" routerLink="/reports" routerLinkActive="active">
            <span class="nav-indicator"></span>
            <mat-icon>insights</mat-icon>
            <span class="nav-label">Reports</span>
          </a>
          @if (auth.isAdmin()) {
            <a class="nav-item" routerLink="/settings" routerLinkActive="active">
              <span class="nav-indicator"></span>
              <mat-icon>settings</mat-icon>
              <span class="nav-label">Settings</span>
            </a>
          }
        </nav>

        <div class="sidebar-footer">
          <div class="user-card">
            <div class="user-avatar">{{ initials() }}</div>
            <div class="user-meta">
              <strong>{{ auth.user()?.username }}</strong>
              <small>{{ auth.user()?.role }}</small>
            </div>
            <button mat-icon-button [matMenuTriggerFor]="userMenu" class="user-menu-trigger">
              <mat-icon>more_vert</mat-icon>
            </button>
            <mat-menu #userMenu="matMenu" xPosition="before">
              <button mat-menu-item (click)="auth.logout()">
                <mat-icon>logout</mat-icon>
                <span>Sign out</span>
              </button>
            </mat-menu>
          </div>
        </div>
      </mat-sidenav>

      <!-- Main content -->
      <mat-sidenav-content class="main-content">
        <header class="top-toolbar">
          <button mat-icon-button (click)="toggleSidenav()" aria-label="Toggle navigation">
            <mat-icon>menu</mat-icon>
          </button>
          <div class="toolbar-title">
            <span class="title-eyebrow">Workspace</span>
            <span class="title-main">Vulnerability Scanner</span>
          </div>
          <span class="spacer"></span>
          <button mat-icon-button matTooltip="Notifications" class="toolbar-action">
            <mat-icon>notifications_none</mat-icon>
          </button>
          <button mat-icon-button matTooltip="Help" class="toolbar-action">
            <mat-icon>help_outline</mat-icon>
          </button>
        </header>

        <div class="content-area">
          <router-outlet />
        </div>
      </mat-sidenav-content>
    </mat-sidenav-container>
  `,
  styles: `
    :host {
      display: block;
      height: 100vh;
    }

    .layout-container {
      height: 100vh;
    }

    /* Sidebar ────────────────────────────────────────────────── */
    .sidenav {
      width: 260px;
      background: var(--gradient-navy);
      color: rgba(255, 255, 255, 0.85);
      border: 0;
      display: flex;
      flex-direction: column;
    }

    .brand {
      display: flex;
      align-items: center;
      gap: 12px;
      padding: 20px 20px 24px;
      border-bottom: 1px solid rgba(255, 255, 255, 0.06);
    }

    .brand-mark {
      width: 40px;
      height: 40px;
      border-radius: 12px;
      background: var(--gradient-brand);
      display: flex;
      align-items: center;
      justify-content: center;
      box-shadow: 0 4px 12px rgba(17, 156, 140, 0.3);
    }

    .brand-mark mat-icon {
      color: #fff;
      font-size: 22px;
      width: 22px;
      height: 22px;
    }

    .brand-text {
      display: flex;
      flex-direction: column;
      line-height: 1.15;
    }

    .brand-name {
      color: #fff;
      font-size: 18px;
      font-weight: 700;
      letter-spacing: 0.01em;
    }

    .brand-tagline {
      font-size: 11px;
      color: rgba(255, 255, 255, 0.55);
      text-transform: uppercase;
      letter-spacing: 0.08em;
      margin-top: 2px;
    }

    .nav-list {
      flex: 1;
      padding: 16px 12px;
      display: flex;
      flex-direction: column;
      gap: 4px;
    }

    .nav-item {
      position: relative;
      display: flex;
      align-items: center;
      gap: 12px;
      padding: 11px 14px 11px 18px;
      border-radius: 10px;
      color: rgba(255, 255, 255, 0.7);
      text-decoration: none;
      font-size: 14px;
      font-weight: 500;
      cursor: pointer;
      transition: background 0.15s ease, color 0.15s ease;
    }

    .nav-item:hover {
      background: rgba(255, 255, 255, 0.06);
      color: #fff;
    }

    .nav-item mat-icon {
      font-size: 20px;
      width: 20px;
      height: 20px;
      color: inherit;
      opacity: 0.9;
    }

    .nav-indicator {
      position: absolute;
      left: 0;
      top: 8px;
      bottom: 8px;
      width: 3px;
      border-radius: 0 3px 3px 0;
      background: transparent;
      transition: background 0.15s ease;
    }

    .nav-item.active {
      background: rgba(17, 156, 140, 0.16);
      color: #fff;
    }

    .nav-item.active .nav-indicator {
      background: var(--brand-teal);
    }

    .nav-item.active mat-icon {
      color: var(--brand-teal);
      opacity: 1;
    }

    /* Sidebar footer ─────────────────────────────────────────── */
    .sidebar-footer {
      padding: 12px;
      border-top: 1px solid rgba(255, 255, 255, 0.06);
    }

    .user-card {
      display: flex;
      align-items: center;
      gap: 10px;
      padding: 10px 12px;
      border-radius: 12px;
      background: rgba(255, 255, 255, 0.04);
    }

    .user-avatar {
      width: 36px;
      height: 36px;
      border-radius: 10px;
      background: var(--gradient-brand);
      color: #fff;
      display: flex;
      align-items: center;
      justify-content: center;
      font-weight: 700;
      font-size: 13px;
      flex-shrink: 0;
    }

    .user-meta {
      display: flex;
      flex-direction: column;
      flex: 1;
      min-width: 0;
      line-height: 1.2;
    }

    .user-meta strong {
      color: #fff;
      font-size: 13px;
      font-weight: 600;
      overflow: hidden;
      text-overflow: ellipsis;
      white-space: nowrap;
    }

    .user-meta small {
      color: rgba(255, 255, 255, 0.55);
      font-size: 11px;
      text-transform: uppercase;
      letter-spacing: 0.05em;
    }

    .user-menu-trigger {
      color: rgba(255, 255, 255, 0.6);
    }

    /* Top toolbar ────────────────────────────────────────────── */
    .top-toolbar {
      position: sticky;
      top: 0;
      z-index: 10;
      display: flex;
      align-items: center;
      gap: 8px;
      height: 64px;
      padding: 0 24px 0 12px;
      background: #ffffffcc;
      backdrop-filter: saturate(140%) blur(8px);
      border-bottom: 1px solid var(--neutral-200);
    }

    .toolbar-title {
      display: flex;
      flex-direction: column;
      line-height: 1.1;
    }

    .title-eyebrow {
      font-size: 11px;
      font-weight: 600;
      color: var(--neutral-500);
      letter-spacing: 0.08em;
      text-transform: uppercase;
    }

    .title-main {
      font-size: 15px;
      font-weight: 600;
      color: var(--neutral-900);
    }

    .spacer {
      flex: 1;
    }

    .toolbar-action {
      color: var(--neutral-700);
    }

    .content-area {
      padding: 28px 32px 40px;
      background: var(--surface-page);
      min-height: calc(100vh - 64px);
    }

    @media (max-width: 768px) {
      .sidenav {
        width: 240px;
      }
      .content-area {
        padding: 16px;
      }
    }
  `,
})
export class LayoutComponent {
  readonly sidenavOpen = signal(true);

  constructor(public readonly auth: AuthService) {}

  toggleSidenav(): void {
    this.sidenavOpen.update((v) => !v);
  }

  initials(): string {
    const u = this.auth.user()?.username ?? '';
    if (!u) return '?';
    const parts = u.split(/[\s._-]+/).filter(Boolean);
    if (parts.length === 1) return parts[0].substring(0, 2).toUpperCase();
    return (parts[0][0] + parts[parts.length - 1][0]).toUpperCase();
  }
}
