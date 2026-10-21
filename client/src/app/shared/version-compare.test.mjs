import assert from 'node:assert/strict';
import { test } from 'node:test';
import { compareVersions } from './version-compare.ts';

test('numeric segments compare numerically, not textually', () => {
  assert.ok(compareVersions('1.10.0', '1.9.0') > 0);
  assert.ok(compareVersions('2.0.3', '2.0.10') < 0);
  assert.equal(compareVersions('2.0', '2.0.0'), 0);
});

test('a pre-release is older than its release', () => {
  assert.ok(compareVersions('2.0.0-rc.1', '2.0.0') < 0);
  assert.ok(compareVersions('2.0.0', '2.0.0-beta') > 0);
});

test('the "fix on another release line" case from a real scan: 1.1.18 is older than 2.0.1', () => {
  assert.ok(compareVersions('1.1.18', '2.0.1') < 0);
  assert.ok(compareVersions('5.0.5', '2.0.1') > 0);
});

test('a leading v is ignored', () => {
  assert.equal(compareVersions('v1.2.3', '1.2.3'), 0);
});
