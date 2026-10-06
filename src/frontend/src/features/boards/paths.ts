// The slice's two addresses (spec § Screens and copy), under mod-015 BR-18's
// team prefix. Ids are untyped route parameters: a page never parses or
// validates them, and the builders re-encode them as received, as mod-002's
// and mod-003's do. `:level` is checked by the page (`isBoardLevel`).
import { BOARD_LEVELS, LANDING_LEVEL, type BoardLevelCode } from './lib/levels';

const TEAM_ROUTE_PREFIX = '/organizations/:organizationId/projects/:projectId/teams/:teamId';

export const BACKLOG_ROUTE = `${TEAM_ROUTE_PREFIX}/backlogs/:level`;
export const BOARD_ROUTE = `${TEAM_ROUTE_PREFIX}/boards/:level`;

const segment = encodeURIComponent;

function teamPrefix(organizationId: string, projectId: string, teamId: string): string {
  return `/organizations/${segment(organizationId)}/projects/${segment(projectId)}/teams/${segment(teamId)}`;
}

/** A team's backlog of one level. */
export function backlogPath(
  organizationId: string,
  projectId: string,
  teamId: string,
  level: BoardLevelCode,
): string {
  return `${teamPrefix(organizationId, projectId, teamId)}/backlogs/${level}`;
}

/** A team's board of one level. */
export function boardPath(
  organizationId: string,
  projectId: string,
  teamId: string,
  level: BoardLevelCode,
): string {
  return `${teamPrefix(organizationId, projectId, teamId)}/boards/${level}`;
}

/** «Бэклоги»'s landing: the team's «Истории» backlog (spec § Navigation). */
export function backlogsLandingPath(
  organizationId: string,
  projectId: string,
  teamId: string,
): string {
  return backlogPath(organizationId, projectId, teamId, LANDING_LEVEL);
}

/** «Доски»'s landing: the team's «Истории» board (spec § Navigation). */
export function boardsLandingPath(
  organizationId: string,
  projectId: string,
  teamId: string,
): string {
  return boardPath(organizationId, projectId, teamId, LANDING_LEVEL);
}

/** The three level tabs of a team's «Бэклоги», in tab order: the entry's page, each of them. */
export function backlogLevelPaths(organizationId: string, projectId: string, teamId: string) {
  return BOARD_LEVELS.map((level) => backlogPath(organizationId, projectId, teamId, level));
}

/** The three level tabs of a team's «Доски», in tab order. */
export function boardLevelPaths(organizationId: string, projectId: string, teamId: string) {
  return BOARD_LEVELS.map((level) => boardPath(organizationId, projectId, teamId, level));
}
