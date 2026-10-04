import { describe, expect, it } from 'vitest';
import { contrastRatio } from './contrast';
import { theme } from './theme';
import { APPEARANCE_TOKENS, CATEGORY_CODES } from './tokens';

// WCAG 2.1 AA (BR-5): text 4.5:1; non-text UI — input borders, the focus
// indicator, selected markers, type and category colours — 3:1.
const TEXT = 4.5;
const NON_TEXT = 3;

const { palette } = theme;

const surfaces = {
  content: palette.background.paper,
  'application background': palette.background.default,
  navigation: palette.shell.navigation,
  'navigation hover': palette.shell.navigationHover,
  'navigation selected': palette.shell.navigationSelected,
  'table header': palette.shell.tableHeader,
  'row hover': palette.action.hover,
  'row selected': palette.shell.rowSelected,
};

// Where type icons and category dots sit (design § 2.3).
const workTrackingSurfaces = {
  content: surfaces.content,
  'application background': surfaces['application background'],
  'row hover': surfaces['row hover'],
  'row selected': surfaces['row selected'],
};

type Pair = [name: string, foreground: string, background: string];

function on(name: string, foreground: string, onto: Record<string, string>): Pair[] {
  return Object.entries(onto).map(([surface, background]) => [
    `${name} on ${surface}`,
    foreground,
    background,
  ]);
}

const textPairs: Pair[] = [
  ...on('neutral text', palette.text.primary, surfaces),
  ...on('secondary text', palette.text.secondary, surfaces),
  // Links are text in the accent.
  ...on('link', palette.primary.main, {
    content: surfaces.content,
    'application background': surfaces['application background'],
  }),
  ['white on the accent', palette.primary.contrastText, palette.primary.main],
  [
    'contrast text on the neutral secondary',
    palette.secondary.contrastText,
    palette.secondary.main,
  ],
  ['white on error', palette.error.contrastText, palette.error.main],
  ['white on warning', palette.warning.contrastText, palette.warning.main],
  ['white on success', palette.success.contrastText, palette.success.main],
  ['white on info', palette.info.contrastText, palette.info.main],
];

const nonTextPairs: Pair[] = [
  ...on('focus indicator', palette.primary.main, surfaces),
  ['selected navigation marker', palette.primary.main, palette.shell.navigationSelected],
  ...on('input border', palette.shell.inputBorder, {
    content: surfaces.content,
    'application background': surfaces['application background'],
  }),
  ...APPEARANCE_TOKENS.flatMap((token) =>
    on(`type ${token}`, theme.workTracking.typeAppearance(token).colour, workTrackingSurfaces),
  ),
  ...CATEGORY_CODES.flatMap((code) =>
    on(`category ${code}`, theme.workTracking.categoryColour(code), workTrackingSurfaces),
  ),
];

describe('US-1 Scenario: Contrast (BR-5)', () => {
  it.each(textPairs)('%s meets 4.5:1', (_name, foreground, background) => {
    expect(contrastRatio(foreground, background)).toBeGreaterThanOrEqual(TEXT);
  });

  it.each(nonTextPairs)('%s meets 3:1', (_name, foreground, background) => {
    expect(contrastRatio(foreground, background)).toBeGreaterThanOrEqual(NON_TEXT);
  });
});

describe('contrastRatio', () => {
  it('gives 21:1 for black on white and 1:1 for a colour on itself', () => {
    expect(contrastRatio('#000000', '#FFFFFF')).toBeCloseTo(21, 5);
    expect(contrastRatio('#0F6CBD', '#0F6CBD')).toBe(1);
  });

  it('is symmetric and reads the short #RGB form', () => {
    expect(contrastRatio('#FFF', '#242424')).toBeCloseTo(contrastRatio('#242424', '#FFFFFF'), 10);
  });

  it('refuses a value that is not a hex colour', () => {
    expect(() => contrastRatio('red', '#FFFFFF')).toThrow();
  });
});
