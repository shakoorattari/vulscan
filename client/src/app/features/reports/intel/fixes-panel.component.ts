import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { IntelAffectedPackage, IntelProduct } from '../../../core/models/api.models';
import { formatProductVersions, formatRange, Recommendation } from './intel-format';

/**
 * What to do about it: the upgrade needed in each affected repository, which package versions the advisory says are
 * affected (with the ones SDD actually uses first), and the affected products NVD lists.
 */
@Component({
  selector: 'app-fixes-panel',
  standalone: true,
  imports: [MatIconModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (recommendations().length) {
      <h4>Recommended action</h4>
      <ul class="recs">
        @for (r of recommendations(); track r.packageName + '@' + r.installed) {
          <li>
            <mat-icon [class.none]="!r.fixedVersion">{{ r.fixedVersion ? 'upgrade' : 'report_problem' }}</mat-icon>
            <div>
              @if (r.fixedVersion) {
                <span><strong>Upgrade {{ r.packageName }}</strong> from <code>{{ r.installed }}</code> to <code>{{ r.fixedVersion }}</code> or later.</span>
              } @else {
                <span>
                  <strong>No fixed version of {{ r.packageName }}</strong> is known for <code>{{ r.installed }}</code>. Replace the
                  package, or apply a mitigation from the advisory.
                </span>
              }
              <div class="where">{{ r.repositories.join(' · ') }}</div>
            </div>
          </li>
        }
      </ul>
    }

    <h4>Affected versions</h4>
    @if (rows().length) {
      <div class="scroll">
        <table>
          <thead>
            <tr><th>Ecosystem</th><th>Package</th><th>Affected versions</th><th></th></tr>
          </thead>
          <tbody>
            @for (a of rows(); track a.ecosystem + a.name) {
              <tr [class.mine]="a.mine">
                <td><span class="eco">{{ a.ecosystem }}</span></td>
                <td class="mono">{{ a.name }}</td>
                <td>
                  @for (r of a.ranges; track $index) {
                    <div class="range" [class.nofix]="!r.fixed">{{ range(r) }}</div>
                  }
                  @if (!a.ranges.length && a.versionsListed) {
                    <div class="range">{{ a.versionsListed }} specific versions listed</div>
                  }
                </td>
                <td>@if (a.mine) { <span class="inuse">In use at SDD</span> }</td>
              </tr>
            }
          </tbody>
        </table>
      </div>
    } @else {
      <p class="empty">No affected package versions were published for this vulnerability.</p>
    }

    @if (products().length) {
      <details class="products">
        <summary>Affected products listed by NVD ({{ products().length }})</summary>
        <div class="scroll">
          <table>
            <thead><tr><th>Vendor</th><th>Product</th><th>Versions</th></tr></thead>
            <tbody>
              @for (p of products(); track $index) {
                <tr>
                  <td class="mono">{{ p.vendor }}</td>
                  <td class="mono">{{ p.product }}</td>
                  <td>{{ productVersions(p) }}</td>
                </tr>
              }
            </tbody>
          </table>
        </div>
      </details>
    }
  `,
  styles: `
    :host { display: block; }
    h4 { margin: 0 0 8px; font-size: 12px; text-transform: uppercase; letter-spacing: 0.05em; color: var(--neutral-700); }
    h4:not(:first-child) { margin-top: 20px; }
    .recs { margin: 0; padding: 0; list-style: none; }
    .recs li { display: flex; gap: 12px; padding: 10px 12px; margin-bottom: 8px; border-radius: var(--radius-sm); background: var(--status-success-bg); }
    .recs li mat-icon { flex: none; color: var(--brand-teal-700); }
    .recs li mat-icon.none { color: #8a5a14; }
    .recs li:has(mat-icon.none) { background: var(--sev-medium-bg); }
    .where { margin-top: 2px; font-size: 12px; color: var(--neutral-700); }
    code, .mono { font-family: ui-monospace, SFMono-Regular, Menlo, Consolas, monospace; font-size: 12.5px; }
    code { padding: 1px 5px; border-radius: 4px; background: rgba(255, 255, 255, 0.7); }
    .scroll { overflow-x: auto; }
    table { width: 100%; border-collapse: collapse; font-size: 13.5px; }
    th { padding: 6px 10px; text-align: left; font-size: 11px; text-transform: uppercase; letter-spacing: 0.04em; color: var(--neutral-700); border-bottom: 1px solid var(--neutral-200); }
    td { padding: 8px 10px; vertical-align: top; border-bottom: 1px solid var(--neutral-200); }
    tr.mine td { background: var(--brand-teal-50); }
    .eco { padding: 1px 8px; border-radius: 4px; font-size: 11px; background: var(--neutral-200); color: var(--neutral-700); }
    .range { font-family: ui-monospace, SFMono-Regular, Menlo, Consolas, monospace; font-size: 12.5px; }
    .range.nofix { color: #8a5a14; }
    .inuse { padding: 2px 8px; border-radius: 999px; font-size: 11px; font-weight: 700; white-space: nowrap; background: var(--brand-teal); color: #fff; }
    .empty { margin: 0; color: var(--neutral-700); }
    .products { margin-top: 18px; }
    .products summary { cursor: pointer; font-weight: 600; color: var(--brand-teal-700); margin-bottom: 8px; }
  `,
})
export class FixesPanelComponent {
  readonly affected = input.required<IntelAffectedPackage[]>();
  readonly products = input.required<IntelProduct[]>();
  /** Lower-case names of the packages SDD's repositories actually use. */
  readonly inUse = input<string[]>([]);
  readonly recommendations = input<Recommendation[]>([]);

  /** Packages SDD uses come first. */
  readonly rows = computed(() => {
    const mine = new Set(this.inUse());
    return this.affected()
      .map((a) => ({ ...a, mine: mine.has(a.name.toLowerCase()) }))
      .sort((a, b) => Number(b.mine) - Number(a.mine));
  });

  range = formatRange;
  productVersions = formatProductVersions;
}
