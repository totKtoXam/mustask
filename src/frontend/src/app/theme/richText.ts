import { colourRoles, typeScale } from './tokens';

/**
 * `theme.richText` (feat-018 design § Palettes, § Code blocks): the colours the
 * stored class tokens of the rich-text subset stand for, one keyed table for
 * light and dark. The token is the contract (`contracts/rich-text/
 * vocabularies.json`); the colour is the theme's, so a palette can change
 * without a data change (ADR-0037 decision 2). The table is keyed by the
 * vocabulary's own class names and `richText.test.ts` fails when a token has no
 * entry here or an entry has no token (ADR-0037 decision 6).
 *
 * The panel has no dark theme yet (`mode: 'light'`): the dark column is
 * specified and tested now and read when it ships.
 */

export type RichTextMode = 'light' | 'dark';

interface Pair {
  light: string;
  dark: string;
}

/** Text colour tokens (design § Palettes), on paper `#FFFFFF` / `#1F1F1F`. */
const textColours = {
  'rt-color-gray': { light: '#616161', dark: '#B8B8B8' },
  'rt-color-red': { light: '#C42B1C', dark: '#FF8F85' },
  'rt-color-orange': { light: '#9D5D00', dark: '#F2A93B' },
  'rt-color-green': { light: '#107C10', dark: '#6CCB6C' },
  'rt-color-teal': { light: '#00796B', dark: '#4DD0C0' },
  'rt-color-blue': { light: '#0B5CAD', dark: '#6CB6F5' },
  'rt-color-purple': { light: '#773B93', dark: '#C9A0F0' },
  'rt-color-pink': { light: '#B4236B', dark: '#FF8FC2' },
} as const satisfies Record<string, Pair>;

/** Highlight tokens: a background; the text on it stays `text.primary` (`#F5F5F5` in dark). */
const highlightColours = {
  'rt-hl-yellow': { light: '#FFE680', dark: '#6B5800' },
  'rt-hl-orange': { light: '#FFC58A', dark: '#7A4300' },
  'rt-hl-red': { light: '#FFB4AB', dark: '#8C2A22' },
  'rt-hl-green': { light: '#B5E5AE', dark: '#1E5E26' },
  'rt-hl-teal': { light: '#9FE0D8', dark: '#0E5E56' },
  'rt-hl-blue': { light: '#B0D6F7', dark: '#1D4E82' },
  'rt-hl-purple': { light: '#D9C2F0', dark: '#5A3880' },
  'rt-hl-gray': { light: '#D4D4D4', dark: '#4D4D4D' },
} as const satisfies Record<string, Pair>;

/** The eight token classes of the code colouring decorations (design § Syntax colouring). */
export const CODE_TOKEN_NAMES = [
  'keyword',
  'string',
  'number',
  'comment',
  'function',
  'type',
  'attribute',
  'punctuation',
] as const;

export type CodeTokenName = (typeof CODE_TOKEN_NAMES)[number];

const codeColours: Record<CodeTokenName, Pair> = {
  keyword: { light: '#8F2D9E', dark: '#D9A0F0' },
  string: { light: '#0F6B2B', dark: '#8FD6A0' },
  number: { light: '#B3410A', dark: '#FFB085' },
  comment: { light: '#5C5C5C', dark: '#ABABAB' },
  function: { light: '#0B5CAD', dark: '#7DBEF7' },
  type: { light: '#8A5A00', dark: '#EBC25A' },
  attribute: { light: '#006D77', dark: '#6FD6DE' },
  punctuation: { light: '#4F4F4F', dark: '#C4C4C4' },
};

/** The dark surfaces the design assumes (paper, raised, and the text on a highlight). */
const darkSurfaces = {
  paper: '#1F1F1F',
  raised: '#2B2B2B',
  highlightText: '#F5F5F5',
} as const;

/** Font size tokens (design § Palettes): `xs` and `sm` are the theme's `caption` and `tableCell` scales. */
const fontSizes = {
  'rt-size-xs': typeScale.caption.size,
  'rt-size-sm': typeScale.tableCell.size,
  'rt-size-lg': 18,
  'rt-size-xl': 24,
} as const;

/** Line height of a sized run, as a multiple of its font size. */
const SIZED_LINE_HEIGHT = 1.4;

/** `theme.richText`: what the stored tokens look like, read through the theme object. */
export interface RichTextTheme {
  /**
   * The colour of a text colour token (the text) or of a highlight token (the
   * background), for the theme's mode; `null` for a class that is neither.
   */
  colour(className: string): string | null;
  /** The text colour on a highlight: `text.primary`'s own, or the light-on-dark one. */
  highlightText(): string;
  /** The colour of one code token class. */
  code(token: CodeTokenName): string;
  /** The font size, in px, of a `rt-size-*` token; `null` for another class. */
  fontSize(className: string): number | null;
  /** A sized run's line height, a multiple of its font size. */
  sizedLineHeight: number;
  /** The code block's background (a theme surface) for the theme's mode. */
  codeBackground(): string;
  /** Every class the table maps, for the completeness test. */
  classes(): string[];
}

export function createRichTextTheme(mode: RichTextMode): RichTextTheme {
  const palette: Record<string, Pair> = { ...textColours, ...highlightColours };
  const sizes: Record<string, number> = fontSizes;

  return {
    colour: (className) =>
      Object.hasOwn(palette, className) ? (palette[className] as Pair)[mode] : null,
    highlightText: () => (mode === 'dark' ? darkSurfaces.highlightText : colourRoles.textPrimary),
    code: (token) => codeColours[token][mode],
    fontSize: (className) =>
      Object.hasOwn(sizes, className) ? (sizes[className] as number) : null,
    sizedLineHeight: SIZED_LINE_HEIGHT,
    codeBackground: () => (mode === 'dark' ? darkSurfaces.raised : colourRoles.tableHeader),
    classes: () => [...Object.keys(palette), ...Object.keys(sizes)],
  };
}

/** The raw table, for the contrast test only; slices read `theme.richText`. */
export const richTextTable = {
  textColours,
  highlightColours,
  codeColours,
  darkSurfaces,
  fontSizes,
};
