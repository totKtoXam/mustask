import { describe, expect, it } from 'vitest';
import { render } from '@testing-library/react';
import AssignmentTurnedInIcon from '@mui/icons-material/AssignmentTurnedIn';
import BugReportIcon from '@mui/icons-material/BugReport';
import EmojiEventsIcon from '@mui/icons-material/EmojiEvents';
import HelpOutlineOutlinedIcon from '@mui/icons-material/HelpOutlineOutlined';
import { CrownIcon } from './CrownIcon';
import { StoryIcon } from './StoryIcon';
import { theme } from './theme';
import { APPEARANCE_TOKENS, CATEGORY_CODES } from './tokens';

const { typeAppearance, categoryColour } = theme.workTracking;

describe('US-1 Scenario: Appearance tokens (BR-4, D-6, D-16)', () => {
  it('has exactly the five tokens epic, feature, story, task and bug', () => {
    expect([...APPEARANCE_TOKENS]).toEqual(['epic', 'feature', 'story', 'task', 'bug']);
  });

  it('gives each token one icon and one colour, no two sharing either', () => {
    const appearances = APPEARANCE_TOKENS.map((token) => typeAppearance(token));

    expect(new Set(appearances.map((appearance) => appearance.colour)).size).toBe(
      APPEARANCE_TOKENS.length,
    );
    expect(new Set(appearances.map((appearance) => appearance.icon)).size).toBe(
      APPEARANCE_TOKENS.length,
    );
    // None of them is the unknown-token fallback.
    appearances.forEach((appearance) => expect(appearance.icon).not.toBe(HelpOutlineOutlinedIcon));
  });

  it('gives each category code Proposed, InProgress, Resolved, Completed and Removed its own colour', () => {
    expect([...CATEGORY_CODES]).toEqual([
      'Proposed',
      'InProgress',
      'Resolved',
      'Completed',
      'Removed',
    ]);
    const colours = CATEGORY_CODES.map((code) => categoryColour(code));

    expect(new Set(colours).size).toBe(CATEGORY_CODES.length);
    colours.forEach((colour) => expect(colour).not.toBe(theme.palette.text.secondary));
  });

  it('never uses the accent for a type or a category', () => {
    const colours = [
      ...APPEARANCE_TOKENS.map((token) => typeAppearance(token).colour),
      ...CATEGORY_CODES.map((code) => categoryColour(code)),
    ];

    colours.forEach((colour) =>
      expect(colour.toUpperCase()).not.toBe(theme.palette.primary.main.toUpperCase()),
    );
  });
});

describe('feat-027 design § 2.4: the type icons of the reference screen', () => {
  it('draws an epic as a crown, a feature as a trophy, a story as an open book, a bug as a bug and a task as a clipboard with a check', () => {
    expect(typeAppearance('epic').icon).toBe(CrownIcon);
    expect(typeAppearance('feature').icon).toBe(EmojiEventsIcon);
    expect(typeAppearance('story').icon).toBe(StoryIcon);
    expect(typeAppearance('bug').icon).toBe(BugReportIcon);
    expect(typeAppearance('task').icon).toBe(AssignmentTurnedInIcon);
  });

  it('builds the story’s book with createSvgIcon as two solid pages, not Material’s page-and-text-lines MenuBook', () => {
    const { container } = render(<StoryIcon />);

    expect(container.querySelector('[data-testid="StoryIcon"]')).not.toBeNull();
    expect(container.querySelector('[data-testid="MenuBookIcon"]')).toBeNull();
    const paths = container.querySelectorAll('svg path');
    expect(paths).toHaveLength(1);
    const pages = (paths[0]?.getAttribute('d') ?? '').split(/(?=M)/);
    // Two closed subpaths — a page each, mirrored across the spine at x = 12.
    expect(pages).toHaveLength(2);
    for (const page of pages) {
      expect(page.trim().endsWith('z')).toBe(true);
    }
    expect(pages[0]).toMatch(/^M2 /);
    expect(pages[1]).toMatch(/^M22 /);
  });

  it('builds the crown with MUI’s own createSvgIcon as one closed path', () => {
    const { container } = render(<CrownIcon />);

    const paths = container.querySelectorAll('svg path');
    expect(container.querySelector('[data-testid="CrownIcon"]')).not.toBeNull();
    expect(paths).toHaveLength(1);
    expect(paths[0]?.getAttribute('d')).toBe('M1.5 5L7 12l5-9 5 9 5.5-7V21H1.5z');
  });
});

describe('feat-027 D-10: the reference’s colours, panel-wide through the one lookup', () => {
  it.each([
    ['epic', '#E06C00'],
    ['feature', '#773B93'],
    ['story', '#0098C7'],
    ['bug', '#CC293D'],
    ['task', '#A4880A'],
  ])('draws the %s icon in %s', (token, colour) => {
    expect(typeAppearance(token).colour).toBe(colour);
  });

  it.each([
    ['Proposed', '#B2B2B2'],
    ['Completed', '#339933'],
    ['InProgress', '#005A9E'],
    ['Resolved', '#00796B'],
    ['Removed', '#A4262C'],
  ])(
    'draws the %s category in %s — the «Новые» and «Завершённые» dots are the reference’s, the others stay',
    (code, colour) => {
      expect(categoryColour(code)).toBe(colour);
    },
  );
});

describe('US-1 Scenario: The lookups take a token name and a category code only (BR-4)', () => {
  it('answers the task icon and colour for a type keyed BUG whose stored token is task', () => {
    const bugType = { key: 'BUG', name: 'Баг', appearanceToken: 'task' };

    const appearance = typeAppearance(bugType.appearanceToken);

    expect(appearance).toEqual(typeAppearance('task'));
    expect(appearance.icon).toBe(AssignmentTurnedInIcon);
  });

  it('answers the InProgress colour for a state named «Готово» in the InProgress category', () => {
    const state = { name: 'Готово', categoryCode: 'InProgress' };

    expect(categoryColour(state.categoryCode)).toBe(categoryColour('InProgress'));
    expect(categoryColour(state.categoryCode)).not.toBe(categoryColour('Completed'));
  });

  it('does not resolve a type name, a type key or a category label', () => {
    expect(typeAppearance('Задача').icon).toBe(HelpOutlineOutlinedIcon);
    expect(typeAppearance('BUG').icon).toBe(HelpOutlineOutlinedIcon);
    expect(categoryColour('В работе')).toBe(theme.palette.text.secondary);
  });
});

describe('US-1 Scenario: Unknown appearance token', () => {
  it('answers a neutral fallback icon and the neutral text colour, and nothing fails', () => {
    expect(() => typeAppearance('rocket')).not.toThrow();

    const appearance = typeAppearance('rocket');

    expect(appearance.icon).toBe(HelpOutlineOutlinedIcon);
    expect(appearance.colour).toBe(theme.palette.text.primary);
  });

  it('answers the secondary text colour for an unknown category code', () => {
    expect(categoryColour('Archived')).toBe(theme.palette.text.secondary);
  });
});
