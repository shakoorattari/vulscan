import { CommonModule } from '@angular/common';
import { Component, OnInit, computed, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatRippleModule } from '@angular/material/core';
import { MatDividerModule } from '@angular/material/divider';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatMenuModule } from '@angular/material/menu';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSelectModule } from '@angular/material/select';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatTableModule } from '@angular/material/table';
import { MatTooltipModule } from '@angular/material/tooltip';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { NgSelectModule } from '@ng-select/ng-select';
import { forkJoin } from 'rxjs';
import { ProjectSummary, ScanRun, Vulnerability } from '../../core/models/api.models';
import { ApiService } from '../../core/services/api.service';

@Component({
  selector: 'app-scan-report',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    RouterLink,
    MatCardModule,
    MatButtonModule,
    MatIconModule,
    MatMenuModule,
    MatTableModule,
    MatFormFieldModule,
    MatSelectModule,
    MatRippleModule,
    MatProgressSpinnerModule,
    MatSnackBarModule,
    MatDividerModule,
    MatTooltipModule,
    NgSelectModule,
  ],
  template: `
    <div class="report-page">
      <!-- Toolbar (hidden in print) -->
      <div class="report-toolbar no-print">
        <div class="back">
          <a mat-button routerLink="/scans">
            <mat-icon>arrow_back</mat-icon>
            Back to Scans
          </a>
        </div>
        <div class="toolbar-actions">
          <button
            mat-stroked-button
            [routerLink]="['/packages']"
            [queryParams]="{ scanRunId: scanId, projectId: selectedProjectId() }"
            [disabled]="!scan()"
            matTooltip="Browse all discovered packages with vulnerability status"
          >
            <mat-icon>inventory_2</mat-icon>
            View Packages
          </button>
          <button
            mat-stroked-button
            (click)="rescan()"
            [disabled]="!scan() || rescanning()"
            matTooltip="Re-run this scan (re-process the same project)"
          >
            <mat-icon>refresh</mat-icon>
            {{ rescanning() ? 'Triggering…' : 'Re-scan' }}
          </button>
          <button
            mat-stroked-button
            (click)="downloadCsv()"
            [disabled]="!scan() || downloading()"
            matTooltip="Export vulnerabilities as CSV (opens in Excel)"
          >
            <mat-icon>table_view</mat-icon>
            Export CSV
          </button>
          <button
            mat-flat-button
            color="primary"
            (click)="downloadPdf()"
            [disabled]="!scan()"
            matTooltip="Open print dialog → Save as PDF"
          >
            <mat-icon>picture_as_pdf</mat-icon>
            Download PDF
          </button>
        </div>
      </div>

      @if (loading()) {
        <div class="loading-container">
          <mat-spinner diameter="48"></mat-spinner>
        </div>
      } @else if (errorMessage()) {
        <mat-card class="error-card">
          <mat-card-content>
            <mat-icon>error_outline</mat-icon>
            <span>{{ errorMessage() }}</span>
          </mat-card-content>
        </mat-card>
      } @else if (scan(); as s) {
        <!-- Report cover -->
        <section class="cover">
          <div class="cover-meta">
            <span class="eyebrow">Vulnerability Scan Report</span>
            <h1>{{ s.instanceName ?? 'Untitled Project' }}</h1>
            <p class="subtitle">
              Scan <strong>#{{ s.id }}</strong> · executed
              {{ s.startedAt | date : 'medium' }}
            </p>
          </div>
          <div class="cover-stamp">
            <span class="status-badge" [class]="s.status.toLowerCase()">
              <span class="status-dot"></span>
              {{ s.status }}
            </span>
            <span class="duration"
              >Duration: {{ s.durationSeconds > 0 ? s.durationSeconds + 's' : '—' }}</span
            >
            <span class="trigger">Triggered by {{ s.triggeredBy ?? 'System' }}</span>
          </div>
        </section>

        <!-- Severity summary -->
        <section class="summary-grid">
          <div class="summary-card critical">
            <div class="sc-icon"><mat-icon>error</mat-icon></div>
            <div class="sc-body">
              <span class="sc-value">{{ s.criticalCount }}</span>
              <span class="sc-label">Critical</span>
            </div>
          </div>
          <div class="summary-card high">
            <div class="sc-icon"><mat-icon>warning</mat-icon></div>
            <div class="sc-body">
              <span class="sc-value">{{ s.highCount }}</span>
              <span class="sc-label">High</span>
            </div>
          </div>
          <div class="summary-card medium">
            <div class="sc-icon"><mat-icon>info</mat-icon></div>
            <div class="sc-body">
              <span class="sc-value">{{ s.mediumCount }}</span>
              <span class="sc-label">Medium</span>
            </div>
          </div>
          <div class="summary-card low">
            <div class="sc-icon"><mat-icon>check_circle</mat-icon></div>
            <div class="sc-body">
              <span class="sc-value">{{ s.lowCount }}</span>
              <span class="sc-label">Low</span>
            </div>
          </div>
          <div class="summary-card total">
            <div class="sc-icon"><mat-icon>bug_report</mat-icon></div>
            <div class="sc-body">
              <span class="sc-value">{{ s.totalVulnerabilities }}</span>
              <span class="sc-label">Total findings</span>
            </div>
          </div>
          <div class="summary-card repos">
            <div class="sc-icon"><mat-icon>folder</mat-icon></div>
            <div class="sc-body">
              <span class="sc-value">{{ s.reposScanned }}</span>
              <span class="sc-label">Repositories scanned</span>
            </div>
          </div>
        </section>

        <!-- Severity distribution bar -->
        <mat-card class="distribution">
          <mat-card-header>
            <mat-card-title>Severity distribution</mat-card-title>
            <mat-card-subtitle>Composition of detected vulnerabilities</mat-card-subtitle>
          </mat-card-header>
          <mat-card-content>
            @if (s.totalVulnerabilities > 0) {
              <div class="bar">
                @if (s.criticalCount > 0) {
                  <span
                    class="bar-segment critical"
                    [style.flex]="s.criticalCount"
                    [matTooltip]="s.criticalCount + ' Critical'"
                  ></span>
                }
                @if (s.highCount > 0) {
                  <span
                    class="bar-segment high"
                    [style.flex]="s.highCount"
                    [matTooltip]="s.highCount + ' High'"
                  ></span>
                }
                @if (s.mediumCount > 0) {
                  <span
                    class="bar-segment medium"
                    [style.flex]="s.mediumCount"
                    [matTooltip]="s.mediumCount + ' Medium'"
                  ></span>
                }
                @if (s.lowCount > 0) {
                  <span
                    class="bar-segment low"
                    [style.flex]="s.lowCount"
                    [matTooltip]="s.lowCount + ' Low'"
                  ></span>
                }
              </div>
              <div class="bar-legend">
                <span class="legend-item"><span class="dot critical"></span>Critical</span>
                <span class="legend-item"><span class="dot high"></span>High</span>
                <span class="legend-item"><span class="dot medium"></span>Medium</span>
                <span class="legend-item"><span class="dot low"></span>Low</span>
              </div>
            } @else {
              <div class="all-clear">
                <mat-icon>verified</mat-icon>
                <div>
                  <strong>No vulnerabilities detected</strong>
                  <span>This scan completed cleanly with zero findings.</span>
                </div>
              </div>
            }
          </mat-card-content>
        </mat-card>

        <!-- Projects scanned (collapsible) -->
        @if (projects().length > 1) {
          <mat-card class="projects-summary">
            <button
              class="expand-toggle"
              (click)="projectsExpanded.set(!projectsExpanded())"
              matRipple
            >
              <div class="summary-header">
                <div class="summary-info">
                  <mat-icon>work_outline</mat-icon>
                  <div>
                    <strong>{{ projects().length }} projects scanned</strong>
                    <span class="sub"
                      >{{ s.reposScanned }} repositor{{
                        s.reposScanned > 1 ? 'ies' : 'y'
                      }}</span
                    >
                  </div>
                </div>
                <mat-icon class="chevron" [class.expanded]="projectsExpanded()">
                  expand_more
                </mat-icon>
              </div>
            </button>
            @if (projectsExpanded()) {
              <div class="project-grid">
                @for (proj of projects(); track proj.projectId) {
                  <button
                    class="project-tile"
                    [class.selected]="selectedProjectId() === proj.projectId"
                    (click)="
                      selectedProjectId.set(
                        selectedProjectId() === proj.projectId ? null : proj.projectId
                      );
                      selectedRepository.set(null);
                    "
                    matRipple
                  >
                    <div class="proj-header">
                      <mat-icon>work</mat-icon>
                      <strong>{{ proj.projectName }}</strong>
                    </div>
                    <div class="proj-stats">
                      <span class="stat"
                        ><mat-icon>folder</mat-icon>{{ proj.repositoryCount }}</span
                      >
                      <span class="stat"
                        ><mat-icon>bug_report</mat-icon>{{ proj.totalVulnerabilities }}</span
                      >
                    </div>
                    @if (proj.totalVulnerabilities > 0) {
                      <div class="proj-severity">
                        @if (proj.criticalCount > 0) {
                          <span class="sev-chip critical">{{ proj.criticalCount }}C</span>
                        }
                        @if (proj.highCount > 0) {
                          <span class="sev-chip high">{{ proj.highCount }}H</span>
                        }
                        @if (proj.mediumCount > 0) {
                          <span class="sev-chip medium">{{ proj.mediumCount }}M</span>
                        }
                        @if (proj.lowCount > 0) {
                          <span class="sev-chip low">{{ proj.lowCount }}L</span>
                        }
                      </div>
                    }
                  </button>
                }
              </div>
            }
          </mat-card>
        }

        <!-- Findings table -->
        <mat-card class="findings">
          <mat-card-header>
            <div class="findings-header">
              <div>
                <mat-card-title>
                  Findings
                  @if (selectedProjectId() || selectedRepository()) {
                    <span class="filter-badge">
                      {{ vulns().length }} of {{ allVulns().length }}
                    </span>
                  }
                </mat-card-title>
                <mat-card-subtitle>
                  @if (selectedProjectName()) {
                    Showing
                    <strong>{{ selectedProjectName() }}</strong>
                    @if (selectedRepository()) {
                      › <strong>{{ selectedRepository() }}</strong>
                    }
                  } @else if (selectedRepository()) {
                    Showing repository <strong>{{ selectedRepository() }}</strong>
                  } @else {
                    All {{ allVulns().length }} vulnerabilities
                  }
                </mat-card-subtitle>
              </div>
              <div class="findings-actions no-print">
                @if (projects().length > 1) {
                  <ng-select
                    class="vs-select"
                    [items]="projects()"
                    bindValue="projectId"
                    bindLabel="projectName"
                    placeholder="All projects"
                    [searchable]="true"
                    [clearable]="true"
                    [ngModel]="selectedProjectId()"
                    (ngModelChange)="selectedProjectId.set($event); selectedRepository.set(null)"
                  ></ng-select>
                }
                @if (repositories().length > 1) {
                  <ng-select
                    class="vs-select"
                    [items]="repositories()"
                    placeholder="All repositories"
                    [searchable]="true"
                    [clearable]="true"
                    [ngModel]="selectedRepository()"
                    (ngModelChange)="selectedRepository.set($event)"
                  ></ng-select>
                }
                @if (selectedProjectId() || selectedRepository()) {
                  <button
                    mat-icon-button
                    (click)="selectedProjectId.set(null); selectedRepository.set(null)"
                    matTooltip="Clear filters"
                  >
                    <mat-icon>clear</mat-icon>
                  </button>
                }
              </div>
            </div>
          </mat-card-header>
          <mat-card-content>
            @if (loadingVulns()) {
              <div class="loading-container small">
                <mat-spinner diameter="28"></mat-spinner>
              </div>
            } @else if (vulns().length > 0) {
              <table mat-table [dataSource]="vulns()" class="modern-table">
                <ng-container matColumnDef="severity">
                  <th mat-header-cell *matHeaderCellDef>Severity</th>
                  <td mat-cell *matCellDef="let v">
                    <span class="sev-pill" [class]="v.severity.toLowerCase()">
                      {{ v.severity }}
                    </span>
                  </td>
                </ng-container>

                <ng-container matColumnDef="cve">
                  <th mat-header-cell *matHeaderCellDef>CVE / Advisory</th>
                  <td mat-cell *matCellDef="let v">
                    <a
                      [routerLink]="['/reports/vulnerabilities', v.cveId]"
                      class="cve-link no-print"
                      >{{ v.cveId }}</a
                    >
                    <span class="print-only cve-text">{{ v.cveId }}</span>
                  </td>
                </ng-container>

                <ng-container matColumnDef="package">
                  <th mat-header-cell *matHeaderCellDef>Package</th>
                  <td mat-cell *matCellDef="let v">
                    <strong>{{ v.packageName }}</strong>
                    <small class="text-muted">{{ v.installedVersion }}</small>
                  </td>
                </ng-container>

                <ng-container matColumnDef="fix">
                  <th mat-header-cell *matHeaderCellDef>Fixed in</th>
                  <td mat-cell *matCellDef="let v">
                    @if (v.fixedVersion) {
                      <span class="fix-version">{{ v.fixedVersion }}</span>
                    } @else {
                      <span class="text-muted">—</span>
                    }
                  </td>
                </ng-container>

                <ng-container matColumnDef="cvss">
                  <th mat-header-cell *matHeaderCellDef>CVSS</th>
                  <td mat-cell *matCellDef="let v">
                    {{ v.cvssScore != null ? (v.cvssScore | number : '1.1-1') : '—' }}
                  </td>
                </ng-container>

                <ng-container matColumnDef="repo">
                  <th mat-header-cell *matHeaderCellDef>Repository</th>
                  <td mat-cell *matCellDef="let v">{{ v.repositoryName }}</td>
                </ng-container>

                <ng-container matColumnDef="status">
                  <th mat-header-cell *matHeaderCellDef>Status</th>
                  <td mat-cell *matCellDef="let v">
                    <span class="status-badge" [class]="v.status.toLowerCase()">{{
                      v.status
                    }}</span>
                  </td>
                </ng-container>

                <tr mat-header-row *matHeaderRowDef="columns"></tr>
                <tr mat-row *matRowDef="let row; columns: columns"></tr>
              </table>
            } @else {
              <p class="text-muted">No findings recorded for this scan.</p>
            }
          </mat-card-content>
        </mat-card>

        @if (s.errorLog) {
          <mat-card class="error-log">
            <mat-card-header>
              <mat-card-title>Errors</mat-card-title>
            </mat-card-header>
            <mat-card-content>
              <pre>{{ s.errorLog }}</pre>
            </mat-card-content>
          </mat-card>
        }

        <footer class="report-footer print-only">
          Generated by Vulscan · {{ today | date : 'medium' }}
        </footer>
      }
    </div>
  `,
  styles: `
    :host {
      display: block;
    }

    .report-page {
      max-width: 1200px;
      margin: 0 auto;
    }

    /* Toolbar -------------------------------------------------- */
    .report-toolbar {
      display: flex;
      align-items: center;
      justify-content: space-between;
      margin-bottom: 20px;
      gap: 12px;
    }

    .toolbar-actions {
      display: flex;
      gap: 8px;
    }

    /* Cover ---------------------------------------------------- */
    .cover {
      display: flex;
      align-items: flex-start;
      justify-content: space-between;
      gap: 24px;
      padding: 28px 32px;
      margin-bottom: 24px;
      background: var(--gradient-brand);
      color: #fff;
      border-radius: var(--radius-lg);
      box-shadow: var(--shadow-md);
    }

    .eyebrow {
      display: inline-block;
      font-size: 11px;
      font-weight: 600;
      letter-spacing: 0.12em;
      text-transform: uppercase;
      padding: 4px 10px;
      border-radius: 999px;
      background: rgba(255, 255, 255, 0.18);
      margin-bottom: 12px;
    }

    .cover h1 {
      font-size: 28px;
      font-weight: 700;
      margin: 0 0 6px;
      letter-spacing: -0.01em;
    }

    .cover .subtitle {
      margin: 0;
      opacity: 0.9;
      font-size: 14px;
    }

    .cover-stamp {
      display: flex;
      flex-direction: column;
      align-items: flex-end;
      gap: 8px;
      flex-shrink: 0;
    }

    .duration,
    .trigger {
      font-size: 12px;
      opacity: 0.85;
    }

    /* Summary cards -------------------------------------------- */
    .summary-grid {
      display: grid;
      grid-template-columns: repeat(6, 1fr);
      gap: 14px;
      margin-bottom: 24px;
    }

    .summary-card {
      background: var(--surface-card);
      border-radius: var(--radius-md);
      padding: 16px;
      display: flex;
      align-items: center;
      gap: 14px;
      border: 1px solid var(--neutral-200);
      box-shadow: var(--shadow-xs);
      transition: transform 0.15s ease, box-shadow 0.15s ease;
    }

    .summary-card:hover {
      transform: translateY(-1px);
      box-shadow: var(--shadow-sm);
    }

    .sc-icon {
      width: 42px;
      height: 42px;
      border-radius: 10px;
      display: flex;
      align-items: center;
      justify-content: center;
      flex-shrink: 0;
    }

    .sc-icon mat-icon {
      font-size: 22px;
      width: 22px;
      height: 22px;
    }

    .summary-card.critical .sc-icon {
      background: var(--sev-critical-bg);
      color: var(--sev-critical);
    }
    .summary-card.high .sc-icon {
      background: var(--sev-high-bg);
      color: var(--sev-high);
    }
    .summary-card.medium .sc-icon {
      background: var(--sev-medium-bg);
      color: var(--sev-medium);
    }
    .summary-card.low .sc-icon {
      background: var(--sev-low-bg);
      color: var(--sev-low);
    }
    .summary-card.total .sc-icon {
      background: var(--brand-teal-50);
      color: var(--brand-teal);
    }
    .summary-card.repos .sc-icon {
      background: var(--brand-navy-50);
      color: var(--brand-navy);
    }

    .sc-body {
      display: flex;
      flex-direction: column;
      line-height: 1.15;
    }

    .sc-value {
      font-size: 22px;
      font-weight: 700;
      color: var(--neutral-900);
    }

    .sc-label {
      font-size: 12px;
      color: var(--neutral-500);
      margin-top: 2px;
      letter-spacing: 0.02em;
    }

    /* Distribution bar ----------------------------------------- */
    .distribution {
      margin-bottom: 24px;
    }

    .bar {
      height: 14px;
      border-radius: 7px;
      overflow: hidden;
      display: flex;
      background: var(--neutral-200);
    }

    .bar-segment.critical {
      background: var(--sev-critical);
    }
    .bar-segment.high {
      background: var(--sev-high);
    }
    .bar-segment.medium {
      background: var(--sev-medium);
    }
    .bar-segment.low {
      background: var(--sev-low);
    }

    .bar-legend {
      display: flex;
      gap: 18px;
      margin-top: 14px;
      flex-wrap: wrap;
    }

    .legend-item {
      display: inline-flex;
      align-items: center;
      gap: 6px;
      font-size: 12px;
      color: var(--neutral-700);
    }

    .dot {
      width: 10px;
      height: 10px;
      border-radius: 50%;
    }
    .dot.critical {
      background: var(--sev-critical);
    }
    .dot.high {
      background: var(--sev-high);
    }
    .dot.medium {
      background: var(--sev-medium);
    }
    .dot.low {
      background: var(--sev-low);
    }

    .all-clear {
      display: flex;
      align-items: center;
      gap: 14px;
      padding: 16px 18px;
      background: var(--brand-teal-50);
      border-radius: var(--radius-md);
      color: var(--brand-teal-700);
    }

    .all-clear mat-icon {
      font-size: 32px;
      width: 32px;
      height: 32px;
    }

    .all-clear strong {
      display: block;
      font-size: 14px;
    }

    .all-clear span {
      font-size: 12px;
      opacity: 0.8;
    }

    /* Projects summary (collapsible) -------------------------- */
    .projects-summary {
      margin-bottom: 24px;
      overflow: hidden;
    }

    .expand-toggle {
      width: 100%;
      background: none;
      border: none;
      padding: 16px 20px;
      cursor: pointer;
      display: block;
      text-align: left;
    }

    .expand-toggle:hover {
      background: var(--neutral-100);
    }

    .summary-header {
      display: flex;
      align-items: center;
      justify-content: space-between;
    }

    .summary-info {
      display: flex;
      align-items: center;
      gap: 12px;
    }

    .summary-info mat-icon {
      color: var(--brand-teal);
      font-size: 24px;
      width: 24px;
      height: 24px;
    }

    .summary-info strong {
      display: block;
      font-size: 14px;
      font-weight: 600;
      color: var(--neutral-900);
    }

    .summary-info .sub {
      display: block;
      font-size: 12px;
      color: var(--neutral-600);
      margin-top: 2px;
    }

    .chevron {
      transition: transform 0.2s ease;
      color: var(--neutral-500);
    }

    .chevron.expanded {
      transform: rotate(180deg);
    }

    .project-grid {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(240px, 1fr));
      gap: 10px;
      padding: 0 20px 20px;
      border-top: 1px solid var(--neutral-200);
      margin-top: 8px;
      padding-top: 16px;
    }

    .project-tile {
      display: flex;
      flex-direction: column;
      gap: 8px;
      padding: 12px 14px;
      background: var(--neutral-100);
      border: 1px solid var(--neutral-200);
      border-radius: var(--radius-md);
      text-align: left;
      cursor: pointer;
      transition: all 0.12s ease;
    }

    .project-tile:hover {
      background: var(--brand-teal-50);
      border-color: var(--brand-teal-600);
    }

    .project-tile.selected {
      background: var(--brand-teal-50);
      border-color: var(--brand-teal);
      border-width: 2px;
      padding: 11px 13px;
    }

    .proj-header {
      display: flex;
      align-items: center;
      gap: 7px;
      font-size: 13px;
      font-weight: 600;
    }

    .proj-header mat-icon {
      font-size: 16px;
      width: 16px;
      height: 16px;
      color: var(--brand-navy);
    }

    .proj-stats {
      display: flex;
      gap: 12px;
      font-size: 11px;
      color: var(--neutral-600);
    }

    .proj-stats .stat {
      display: inline-flex;
      align-items: center;
      gap: 3px;
    }

    .proj-stats mat-icon {
      font-size: 13px;
      width: 13px;
      height: 13px;
    }

    .proj-severity {
      display: flex;
      gap: 4px;
      flex-wrap: wrap;
    }

    .sev-chip {
      display: inline-block;
      padding: 2px 6px;
      border-radius: 3px;
      font-size: 9px;
      font-weight: 700;
      letter-spacing: 0.03em;
    }

    .sev-chip.critical {
      background: var(--sev-critical);
      color: #fff;
    }
    .sev-chip.high {
      background: var(--sev-high);
      color: #fff;
    }
    .sev-chip.medium {
      background: var(--sev-medium);
      color: #fff;
    }
    .sev-chip.low {
      background: var(--sev-low);
      color: #fff;
    }

    /* Findings table ------------------------------------------- */
    .findings {
      margin-bottom: 24px;
    }

    .findings-header {
      display: flex;
      align-items: flex-start;
      justify-content: space-between;
      width: 100%;
      gap: 20px;
    }

    .filter-badge {
      display: inline-flex;
      align-items: center;
      padding: 3px 10px;
      border-radius: 999px;
      background: var(--brand-teal-50);
      color: var(--brand-teal-700);
      font-size: 11px;
      font-weight: 700;
      margin-left: 8px;
      vertical-align: middle;
    }

    .findings-actions {
      display: flex;
      align-items: center;
      gap: 8px;
    }

    .filter-field {
      width: 160px;
      font-size: 13px;
    }

    .modern-table {
      width: 100%;
      background: transparent;
    }

    .modern-table th.mat-mdc-header-cell {
      font-weight: 600;
      color: var(--neutral-700);
      letter-spacing: 0.02em;
      font-size: 11px;
      text-transform: uppercase;
      background: var(--neutral-100);
    }

    .modern-table td.mat-mdc-cell,
    .modern-table th.mat-mdc-header-cell {
      padding: 12px 14px;
      border-bottom: 1px solid var(--neutral-200);
      font-size: 13px;
    }

    .text-muted {
      color: var(--neutral-500);
      margin-left: 6px;
      font-size: 12px;
    }

    .cve-link {
      color: var(--brand-teal-700);
      text-decoration: none;
      font-weight: 600;
      font-family: 'SF Mono', Menlo, monospace;
      font-size: 12px;
    }

    .cve-link:hover {
      text-decoration: underline;
    }

    .cve-text {
      font-family: 'SF Mono', Menlo, monospace;
      font-size: 12px;
    }

    .fix-version {
      font-family: 'SF Mono', Menlo, monospace;
      font-size: 12px;
      color: var(--brand-teal-700);
    }

    .sev-pill {
      display: inline-block;
      padding: 3px 10px;
      border-radius: 999px;
      font-size: 11px;
      font-weight: 700;
      letter-spacing: 0.02em;
      text-transform: capitalize;
    }

    .sev-pill.critical {
      background: var(--sev-critical-bg);
      color: var(--sev-critical);
    }
    .sev-pill.high {
      background: var(--sev-high-bg);
      color: var(--sev-high);
    }
    .sev-pill.medium {
      background: var(--sev-medium-bg);
      color: var(--sev-medium);
    }
    .sev-pill.low {
      background: var(--sev-low-bg);
      color: var(--sev-low);
    }

    .status-badge {
      display: inline-flex;
      align-items: center;
      gap: 6px;
      padding: 3px 10px;
      border-radius: 999px;
      font-size: 11px;
      font-weight: 600;
      text-transform: capitalize;
    }

    .status-dot {
      width: 6px;
      height: 6px;
      border-radius: 50%;
      background: currentColor;
    }

    .status-badge.completed,
    .status-badge.open {
      background: var(--status-success-bg);
      color: var(--status-success);
    }
    .status-badge.running,
    .status-badge.queued {
      background: var(--status-info-bg);
      color: var(--status-info);
    }
    .status-badge.failed,
    .status-badge.unresolved {
      background: var(--status-error-bg);
      color: var(--status-error);
    }
    .status-badge.resolved,
    .status-badge.fixed {
      background: var(--brand-teal-50);
      color: var(--brand-teal-700);
    }
    .status-badge.suppressed,
    .status-badge.acknowledged {
      background: var(--status-warn-bg);
      color: var(--status-warn);
    }

    /* Misc ----------------------------------------------------- */
    .loading-container {
      display: flex;
      justify-content: center;
      padding: 64px;
    }

    .loading-container.small {
      padding: 24px;
    }

    .error-card {
      text-align: center;
      color: var(--status-error);
    }

    .error-card mat-icon {
      vertical-align: middle;
      margin-right: 8px;
    }

    .error-log pre {
      white-space: pre-wrap;
      font-size: 12px;
      color: var(--neutral-700);
      background: var(--neutral-100);
      padding: 12px;
      border-radius: var(--radius-sm);
      margin: 0;
    }

    .report-footer {
      text-align: center;
      font-size: 11px;
      color: var(--neutral-500);
      padding: 24px 0;
    }

    @media (max-width: 1024px) {
      .summary-grid {
        grid-template-columns: repeat(3, 1fr);
      }
    }

    @media (max-width: 600px) {
      .summary-grid {
        grid-template-columns: repeat(2, 1fr);
      }
      .cover {
        flex-direction: column;
        align-items: flex-start;
      }
      .cover-stamp {
        align-items: flex-start;
      }
    }
  `,
})
export class ScanReportComponent implements OnInit {
  readonly today = new Date();
  readonly loading = signal(true);
  readonly loadingVulns = signal(true);
  readonly downloading = signal(false);
  readonly rescanning = signal(false);
  readonly scan = signal<ScanRun | null>(null);
  readonly projects = signal<ProjectSummary[]>([]);
  readonly allVulns = signal<Vulnerability[]>([]);
  readonly errorMessage = signal('');

