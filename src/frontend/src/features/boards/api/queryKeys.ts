// TanStack Query keys of the slice, `['boards', <entity>, …]` (frontend.md
// "Data and state"). A write on one surface of a team changes the other too —
// a reorder the board's order, a card move the backlog's «Статус» — so every
// write reads both of that team again, by the team prefixes. The project's own
// views (feat-039) have entities of their own, `project-backlog` and
// `project-board`, so a team's prefix never reaches them and the project's never
// reaches a team's: a write on a project page reads the project's two views, and
// a team's are read when they open (BR-M4-28).
import type { BoardLevelCode } from '../lib/levels';
import { scopeTeamId, type BoardScope } from '../lib/scope';

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
  /** Every backlog of a project, all levels. */
  projectBacklogs: (organizationId: string, projectId: string) =>
    [ROOT, 'project-backlog', organizationId, projectId] as const,
  /** The project's backlog under a team filter (mod-017 § 3): the chosen values, sorted, are part of the key. */
  projectBacklog: (
    organizationId: string,
    projectId: string,
    level: BoardLevelCode,
    teamIds: readonly string[] = [],
  ) => [ROOT, 'project-backlog', organizationId, projectId, level, [...teamIds].sort()] as const,
  /** Every board of a project, all levels. */
  projectBoards: (organizationId: string, projectId: string) =>
    [ROOT, 'project-board', organizationId, projectId] as const,
  projectBoard: (
    organizationId: string,
    projectId: string,
    level: BoardLevelCode,
    teamIds: readonly string[] = [],
  ) => [ROOT, 'project-board', organizationId, projectId, level, [...teamIds].sort()] as const,
};

/** The backlog of one level under a scope. */
export function backlogKey(
  organizationId: string,
  projectId: string,
  scope: BoardScope,
  level: BoardLevelCode,
  teamIds: readonly string[] = [],
) {
  const teamId = scopeTeamId(scope);
  return teamId === null
    ? boardsKeys.projectBacklog(organizationId, projectId, level, teamIds)
    : boardsKeys.backlog(organizationId, projectId, teamId, level);
}

/** The board of one level under a scope. */
export function boardKey(
  organizationId: string,
  projectId: string,
  scope: BoardScope,
  level: BoardLevelCode,
  teamIds: readonly string[] = [],
) {
  const teamId = scopeTeamId(scope);
  return teamId === null
    ? boardsKeys.projectBoard(organizationId, projectId, level, teamIds)
    : boardsKeys.board(organizationId, projectId, teamId, level);
}
