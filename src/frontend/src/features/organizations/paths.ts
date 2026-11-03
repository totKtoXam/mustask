// The slice's addresses (feat-043, ADR-0032 §§ 1, 2): everything under
// `/organizations/:organizationId/`, one prefix per kind — `areas/:areaId`, `projects/:projectId`,
// `teams/:teamId` — and the organization's own pages beside them. The menu table of
// `app/navigation/addressModel.ts` names the segments; the builders here give the pages the
// links they already use. Ids are untyped route parameters: a page never parses or validates
// them, and the builders re-encode them as received (api-contract § 0.2). The retired addresses
// (`…/settings/teams|areas|projects|members`, a project's `…/settings/general|teams|areas`) are
// no route; `addressModel.RETIRED_ADDRESSES` lists them.

/** The organization list, active tab. Also the selector's «Все организации» (D-d). */
export const ORGANIZATIONS_PATH = '/organizations';

export const ORGANIZATIONS_ROUTE = ORGANIZATIONS_PATH;
export const DEACTIVATED_ORGANIZATIONS_ROUTE = '/organizations/deactivated';
export const ORGANIZATION_GENERAL_ROUTE = '/organizations/:organizationId/settings/general';
/** «Обзор» of the organization (nothing chosen). */
export const ORGANIZATION_OVERVIEW_ROUTE = '/organizations/:organizationId/overview';
/** «Проекты» of the organization; `?status=deactivated` is the administrator's other view (D-5). */
export const PROJECTS_ROUTE = '/organizations/:organizationId/projects';
/** «Участники» (mod-016 design § 1). */
export const MEMBERS_ROUTE = '/organizations/:organizationId/members';
/** «Команды» of the organization (feat-028 design § 1, BR-OL-12): the teams are its own. */
export const ORG_TEAMS_ROUTE = '/organizations/:organizationId/teams';
/** «Структура» of the organization (mod-018 design § 1): the tree is the organization's. */
export const ORG_AREAS_ROUTE = '/organizations/:organizationId/areas';

/** A team's pages: the bare address opens the first entry of its menu. */
export const TEAM_PREFIX_ROUTE = '/organizations/:organizationId/teams/:teamId';
export const ORG_TEAM_ROUTE = `${TEAM_PREFIX_ROUTE}/overview`;
export const TEAM_PROJECTS_ROUTE = `${TEAM_PREFIX_ROUTE}/projects`;
export const TEAM_MEMBERS_ROUTE = `${TEAM_PREFIX_ROUTE}/members`;
export const TEAM_SETTINGS_ROUTE = `${TEAM_PREFIX_ROUTE}/settings`;

/** An area's pages (a department; a project's node or a sub-area on the same prefix). */
export const AREA_PREFIX_ROUTE = '/organizations/:organizationId/areas/:areaId';
export const ORG_AREA_ROUTE = `${AREA_PREFIX_ROUTE}/overview`;
export const AREA_PROJECTS_ROUTE = `${AREA_PREFIX_ROUTE}/projects`;
export const AREA_TEAMS_ROUTE = `${AREA_PREFIX_ROUTE}/teams`;
export const AREA_SETTINGS_ROUTE = `${AREA_PREFIX_ROUTE}/settings`;

/** A project's pages. «Рабочие элементы», «Канбан» and «Бэклог» are the slices' own. */
export const PROJECT_PREFIX_ROUTE = '/organizations/:organizationId/projects/:projectId';
export const PROJECT_GENERAL_ROUTE = `${PROJECT_PREFIX_ROUTE}/general`;
/** The project's «Команды»: its linked teams (feat-028 design § 7). */
export const TEAMS_ROUTE = `${PROJECT_PREFIX_ROUTE}/teams`;
export const PROJECT_SETTINGS_ROUTE = `${PROJECT_PREFIX_ROUTE}/settings`;

/** Every route of the slice, in design § 2's order. */
export const ORGANIZATIONS_ROUTES = [
  ORGANIZATIONS_ROUTE,
  DEACTIVATED_ORGANIZATIONS_ROUTE,
  ORGANIZATION_GENERAL_ROUTE,
  ORGANIZATION_OVERVIEW_ROUTE,
  ORG_AREAS_ROUTE,
  PROJECTS_ROUTE,
  ORG_TEAMS_ROUTE,
  MEMBERS_ROUTE,
  ORG_AREA_ROUTE,
  AREA_PROJECTS_ROUTE,
  AREA_TEAMS_ROUTE,
  AREA_SETTINGS_ROUTE,
  ORG_TEAM_ROUTE,
  TEAM_PROJECTS_ROUTE,
  TEAM_MEMBERS_ROUTE,
  TEAM_SETTINGS_ROUTE,
  PROJECT_GENERAL_ROUTE,
  TEAMS_ROUTE,
  PROJECT_SETTINGS_ROUTE,
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

export function organizationOverviewPath(organizationId: string): string {
  return `${organizationPrefix(organizationId)}/overview`;
}

export function projectsPath(organizationId: string): string {
  return `${organizationPrefix(organizationId)}/projects`;
}

export function deactivatedProjectsPath(organizationId: string): string {
  return `${projectsPath(organizationId)}?status=deactivated`;
}

export function membersPath(organizationId: string): string {
  return `${organizationPrefix(organizationId)}/members`;
}

export function organizationTeamsPath(organizationId: string): string {
  return `${organizationPrefix(organizationId)}/teams`;
}

export function organizationTeamPath(organizationId: string, teamId: string): string {
  return `${organizationTeamsPath(organizationId)}/${segment(teamId)}/overview`;
}

export function projectGeneralPath(organizationId: string, projectId: string): string {
  return `${projectPrefix(organizationId, projectId)}/general`;
}

/** The project's «Команды». */
export function teamsPath(organizationId: string, projectId: string): string {
  return `${projectPrefix(organizationId, projectId)}/teams`;
}

export function organizationAreasPath(organizationId: string): string {
  return `${organizationPrefix(organizationId)}/areas`;
}

export function organizationAreaPath(organizationId: string, areaId: string): string {
  return `${organizationAreasPath(organizationId)}/${segment(areaId)}/overview`;
}

export function areaProjectsPath(organizationId: string, areaId: string): string {
  return `${organizationAreasPath(organizationId)}/${segment(areaId)}/projects`;
}

export function areaTeamsPath(organizationId: string, areaId: string): string {
  return `${organizationAreasPath(organizationId)}/${segment(areaId)}/teams`;
}