  // Filters
  readonly selectedProjectId = signal<string | null>(null);
  readonly selectedRepository = signal<string | null>(null);
  readonly projectsExpanded = signal(false);

  // Computed filtered vulnerabilities
  readonly vulns = computed(() => {
    let filtered = this.allVulns();
    const projId = this.selectedProjectId();
    const repo = this.selectedRepository();

    if (projId) {
      const project = this.projects().find(p => p.projectId === projId);
      if (project) {
        // Filter to vulns that belong to repos in this project
        // We need to check the projectName from vulnerability (not directly available)
        // For now, we'll use repository filtering
        filtered = filtered.filter(v => v.projectName === project.projectName);
      }
    }

    if (repo) {
      filtered = filtered.filter(v => v.repositoryName === repo);
    }

    return filtered;
  });

  // Unique repositories from all vulns (filtered by selected project if any)
  readonly repositories = computed(() => {
    const projId = this.selectedProjectId();
    let vulnsToFilter = this.allVulns();
    
    // If project is selected, only show repos from that project
    if (projId) {
      const project = this.projects().find(p => p.projectId === projId);
      if (project) {
        vulnsToFilter = vulnsToFilter.filter(v => v.projectName === project.projectName);
      }
    }
    
    const repos = new Set(vulnsToFilter.map(v => v.repositoryName));
    return Array.from(repos).sort();
  });

