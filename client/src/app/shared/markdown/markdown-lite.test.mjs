import assert from 'node:assert/strict';
import { test } from 'node:test';
import { parseInline, parseMarkdown, safeHref } from './markdown-lite.ts';

const texts = (inlines) => inlines.map((i) => `${i.kind}:${i.text}${i.href ? '→' + i.href : ''}`);

test('the real GitHub advisory text becomes a paragraph, a heading and a paragraph', () => {
  const blocks = parseMarkdown(
    'Versions of `uglify-js` prior to 2.4.24 are affected by a vulnerability which may cause crafted JavaScript to have altered functionality after minification.\n\n## Recommendation\n\nUpgrade UglifyJS to version >= 2.4.24.',
  );
  assert.deepEqual(blocks.map((b) => b.kind), ['paragraph', 'heading', 'paragraph']);
  assert.deepEqual(texts(blocks[0].inlines).slice(0, 3), ['text:Versions of ', 'code:uglify-js', 'text: prior to 2.4.24 are affected by a vulnerability which may cause crafted JavaScript to have altered functionality after minification.']);
  assert.equal(blocks[1].level, 2);
  assert.equal(blocks[2].inlines[0].text, 'Upgrade UglifyJS to version >= 2.4.24.');
});

test('HTML in the text is never interpreted — it stays literal text', () => {
  const [p] = parseMarkdown('<script>alert(1)</script> and <img src=x onerror=alert(1)>');
  assert.equal(p.kind, 'paragraph');
  assert.ok(p.inlines.every((i) => i.kind === 'text'));
  assert.equal(p.inlines.map((i) => i.text).join(''), '<script>alert(1)</script> and <img src=x onerror=alert(1)>');
});

test('only absolute http(s) links become links; unsafe ones are kept as the literal text', () => {
  for (const bad of ['javascript:alert(1)', 'JaVaScRiPt:alert(1)', 'data:text/html,<b>x</b>', 'vbscript:x', 'file:///etc/passwd', '//evil.example', '/relative', 'https://user:pw@host/x']) {
    const inlines = parseInline(`[click](${bad})`);
    assert.ok(inlines.every((i) => i.kind !== 'link'), `${bad} must not become a link`);
    assert.equal(inlines.map((i) => i.text).join(''), `[click](${bad})`);
  }
  assert.deepEqual(texts(parseInline('[docs](https://example.org/a)')), ['link:docs→https://example.org/a']);
  assert.deepEqual(texts(parseInline('[docs](http://example.org/a)')), ['link:docs→http://example.org/a']);
});

test('bare URLs and <autolinks> become links, without the sentence punctuation after them', () => {
  assert.deepEqual(texts(parseInline('See https://example.org/advisory.')), ['text:See ', 'link:https://example.org/advisory→https://example.org/advisory', 'text:.']);
  assert.deepEqual(texts(parseInline('<https://example.org/x>')), ['link:https://example.org/x→https://example.org/x']);
  assert.deepEqual(texts(parseInline('(https://example.org/x)')), ['text:(', 'link:https://example.org/x→https://example.org/x', 'text:)']);
});

test('safeHref rejects control characters, whitespace and overlong URLs', () => {
  assert.equal(safeHref('https://example.org/a b'), null);
  assert.equal(safeHref('https://example.org/a\nb'), null);
  assert.equal(safeHref('https://example.org/' + 'a'.repeat(2100)), null);
  assert.equal(safeHref(''), null);
  assert.equal(safeHref('https://example.org/path'), 'https://example.org/path');
});

test('fenced code is kept verbatim, including markdown and html inside it', () => {
  const [code] = parseMarkdown('```js\nconst x = "<b>**not bold**</b>";\n```');
  assert.equal(code.kind, 'code');
  assert.equal(code.text, 'const x = "<b>**not bold**</b>";');
});

test('an unterminated fence swallows the rest as code rather than failing', () => {
  const blocks = parseMarkdown('before\n\n```\nnever closed\nstill code');
  assert.deepEqual(blocks.map((b) => b.kind), ['paragraph', 'code']);
  assert.equal(blocks[1].text, 'never closed\nstill code');
});

test('bullet and numbered lists, with wrapped continuation lines', () => {
  const [bullets, numbers] = parseMarkdown('- first\n- second item\n  continued here\n\n1. one\n2. two');
  assert.equal(bullets.kind, 'list');
  assert.equal(bullets.ordered, false);
  assert.equal(bullets.items.length, 2);
  assert.equal(bullets.items[1].map((i) => i.text).join(''), 'second item continued here');
  assert.equal(numbers.ordered, true);
  assert.equal(numbers.items.length, 2);
});

test('headings, blockquotes and soft-wrapped paragraphs', () => {
  const blocks = parseMarkdown('# Title\n> quoted\n> more\n\nline one\nline two');
  assert.deepEqual(blocks.map((b) => b.kind), ['heading', 'quote', 'paragraph']);
  assert.equal(blocks[1].inlines[0].text, 'quoted more');
  assert.equal(blocks[2].inlines[0].text, 'line one line two');
});

test('bold and inline code, and underscores inside identifiers are left alone', () => {
  assert.deepEqual(texts(parseInline('a **b** `c` __d__')), ['text:a ', 'bold:b', 'text: ', 'code:c', 'text: ', 'bold:d']);
  assert.deepEqual(texts(parseInline('use my_function_name here')), ['text:use my_function_name here']);
});

test('empty, null and enormous input are handled', () => {
  assert.deepEqual(parseMarkdown(''), []);
  assert.deepEqual(parseMarkdown(null), []);
  assert.deepEqual(parseMarkdown(undefined), []);
  const huge = parseMarkdown('word '.repeat(500_000));
  assert.ok(huge.length <= 1_500);
  const manyBlocks = parseMarkdown(Array.from({ length: 5000 }, (_, i) => `para ${i}`).join('\n\n'));
  assert.ok(manyBlocks.length <= 1_500);
});

test('CRLF line endings are normalised', () => {
  const blocks = parseMarkdown('# A\r\n\r\ntext\r\nmore');
  assert.deepEqual(blocks.map((b) => b.kind), ['heading', 'paragraph']);
});
