// Pure TypeScript (no framework imports) so it can be unit-tested with `node --test`.

/** Numeric-aware dotted version comparison. A pre-release tag sorts below the release it precedes. */
export function compareVersions(a: string, b: string): number {
  const split = (v: string) => v.trim().replace(/^v/i, '').split(/[.\-+_]/);
  const pa = split(a);
  const pb = split(b);
  const isNum = (s: string) => /^\d+$/.test(s);
  for (let i = 0; i < Math.max(pa.length, pb.length); i++) {
    const x = pa[i];
    const y = pb[i];
    if (x === undefined) {
      if (isNum(y) && Number(y) === 0) continue;
      return isNum(y) ? -1 : 1;
    }
    if (y === undefined) {
      if (isNum(x) && Number(x) === 0) continue;
      return isNum(x) ? 1 : -1;
    }
    if (isNum(x) && isNum(y)) {
      const d = Number(x) - Number(y);
      if (d !== 0) return d;
    } else if (x !== y) {
      return x.localeCompare(y, undefined, { numeric: true });
    }
  }
  return 0;
}