  // Selected project name for display
  readonly selectedProjectName = computed(() => {
    const projId = this.selectedProjectId();
    if (!projId) return null;
    return this.projects().find(p => p.projectId === projId)?.projectName ?? null;
  });

  readonly columns = ['severity', 'cve', 'package', 'fix', 'cvss', 'repo', 'status'];

  scanId = '';
  pendingInstanceId: string | null = null;

  constructor(
    private readonly route: ActivatedRoute,
    private readonly api: ApiService,
    private readonly snackBar: MatSnackBar,
  ) {}

  ngOnInit(): void {
    const idParam = this.route.snapshot.paramMap.get('scanId');
    this.scanId = idParam ?? '';
    if (!this.scanId) {
      this.errorMessage.set('Invalid scan id.');
      this.loading.set(false);
      this.loadingVulns.set(false);
      return;
    }

    // Auto-apply filters from query params (e.g., navigated from scans tab with filtered instance)
    const qp = this.route.snapshot.queryParamMap;
    const pid = qp.get('projectId');
    const repo = qp.get('repository');
    const iid = qp.get('instanceId');
    if (pid) this.selectedProjectId.set(pid);
    if (repo) this.selectedRepository.set(repo);
    // If instanceId provided, we'll load and auto-select its project after scan loads
    this.pendingInstanceId = iid ?? null;

    this.load();
  }

