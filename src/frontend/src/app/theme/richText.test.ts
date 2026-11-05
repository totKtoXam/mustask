import { describe, expect, it } from 'vitest';
import { FONT_SIZES, HIGHLIGHTS, TEXT_COLOURS } from '../../shared/lib/richText/vocabulary';
import { contrastRatio } from './contrast';
import { CODE_TOKEN_NAMES, createRichTextTheme, richTextTable } from './richText';
import { theme } from './theme';

// feat-018 design § Palettes and § Syntax colouring: the token is the contract,
// the colour is the theme's. WCAG 2.1 AA text contrast is 4.5:1.
const TEXT = 4.5;

const { textColours, highlightColours, codeColours, darkSurfaces } = richTextTable;
const paper = { light: theme.palette.background.paper, dark: darkSurfaces.paper };
const codeBackground = { light: theme.palette.shell.tableHeader, dark: darkSurfaces.raised };
const highlightText = { light: theme.palette.text.primary, dark: darkSurfaces.highlightText };

describe('theme.richText maps the vocabulary one to one (ADR-0037 decision 6)', () => {
  const mapped = new Set(theme.richText.classes());
  const tokens = [...TEXT_COLOURS, ...HIGHLIGHTS, ...FONT_SIZES].map((token) => token.class);

  it('has an entry for every token of the vocabulary', () => {
    for (const className of tokens) {
      expect(mapped.has(className), className).toBe(true);
    }
  });

  it('has no entry for a token the vocabulary lacks', () => {
    expect([...mapped].filter((className) => !tokens.includes(className))).toEqual([]);
  });

  it('answers a colour for the palettes and a size for the size tokens, nothing for a stranger', () => {
    for (const token of [...TEXT_COLOURS, ...HIGHLIGHTS]) {
      expect(theme.richText.colour(token.class)).toMatch(/^#[0-9A-F]{6}$/);
    }
    for (const token of FONT_SIZES) {
      expect(theme.richText.fontSize(token.class)).toBeGreaterThan(0);
    }
    expect(theme.richText.colour('rt-color-chartreuse')).toBeNull();
    expect(theme.richText.colour('__proto__')).toBeNull();
    expect(theme.richText.fontSize('constructor')).toBeNull();
  });

  it('gives the four sizes of the design: 12, 13, 18 and 24 px, ascending', () => {
    expect(FONT_SIZES.map((token) => theme.richText.fontSize(token.class))).toEqual([
      12, 13, 18, 24,
    ]);
  });

  it('reads the column of the theme mode', () => {
    const dark = createRichTextTheme('dark');
    const red = TEXT_COLOURS.find((token) => token.name === 'red')?.class as string;

    expect(theme.palette.mode).toBe('light');
    expect(theme.richText.colour(red)).toBe(textColours['rt-color-red'].light);
    expect(dark.colour(red)).toBe(textColours['rt-color-red'].dark);
    expect(dark.highlightText()).toBe(darkSurfaces.highlightText);
  });

  it('has the eight code token colours of the design', () => {
    expect([...CODE_TOKEN_NAMES]).toEqual([
      'keyword',
      'string',
      'number',
      'comment',
      'function',
      'type',
      'attribute',
      'punctuation',
    ]);
    for (const name of CODE_TOKEN_NAMES) {
      expect(theme.richText.code(name)).toBe(codeColours[name].light);
    }
  });
});

describe('contrast of the mapped colours (design § Palettes, BR-RT-14)', () => {
  const modes = ['light', 'dark'] as const;

  describe.each(modes)('%s', (mode) => {
    it('every text colour reads on paper at 4.5:1', () => {
      for (const [className, pair] of Object.entries(textColours)) {
        expect(contrastRatio(pair[mode], paper[mode]), className).toBeGreaterThanOrEqual(TEXT);
      }
    });

    it('the text on every highlight reads at 4.5:1', () => {
      for (const [className, pair] of Object.entries(highlightColours)) {
        expect(contrastRatio(highlightText[mode], pair[mode]), className).toBeGreaterThanOrEqual(
          TEXT,
        );
      }
    });

    it('every code token colour reads on the code block at 4.5:1', () => {
      for (const [name, pair] of Object.entries(codeColours)) {
        expect(contrastRatio(pair[mode], codeBackground[mode]), name).toBeGreaterThanOrEqual(TEXT);
      }
    });
  });

  it('keeps the design numbers: the worst light text colour is 5.25:1 (orange)', () => {
    const worst = Math.min(
      ...Object.values(textColours).map((pair) => contrastRatio(pair.light, paper.light)),
    );

    expect(worst).toBeGreaterThan(5.2);
    expect(worst).toBeLessThan(5.3);
  });
});
