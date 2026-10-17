// The organizations module over HTTP, strictly per
// docs/features/mod-002-organizations-projects-teams/api-contract.md § 1–§ 5:
// one function per route, camelCase as all of Mustask.Api. Ids come from the
// address or from an answer and are sent as received, re-encoded in the path
// and never validated (§ 0.2). Every list is a plain array in the server's
// display order, rendered as received (§ 0.6).
import { apiFetch } from '../../../shared/api/httpClient';

/** § 1.1: an organization in a list. */
export interface OrganizationListItem {
  id: string;
  name: string;
}

/** § 1.3: one organization. */
export interface Organization {
  id: string;
  name: string;
}

/** § 2.1: a project in a list. */
export interface ProjectListItem {
  id: string;
  name: string;
  key: string;
}

/** § 2.3: one project. */
export interface Project {
  id: string;
  organizationId: string;
  name: string;
  key: string;
}

/**
 * mod-017 api-contract § 1.3: how a team stands for the project's default
 * (BR-OT-4). `named`: the settings name it; `onlyTeam`: nothing is named and it
 * is the project's only active team; `null`: neither.
 */
export type DefaultMark = 'named' | 'onlyTeam';

/** mod-017 api-contract § 1.3: the one shape every team answer uses. */
export interface Team {
  id: string;
  projectId: string;
  name: string;
  defaultMark: DefaultMark | null;
}

/**
 * mod-016 api-contract § 3.3: a team of the project's list also says whether
 * the caller is in it. The single-team read and the writes carry no such flag.
 */
export interface TeamInList extends Team {
  isMember: boolean;
}

/** mod-017 api-contract § 1.6: one node of the area tree, which comes in pre-order. */
export interface AreaNode {
  id: string;
  /** `null` for the root only. */
  parentId: string | null;
  name: string;
  path: string;
  /** 0 for the root. */
  depth: number;
}

/** mod-017 api-contract § 1.6: what every area write returns. */
export interface Area {
  id: string;
  projectId: string;
  parentId: string;
  name: string;
}

/** A list's status tab (§ 1.1/§ 1.2, § 2.1/§ 2.2). */
export type ListView = 'active' | 'deactivated';

const segment = encodeURIComponent;

/** `/api/organizations…`, the module's one prefix (§ 5). */
export const ORGANIZATIONS_API = '/api/organizations';

export function organizationApiPath(organizationId: string): string {
  return `${ORGANIZATIONS_API}/${segment(organizationId)}`;
}

export function projectApiPath(organizationId: string, projectId: string): string {
  return `${organizationApiPath(organizationId)}/projects/${segment(projectId)}`;
}

function teamApiPath(organizationId: string, projectId: string, teamId: string): string {
  return `${projectApiPath(organizationId, projectId)}/teams/${segment(teamId)}`;
}

function areaApiPath(organizationId: string, projectId: string, areaId: string): string {
  return `${projectApiPath(organizationId, projectId)}/areas/${segment(areaId)}`;
}

function listSuffix(view: ListView): string {
  return view === 'deactivated' ? '/deactivated' : '';
}

// § 1 Organizations

export function getOrganizations(view: ListView = 'active'): Promise<OrganizationListItem[]> {
  return apiFetch(`${ORGANIZATIONS_API}${listSuffix(view)}`);
}

export function createOrganization(name: string): Promise<Organization> {
  return apiFetch(ORGANIZATIONS_API, { method: 'POST', body: { name } });
}

export function getOrganization(organizationId: string): Promise<Organization> {
  return apiFetch(organizationApiPath(organizationId));
}

export function renameOrganization(organizationId: string, name: string): Promise<Organization> {
  return apiFetch(`${organizationApiPath(organizationId)}/name`, { method: 'PUT', body: { name } });
}

export function deactivateOrganization(organizationId: string): Promise<void> {
  return apiFetch(`${organizationApiPath(organizationId)}/deactivate`, { method: 'POST' });
}

export function restoreOrganization(organizationId: string): Promise<Organization> {
  return apiFetch(`${organizationApiPath(organizationId)}/restore`, { method: 'POST' });
}

// § 2 Projects