  private load(): void {
    this.loading.set(true);
    this.loadingVulns.set(true);

    forkJoin({
      scan: this.api.getScanById(this.scanId),
      projects: this.api.getProjectSummaries(this.scanId),
      vulns: this.api.getVulnerabilities({
        scanRunId: this.scanId,
        page: 1,
        pageSize: 250,
        sortBy: 'CvssScore',
        sortDescending: 'true',
      }),
    }).subscribe({
      next: ({ scan, projects, vulns }) => {
        this.loading.set(false);
        this.loadingVulns.set(false);
        if (scan.success && scan.data) {
          this.scan.set(scan.data);
          // If instanceId was passed, auto-select its project
          if (this.pendingInstanceId && projects.success && projects.data) {
            const matchingProject = projects.data.find(
              (p) => p.projectId === this.pendingInstanceId,
            );
            if (matchingProject) {
              this.selectedProjectId.set(matchingProject.projectId);
            }
          }
        } else {
          this.errorMessage.set(scan.message ?? 'Scan not found.');
        }
        if (projects.success && projects.data) {
          this.projects.set(projects.data);
        }
        if (vulns.success && vulns.data) {
          this.allVulns.set(vulns.data.items);
        }
      },
      error: () => {
        this.loading.set(false);
        this.loadingVulns.set(false);
        this.errorMessage.set('Failed to load scan report.');
      },
    });
  }

