import type { Theme } from '@mui/material/styles';

/** How the pressed level chips narrow the diagram (BR-SD-14). */
export type LevelMode = 'only' | 'from';

export interface LevelChoice {
  /** Pressed chips; none means every level. */
  selected: ReadonlySet<number>;
  mode: LevelMode;
}

export const NO_LEVELS: LevelChoice = { selected: new Set(), mode: 'only' };

/** Whether a card of the level is drawn under the choice. */
export function levelVisible(choice: LevelChoice, level: number): boolean {
  if (choice.selected.size === 0) return true;
  if (choice.mode === 'only') return choice.selected.has(level);
  return level >= Math.min(...choice.selected);
}

/** A chip that is not pressed but is shown by the mode «Уровень и вложенные» (a light tint). */
export function levelIncluded(choice: LevelChoice, level: number): boolean {
  return choice.selected.size > 0 && !choice.selected.has(level) && levelVisible(choice, level);
}

/** The deepest level that has a colour of its own; deeper ones share the last. */
export const LEVEL_COLOUR_COUNT = 5;

/** The colour of a nesting level (design § 4.4): theme roles only, level 5 and deeper share one. */
export function levelColour(theme: Theme, level: number): string {
  switch (Math.min(Math.max(level, 1), LEVEL_COLOUR_COUNT)) {
    case 1:
      return theme.palette.primary.dark;
    case 2:
      return theme.palette.success.main;
    case 3:
      return theme.palette.shell.levelThree;
    case 4:
      return theme.palette.shell.levelFour;
    default:
      return theme.palette.text.secondary;
  }
}
