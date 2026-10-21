import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { MatTooltipModule } from '@angular/material/tooltip';
import { IntelCvss } from '../../../core/models/api.models';
import { metricTone, severityKey } from './intel-format';

/** The CVSS rating(s) of a vulnerability: the headline score, what each metric means, and any other ratings. */
@Component({
  selector: 'app-cvss-panel',
  standalone: true,
  imports: [MatIconModule, MatTooltipModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (primary(); as p) {
      <div class="primary">
        <div class="score" [class]="tone(p)">
          <span class="num">{{ p.score != null ? p.score.toFixed(1) : 'n/a' }}</span>
          <span class="sev">{{ p.severity ?? 'Not scored' }}</span>
        </div>
        <div class="meta">
          <div class="ver">CVSS v{{ p.version }} base score</div>
          <div class="src">Rated by {{ p.source }}</div>
        </div>
      </div>
      <code class="vector">{{ p.vector }}</code>

      <ul class="metrics" aria-label="CVSS metrics">
        @for (m of p.metrics; track m.code) {
          <li [class.risk]="isRisk(m.code, m.value)">
            <span class="mn">{{ m.name }}</span>
            <span class="mv">{{ m.value }}</span>
          </li>
        }
      </ul>

      @if (p.score == null) {
        <p class="note">
          CVSS 4.0 scores are published by the rating source rather than calculated, and none has been published for
          this vulnerability yet.
        </p>
      }
    } @else {
      <p class="empty">No CVSS score has been published for this vulnerability.</p>
    }

    @if (others().length) {
      <h4>Other ratings</h4>
      <ul class="others">
        @for (r of others(); track r.vector) {
          <li>
            <span class="chip" [class]="tone(r)">{{ r.score != null ? r.score.toFixed(1) : '—' }}</span>
            <span class="what">CVSS v{{ r.version }} · {{ r.source }}</span>
            <code [matTooltip]="r.vector">{{ r.vector }}</code>
          </li>
        }
      </ul>
    }

    @if (vendorSeverity()) {
      <p class="vendor"><mat-icon>label</mat-icon> GitHub rates this <strong>{{ vendorSeverity() }}</strong>.</p>
    }
  `,
  styles: `
    :host { display: block; }
    .primary { display: flex; align-items: center; gap: 16px; margin-bottom: 12px; }
    .score {
      display: flex; flex-direction: column; align-items: center; justify-content: center;
      min-width: 88px; padding: 10px 12px; border-radius: var(--radius-md);
      background: var(--neutral-200); color: var(--neutral-900);
    }
    .num { font-size: 32px; font-weight: 700; line-height: 1; }
    .sev { margin-top: 4px; font-size: 11px; font-weight: 700; letter-spacing: 0.05em; text-transform: uppercase; }
    .score.critical { background: var(--sev-critical-bg); color: var(--sev-critical); }
    .score.high { background: var(--sev-high-bg); color: var(--sev-high); }
    .score.medium { background: var(--sev-medium-bg); color: #8a5a14; }
    .score.low { background: var(--sev-low-bg); color: var(--brand-teal-700); }
    .ver { font-weight: 600; }
    .src { font-size: 13px; color: var(--neutral-700); }
    .vector {
      display: block; margin-bottom: 14px; padding: 8px 10px; border-radius: var(--radius-sm); background: var(--neutral-100);
      font-family: ui-monospace, SFMono-Regular, Menlo, Consolas, monospace; font-size: 12px; overflow-wrap: anywhere;
    }
    .metrics { display: grid; grid-template-columns: repeat(auto-fill, minmax(150px, 1fr)); gap: 6px; margin: 0; padding: 0; list-style: none; }
    .metrics li {
      display: flex; flex-direction: column; padding: 6px 10px; border-radius: var(--radius-sm);
      background: var(--neutral-100); border: 1px solid var(--neutral-200);
    }
    .metrics li.risk { background: var(--sev-medium-bg); border-color: #ecd2a8; }
    .mn { font-size: 11px; color: var(--neutral-700); }
    .mv { font-size: 14px; font-weight: 600; }
    .note, .empty { margin: 12px 0 0; font-size: 13px; color: var(--neutral-700); }
    h4 { margin: 18px 0 8px; font-size: 12px; text-transform: uppercase; letter-spacing: 0.05em; color: var(--neutral-700); }
    .others { margin: 0; padding: 0; list-style: none; }
    .others li { display: flex; flex-wrap: wrap; align-items: center; gap: 4px 10px; padding: 6px 0; border-top: 1px solid var(--neutral-200); font-size: 13px; }
    .chip { min-width: 34px; padding: 1px 8px; border-radius: 999px; text-align: center; font-weight: 700; background: var(--neutral-200); }
    .chip.critical { background: var(--sev-critical-bg); color: var(--sev-critical); }
    .chip.high { background: var(--sev-high-bg); color: var(--sev-high); }
    .chip.medium { background: var(--sev-medium-bg); color: #8a5a14; }
    .chip.low { background: var(--sev-low-bg); color: var(--brand-teal-700); }
    .others code { flex-basis: 100%; font-size: 11.5px; color: var(--neutral-700); overflow-wrap: anywhere; }
    .vendor { display: flex; align-items: center; gap: 6px; margin: 14px 0 0; font-size: 13px; color: var(--neutral-700); }
    .vendor mat-icon { font-size: 18px; width: 18px; height: 18px; }
  `,
})
export class CvssPanelComponent {
  readonly ratings = input.required<IntelCvss[]>();
  readonly vendorSeverity = input<string | null | undefined>(null);

  readonly primary = computed(() => this.ratings().find((r) => r.isPrimary) ?? this.ratings()[0] ?? null);
  readonly others = computed(() => this.ratings().filter((r) => r !== this.primary()));

  tone(r: IntelCvss): string {
    return severityKey(r.severity);
  }

  isRisk(code: string, value: string): boolean {
    return metricTone(code, value) === 'risk';
  }
}
