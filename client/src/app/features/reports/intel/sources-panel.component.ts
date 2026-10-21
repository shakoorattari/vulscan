import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { IntelSource } from '../../../core/models/api.models';
import { ageLabel, sourceHealth } from './intel-format';

/** Where the information came from, whether each source answered, and a way to ask them again. */
@Component({
  selector: 'app-sources-panel',
  standalone: true,
  imports: [DatePipe, MatIconModule, MatButtonModule, MatProgressSpinnerModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <ul>
      @for (s of sources(); track s.name) {
        <li>
          <mat-icon [class]="health(s).tone">{{ health(s).icon }}</mat-icon>
          <div class="body">
            <div class="top">
              @if (s.url) {
                <a [href]="s.url" target="_blank" rel="noopener noreferrer">{{ s.name }}</a>
              } @else {
                <span class="name">{{ s.name }}</span>
              }
              <span class="state" [class]="health(s).tone">{{ health(s).label }}</span>
            </div>
            @if (s.message) {
              <div class="msg">{{ s.message }}</div>
            }
            @if (s.modified) {
              <div class="msg">Record updated {{ age(s.modified) }}</div>
            }
          </div>
        </li>
      }
    </ul>

    <p class="foot">
      Gathered {{ fetchedAt() | date: 'd MMM y, HH:mm' }} ({{ age(fetchedAt()) }}).
      @if (stale()) {
        <strong>Some of this is out of date and will be refreshed automatically.</strong>
      }
    </p>

    @if (canRefresh()) {
      <button mat-stroked-button type="button" [disabled]="refreshing()" (click)="refresh.emit()">
        @if (refreshing()) {
          <mat-spinner diameter="18" />
        } @else {
          <mat-icon>refresh</mat-icon>
        }
        Refresh from sources
      </button>
    }
  `,
  styles: `
    :host { display: block; }
    ul { margin: 0 0 8px; padding: 0; list-style: none; }
    li { display: flex; gap: 10px; padding: 8px 0; border-top: 1px solid var(--neutral-200); }
    li:first-child { border-top: 0; }
    li mat-icon { flex: none; font-size: 20px; width: 20px; height: 20px; margin-top: 1px; }
    .ok { color: var(--brand-teal); } .warn { color: #8a5a14; } .bad { color: var(--sev-critical); } .muted { color: var(--neutral-500); }
    .top { display: flex; flex-wrap: wrap; align-items: baseline; gap: 4px 10px; }
    .top a, .name { font-weight: 600; }
    .top a { color: var(--brand-teal-700); }
    .state { font-size: 12px; }
    .msg { margin-top: 2px; font-size: 12px; color: var(--neutral-700); overflow-wrap: anywhere; }
    .foot { margin: 8px 0 12px; font-size: 12px; color: var(--neutral-700); }
    button { display: inline-flex; align-items: center; gap: 6px; }
  `,
})
export class SourcesPanelComponent {
  readonly sources = input.required<IntelSource[]>();
  readonly fetchedAt = input.required<string>();
  readonly stale = input(false);
  readonly canRefresh = input(false);
  readonly refreshing = input(false);
  readonly refresh = output<void>();

  health = (s: IntelSource) => sourceHealth(s.status);
  age = (iso: string | undefined) => ageLabel(iso);
}
