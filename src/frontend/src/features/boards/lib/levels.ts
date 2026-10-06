// The three levels a team's backlogs and boards have in this cut (spec
// § Model; api-contract § 0.2; OQ-3: no «Задачи»): the address segment, which
// is also what the API is asked under, and the tab's label. The tabs are drawn
// before the read arrives, so their labels are the panel's, as the spec fixes
// them; the order is the tabs'.

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
 * Whether an address segment names one of the three levels, exact spelling
 * and lower case (api-contract § 0.2); any other is an unknown address
 * (mod-015 BR-13) and never reaches the API.
 */
export function isBoardLevel(segment: string | undefined): segment is BoardLevelCode {
  return (BOARD_LEVELS as readonly string[]).includes(segment ?? '');
}
