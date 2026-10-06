// TanStack Query keys of the slice, `['boards', <entity>, …]` (frontend.md
// "Data and state"). A write on one surface of a team changes the other too —
// a reorder the board's order, a card move the backlog's «Статус» — so every
// write reads both of that team again, by the team prefixes.
import type { BoardLevelCode } from '../lib/levels';

const ROOT = 'boards';

export const boardsKeys = {
  all: [ROOT] as const,
  /** Every backlog of a team, all levels. */
  backlogs: (organizationId: string, projectId: string, teamId: string) =>
    [ROOT, 'backlog', organizationId, projectId, teamId] as const,
  backlog: (organizationId: string, projectId: string, teamId: string, level: BoardLevelCode) =>
    [ROOT, 'backlog', organizationId, projectId, teamId, level] as const,
  /** Every board of a team, all levels. */
  boards: (organizationId: string, projectId: string, teamId: string) =>
    [ROOT, 'board', organizationId, projectId, teamId] as const,
  board: (organizationId: string, projectId: string, teamId: string, level: BoardLevelCode) =>
    [ROOT, 'board', organizationId, projectId, teamId, level] as const,
};
