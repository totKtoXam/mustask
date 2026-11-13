import { promptAddress } from './linkAddress';
import { isUnicodeWhiteSpace } from './whitespace';

/** One address found in a run of text: `[start, end)` in UTF-16 units, and the `href` to store. */
export interface AutoLink {
  start: number;
  end: number;
  href: string;
}

const START = /^(?:https?:\/\/|mailto:|www\.)/i;
const TRAILING = new Set(['.', ',', ';', ':', '!', '?', '»', '"', "'"]);

function count(text: string, character: string): number {
  let found = 0;
  for (const own of text) if (own === character) found++;
  return found;
}

/**
 * What the typed and pasted auto-link makes a link (BR-RT-18, plan § 7.4):
 * tokens split on Unicode white space; a token qualifies when, after trimming
 * trailing `. , ; : ! ? » " '` and an unbalanced `)` or `]` (balanced pairs
 * stay: `https://example.org/a_(b)`), it starts with `http://`, `https://`,
 * `mailto:` or `www.` (case-insensitive) and passes `promptAddress` (a
 * non-empty host). Bare domains, bare e-mails, file names and `ftp://` do not
 * qualify. Where it may apply (not in code, not in a link) is the editor's.
 */
export function findAutoLinks(text: string): AutoLink[] {
  const found: AutoLink[] = [];
  let index = 0;

  while (index < text.length) {
    while (index < text.length && isUnicodeWhiteSpace(text.charCodeAt(index))) index++;
    const start = index;
    while (index < text.length && !isUnicodeWhiteSpace(text.charCodeAt(index))) index++;
    let end = index;
    if (end === start) continue;

    for (;;) {
      const last = text[end - 1] as string;
      const token = text.slice(start, end);
      if (TRAILING.has(last)) {
        end--;
      } else if (last === ')' && count(token, ')') > count(token, '(')) {
        end--;
      } else if (last === ']' && count(token, ']') > count(token, '[')) {
        end--;
      } else {
        break;
      }
      if (end === start) break;
    }
    if (end === start) continue;

    const candidate = text.slice(start, end);
    if (!START.test(candidate)) continue;
    const href = promptAddress(candidate);
    if (href !== null) found.push({ start, end, href });
  }

  return found;
}
