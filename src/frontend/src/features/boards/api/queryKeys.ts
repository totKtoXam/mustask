// TanStack Query keys of the slice, `['boards', <entity>, …]` (frontend.md
// "Data and state"). A write on one surface of a team changes the other too —
// a reorder the board's order, a card move the backlog's «Статус» — so every
// write reads both of that team again, by the team prefixes. The project's own
// views (feat-039) have entities of their own, `project-backlog` and
// `project-board`, so a team's prefix never reaches them and the project's never
// reaches a team's: a write on a project page reads the project's two views, and
// a team's are read when they open (BR-M4-28).
//
// feat-040: a team's views are the team's alone (BR-TS-1) — no project in the key — and carry
// the sorted project filter after the level, as the project's carry the team filter: the key
// of a read is `[boards, entity, organization, owner, level, filter]`, so one filter's rows
// stay on screen while another is read.
import type { BoardLevelCode } from '../lib/levels';
import type { BoardScope } from '../lib/scope';

const ROOT = 'boards';

export const boardsKeys = {
  all: [ROOT] as const,
  /** Every backlog of a team, all levels and filters. */
  backlogs: (organizationId: string, teamId: string) =>
    [ROOT, 'backlog', organizationId, teamId] as const,
  /** The team's backlog under a project filter (feat-040 BR-TS-8): the chosen values, sorted, are part of the key. */
  backlog: (
    organizationId: string,
    teamId: string,
    level: BoardLevelCode,
    projectIds: readonly string[] = [],
  ) => [ROOT, 'backlog', organizationId, teamId, level, [...projectIds].sort()] as const,
  /** Every board of a team, all levels and filters. */
  boards: (organizationId: string, teamId: string) =>
    [ROOT, 'board', organizationId, teamId] as const,
  board: (
    organizationId: string,
    teamId: string,
    level: BoardLevelCode,
    projectIds: readonly string[] = [],
  ) => [ROOT, 'board', organizationId, teamId, level, [...projectIds].sort()] as const,
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

/**
 * The backlog of one level under a scope and its filter: the project filter on a team's views, the
 * team filter on the project's. The filter that does not belong to the scope is never part of its key.
 */
export function backlogKey(
  organizationId: string,
  scope: BoardScope,
  level: BoardLevelCode,
  filterIds: readonly string[] = [],
) {
  return scope.kind === 'team'
    ? boardsKeys.backlog(organizationId, scope.teamId, level, filterIds)
    : boardsKeys.projectBacklog(organizationId, scope.projectId, level, filterIds);
}

/** The board of one level under a scope and its filter. */
export function boardKey(
  organizationId: string,
  scope: BoardScope,
  level: BoardLevelCode,
  filterIds: readonly string[] = [],
) {
  return scope.kind === 'team'
    ? boardsKeys.board(organizationId, scope.teamId, level, filterIds)
    : boardsKeys.projectBoard(organizationId, scope.projectId, level, filterIds);
}
