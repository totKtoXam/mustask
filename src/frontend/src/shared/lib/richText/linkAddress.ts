import { trimUnicodeWhiteSpace } from './whitespace';

/**
 * The link address rule of US-2 / BR-RT-18, a port of the server's
 * `LinkAddress.TryRead` (plan-frontend § 4.4): the address is read as a browser
 * reads it (leading and trailing C0 controls and spaces trimmed, ASCII tab, LF
 * and CR removed anywhere) and kept only if it is `http` or `https` with
 * `//` right after the colon and a non-empty host, or `mailto` with a non-empty
 * address. Relative, protocol-relative, fragment-only, empty and every other
 * scheme are refused. The result is the address as read: never re-encoded,
 * never resolved.
 */
export function readLinkAddress(raw: string | null | undefined): string | null {
  if (raw === null || raw === undefined) return null;

  let start = 0;
  let end = raw.length;
  while (start < end && raw.charCodeAt(start) <= 0x20) start++;
  while (end > start && raw.charCodeAt(end - 1) <= 0x20) end--;

  const cleaned = removeTabAndLineBreaks(raw.slice(start, end));

  const colon = cleaned.indexOf(':');
  if (colon <= 0 || !isSchemeStart(cleaned.charCodeAt(0))) return null;

  for (let index = 1; index < colon; index++) {
    if (!isSchemeCharacter(cleaned.charCodeAt(index))) return null;
  }

  const scheme = cleaned.slice(0, colon).toLowerCase();
  const rest = cleaned.slice(colon + 1);

  if (scheme === 'http' || scheme === 'https') {
    if (!hasHost(rest)) return null;
  } else if (scheme === 'mailto') {
    if (!hasAddress(rest)) return null;
  } else {
    return null;
  }

  return cleaned;
}

function removeTabAndLineBreaks(text: string): string {
  if (!/[\t\n\r]/.test(text)) return text;
  return text.replace(/[\t\n\r]/g, '');
}

/** `//authority` with a non-empty host; the authority holds no control, space or markup character. */
function hasHost(rest: string): boolean {
  if (rest.length < 3 || rest[0] !== '/' || rest[1] !== '/') return false;

  let authority = rest.slice(2);
  const stop = authority.search(/[/?#]/);
  if (stop >= 0) authority = authority.slice(0, stop);

  for (let index = 0; index < authority.length; index++) {
    const unit = authority.charCodeAt(index);
    if (unit <= 0x20 || unit === 0x7f || '\\<>"`{}|^'.includes(authority[index] as string))
      return false;
  }

  const at = authority.lastIndexOf('@');
  if (at >= 0) authority = authority.slice(at + 1);

  // A port is not a host: ":80" has none.
  let host = authority;
  if (!authority.startsWith('[')) {
    const colon = authority.indexOf(':');
    host = colon >= 0 ? authority.slice(0, colon) : authority;
  }

  return host.length > 0;
}

/** `mailto:` needs an address before any `?`, with no control or space in it. */
function hasAddress(rest: string): boolean {
  const query = rest.indexOf('?');
  const address = query >= 0 ? rest.slice(0, query) : rest;
  if (address.length === 0) return false;

  for (let index = 0; index < address.length; index++) {
    const unit = address.charCodeAt(index);
    if (unit <= 0x20 || unit === 0x7f) return false;
  }
  return true;
}

function isSchemeStart(unit: number): boolean {
  return (unit >= 0x61 && unit <= 0x7a) || (unit >= 0x41 && unit <= 0x5a);
}

function isSchemeCharacter(unit: number): boolean {
  return (
    isSchemeStart(unit) ||
    (unit >= 0x30 && unit <= 0x39) ||
    unit === 0x2b ||
    unit === 0x2d ||
    unit === 0x2e
  );
}

const WWW_PREFIX = /^www\./i;
const AFTER_WWW = /[^/?#\s]/u;

/**
 * What the link prompt and the auto-link do with typed text (BR-RT-18, F-10):
 * trimmed; `www.` followed by at least one character that is not `/?#` or
 * white space becomes `https://` + the text; then the server's rule. `null`
 * means the address is refused. The result is what is stored as `href`.
 */
export function promptAddress(input: string): string | null {
  const trimmed = trimUnicodeWhiteSpace(input);
  if (trimmed.length === 0) return null;

  if (WWW_PREFIX.test(trimmed)) {
    if (!AFTER_WWW.test(trimmed.slice(4))) return null;
    return readLinkAddress('https://' + trimmed);
  }

  return readLinkAddress(trimmed);
}
