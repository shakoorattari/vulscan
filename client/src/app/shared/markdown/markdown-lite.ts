// A deliberately small Markdown reader for advisory text (GitHub / OSV write their details in Markdown).
//
// It never produces HTML. The output is a list of plain blocks and inline tokens that a template renders with
// ordinary bindings, so text from a third party can only ever be shown as text: a <script> tag in an advisory
// appears as the literal characters, and a link is kept only if it is an absolute http(s) URL.
//
// Pure TypeScript (no framework imports, no enums) so it can be unit-tested with `node --test`.

export type Inline =
  | { kind: 'text'; text: string }
  | { kind: 'code'; text: string }
  | { kind: 'bold'; text: string }
  | { kind: 'link'; text: string; href: string };

export type Block =
  | { kind: 'heading'; level: number; inlines: Inline[] }
  | { kind: 'paragraph'; inlines: Inline[] }
  | { kind: 'list'; ordered: boolean; items: Inline[][] }
  | { kind: 'quote'; inlines: Inline[] }
  | { kind: 'code'; text: string };

const MAX_INPUT = 100_000;
const MAX_BLOCKS = 1_500;

/** Only absolute http(s) URLs become links; everything else (javascript:, data:, relative…) stays plain text. */
export function safeHref(url: string): string | null {
  const trimmed = url.trim();
  if (trimmed.length === 0 || trimmed.length > 2000 || /[\s\u0000-\u001f]/.test(trimmed)) return null;
  try {
    const u = new URL(trimmed);
    if (u.protocol !== 'http:' && u.protocol !== 'https:') return null;
    if (u.username || u.password) return null;
    return u.href === trimmed || u.href === trimmed + '/' ? trimmed : u.href;
  } catch {
    return null;
  }
}

// `code` | **bold** | __bold__ | [text](url) | <https://autolink> | bare https://url
const INLINE =
  /`([^`\n]+)`|\*\*([^*\n]+)\*\*|__([^_\n]+)__|\[([^\]\n]+)\]\(([^)\s]+)\)|<(https?:\/\/[^>\s]+)>|(https?:\/\/[^\s<>()\[\]]+)/g;

export function parseInline(text: string): Inline[] {
  const out: Inline[] = [];
  let last = 0;
  const push = (t: string) => {
    if (t) out.push({ kind: 'text', text: t });
  };

  for (const m of text.matchAll(INLINE)) {
    push(text.slice(last, m.index));
    last = m.index! + m[0].length;

    if (m[1] !== undefined) {
      out.push({ kind: 'code', text: m[1] });
    } else if (m[2] !== undefined || m[3] !== undefined) {
      out.push({ kind: 'bold', text: (m[2] ?? m[3])! });
    } else if (m[4] !== undefined) {
      const href = safeHref(m[5]);
      if (href) out.push({ kind: 'link', text: m[4], href });
      else push(m[0]); // an unsafe link is shown as the literal text it was written as
    } else {
      // <autolink> or a bare URL; trailing sentence punctuation is not part of the URL
      const raw = (m[6] ?? m[7])!;
      const trailing = m[6] !== undefined ? '' : (raw.match(/[.,;:!?'"]+$/)?.[0] ?? '');
      const url = trailing ? raw.slice(0, -trailing.length) : raw;
      const href = safeHref(url);
      if (href) {
        out.push({ kind: 'link', text: url, href });
        push(trailing);
      } else {
        push(m[0]);
      }
    }
  }
  push(text.slice(last));
  return out;
}

const HEADING = /^(#{1,6})\s+(.*?)\s*#*\s*$/;
const BULLET = /^\s{0,3}([-*+])\s+(.*)$/;
const ORDERED = /^\s{0,3}(\d{1,3})[.)]\s+(.*)$/;
const FENCE = /^\s{0,3}(```|~~~)/;
const QUOTE = /^\s{0,3}>\s?(.*)$/;

export function parseMarkdown(input: string | null | undefined): Block[] {
  if (!input) return [];
  const lines = input.slice(0, MAX_INPUT).replace(/\r\n?/g, '\n').split('\n');
  const blocks: Block[] = [];
  let paragraph: string[] = [];

  const flushParagraph = () => {
    if (paragraph.length) {
      // a single newline inside a paragraph is a soft wrap, not a line break
      blocks.push({ kind: 'paragraph', inlines: parseInline(paragraph.join(' ').trim()) });
      paragraph = [];
    }
  };

  for (let i = 0; i < lines.length && blocks.length < MAX_BLOCKS; i++) {
    const line = lines[i];

    if (FENCE.test(line)) {
      flushParagraph();
      const body: string[] = [];
      for (i++; i < lines.length && !FENCE.test(lines[i]); i++) body.push(lines[i]);
      blocks.push({ kind: 'code', text: body.join('\n') });
      continue;
    }

    if (line.trim() === '') {
      flushParagraph();
      continue;
    }

    const heading = HEADING.exec(line);
    if (heading) {
      flushParagraph();
      blocks.push({ kind: 'heading', level: heading[1].length, inlines: parseInline(heading[2]) });
      continue;
    }

    const bullet = BULLET.exec(line);
    const ordered = bullet ? null : ORDERED.exec(line);
    if (bullet || ordered) {
      flushParagraph();
      const isOrdered = !!ordered;
      const items: string[] = [(bullet ?? ordered)![2]];
      // following list lines, and indented continuation lines, belong to the same list
      while (i + 1 < lines.length) {
        const next = lines[i + 1];
        const nb = BULLET.exec(next);
        const no = ORDERED.exec(next);
        if (!isOrdered && nb) items.push(nb[2]);
        else if (isOrdered && no) items.push(no[2]);
        else if (/^\s{2,}\S/.test(next) && !nb && !no) items[items.length - 1] += ' ' + next.trim();
        else break;
        i++;
      }
      blocks.push({ kind: 'list', ordered: isOrdered, items: items.map(parseInline) });
      continue;
    }

    const quote = QUOTE.exec(line);
    if (quote) {
      flushParagraph();
      const body = [quote[1]];
      while (i + 1 < lines.length && QUOTE.test(lines[i + 1])) body.push(QUOTE.exec(lines[++i])![1]);
      blocks.push({ kind: 'quote', inlines: parseInline(body.join(' ').trim()) });
      continue;
    }

    paragraph.push(line.trim());
  }

  flushParagraph();
  return blocks;
}
