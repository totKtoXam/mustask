import { DESCRIPTION_RAW_CEILING } from './limits';
import { CODE_LANGUAGES, firstToken } from './vocabulary';
import { cleanText } from './whitespace';

/**
 * The client-only pre-pass of a pasted html (plan-frontend § 7.5, BR-RT-18,
 * US-1): what the server's reduction never reads — inline `style` — is read
 * here into the marks the subset has, and code that came from an IDE or a page
 * is made one `<pre><code>` of its plain text, before the shared normaliser
 * (the server's own rules, F-1) takes the document. The document is the parsed
 * inert one; nothing is serialised to a string and parsed again.
 *
 * Rules, all the spec's:
 * - `font-weight` of 600 or more, `bold`, `bolder` → bold; `font-style` italic or
 *   oblique → italic; `text-decoration(-line)` with underline → underline, with
 *   line-through → strike. A `b` or `strong` that declares `font-weight: normal`
 *   or `400` — the Google Docs wrapper of a whole paste — is not bold: it is
 *   unwrapped, and only what is declared below it counts. A heading takes no
 *   mark from its style (it is bold already).
 * - Foreign colour, background and size are never mapped to the palette
 *   (BR-RT-15): the normaliser drops every `style`.
 * - An element that is `pre`, or declares `white-space: pre|pre-wrap` together
 *   with a monospace `font-family`, becomes one code block of its plain text
 *   (lines from block children, `br` and source newlines); a language class
 *   on it or on a `code` inside it is kept only when it is in the closed
 *   list.
 */

export interface ReducedPaste {
  /** The adjusted `<body>`, ready for `normaliseBody`. */
  body: HTMLElement;
  /** An image was among what was pasted: it is dropped and the member told (D-7). */
  hadImage: boolean;
}

const WRAPPER = '<!DOCTYPE html><html><head></head><body>';

const BLOCKS = new Set([
  'address',
  'article',
  'aside',
  'blockquote',
  'dd',
  'details',
  'div',
  'dl',
  'dt',
  'fieldset',
  'figcaption',
  'figure',
  'footer',
  'form',
  'h1',
  'h2',
  'h3',
  'h4',
  'h5',
  'h6',
  'header',
  'hr',
  'li',
  'main',
  'nav',
  'ol',
  'p',
  'pre',
  'section',
  'table',
  'tbody',
  'td',
  'tfoot',
  'th',
  'thead',
  'tr',
  'ul',
]);

const MONOSPACE =
  /\bmonospace\b|\bmono\b|consolas|courier|menlo|monaco|fira code|source code|cascadia code|lucida console/i;

function declarations(style: string | null): Map<string, string> {
  const found = new Map<string, string>();
  if (!style) return found;
  for (const part of style.split(';')) {
    const colon = part.indexOf(':');
    if (colon <= 0) continue;
    found.set(
      part.slice(0, colon).trim().toLowerCase(),
      part
        .slice(colon + 1)
        .trim()
        .toLowerCase(),
    );
  }
  return found;
}

function isBold(weight: string | undefined): boolean {
  if (weight === undefined) return false;
  if (weight === 'bold' || weight === 'bolder') return true;
  const number = Number.parseInt(weight, 10);
  return Number.isFinite(number) && number >= 600;
}

function isNormalWeight(weight: string | undefined): boolean {
  return weight === 'normal' || weight === '400';
}

function wrapChildren(
  document: Document,
  element: Element,
  tag: 'strong' | 'em' | 'u' | 's',
): void {
  const wrapper = document.createElement(tag);
  while (element.firstChild) wrapper.appendChild(element.firstChild);
  element.appendChild(wrapper);
}

function unwrap(element: Element): void {
  const parent = element.parentNode;
  if (!parent) return;
  while (element.firstChild) parent.insertBefore(element.firstChild, element);
  parent.removeChild(element);
}

