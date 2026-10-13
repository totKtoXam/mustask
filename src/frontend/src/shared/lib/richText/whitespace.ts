/**
 * Which code points the server calls white space when it decides a value is
 * blank (BR-RT-6): `System.Text.Rune.IsWhiteSpace`, which is the Unicode
 * `White_Space` property (categories Zs, Zl, Zp, plus U+0009..U+000D and
 * U+0085). It is spelled out here, not taken from the `\s` of a regular
 * expression, because the two disagree: `\s` also matches U+FEFF and not
 * U+0085 (plan-frontend § 4.6 item 2).
 */
export function isUnicodeWhiteSpace(codePoint: number): boolean {
  if (codePoint <= 0x20) {
    return codePoint === 0x20 || (codePoint >= 0x09 && codePoint <= 0x0d);
  }
  if (codePoint < 0x85) return false;

  return (
    codePoint === 0x85 ||
    codePoint === 0xa0 ||
    codePoint === 0x1680 ||
    (codePoint >= 0x2000 && codePoint <= 0x200a) ||
    codePoint === 0x2028 ||
    codePoint === 0x2029 ||
    codePoint === 0x202f ||
    codePoint === 0x205f ||
    codePoint === 0x3000
  );
}

/** HTML's "ASCII whitespace": space, tab, LF, FF, CR. U+00A0 is not one. */
export function isAsciiWhitespace(codeUnit: number): boolean {
  return (
    codeUnit === 0x20 ||
    codeUnit === 0x09 ||
    codeUnit === 0x0a ||
    codeUnit === 0x0c ||
    codeUnit === 0x0d
  );
}

/** True when every code unit of `text` is ASCII whitespace (the empty string is). */
export function isAllAsciiWhitespace(text: string): boolean {
  for (let index = 0; index < text.length; index++) {
    if (!isAsciiWhitespace(text.charCodeAt(index))) return false;
  }
  return true;
}

/** `String.prototype.trim` with the server's white space set, not the engine's. */
export function trimUnicodeWhiteSpace(text: string): string {
  let start = 0;
  let end = text.length;
  while (start < end && isUnicodeWhiteSpace(text.charCodeAt(start))) start++;
  while (end > start && isUnicodeWhiteSpace(text.charCodeAt(end - 1))) end--;
  return text.slice(start, end);
}

/** Unicode code points in `text` (a lone surrogate counts one), never `text.length` (BR-RT-8). */
export function codePointLength(text: string): number {
  let count = 0;
  for (let index = 0; index < text.length; index++) {
    const unit = text.charCodeAt(index);
    if (unit >= 0xd800 && unit <= 0xdbff && index + 1 < text.length) {
      const next = text.charCodeAt(index + 1);
      if (next >= 0xdc00 && next <= 0xdfff) index++;
    }
    count++;
  }
  return count;
}

/**
 * U+0000 removed and each lone surrogate replaced by U+FFFD (BR-RT-8,
 * BR-RT-9), before anything parses the text. The server does this to the raw
 * request value; the panel does it to every string it normalises or converts.
 */
export function cleanText(raw: string): string {
  let clean = true;
  for (let index = 0; index < raw.length && clean; index++) {
    const unit = raw.charCodeAt(index);
    if (unit === 0) {
      clean = false;
    } else if (unit >= 0xd800 && unit <= 0xdbff) {
      const next = index + 1 < raw.length ? raw.charCodeAt(index + 1) : 0;
      if (next >= 0xdc00 && next <= 0xdfff) {
        index++;
      } else {
        clean = false;
      }
    } else if (unit >= 0xdc00 && unit <= 0xdfff) {
      clean = false;
    }
  }
  if (clean) return raw;

  let result = '';
  let runStart = 0;
  for (let index = 0; index < raw.length; index++) {
    const unit = raw.charCodeAt(index);
    let replacement: string | null = null;
    let skip = 0;
    if (unit === 0) {
      replacement = '';
    } else if (unit >= 0xd800 && unit <= 0xdbff) {
      const next = index + 1 < raw.length ? raw.charCodeAt(index + 1) : 0;
      if (next >= 0xdc00 && next <= 0xdfff) {
        skip = 1;
      } else {
        replacement = '\ufffd';
      }
    } else if (unit >= 0xdc00 && unit <= 0xdfff) {
      replacement = '\ufffd';
    }
    if (replacement !== null) {
      result += raw.slice(runStart, index) + replacement;
      runStart = index + 1;
    }
    index += skip;
  }
  return result + raw.slice(runStart);
}
