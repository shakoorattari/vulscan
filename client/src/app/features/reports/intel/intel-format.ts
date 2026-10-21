// Display helpers for vulnerability intelligence. Pure TypeScript (no framework imports) so they can be
// unit-tested with `node --test`.

export interface Range {
  introduced?: string;
  fixed?: string;
  lastAffected?: string;
}

export function severityKey(severity?: string | null): 'critical' | 'high' | 'medium' | 'low' | 'none' {
  switch ((severity ?? '').toLowerCase()) {
    case 'critical':
      return 'critical';
    case 'high':
      return 'high';
    case 'medium':
    case 'moderate':
      return 'medium';
    case 'low':
      return 'low';
    default:
      return 'none';
  }
}

/** "< 2.4.24", "≥ 2.0.0, < 2.0.3", "≤ 3.1.0 (no fix)", "all versions (no fix)". */
export function formatRange(r: Range): string {
  const from = r.introduced && r.introduced !== '0' ? r.introduced : null;
  if (r.fixed) return from ? `≥ ${from}, < ${r.fixed}` : `< ${r.fixed}`;
  if (r.lastAffected) return from ? `≥ ${from}, ≤ ${r.lastAffected} (no fix)` : `≤ ${r.lastAffected} (no fix)`;
  return from ? `≥ ${from} (no fix)` : 'all versions (no fix)';
}

/** EPSS is a probability: 0.03559 → "3.56%", 0.99999 → "100.0%", tiny values stay readable. */
export function formatEpss(score: number): string {
  const pct = score * 100;
  if (pct > 0 && pct < 0.01) return '<0.01%';
  return `${pct.toFixed(pct >= 10 ? 1 : 2)}%`;
}

export function ordinal(n: number): string {
  const v = n % 100;
  if (v >= 11 && v <= 13) return `${n}th`;
  return `${n}${({ 1: 'st', 2: 'nd', 3: 'rd' } as Record<number, string>)[n % 10] ?? 'th'}`;
}

/** How worrying an EPSS score is, on the same scale as severity. */
export function epssTone(score: number): 'critical' | 'high' | 'medium' | 'low' {
  if (score >= 0.5) return 'critical';
  if (score >= 0.1) return 'high';
  if (score >= 0.01) return 'medium';
  return 'low';
}

export const KIND_ORDER = ['fix', 'advisory', 'exploit', 'issue', 'package', 'article', 'other'] as const;

const KIND_LABELS: Record<string, string> = {
  fix: 'Fix / patch',
  advisory: 'Advisory',
  exploit: 'Exploit / PoC',
  issue: 'Issue / report',
  package: 'Package',
  article: 'Article / discussion',
  other: 'Other',
};

const KIND_ICONS: Record<string, string> = {
  fix: 'build_circle',
  advisory: 'policy',
  exploit: 'bug_report',
  issue: 'forum',
  package: 'inventory_2',
  article: 'article',
  other: 'link',
};

export const kindLabel = (k: string): string => KIND_LABELS[k] ?? 'Other';
export const kindIcon = (k: string): string => KIND_ICONS[k] ?? 'link';

/** Metric values that make a vulnerability easier to exploit or more damaging are shown emphasised. */
export function metricTone(code: string, value: string): 'risk' | 'neutral' {
  const v = value.toLowerCase();
  switch (code) {
    case 'AV':
      return v === 'network' ? 'risk' : 'neutral';
    case 'AC':
      return v === 'low' ? 'risk' : 'neutral';
    case 'PR':
    case 'UI':
    case 'AT':
      return v === 'none' ? 'risk' : 'neutral';
    case 'S':
      return v === 'changed' ? 'risk' : 'neutral';
    case 'C':
    case 'I':
    case 'A':
    case 'VC':
    case 'VI':
    case 'VA':
    case 'SC':
    case 'SI':
    case 'SA':
      return v === 'high' || v === 'complete' ? 'risk' : 'neutral';
    default:
      return 'neutral';
  }
}

