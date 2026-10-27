// The slice's addresses (spec § Screens and copy). Since feat-040 (BR-TS-1) a team's views
// are at the team's address, `/organizations/:o/teams/:t/…`, with no project in it; the
// project-nested `…/projects/:p/teams/:t/…` is no route and answers «Страница не найдена».
// feat-039 adds the project's own two (BR-PB-1), under the project prefix. Ids are untyped
// route parameters: a page never parses or validates them, and the builders re-encode them
// as received, as mod-002's and mod-003's do. `:level` is checked by the page (`isBoardLevel`).
import { matchPath } from 'react-router-dom';
import { BOARD_LEVELS, LANDING_LEVEL, isBoardLevel, type BoardLevelCode } from './lib/levels';

const TEAM_ROUTE_PREFIX = '/organizations/:organizationId/teams/:teamId';
const PROJECT_ROUTE_PREFIX = '/organizations/:organizationId/projects/:projectId';

export const BACKLOG_ROUTE = `${TEAM_ROUTE_PREFIX}/backlogs/:level`;
export const BOARD_ROUTE = `${TEAM_ROUTE_PREFIX}/boards/:level`;
/** feat-039 BR-PB-1: the project's backlog of one level. */
export const PROJECT_BACKLOG_ROUTE = `${PROJECT_ROUTE_PREFIX}/backlogs/:level`;
/** feat-039 BR-PB-1: the project's board of one level. */
export const PROJECT_BOARD_ROUTE = `${PROJECT_ROUTE_PREFIX}/boards/:level`;

const segment = encodeURIComponent;

function teamPrefix(organizationId: string, teamId: string): string {
  return `/organizations/${segment(organizationId)}/teams/${segment(teamId)}`;
}

function projectPrefix(organizationId: string, projectId: string): string {
  return `/organizations/${segment(organizationId)}/projects/${segment(projectId)}`;
}

/** A team's backlog of one level (feat-040 BR-TS-1). */
export function backlogPath(organizationId: string, teamId: string, level: BoardLevelCode): string {
  return `${teamPrefix(organizationId, teamId)}/backlogs/${level}`;
}

/** A team's board of one level (feat-040 BR-TS-1). */
export function boardPath(organizationId: string, teamId: string, level: BoardLevelCode): string {
  return `${teamPrefix(organizationId, teamId)}/boards/${level}`;
}

/** The project's backlog of one level (feat-039 BR-PB-1). */
export function projectBacklogPath(
  organizationId: string,
  projectId: string,
  level: BoardLevelCode,
): string {
  return `${projectPrefix(organizationId, projectId)}/backlogs/${level}`;
}

/** The project's board of one level (feat-039 BR-PB-1). */
export function projectBoardPath(
  organizationId: string,
  projectId: string,
  level: BoardLevelCode,
): string {
  return `${projectPrefix(organizationId, projectId)}/boards/${level}`;
}

/**
 * The level of an address that is a project's backlog (`…/projects/:p/backlogs/:level`), or
 * `null` for any other address — and for a level the page does not serve, which answers
 * «Страница не найдена» and is no page of an entry (BR-PB-1; mod-015 BR-13).
 */
export function projectBacklogLevelOf(pathname: string): BoardLevelCode | null {
  return projectLevelOf(PROJECT_BACKLOG_ROUTE, pathname);
}

/** The level of an address that is a project's board, or `null`. */
export function projectBoardLevelOf(pathname: string): BoardLevelCode | null {
  return projectLevelOf(PROJECT_BOARD_ROUTE, pathname);
}

function projectLevelOf(route: string, pathname: string): BoardLevelCode | null {
  const level = matchPath({ path: route, end: true }, pathname)?.params.level;
  return isBoardLevel(level) ? level : null;
}

/** «Бэклоги»'s landing: the team's «Истории» backlog (spec § Navigation). */
export function backlogsLandingPath(organizationId: string, teamId: string): string {
  return backlogPath(organizationId, teamId, LANDING_LEVEL);
}

/** «Канбан»'s landing: the team's «Истории» board (spec § Navigation; feat-001 BR-H-16). */
export function boardsLandingPath(organizationId: string, teamId: string): string {
  return boardPath(organizationId, teamId, LANDING_LEVEL);
}

/** The three levels of a team's «Бэклоги», in rank order: the entry's page, each of them. */
export function backlogLevelPaths(organizationId: string, teamId: string) {
  return BOARD_LEVELS.map((level) => backlogPath(organizationId, teamId, level));
}

/** The three levels of a team's «Канбан», in rank order. */
export function boardLevelPaths(organizationId: string, teamId: string) {
  return BOARD_LEVELS.map((level) => boardPath(organizationId, teamId, level));
}
