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

// Where a kit field stands: on a card or a form (white) and in the card's band
// (the application background).
const fieldSurfaces = {
  content: surfaces.content,
  'application background': surfaces['application background'],
};

type Pair = [name: string, foreground: string, background: string];

function on(name: string, foreground: string, onto: Record<string, string>): Pair[] {
  return Object.entries(onto).map(([surface, background]) => [
    `${name} on ${surface}`,
    foreground,
    background,
  ]);
}

/** A translucent `rgba(r, g, b, a)` laid on a `#RRGGBB` surface, as the `#RRGGBB` it looks like. */
function flatten(translucent: string, surface: string): string {
  const found = translucent.match(/[\d.]+/g)?.map(Number);
  if (!found || found.length < 4) throw new Error(`Not an rgba colour: ${translucent}`);
  const [red, green, blue, alpha] = found as [number, number, number, number];
  const base = [0, 2, 4].map((offset) => parseInt(surface.slice(1 + offset, 3 + offset), 16));
  const mixed = [red, green, blue].map((channel, index) =>
    Math.round(alpha * channel + (1 - alpha) * (base[index] as number)),
  );
  return `#${mixed.map((channel) => channel.toString(16).padStart(2, '0')).join('')}`;
}

/**
 * Graphics exempt from the 3:1 test, each with the reason (feat-027 D-10,
 * Q-1 (a)). WCAG 1.4.11 asks 3:1 of a graphic that is the only carrier of what
 * it shows — the focus ring, an input's border, the selected markers, which
 * stay below. These repeat what text on the same row already says, so the
 * reference's lighter colours are taken panel-wide. The list is closed: an entry
 * must still miss 3:1 somewhere (otherwise it is tested strictly again), and
 * must still show at all.
 */
const REPEATED_IN_TEXT: readonly {
  name: string;
  /** The graphic's colour on a given surface. */
  colour: (surface: string) => string;
  reason: string;
}[] = [
  ...(['epic', 'story', 'task'] as const).map((token) => ({
    name: `type ${token}`,
    colour: () => theme.workTracking.typeAppearance(token).colour,
    reason:
      'the type is named in words beside its icon — «Тип» on the backlog, the identifier or the name on a card, the form and the pickers — so the icon repeats text',
  })),
  {
    name: 'category Proposed',
    colour: () => theme.workTracking.categoryColour('Proposed'),
    reason: 'the state’s name follows the dot on every row, card and list that draws it',
  },
  {
    name: 'drag grip dots',
    colour: (surface) => flatten(palette.action.disabled, surface),
    reason:
      'the grip is aria-hidden and only a hint that the row can be taken — the whole row is the drag source (D-11), so a low-vision user needs no dot to find it — and the same moves are in «Ещё действия», the keyboard path (F-13)',
  },
];

/** Exempt is not invisible: the lightest exempt graphic, «Новые» on the selected row, is 1.8:1. */
const STILL_SHOWS = 1.5;

const exemptNames = new Set(REPEATED_IN_TEXT.map((entry) => entry.name));

