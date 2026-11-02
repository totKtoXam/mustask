// The slice's addresses (design § 1; mod-016 adds «Участники»; feat-028 puts the teams in the
// organization's settings), under mod-015 D-17's context prefixes. Ids are untyped route
// parameters: a page never parses or validates them, and the builders re-encode them as
// received (api-contract § 0.2), as `processDefinitionPath` does.

/** The organization list, active tab. Also the switcher's «Все организации» (D-d). */
export const ORGANIZATIONS_PATH = '/organizations';

export const ORGANIZATIONS_ROUTE = ORGANIZATIONS_PATH;
export const DEACTIVATED_ORGANIZATIONS_ROUTE = '/organizations/deactivated';
export const ORGANIZATION_GENERAL_ROUTE = '/organizations/:organizationId/settings/general';
export const PROJECTS_ROUTE = '/organizations/:organizationId/settings/projects';
export const DEACTIVATED_PROJECTS_ROUTE =
  '/organizations/:organizationId/settings/projects/deactivated';
/** «Участники» (mod-016 design § 1): the address mod-015 § 4.1 reserved; `members` clashes with no id. */
export const MEMBERS_ROUTE = '/organizations/:organizationId/settings/members';
/** «Команды» of the organization (feat-028 design § 1, BR-OL-12): the teams are its own. */
export const ORG_TEAMS_ROUTE = '/organizations/:organizationId/settings/teams';
/** A team's page, beneath «Команды» of the organization. */
export const ORG_TEAM_ROUTE = '/organizations/:organizationId/settings/teams/:teamId';
/** «Области» of the organization (mod-018 design § 1): the tree is the organization's. */
export const ORG_AREAS_ROUTE = '/organizations/:organizationId/settings/areas';
/** An area's page, beneath «Области». */
export const ORG_AREA_ROUTE = '/organizations/:organizationId/settings/areas/:areaId';
export const PROJECT_GENERAL_ROUTE =
  '/organizations/:organizationId/projects/:projectId/settings/general';
/**
 * The project's «Команды»: its linked teams (feat-028 design § 7). There is no team page beneath it any
 * more — `…/projects/:p/settings/teams/:t` is no route and the catch-all answers «Страница не найдена».
 */
export const TEAMS_ROUTE = '/organizations/:organizationId/projects/:projectId/settings/teams';
/** The project's «Области» (mod-018 design § 7): its node and the sub-areas below it. */
export const AREAS_ROUTE = '/organizations/:organizationId/projects/:projectId/settings/areas';

/** Every route of the slice, in design § 1's order. */
export const ORGANIZATIONS_ROUTES = [
  ORGANIZATIONS_ROUTE,
  DEACTIVATED_ORGANIZATIONS_ROUTE,
  ORGANIZATION_GENERAL_ROUTE,
  PROJECTS_ROUTE,
  DEACTIVATED_PROJECTS_ROUTE,
  MEMBERS_ROUTE,
  ORG_TEAMS_ROUTE,
  ORG_TEAM_ROUTE,
  ORG_AREAS_ROUTE,
  ORG_AREA_ROUTE,
  PROJECT_GENERAL_ROUTE,
  TEAMS_ROUTE,
] as const;

const segment = encodeURIComponent;

function organizationPrefix(organizationId: string): string {
  return `/organizations/${segment(organizationId)}`;
}

function projectPrefix(organizationId: string, projectId: string): string {
  return `${organizationPrefix(organizationId)}/projects/${segment(projectId)}`;
}

export function organizationGeneralPath(organizationId: string): string {
  return `${organizationPrefix(organizationId)}/settings/general`;
}

export function projectsPath(organizationId: string): string {
  return `${organizationPrefix(organizationId)}/settings/projects`;
}

export function deactivatedProjectsPath(organizationId: string): string {
  return `${projectsPath(organizationId)}/deactivated`;
}

export function membersPath(organizationId: string): string {
  return `${organizationPrefix(organizationId)}/settings/members`;
}

export function organizationTeamsPath(organizationId: string): string {
  return `${organizationPrefix(organizationId)}/settings/teams`;
}

export function organizationTeamPath(organizationId: string, teamId: string): string {
  return `${organizationTeamsPath(organizationId)}/${segment(teamId)}`;
}

export function projectGeneralPath(organizationId: string, projectId: string): string {
  return `${projectPrefix(organizationId, projectId)}/settings/general`;
}

export function teamsPath(organizationId: string, projectId: string): string {
  return `${projectPrefix(organizationId, projectId)}/settings/teams`;
}

export function areasPath(organizationId: string, projectId: string): string {
  return `${projectPrefix(organizationId, projectId)}/settings/areas`;
}

export function organizationAreasPath(organizationId: string): string {
  return `${organizationPrefix(organizationId)}/settings/areas`;
}

export function organizationAreaPath(organizationId: string, areaId: string): string {
  return `${organizationAreasPath(organizationId)}/${segment(areaId)}`;
}
