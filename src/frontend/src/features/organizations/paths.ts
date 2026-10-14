// The slice's ten addresses (design § 1; mod-016 adds «Участники»), under mod-015 D-17's context
// prefixes. Ids are untyped route parameters: a page never parses or
// validates them, and the builders re-encode them as received (api-contract
// § 0.2), as `processDefinitionPath` does.

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
export const PROJECT_GENERAL_ROUTE =
  '/organizations/:organizationId/projects/:projectId/settings/general';
export const TEAMS_ROUTE = '/organizations/:organizationId/projects/:projectId/settings/teams';
export const TEAM_ROUTE =
  '/organizations/:organizationId/projects/:projectId/settings/teams/:teamId';
export const AREAS_ROUTE = '/organizations/:organizationId/projects/:projectId/settings/areas';

/** Every route of the slice, in design § 1's order. */
export const ORGANIZATIONS_ROUTES = [
  ORGANIZATIONS_ROUTE,
  DEACTIVATED_ORGANIZATIONS_ROUTE,
  ORGANIZATION_GENERAL_ROUTE,
  PROJECTS_ROUTE,
  DEACTIVATED_PROJECTS_ROUTE,
  MEMBERS_ROUTE,
  PROJECT_GENERAL_ROUTE,
  TEAMS_ROUTE,
  TEAM_ROUTE,
  AREAS_ROUTE,
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

export function projectGeneralPath(organizationId: string, projectId: string): string {
  return `${projectPrefix(organizationId, projectId)}/settings/general`;
}

export function teamsPath(organizationId: string, projectId: string): string {
  return `${projectPrefix(organizationId, projectId)}/settings/teams`;
}

export function teamPath(organizationId: string, projectId: string, teamId: string): string {
  return `${teamsPath(organizationId, projectId)}/${segment(teamId)}`;
}

export function areasPath(organizationId: string, projectId: string): string {
  return `${projectPrefix(organizationId, projectId)}/settings/areas`;
}
