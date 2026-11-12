import { cleanText } from './whitespace';

/**
 * BR-RT-9: the panel's lazy conversion of a stored plain description to html
 * (plan-frontend § 4.5, `contracts/rich-text/fixtures/legacy.json`). U+0000 is
 * removed and a lone surrogate replaced by U+FFFD, `CR LF` and a lone `CR`
 * become `LF`, the text is split at `LF`, each line is a paragraph with `&`,
 * `<`, `>` and U+00A0 escaped (so the output is a fixed point of the
 * normaliser), an empty line is an empty paragraph, spaces and tabs are kept.
 * An empty value stays `''`, not `<p></p>`: the baseline of an empty value
 * must equal the normalised empty editor. Linear time.
 */
export function legacyToHtml(plain: string): string {
  if (plain.length === 0) return '';

  const lines = cleanText(plain).replace(/\r\n?/g, '\n').split('\n');
  const parts: string[] = [];
  for (const line of lines) {
    parts.push('<p>', escapeText(line), '</p>');
  }
  return parts.join('');
}

function escapeText(text: string): string {
  return text.replace(/[&<>\u00a0]/g, (character) => {
    switch (character) {
      case '&':
        return '&amp;';
      case '<':
        return '&lt;';
      case '>':
        return '&gt;';
      default:
        return '&nbsp;';
    }
  });
}