  downloadPdf(): void {
    // Use the browser's print-to-PDF — produces high-quality, paginated output
    // and works without any external dependency.
    window.print();
  }

  rescan(): void {
    const s = this.scan();
    if (!s || !s.instanceId || this.rescanning()) return;
    this.rescanning.set(true);
    this.api.triggerScan({ instanceId: s.instanceId }).subscribe({
      next: (res) => {
        this.rescanning.set(false);
        const newId = res.data?.scanRunId;
        this.snackBar.open(
          newId ? `Re-scan started — new scan #${newId}` : 'Re-scan started',
          'View',
          { duration: 6000 },
        ).onAction().subscribe(() => {
          if (newId) {
            window.location.href = `/scans/${newId}/report`;
          }
        });
      },
      error: () => {
        this.rescanning.set(false);
        this.snackBar.open('Failed to trigger re-scan.', 'Close', { duration: 5000 });
      },
    });
  }

  downloadCsv(): void {
    const s = this.scan();
    if (!s) return;
    this.downloading.set(true);
    this.api.exportVulnerabilitiesCsv(undefined, this.scanId).subscribe({
      next: (blob) => {
        this.downloading.set(false);
        const filename = `scan-${s.id}-${(s.instanceName ?? 'report').replace(/\s+/g, '_')}.csv`;
        const url = window.URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = filename;
        document.body.appendChild(a);
        a.click();
        document.body.removeChild(a);
        window.URL.revokeObjectURL(url);
      },
      error: () => {
        this.downloading.set(false);
        this.snackBar.open('Failed to export CSV.', 'Close', { duration: 5000 });
      },
    });
  }
}
