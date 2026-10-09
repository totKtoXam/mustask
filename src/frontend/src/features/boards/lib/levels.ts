// The three levels a team's backlogs and boards have in this cut (spec
// § Model; api-contract § 0.2; OQ-3: no «Задачи»): the address segment, which
// is also what the API is asked under, and the label the level drop-down
// reads (feat-001 § Level drop-down). The drop-down is drawn before the read
// arrives, so its labels are the panel's, as the spec fixes them; the order
// is the menu's, by rank.

export const BOARD_LEVELS = ['epics', 'features', 'stories'] as const;

export type BoardLevelCode = (typeof BOARD_LEVELS)[number];

/** The level both entries land on (spec § Navigation). */
export const LANDING_LEVEL: BoardLevelCode = 'stories';

export const LEVEL_LABELS: Readonly<Record<BoardLevelCode, string>> = {
  epics: 'Эпики',
  features: 'Фичи',
  stories: 'Истории',
};

/**
 * The level directly above, or `null` for «Эпики»: a row of «Фичи» or
 * «Истории» is given a parent of that level from the backlog (feat-001
 * BR-H-14); its label names it in the dialog's helper.
 */
export function levelAbove(level: BoardLevelCode): BoardLevelCode | null {
  const rank = BOARD_LEVELS.indexOf(level);
  return rank > 0 ? (BOARD_LEVELS[rank - 1] ?? null) : null;
}

/**
 * Whether an address segment names one of the three levels, exact spelling
 * and lower case (api-contract § 0.2); any other is an unknown address
 * (mod-015 BR-13) and never reaches the API.
 */
export function isBoardLevel(segment: string | undefined): segment is BoardLevelCode {
  return (BOARD_LEVELS as readonly string[]).includes(segment ?? '');
}
