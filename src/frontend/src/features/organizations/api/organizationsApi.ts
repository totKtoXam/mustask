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

/**
 * feat-028 api-contract § 1.2: a row of the organization's list of teams; feat-040 api-contract
 * § 4.1 adds whether the caller is in the team, which orders the switcher's team level (BR-TS-10).
 */
export interface TeamListItem extends Team {
  projects: TeamListProject[];
  isMember: boolean;
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
  /** mod-018 api-contract § 5: departments only. */
  areaIds?: string[];
}

/** feat-028 api-contract § 2.4: the creation of a project. */
export interface CreateProjectBody {
  name: string;
  key: string;
  description?: string;
  teamIds?: string[];
  /** mod-018 api-contract § 4: the department to place the project's node under; absent: the top level. */
  parentAreaId?: string;
}

/** The wire `type` of an area node (mod-018 api-contract § 2, ADR-0031). */
export type AreaType = 'department' | 'project' | 'area';

/** mod-018 api-contract § 2: one node of the organization's area tree, which comes in pre-order. */
export interface AreaNode {
  id: string;
  /** `null` at the top level. */
  parentId: string | null;
  type: AreaType;
  /** `null` for a department. */
  projectId: string | null;
  /** `null` for a department; `false`: the node's project is deactivated, the node is read-only. */
  projectActive: boolean | null;
  name: string;
  description: string | null;
  /** Names from the top level joined by « / ». */
  path: string;
  /** 1 at the top level. */
  depth: number;
}

/** mod-018 api-contract § 2: what create, rename, describe and move return. */
export interface Area {
  id: string;
  parentId: string | null;
  type: AreaType;
  projectId: string | null;
  name: string;
  description: string | null;
  path: string;
}

/** mod-018 api-contract § 2: a department as a team's page lists it. */
export interface TeamArea {
  id: string;
  name: string;
  path: string;
}

/** mod-018 api-contract § 2: a node's own page: its parent and its direct children. */
export interface AreaPage {
  id: string;
  parentId: string | null;
  type: AreaType;
  projectId: string | null;
  projectActive: boolean | null;
  name: string;
  description: string | null;
  path: string;
  parent: { id: string; name: string; path: string } | null;
  children: { id: string; type: AreaType; name: string; description: string | null }[];
}

export interface LinkedTeam {
  id: string;
  name: string;
}

/** mod-018 api-contract § 2: what a department's deletion would end. */
export interface AreaDeletion {
  teams: { id: string; name: string }[];
}

/** mod-018 api-contract § 3: no `parentId` creates a department, with one a sub-area. */
export interface CreateAreaBody {
  name: string;
  parentId?: string;
  description?: string;
  teamIds?: string[];
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

/** `…/areas/{a}`: an area's address is the organization's (mod-018 api-contract § 0). */
export function areaApiPath(organizationId: string, areaId: string): string {
  return `${organizationApiPath(organizationId)}/areas/${segment(areaId)}`;
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

// mod-018 The organization's areas (api-contract §§ 3-5)

/** § 3: the whole tree in pre-order, siblings by name. */
export function getAreaTree(organizationId: string): Promise<AreaNode[]> {
  return apiFetch(`${organizationApiPath(organizationId)}/areas`);
}

/** § 4: the project's node and the sub-areas below it, with the organization tree's own paths. */
export function getProjectAreaTree(organizationId: string, projectId: string): Promise<AreaNode[]> {
  return apiFetch(`${projectApiPath(organizationId, projectId)}/areas`);
}

/** § 3: no `parentId`: a department; with one: a sub-area. */
export function createArea(organizationId: string, body: CreateAreaBody): Promise<Area> {
  return apiFetch(`${organizationApiPath(organizationId)}/areas`, { method: 'POST', body });
}

export function getArea(organizationId: string, areaId: string): Promise<AreaPage> {
  return apiFetch(areaApiPath(organizationId, areaId));
}

export function renameArea(organizationId: string, areaId: string, name: string): Promise<Area> {
  return apiFetch(`${areaApiPath(organizationId, areaId)}/name`, {
    method: 'PUT',
    body: { name },
  });
}

/** § 3: `null` or an empty text clears it. */
export function describeArea(
  organizationId: string,
  areaId: string,
  description: string | null,
): Promise<Area> {
  return apiFetch(`${areaApiPath(organizationId, areaId)}/description`, {
    method: 'PUT',
    body: { description },
  });
}

/** § 3: `parentId: null` is the top level (a project node only). */
export function moveArea(
  organizationId: string,
  areaId: string,
  parentId: string | null,
): Promise<Area> {
  return apiFetch(`${areaApiPath(organizationId, areaId)}/parent`, {
    method: 'PUT',
    body: { parentId },
  });
}

export function deleteArea(organizationId: string, areaId: string): Promise<void> {
  return apiFetch(areaApiPath(organizationId, areaId), { method: 'DELETE' });
}

/** § 3: what the deletion ends; read when the confirmation opens. */
export function getAreaDeletion(organizationId: string, areaId: string): Promise<AreaDeletion> {
  return apiFetch(`${areaApiPath(organizationId, areaId)}/deletion`);
}

/** § 3: a department's linked teams (the other types answer an empty list). */
export function getAreaTeams(organizationId: string, areaId: string): Promise<LinkedTeam[]> {
  return apiFetch(`${areaApiPath(organizationId, areaId)}/teams`);
}

export function linkTeamsToArea(
  organizationId: string,
  areaId: string,
  teamIds: readonly string[],
): Promise<void> {
  return apiFetch(`${areaApiPath(organizationId, areaId)}/teams`, {
    method: 'POST',
    body: { teamIds },
  });
}

export function unlinkTeamFromArea(
  organizationId: string,
  areaId: string,
  teamId: string,
): Promise<void> {
  return apiFetch(`${areaApiPath(organizationId, areaId)}/teams/${segment(teamId)}`, {
    method: 'DELETE',
  });
}

// § 5 The team's end: the departments it is linked to

export function getTeamAreas(organizationId: string, teamId: string): Promise<TeamArea[]> {
  return apiFetch(`${teamApiPath(organizationId, teamId)}/areas`);
}

export function linkAreasToTeam(
  organizationId: string,
  teamId: string,
  areaIds: readonly string[],
): Promise<void> {
  return apiFetch(`${teamApiPath(organizationId, teamId)}/areas`, {
    method: 'POST',
    body: { areaIds },
  });
}

export function unlinkAreaFromTeam(
  organizationId: string,
  teamId: string,
  areaId: string,
): Promise<void> {
  return apiFetch(`${teamApiPath(organizationId, teamId)}/areas/${segment(areaId)}`, {
    method: 'DELETE',
  });
}