/** The plain text of an element: `br` and block boundaries are line feeds, source newlines stay. */
function plainText(root: Element): string {
  let output = '';
  let pendingBreak = false;

  const write = (text: string) => {
    if (text.length === 0) return;
    if (pendingBreak && output.length > 0 && !output.endsWith('\n')) output += '\n';
    pendingBreak = false;
    output += text;
  };

  const visit = (node: Node) => {
    if (node.nodeType === 3) {
      write((node as Text).data);
      return;
    }
    if (node.nodeType !== 1) return;
    const element = node as Element;
    const name = element.localName;
    if (name === 'br') {
      pendingBreak = false;
      output += '\n';
      return;
    }
    if (name === 'script' || name === 'style') return;
    const block = BLOCKS.has(name) && element !== root;
    if (block) pendingBreak = true;
    for (const child of Array.from(element.childNodes)) visit(child);
    if (block) pendingBreak = true;
  };

  visit(root);
  return output;
}

function isCodeCandidate(element: Element): boolean {
  if (element.localName === 'pre') return true;
  const style = declarations(element.getAttribute('style'));
  const whiteSpace = style.get('white-space');
  const family = style.get('font-family');
  return (
    (whiteSpace === 'pre' || whiteSpace === 'pre-wrap') &&
    family !== undefined &&
    MONOSPACE.test(family)
  );
}

function languageClass(element: Element): string | null {
  const own = firstToken(CODE_LANGUAGES, element.getAttribute('class'));
  if (own) return own.class;
  for (const code of Array.from(element.querySelectorAll('code'))) {
    const token = firstToken(CODE_LANGUAGES, code.getAttribute('class'));
    if (token) return token.class;
  }
  return null;
}

export function reducePastedHtml(html: string): ReducedPaste | null {
  if (html.length > DESCRIPTION_RAW_CEILING) return null;

  const document = new DOMParser().parseFromString(WRAPPER + cleanText(html), 'text/html');
  const body = document.body as HTMLElement | null;
  if (body === null || body.localName !== 'body') return null;

  const hadImage =
    body.querySelector('img') !== null || body.querySelector('[src^="data:" i]') !== null;

  // Code first, outermost candidates only: whatever styles it holds are gone with it.
  const elements = Array.from(body.querySelectorAll('*'));
  const replaced = new Set<Element>();
  for (const element of elements) {
    if (replaced.has(element) || !isCodeCandidate(element)) continue;
    let nested = false;
    for (let ancestor = element.parentElement; ancestor; ancestor = ancestor.parentElement) {
      if (replaced.has(ancestor)) {
        nested = true;
        break;
      }
    }
    if (nested) continue;

    const text = plainText(element);
    const language = languageClass(element);
    const pre = document.createElement('pre');
    const code = document.createElement('code');
    if (language) code.setAttribute('class', language);
    code.appendChild(document.createTextNode(text));
    pre.appendChild(code);
    element.replaceWith(pre);
    for (const inner of Array.from(element.querySelectorAll('*'))) replaced.add(inner);
    replaced.add(element);
  }

  // Marks from style, then the Docs wrapper.
  for (const element of Array.from(body.querySelectorAll('*'))) {
    if (replaced.has(element) || element.closest('pre') !== null) continue;
    const name = element.localName;
    const style = declarations(element.getAttribute('style'));
    const weight = style.get('font-weight');

    if ((name === 'b' || name === 'strong') && isNormalWeight(weight)) {
      unwrap(element);
      continue;
    }
    if (/^h[1-6]$/.test(name) || style.size === 0) continue;

    if (isBold(weight)) wrapChildren(document, element, 'strong');
    const fontStyle = style.get('font-style');
    if (fontStyle === 'italic' || fontStyle === 'oblique') wrapChildren(document, element, 'em');
    const decoration = `${style.get('text-decoration') ?? ''} ${style.get('text-decoration-line') ?? ''}`;
    if (decoration.includes('underline')) wrapChildren(document, element, 'u');
    if (decoration.includes('line-through')) wrapChildren(document, element, 's');
  }

  return { body, hadImage };
}
