import raw from '../../../../../../contracts/rich-text/vocabularies.json';

/**
 * The closed vocabularies of the rich-text subset as a typed view of
 * `contracts/rich-text/vocabularies.json` (ADR-0027 decision 6). The server
 * embeds the same file; neither side hand-writes a token. No class literal of
 * the vocabulary appears anywhere else in `src/` outside tests and the theme
 * table (`vocabulary.test.ts` greps for it).
 */

export interface VocabularyToken {
  /** The name the editor, the toolbar and the theme know the token by. */
  readonly name: string;
  /** The class written to the stored html. */
  readonly class: string;
}

/** The marks of the canonical form, outermost first (BR-RT-15). */
export type MarkKind =
  'link' | 'bold' | 'italic' | 'underline' | 'strike' | 'highlight' | 'textColor' | 'fontSize';

const MARK_KINDS: readonly MarkKind[] = [
  'link',
  'bold',
  'italic',
  'underline',
  'strike',
  'highlight',
  'textColor',
  'fontSize',
];

interface RawFamily {
  kind: string;
  element: string;
  plain?: string;
  tokens: readonly { name: string; class: string }[];
}

function familyOf(kind: string): RawFamily {
  const family = (raw.families as readonly RawFamily[]).find(
    (candidate) => candidate.kind === kind,
  );
  if (!family) {
    throw new Error(`The rich-text vocabulary has no family '${kind}'.`);
  }
  return family;
}

function tokensOf(kind: string): readonly VocabularyToken[] {
  return Object.freeze(
    familyOf(kind).tokens.map((token) => Object.freeze({ name: token.name, class: token.class })),
  );
}

function markOrder(): readonly MarkKind[] {
  const order = raw.markOrder.map((name) => {
    const kind = MARK_KINDS.find((candidate) => candidate === name);
    if (!kind)
      throw new Error(`The rich-text vocabulary's markOrder names an unknown mark '${name}'.`);
    return kind;
  });
  if (order.length !== MARK_KINDS.length || new Set(order).size !== MARK_KINDS.length) {
    throw new Error("The rich-text vocabulary's markOrder must name every mark exactly once.");
  }
  return Object.freeze(order);
}

export const MARK_ORDER: readonly MarkKind[] = markOrder();
export const TEXT_COLOURS: readonly VocabularyToken[] = tokensOf('textColor');
export const HIGHLIGHTS: readonly VocabularyToken[] = tokensOf('highlight');
export const FONT_SIZES: readonly VocabularyToken[] = tokensOf('fontSize');
export const CODE_LANGUAGES: readonly VocabularyToken[] = tokensOf('codeLanguage');

/** The language value of a code block that is shown as plain text (BR-RT-16). */
export const CODE_LANGUAGE_PLAIN: string = familyOf('codeLanguage').plain ?? 'plain';

function isAsciiWhitespaceUnit(unit: number): boolean {
  return unit === 0x20 || unit === 0x09 || unit === 0x0a || unit === 0x0c || unit === 0x0d;
}

/**
 * The first token of `family` found in a `class` attribute value: split on
 * ASCII whitespace only (U+00A0 is not a separator), each part compared
 * exactly and case-sensitively, the first valid part in attribute order wins
 * (the server's `TokenFamily.FirstMatch`). `null` when none is.
 */
export function firstToken(
  family: readonly VocabularyToken[],
  classValue: string | null | undefined,
): VocabularyToken | null {
  if (!classValue) return null;

  let start = -1;
  for (let index = 0; index <= classValue.length; index++) {
    const isSeparator =
      index === classValue.length || isAsciiWhitespaceUnit(classValue.charCodeAt(index));
    if (!isSeparator) {
      if (start < 0) start = index;
      continue;
    }
    if (start >= 0) {
      const part = classValue.slice(start, index);
      const found = family.find((token) => token.class === part);
      if (found) return found;
      start = -1;
    }
  }
  return null;
}

/** The token of `family` named `name`, or `null`. */
export function tokenByName(
  family: readonly VocabularyToken[],
  name: string | null | undefined,
): VocabularyToken | null {
  if (!name) return null;
  return family.find((token) => token.name === name) ?? null;
}