export function getProjects(
  organizationId: string,
  view: ListView = 'active',
): Promise<ProjectListItem[]> {
  return apiFetch(`${organizationApiPath(organizationId)}/projects${listSuffix(view)}`);
}

export function createProject(
  organizationId: string,
  body: { name: string; key: string },
): Promise<Project> {
  return apiFetch(`${organizationApiPath(organizationId)}/projects`, { method: 'POST', body });
}

export function getProject(organizationId: string, projectId: string): Promise<Project> {
  return apiFetch(projectApiPath(organizationId, projectId));
}

export function renameProject(
  organizationId: string,
  projectId: string,
  name: string,
): Promise<Project> {
  return apiFetch(`${projectApiPath(organizationId, projectId)}/name`, {
    method: 'PUT',
    body: { name },
  });
}

export function deactivateProject(organizationId: string, projectId: string): Promise<void> {
  return apiFetch(`${projectApiPath(organizationId, projectId)}/deactivate`, { method: 'POST' });
}

export function restoreProject(organizationId: string, projectId: string): Promise<Project> {
  return apiFetch(`${projectApiPath(organizationId, projectId)}/restore`, { method: 'POST' });
}

// § 3 Teams

export function getTeams(organizationId: string, projectId: string): Promise<TeamInList[]> {
  return apiFetch(`${projectApiPath(organizationId, projectId)}/teams`);
}

export function getTeam(organizationId: string, projectId: string, teamId: string): Promise<Team> {
  return apiFetch(teamApiPath(organizationId, projectId, teamId));
}

export function addTeam(organizationId: string, projectId: string, name: string): Promise<Team> {
  return apiFetch(`${projectApiPath(organizationId, projectId)}/teams`, {
    method: 'POST',
    body: { name },
  });
}

export function renameTeam(
  organizationId: string,
  projectId: string,
  teamId: string,
  name: string,
): Promise<Team> {
  return apiFetch(`${teamApiPath(organizationId, projectId, teamId)}/name`, {
    method: 'PUT',
    body: { name },
  });
}

/** mod-017 api-contract § 2.8: 204; `teamId: null` is «Не выбрана». The panel reads the teams again. */
export function setDefaultTeam(
  organizationId: string,
  projectId: string,
  teamId: string | null,
): Promise<void> {
  return apiFetch(`${projectApiPath(organizationId, projectId)}/default-team`, {
    method: 'PUT',
    body: { teamId },
  });
}

/** mod-017 api-contract § 2.9: what a deletion would reassign, in any state; read when the dialog opens. */
export function getOwnedItemsCount(
  organizationId: string,
  projectId: string,
  teamId: string,
): Promise<{ count: number }> {
  return apiFetch(`${teamApiPath(organizationId, projectId, teamId)}/owned-items/count`);
}

export function deleteTeam(
  organizationId: string,
  projectId: string,
  teamId: string,
): Promise<void> {
  return apiFetch(teamApiPath(organizationId, projectId, teamId), { method: 'DELETE' });
}

// § 4 Areas

export function getAreaTree(organizationId: string, projectId: string): Promise<AreaNode[]> {
  return apiFetch(`${projectApiPath(organizationId, projectId)}/areas`);
}

export function createArea(
  organizationId: string,
  projectId: string,
  body: { parentId: string; name: string },
): Promise<Area> {
  return apiFetch(`${projectApiPath(organizationId, projectId)}/areas`, { method: 'POST', body });
}

export function renameArea(
  organizationId: string,
  projectId: string,
  areaId: string,
  name: string,
): Promise<Area> {
  return apiFetch(`${areaApiPath(organizationId, projectId, areaId)}/name`, {
    method: 'PUT',
    body: { name },
  });
}

export function moveArea(
  organizationId: string,
  projectId: string,
  areaId: string,
  parentId: string,
): Promise<Area> {
  return apiFetch(`${areaApiPath(organizationId, projectId, areaId)}/parent`, {
    method: 'PUT',
    body: { parentId },
  });
}

export function deleteArea(
  organizationId: string,
  projectId: string,
  areaId: string,
): Promise<void> {
  return apiFetch(areaApiPath(organizationId, projectId, areaId), { method: 'DELETE' });
}
