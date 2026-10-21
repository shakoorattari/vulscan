import { ChangeDetectionStrategy, Component, computed, input, signal } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { IntelReference } from '../../../core/models/api.models';
import { KIND_ORDER, kindIcon, kindLabel } from './intel-format';

/** Every link the sources give for a vulnerability, fixes first, filterable by what kind of page it is. */
@Component({
  selector: 'app-references-panel',
  standalone: true,
  imports: [MatIconModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (references().length === 0) {
      <p class="empty">No references were published for this vulnerability.</p>
    } @else {
      <div class="filters" role="group" aria-label="Filter references">
        <button type="button" [class.on]="filter() === 'all'" [attr.aria-pressed]="filter() === 'all'" (click)="setFilter('all')">
          All <strong>{{ references().length }}</strong>
        </button>
        @for (k of kinds(); track k.kind) {
          <button type="button" [class.on]="filter() === k.kind" [attr.aria-pressed]="filter() === k.kind" (click)="setFilter(k.kind)">
            {{ k.label }} <strong>{{ k.count }}</strong>
          </button>
        }
      </div>

      <ul class="refs">
        @for (r of visible(); track r.url) {
          <li>
            <mat-icon class="kind" [class]="r.kinds[0]">{{ icon(r.kinds[0]) }}</mat-icon>
            <div class="body">
              <a [href]="r.url" target="_blank" rel="noopener noreferrer">{{ r.url }}</a>
              <div class="sub">
                <span class="host">{{ r.host }}</span>
                @for (k of r.kinds; track k) {
                  <span class="tag" [class]="k">{{ label(k) }}</span>
                }
                @for (l of r.labels; track l) {
                  <span class="label">{{ l }}</span>
                }
                <span class="from">{{ r.sources.join(' · ') }}</span>
              </div>
            </div>
          </li>
        }
      </ul>

      @if (hiddenCount() > 0) {
        <button type="button" class="more" (click)="expanded.set(true)">Show {{ hiddenCount() }} more</button>
      } @else if (expanded() && filtered().length > limit) {
        <button type="button" class="more" (click)="expanded.set(false)">Show fewer</button>
      }

      @if (filter() === 'exploit') {
        <p class="caution">
          <mat-icon>warning_amber</mat-icon> Links tagged as exploits point to third-party pages and may describe or
          host proof-of-concept code. Open them from an isolated environment.
        </p>
      }
    }
  `,
  styles: `
    :host { display: block; }
    .empty { margin: 0; color: var(--neutral-700); }
    .filters { display: flex; flex-wrap: wrap; gap: 6px; margin-bottom: 12px; }
    .filters button {
      display: inline-flex; gap: 6px; align-items: center; height: 30px; padding: 0 12px; cursor: pointer;
      font: inherit; font-size: 13px; color: var(--neutral-900);
      background: var(--surface-card); border: 1px solid var(--neutral-300); border-radius: 999px;
    }
    .filters button:hover { border-color: var(--neutral-500); }
    .filters button.on { background: var(--brand-teal-50); border-color: var(--brand-teal); color: var(--brand-teal-700); font-weight: 600; }
    .filters button:focus-visible, .more:focus-visible { outline: 2px solid var(--brand-teal); outline-offset: 2px; }
    .refs { margin: 0; padding: 0; list-style: none; }
    .refs li { display: flex; gap: 12px; padding: 10px 0; border-top: 1px solid var(--neutral-200); }
    .refs li:first-child { border-top: 0; }
    .kind { flex: none; margin-top: 2px; color: var(--neutral-500); }
    .kind.fix { color: var(--brand-teal-700); }
    .kind.exploit { color: var(--sev-critical); }
    .body { min-width: 0; }
    .body a { color: var(--brand-teal-700); font-size: 14px; overflow-wrap: anywhere; }
    .sub { display: flex; flex-wrap: wrap; align-items: center; gap: 4px 8px; margin-top: 3px; font-size: 12px; color: var(--neutral-700); }
    .host { font-weight: 500; }
    .tag { padding: 0 8px; border-radius: 999px; font-weight: 600; background: var(--neutral-200); }
    .tag.fix { background: var(--status-success-bg); color: var(--brand-teal-700); }
    .tag.exploit { background: var(--sev-critical-bg); color: var(--sev-critical); }
    .tag.advisory { background: var(--status-info-bg); color: var(--status-info); }
    .label { color: var(--neutral-500); }
    .from { margin-left: auto; color: var(--neutral-500); }
    .more { margin-top: 8px; padding: 4px 0; border: 0; background: transparent; cursor: pointer; font: inherit; font-weight: 600; color: var(--brand-teal-700); }
    .more:hover { text-decoration: underline; }
    .caution { display: flex; gap: 8px; margin: 12px 0 0; padding: 10px 12px; border-radius: var(--radius-sm); background: var(--sev-medium-bg); font-size: 13px; }
    .caution mat-icon { flex: none; font-size: 20px; width: 20px; height: 20px; color: #8a5a14; }
  `,
})
export class ReferencesPanelComponent {
  readonly references = input.required<IntelReference[]>();

  readonly limit = 10;
  readonly filter = signal<string>('all');
  readonly expanded = signal(false);

  readonly kinds = computed(() =>
    KIND_ORDER.map((kind) => ({
      kind,
      label: kindLabel(kind),
      count: this.references().filter((r) => r.kinds.includes(kind)).length,
    })).filter((k) => k.count > 0),
  );

  readonly filtered = computed(() => {
    const f = this.filter();
    return f === 'all' ? this.references() : this.references().filter((r) => r.kinds.includes(f));
  });

  readonly visible = computed(() => (this.expanded() ? this.filtered() : this.filtered().slice(0, this.limit)));
  readonly hiddenCount = computed(() => this.filtered().length - this.visible().length);

  setFilter(kind: string): void {
    this.filter.set(kind);
    this.expanded.set(false);
  }

  icon = kindIcon;
  label = kindLabel;
}