/**
 * "today", "yesterday", "3 days ago", "5 months ago", "2 years ago", "tomorrow", "in 4 days". Counted in whole UTC
 * calendar days, the same days the page displays, so a few hours of clock difference never reads as "in 1 day".
 */
export function ageLabel(iso: string | undefined | null, now: Date = new Date()): string {
  if (!iso) return '';
  const day = (d: Date) => Math.floor(d.getTime() / 86_400_000);
  const days = day(now) - day(new Date(iso));
  if (days < -1) return `in ${-days} days`;
  if (days === -1) return 'tomorrow';
  if (days === 0) return 'today';
  if (days === 1) return 'yesterday';
  if (days < 60) return `${days} days ago`;
  if (days < 730) return `${Math.round(days / 30.4)} months ago`;
  return `${Math.floor(days / 365)} years ago`;
}

export interface SourceHealth {
  icon: string;
  tone: 'ok' | 'warn' | 'bad' | 'muted';
  label: string;
}

export function sourceHealth(status: string): SourceHealth {
  switch (status) {
    case 'ok':
      return { icon: 'check_circle', tone: 'ok', label: 'Retrieved' };
    case 'not_found':
      return { icon: 'search_off', tone: 'muted', label: 'No record' };
    case 'not_applicable':
      return { icon: 'remove_circle_outline', tone: 'muted', label: 'Not applicable' };
    case 'disabled':
      return { icon: 'block', tone: 'muted', label: 'Disabled' };
    case 'skipped':
      return { icon: 'hourglass_top', tone: 'warn', label: 'Rate limited — will retry' };
    default:
      return { icon: 'error', tone: 'bad', label: 'Unreachable — will retry' };
  }
}

export interface Recommendation {
  packageName: string;
  installed: string;
  fixedVersion: string | null;
  repositories: string[];
}

export interface ProductVersions {
  version?: string;
  startIncluding?: string;
  startExcluding?: string;
  endIncluding?: string;
  endExcluding?: string;
}

/** NVD's version bounds for a product: "< 2.4.24", "≥ 1.0, < 2.0", "= 3.1", "all versions". */
export function formatProductVersions(p: ProductVersions): string {
  if (p.version) return `= ${p.version}`;
  const parts: string[] = [];
  if (p.startIncluding) parts.push(`≥ ${p.startIncluding}`);
  if (p.startExcluding) parts.push(`> ${p.startExcluding}`);
  if (p.endIncluding) parts.push(`≤ ${p.endIncluding}`);
  if (p.endExcluding) parts.push(`< ${p.endExcluding}`);
  return parts.length ? parts.join(', ') : 'all versions';
}

export interface AffectedLike {
  ecosystem: string;
  name: string;
  ranges: Range[];
}

/**
 * The version that fixes the advisory for `installed`, read from the advisory's own ranges: the range that contains the
 * installed version decides. Used when a finding from an older scan has no (or an unusable) stored fix. Null if the
 * advisory lists no range that contains it, or the range has no fix.
 */
export function inferFix(
  affected: AffectedLike[],
  packageName: string,
  installed: string,
  compare: (a: string, b: string) => number,
): string | null {
  const wanted = packageName.trim().toLowerCase();
  const fixes: string[] = [];
  for (const a of affected) {
    if (a.name.trim().toLowerCase() !== wanted) continue;
    for (const r of a.ranges) {
      const startsAfter = r.introduced && r.introduced !== '0' && compare(installed, r.introduced) < 0;
      if (startsAfter) continue;
      if (r.fixed) {
        if (compare(installed, r.fixed) < 0) fixes.push(r.fixed);
      } else if (r.lastAffected && compare(installed, r.lastAffected) <= 0) {
        return null; // inside a range that has no fix
      } else if (!r.lastAffected) {
        return null; // affected in every newer release
      }
    }
  }
  return fixes.length ? fixes.reduce((x, y) => (compare(y, x) > 0 ? y : x)) : null;
}
