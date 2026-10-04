import { describe, expect, it } from 'vitest';
import AssignmentIcon from '@mui/icons-material/Assignment';
import HelpOutlineOutlinedIcon from '@mui/icons-material/HelpOutlineOutlined';
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

describe('US-1 Scenario: The lookups take a token name and a category code only (BR-4)', () => {
  it('answers the task icon and colour for a type keyed BUG whose stored token is task', () => {
    const bugType = { key: 'BUG', name: 'Баг', appearanceToken: 'task' };

    const appearance = typeAppearance(bugType.appearanceToken);

    expect(appearance).toEqual(typeAppearance('task'));
    expect(appearance.icon).toBe(AssignmentIcon);
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