const textPairs: Pair[] = [
  ...on('neutral text', palette.text.primary, surfaces),
  ...on('secondary text', palette.text.secondary, surfaces),
  // A link is text in the accent's dark companion: the accent itself is 4.26:1 on
  // the application background (components.ts, `MuiLink`; theme.test.tsx draws one).
  ...on('link', palette.primary.dark, {
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
  // The grey secondary button of the team head (feat-027 design § 2.2).
  ['neutral text on the grey button', palette.text.primary, palette.shell.buttonNeutral],
  [
    'neutral text on the grey button’s hover',
    palette.text.primary,
    palette.shell.buttonNeutralHover,
  ],
  // The kit's pairs (tech-015 design § 6). A field's and the band's label is
  // `cardLabel`, lighter than `text.secondary`: 4.95 on white and 4.66 on the
  // band, so a lighter grey (`#737373` is 4.46 on the band) fails here.
  ...on('field label', palette.shell.cardLabel, fieldSurfaces),
  ...on('hint or empty text', palette.text.secondary, fieldSurfaces),
  ['eyebrow on content', palette.primary.dark, surfaces.content],
  ['error line on content', palette.error.main, surfaces.content],
  ['tag text on the neutral tag', palette.text.primary, palette.shell.buttonNeutral],
  ['tag text on the accent tag', palette.text.primary, palette.shell.rowSelected],
];

const nonTextPairs: Pair[] = [
  ...on('focus indicator', palette.primary.main, surfaces),
  // The backlog row's «+» and the selected row's dark-blue type icon (feat-027
  // design § 2.2, § 4.4) stand on the content, the hover and the selected row.
  ...on('row add action', palette.shell.addAction, workTrackingSurfaces),
  ...on('selected row type icon', palette.primary.dark, workTrackingSurfaces),
  ['selected navigation marker', palette.primary.main, palette.shell.navigationSelected],
  ...on('input border', palette.shell.inputBorder, {
    content: surfaces.content,
    'application background': surfaces['application background'],
  }),
  // The idle «+» of the hierarchy header (feat-027 V-3) stands on the white
  // header and is the only carrier of the button: tested strictly.
  ...on('idle level button', palette.text.secondary, { content: surfaces.content }),
  // The focus frame of a kit control on the grey button's fill (white, the band
  // and the selected row's tint are in `surfaces` above), and an error frame.
  ['focus indicator on the grey button', palette.primary.main, palette.shell.buttonNeutral],
  ['error frame on content', palette.error.main, surfaces.content],
  ...APPEARANCE_TOKENS.filter((token) => !exemptNames.has(`type ${token}`)).flatMap((token) =>
    on(`type ${token}`, theme.workTracking.typeAppearance(token).colour, workTrackingSurfaces),
  ),
  ...CATEGORY_CODES.filter((code) => !exemptNames.has(`category ${code}`)).flatMap((code) =>
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

describe('feat-027 D-10 Scenario: graphics whose meaning the row’s text repeats', () => {
  it('exempts exactly the epic, story and task icons, the «Новые» dot and the drag grip — and the other types and categories stay under 3:1', () => {
    expect([...exemptNames].sort()).toEqual(
      ['category Proposed', 'drag grip dots', 'type epic', 'type story', 'type task'].sort(),
    );
    // Feature, bug, «В работе», «Решённые», «Завершённые» and «Удалённые» keep the strict test above.
    const strict = nonTextPairs.map(([name]) => name);
    for (const kept of [
      'type feature on content',
      'type bug on row selected',
      'category InProgress on row selected',
      'category Resolved on content',
      'category Completed on row selected',
      'category Removed on row hover',
    ]) {
      expect(strict, kept).toContain(kept);
    }
  });

  it.each(REPEATED_IN_TEXT.map((entry) => [entry.name, entry] as const))(
    '%s gives its reason in words',
    (_name, entry) => {
      expect(entry.reason.length).toBeGreaterThan(30);
    },
  );

  it.each(REPEATED_IN_TEXT.map((entry) => [entry.name, entry] as const))(
    '%s needs the exemption: it misses 3:1 on some surface, so it is not tested strictly by luck',
    (_name, entry) => {
      const worst = Math.min(
        ...Object.values(workTrackingSurfaces).map((surface) =>
          contrastRatio(entry.colour(surface), surface),
        ),
      );
      expect(worst).toBeLessThan(NON_TEXT);
    },
  );

  it.each(REPEATED_IN_TEXT.map((entry) => [entry.name, entry] as const))(
    '%s still shows: at least 1.5:1 on every surface it sits on',
    (_name, entry) => {
      for (const [surface, background] of Object.entries(workTrackingSurfaces)) {
        expect(
          contrastRatio(entry.colour(background), background),
          `${entry.name} on ${surface}`,
        ).toBeGreaterThanOrEqual(STILL_SHOWS);
      }
    },
  );

  it('keeps «Завершённые» (the reference’s #339933) under the strict test: it clears 3:1 on every surface, so it needs no exemption', () => {
    expect(exemptNames.has('category Completed')).toBe(false);
    for (const background of Object.values(workTrackingSurfaces)) {
      expect(
        contrastRatio(theme.workTracking.categoryColour('Completed'), background),
      ).toBeGreaterThanOrEqual(NON_TEXT);
    }
  });
});

describe('tech-015 Scenario: the field’s hover frame is decoration', () => {
  const { fieldHover, inputBorder } = palette.shell;

  it.each(Object.entries(fieldSurfaces))(
    'sits between 1.5 and 2:1 on %s and under the input border’s ratio',
    (_surface, background) => {
      const ratio = contrastRatio(fieldHover, background);
      expect(ratio).toBeGreaterThanOrEqual(1.5);
      expect(ratio).toBeLessThan(2);
      expect(ratio).toBeLessThan(contrastRatio(inputBorder, background));
    },
  );
});

/**
 * Pairs the kit draws and the contrast floor does not ask of (tech-015 design
 * § 6): recorded, not asserted. Each still misses its floor (otherwise it is
 * tested strictly again) and still shows.
 */
const EXEMPT_FROM_FLOOR: readonly {
  name: string;
  foreground: string;
  background: string;
  floor: number;
  /** The least ratio at which the pair still reads. */
  shows: number;
  reason: string;
}[] = [
  {
    name: 'disabled split button label',
    foreground: palette.shell.splitDisabledText,
    background: palette.shell.splitDisabledFill,
    floor: TEXT,
    shows: 2.5,
    reason:
      'a disabled control is outside WCAG 1.4.3, and the pair is the reference’s own clean look; MUI’s disabled grey is a darker block with a fainter label',
  },
  {
    name: 'section rule',
    foreground: palette.shell.sectionRule,
    background: palette.background.paper,
    floor: NON_TEXT,
    shows: 1.1,
    reason:
      'a decorative line that nothing is lost without — the section’s head and its content carry the structure — and lighter than the divider',
  },
];

describe('tech-015 Scenario: pairs exempt from the floor', () => {
  it.each(EXEMPT_FROM_FLOOR.map((entry) => [entry.name, entry] as const))(
    '%s gives its reason in words',
    (_name, entry) => {
      expect(entry.reason.length).toBeGreaterThan(30);
    },
  );

  it.each(EXEMPT_FROM_FLOOR.map((entry) => [entry.name, entry] as const))(
    '%s needs the exemption: it misses its floor, so it is not tested strictly by luck',
    (_name, entry) => {
      expect(contrastRatio(entry.foreground, entry.background)).toBeLessThan(entry.floor);
    },
  );

  it.each(EXEMPT_FROM_FLOOR.map((entry) => [entry.name, entry] as const))(
    '%s still shows',
    (_name, entry) => {
      expect(contrastRatio(entry.foreground, entry.background)).toBeGreaterThanOrEqual(entry.shows);
    },
  );

  it('keeps the disabled split button at the reference’s 2.63:1 and the rule lighter than the divider', () => {
    expect(
      contrastRatio(palette.shell.splitDisabledText, palette.shell.splitDisabledFill),
    ).toBeCloseTo(2.63, 1);
    expect(contrastRatio(palette.shell.sectionRule, palette.background.paper)).toBeLessThan(
      contrastRatio(palette.divider, palette.background.paper),
    );
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
