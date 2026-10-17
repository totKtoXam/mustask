// The organizations module over HTTP, strictly per
// docs/features/mod-002-organizations-projects-teams/api-contract.md § 1–§ 5
// and — for the teams, their links and the descriptions —
// docs/features/feat-028-org-level-teams/api-contract.md: one function per
// route, camelCase as all of Mustask.Api. Ids come from the address or from an
// answer and are sent as received, re-encoded in the path and never validated
// (§ 0.2). Every list is a plain array in the server's display order, rendered
// as received (§ 0.6).
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

/** § 2.3 and feat-028 § 1.4: one project; `description` is `null` when there is none. */
export interface Project {
  id: string;
  organizationId: string;
  name: string;
  key: string;
  description: string | null;
}

/**
 * feat-028 api-contract § 1.3: how a linked team stands for the project's default
 * (BR-OL-7). `named`: the settings name it; `onlyTeam`: nothing is named and it
 * is the project's only linked team; `null`: neither.
 */
export type DefaultMark = 'named' | 'onlyTeam';

/** feat-028 api-contract § 1.1: a team is the organization's; no project, no mark. */
export interface Team {
  id: string;
  name: string;
  /** `null` when there is none. */
  description: string | null;
}

/** feat-028 api-contract § 1.2: a project in the organization's list of teams (active projects only). */
export interface TeamListProject {
  id: string;
  name: string;
}

/** feat-028 api-contract § 1.2: a row of the organization's list of teams. */
export interface TeamListItem extends Team {
  projects: TeamListProject[];
}

/** feat-028 api-contract § 1.2: a project on a team's page (active projects only). */
export interface TeamProject {
  id: string;
  name: string;
  key: string;
}

/**
 * feat-028 api-contract § 1.3 (with mod-016 § 3.3): a team linked to the project, in the project's
 * list — its mark, and whether the caller is in it.
 */
export interface ProjectTeam {
  id: string;
  name: string;
  defaultMark: DefaultMark | null;
  isMember: boolean;
}

/** feat-028 api-contract § 1.5: what a team owns in one linked project, deactivated ones included. */
export interface DeletionCount {
  projectId: string;
  name: string;
  active: boolean;
  count: number;
}

export interface DeletionCounts {
  projects: DeletionCount[];
}

/** feat-028 api-contract § 1.5: what an unlink would reassign. */
export interface UnlinkCount {
  count: number;
}

/** feat-028 api-contract § 2.1: the creation of a team, in one operation. */
export interface CreateTeamBody {
  name: string;
  description?: string;
  memberIds?: string[];
  projectIds?: string[];
}

/** feat-028 api-contract § 2.4: the creation of a project. */
export interface CreateProjectBody {
  name: string;
  key: string;
  description?: string;
  teamIds?: string[];
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

/** `…/teams/{t}`: a team's address is the organization's (feat-028 § 0.4). */
export function teamApiPath(organizationId: string, teamId: string): string {
  return `${organizationApiPath(organizationId)}/teams/${segment(teamId)}`;
}

/** `…/projects/{p}/teams/{t}`: the pair of a link. */
function linkApiPath(organizationId: string, projectId: string, teamId: string): string {
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

/** feat-028 § 2.4: creates no team; `teamIds` links the chosen ones in the same operation. */
export function createProject(organizationId: string, body: CreateProjectBody): Promise<Project> {
  return apiFetch(`${organizationApiPath(organizationId)}/projects`, { method: 'POST', body });
}

/** feat-028 § 2.4: `null` (or an empty text) clears the description. */
export function describeProject(
  organizationId: string,
  projectId: string,
  description: string | null,
): Promise<Project> {
  return apiFetch(`${projectApiPath(organizationId, projectId)}/description`, {
    method: 'PUT',
    body: { description },
  });
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

// feat-028 §§ 2.1–2.4 Teams, their projects and their links

/** § 2.1: the organization's active teams by name, with their active projects; `[]` is the first state. */
export function getOrganizationTeams(organizationId: string): Promise<TeamListItem[]> {
  return apiFetch(`${organizationApiPath(organizationId)}/teams`);
}

/** § 2.1: one transaction — the team, its members and its links; nothing is created on a refusal. */
export function createTeam(organizationId: string, body: CreateTeamBody): Promise<Team> {
  return apiFetch(`${organizationApiPath(organizationId)}/teams`, { method: 'POST', body });
}

export function getTeam(organizationId: string, teamId: string): Promise<Team> {
  return apiFetch(teamApiPath(organizationId, teamId));
}

export function renameTeam(organizationId: string, teamId: string, name: string): Promise<Team> {
  return apiFetch(`${teamApiPath(organizationId, teamId)}/name`, {
    method: 'PUT',
    body: { name },
  });
}

/** § 2.1: the description as typed; `null` or an empty text clears it. */
export function describeTeam(
  organizationId: string,
  teamId: string,
  description: string | null,
): Promise<Team> {
  return apiFetch(`${teamApiPath(organizationId, teamId)}/description`, {
    method: 'PUT',
    body: { description },
  });
}

/** § 2.1: what a deletion would reassign, per linked project; read when the confirmation opens. */
export function getDeletionCounts(organizationId: string, teamId: string): Promise<DeletionCounts> {
  return apiFetch(`${teamApiPath(organizationId, teamId)}/deletion-counts`);
}

/** § 2.1: 204; no 409 — a project may have no team. */
export function deleteTeam(organizationId: string, teamId: string): Promise<void> {
  return apiFetch(teamApiPath(organizationId, teamId), { method: 'DELETE' });
}

/** § 2.2: the team's active projects by name. */
export function getTeamProjects(organizationId: string, teamId: string): Promise<TeamProject[]> {
  return apiFetch(`${teamApiPath(organizationId, teamId)}/projects`);
}

/** § 2.2: 204; all or nothing; linking a linked pair changes nothing. */
export function linkProjectsToTeam(
  organizationId: string,
  teamId: string,
  projectIds: readonly string[],
): Promise<void> {
  return apiFetch(`${teamApiPath(organizationId, teamId)}/projects`, {
    method: 'POST',
    body: { projectIds },
  });
}

/** § 2.4 (with mod-016 § 3.3): the project's linked teams by name, each with its mark and `isMember`. */
export function getTeams(organizationId: string, projectId: string): Promise<ProjectTeam[]> {
  return apiFetch(`${projectApiPath(organizationId, projectId)}/teams`);
}

/** § 2.4: 204; all or nothing; the path that once added a team now links existing ones. */
export function linkTeamsToProject(
  organizationId: string,
  projectId: string,
  teamIds: readonly string[],
): Promise<void> {
  return apiFetch(`${projectApiPath(organizationId, projectId)}/teams`, {
    method: 'POST',
    body: { teamIds },
  });
}

/** § 2.4: what an unlink would reassign in this project; read when the confirmation opens. */
export function getUnlinkCount(
  organizationId: string,
  projectId: string,
  teamId: string,
): Promise<UnlinkCount> {
  return apiFetch(`${linkApiPath(organizationId, projectId, teamId)}/unlink-count`);
}

/**
 * § 2.4 (SC-09): takes the team off the project; its items there go to `destinationTeamId`, or to
 * «Без команды» for `null`. The `destination` wrapper and its `teamId` key are always sent (D-8).
 */
export function unlinkTeam(
  organizationId: string,
  projectId: string,
  teamId: string,
  destinationTeamId: string | null,
): Promise<void> {
  return apiFetch(`${linkApiPath(organizationId, projectId, teamId)}/unlink`, {
    method: 'POST',
    body: { destination: { teamId: destinationTeamId } },
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
