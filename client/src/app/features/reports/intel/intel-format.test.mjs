import assert from 'node:assert/strict';
import { test } from 'node:test';
import { compareVersions } from '../../../shared/version-compare.ts';
import { ageLabel, epssTone, formatEpss, formatProductVersions, formatRange, inferFix, metricTone, ordinal, severityKey, sourceHealth } from './intel-format.ts';

test('ranges read the way an advisory would', () => {
  assert.equal(formatRange({ introduced: '0', fixed: '2.4.24' }), '< 2.4.24');
  assert.equal(formatRange({ introduced: '2.0.0', fixed: '2.0.3' }), '≥ 2.0.0, < 2.0.3');
  assert.equal(formatRange({ introduced: '0', lastAffected: '3.1.0' }), '≤ 3.1.0 (no fix)');
  assert.equal(formatRange({ introduced: '1.0.0', lastAffected: '1.4.0' }), '≥ 1.0.0, ≤ 1.4.0 (no fix)');
  assert.equal(formatRange({ introduced: '1.0.0' }), '≥ 1.0.0 (no fix)');
  assert.equal(formatRange({ introduced: '0' }), 'all versions (no fix)');
  assert.equal(formatRange({}), 'all versions (no fix)');
});

test('EPSS probabilities are shown as percentages with sensible precision', () => {
  assert.equal(formatEpss(0.03559), '3.56%');
  assert.equal(formatEpss(0.99999), '100.0%');
  assert.equal(formatEpss(0.5), '50.0%');
  assert.equal(formatEpss(0.00043), '0.04%');
  assert.equal(formatEpss(0.000001), '<0.01%');
  assert.equal(formatEpss(0), '0.00%');
});

test('EPSS tone and ordinals', () => {
  assert.equal(epssTone(0.97), 'critical');
  assert.equal(epssTone(0.2), 'high');
  assert.equal(epssTone(0.03), 'medium');
  assert.equal(epssTone(0.001), 'low');
  assert.deepEqual([1, 2, 3, 4, 11, 12, 13, 21, 22, 89, 100].map(ordinal), ['1st', '2nd', '3rd', '4th', '11th', '12th', '13th', '21st', '22nd', '89th', '100th']);
});

test('severity names from different sources map to one scale', () => {
  assert.equal(severityKey('CRITICAL'), 'critical');
  assert.equal(severityKey('Moderate'), 'medium');
  assert.equal(severityKey('High'), 'high');
  assert.equal(severityKey(undefined), 'none');
  assert.equal(severityKey('weird'), 'none');
});

test('metrics that make exploitation easier are emphasised', () => {
  assert.equal(metricTone('AV', 'Network'), 'risk');
  assert.equal(metricTone('AV', 'Local'), 'neutral');
  assert.equal(metricTone('PR', 'None'), 'risk');
  assert.equal(metricTone('C', 'High'), 'risk');
  assert.equal(metricTone('C', 'None'), 'neutral');
  assert.equal(metricTone('S', 'Changed'), 'risk');
});

test('ages are whole UTC calendar days in the largest sensible unit', () => {
  const now = new Date('2026-10-21T12:00:00Z');
  assert.equal(ageLabel('2026-10-21T01:00:00Z', now), 'today');
  assert.equal(ageLabel('2026-10-20T23:59:00Z', now), 'yesterday');
  assert.equal(ageLabel('2026-10-19T00:00:00Z', now), '2 days ago');
  assert.equal(ageLabel('2026-10-01T00:00:00Z', now), '20 days ago');
  assert.equal(ageLabel('2026-04-21T00:00:00Z', now), '6 months ago');
  assert.equal(ageLabel('2017-01-23T21:59:00Z', now), '9 years ago');
  assert.equal(ageLabel(undefined, now), '');
});

test('a time a little ahead of the clock is "today", not "in 1 day"; real future dates are described as such', () => {
  const now = new Date('2026-10-21T06:30:00Z');
  assert.equal(ageLabel('2026-10-21T08:00:00Z', now), 'today');      // 1.5 hours of clock difference
  assert.equal(ageLabel('2026-10-22T00:00:00Z', now), 'tomorrow');
  assert.equal(ageLabel('2026-10-25T00:00:00Z', now), 'in 4 days');
});

test('a source that is unreachable or rate limited is flagged as retrying, not as "no data"', () => {
  assert.equal(sourceHealth('ok').tone, 'ok');
  assert.equal(sourceHealth('not_found').tone, 'muted');
  assert.equal(sourceHealth('error').tone, 'bad');
  assert.equal(sourceHealth('skipped').tone, 'warn');
  assert.match(sourceHealth('error').label, /retry/);
});

test('NVD product version bounds', () => {
  assert.equal(formatProductVersions({ endExcluding: '2.4.24' }), '< 2.4.24');
  assert.equal(formatProductVersions({ startIncluding: '1.0', endExcluding: '2.0' }), '≥ 1.0, < 2.0');
  assert.equal(formatProductVersions({ startExcluding: '1.0', endIncluding: '1.9' }), '> 1.0, ≤ 1.9');
  assert.equal(formatProductVersions({ version: '3.1' }), '= 3.1');
  assert.equal(formatProductVersions({}), 'all versions');
});

const log4j = [
  { ecosystem: 'Maven', name: 'org.apache.logging.log4j:log4j-core', ranges: [{ introduced: '2.13.0', fixed: '2.15.0' }] },
  { ecosystem: 'Maven', name: 'org.apache.logging.log4j:log4j-core', ranges: [{ introduced: '2.0-beta9', fixed: '2.3.1' }] },
  { ecosystem: 'Maven', name: 'org.apache.logging.log4j:log4j-core', ranges: [{ introduced: '2.4', fixed: '2.12.2' }] },
];

test('the fix is read from the range that contains the installed version (Log4Shell has three)', () => {
  const name = 'org.apache.logging.log4j:log4j-core';
  assert.equal(inferFix(log4j, name, '2.14.1', compareVersions), '2.15.0');
  assert.equal(inferFix(log4j, name, '2.8.2', compareVersions), '2.12.2');
  assert.equal(inferFix(log4j, name, '2.0', compareVersions), '2.3.1');
  assert.equal(inferFix(log4j, name, '2.17.1', compareVersions), null); // already past every range
});

test('inferFix: name match ignores case, a range with no fix means no fix, other packages are ignored', () => {
  const a = [{ ecosystem: 'npm', name: 'uglify-js', ranges: [{ introduced: '0', fixed: '2.4.24' }] }];
  assert.equal(inferFix(a, 'Uglify-JS', '1.3.5', compareVersions), '2.4.24');
  assert.equal(inferFix(a, 'other', '1.3.5', compareVersions), null);
  assert.equal(inferFix([{ ecosystem: 'npm', name: 'x', ranges: [{ introduced: '0', lastAffected: '3.0.0' }] }], 'x', '2.0.0', compareVersions), null);
  assert.equal(inferFix([{ ecosystem: 'npm', name: 'x', ranges: [{ introduced: '1.0.0' }] }], 'x', '9.0.0', compareVersions), null);
  assert.equal(inferFix([{ ecosystem: 'npm', name: 'x', ranges: [{ introduced: '2.0.0', fixed: '2.5.0' }] }], 'x', '1.0.0', compareVersions), null); // before the range
});
