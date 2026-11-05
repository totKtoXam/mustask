// QA pass for feat-049 BR-SD-12 / BR-SD-17: the contrast the spec asks of the level colours, the
// card text on each tint and the line strokes, computed from the theme the diagram draws with.
import { describe, expect, it } from 'vitest';
import { alpha } from '@mui/material/styles';
import { contrastRatio } from '../../../app/theme/contrast';
import { theme } from '../../../app/theme/theme';
import { levelColour } from './diagramLevels';

const { palette } = theme;
const paper = palette.background.paper;
const LEVELS = [1, 2, 3, 4, 5, 6] as const;

/** An `alpha(colour, a)` tint laid on the frame's paper, as the `#RRGGBB` it looks like. */
function onPaper(translucent: string): string {
  const found = translucent.match(/[\d.]+/g)!.map(Number);
  const [r, g, b, a] = found as [number, number, number, number];
  const mixed = [r, g, b].map((c, i) => {
    const base = parseInt(paper.slice(1 + i * 2, 3 + i * 2), 16);
    return Math.round(a * c + (1 - a) * base);
  });
  return `#${mixed.map((c) => c.toString(16).padStart(2, '0')).join('')}`;
}

// The tints cardSx lays: rest 0.08, hover and related 0.14, chosen and «Подсветка» related 0.18.
const TINTS = [0.08, 0.14, 0.18] as const;

describe('feat-049 BR-SD-12: level colours', () => {
  it.each(LEVELS)('level %i outline and edge reach 3:1 on the frame', (level) => {
    expect(contrastRatio(levelColour(theme, level), paper)).toBeGreaterThanOrEqual(3);
  });

  it.each(LEVELS)('level %i badge text (white) reaches 4.5:1 on its fill', (level) => {
    expect(
      contrastRatio(palette.primary.contrastText, levelColour(theme, level)),
    ).toBeGreaterThanOrEqual(4.5);
  });

  it.each(LEVELS)('level %i unpressed chip text reaches 4.5:1 on paper', (level) => {
    expect(contrastRatio(levelColour(theme, level), paper)).toBeGreaterThanOrEqual(4.5);
  });

  // An included chip (mode «Уровень и вложенные») wears plain text on the 18 % tint: the level
  // colour itself missed 4.5:1 there for levels 2 and 3 (qa.md F-1).
  it.each(LEVELS)('level %i included chip text reaches 4.5:1 on its tint', (level) => {
    const fill = onPaper(alpha(levelColour(theme, level), 0.18));
    expect(contrastRatio(palette.text.primary, fill)).toBeGreaterThanOrEqual(4.5);
  });

  it.each(LEVELS.flatMap((level) => TINTS.map((tint) => [level, tint] as const)))(
    'level %i card name text reaches 4.5:1 on a %f tint',
    (level, tint) => {
      const fill = onPaper(alpha(levelColour(theme, level), tint));
      expect(contrastRatio(palette.text.primary, fill)).toBeGreaterThanOrEqual(4.5);
    },
  );

  it.each(LEVELS.flatMap((level) => TINTS.map((tint) => [level, tint] as const)))(
    'level %i quiet card text (type, key, counts) reaches 4.5:1 on a %f tint',
    (level, tint) => {
      const fill = onPaper(alpha(levelColour(theme, level), tint));
      expect(contrastRatio(palette.text.secondary, fill)).toBeGreaterThanOrEqual(4.5);
    },
  );

  it('a deactivated card keeps readable text on its neutral fill', () => {
    expect(contrastRatio(palette.text.primary, palette.shell.buttonNeutral)).toBeGreaterThanOrEqual(
      4.5,
    );
    expect(
      contrastRatio(palette.text.secondary, palette.shell.buttonNeutral),
    ).toBeGreaterThanOrEqual(4.5);
  });
});

describe('feat-049 BR-SD-17: line strokes', () => {
  it.each([
    ['nesting', palette.text.secondary],
    ['team on a project / through a hidden card', palette.primary.dark],
    ['copy join', palette.text.primary],
  ])('%s stroke reaches 3:1 on the frame', (_name, colour) => {
    expect(contrastRatio(colour, paper)).toBeGreaterThanOrEqual(3);
  });
});
