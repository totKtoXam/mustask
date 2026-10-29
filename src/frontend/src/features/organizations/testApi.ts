// MSW handlers over an in-memory copy of the stories' dataset, answering per
// api-contract: lists in display order, not-found from the outside in, the
// order of answers of § 0.4 and the refusals the screens place. Flow tests
// (create → listed → switcher) run without a handler per step. It keeps the
// contract's answers, not every rule of the backend: what a test needs. Since
// feat-028 the teams are the organization's and linked to projects
// (`Dataset.teams`, `Dataset.links`); the routes follow its api-contract.
import { HttpResponse, http, type HttpHandler } from 'msw';
import { server } from '../../test/mocks/server';
import { servedFor } from '../../test/organizationRoles';
import type {
  Area,
  AreaDeletion,
  AreaNode,
  AreaPage,
  DeletionCounts,
  Organization,
  OrganizationListItem,
  Project,
  ProjectListItem,
  ProjectTeam,
  Team,
  TeamArea,
  TeamListItem,
  LinkedTeam,
  TeamProject,
} from './api/organizationsApi';
import {
  AREAS_URL,
  AREA_URL,
  ORGANIZATIONS_URL,
  ORGANIZATION_URL,
  ORG_TEAMS_URL,
  ORG_TEAM_URL,
  PROBLEM_HEADERS,
  PROJECT_AREAS_URL,
  PROJECTS_URL,
  PROJECT_TEAM_URL,
  PROJECT_URL,
  TEAMS_URL,
  TEAM_AREAS_URL,
  TEAM_AREA_URL,
  exampleDataset,
  projectNode,
  ownedKey,
  problem,
  validationProblem,
  type AreaRecord,
  type Dataset,
  type OrganizationRecord,
  type ProblemBody,
  type ProjectRecord,
  type TeamRecord,
} from './testFixtures';

/** A request the API received, for "nothing was sent" and "this was sent" assertions. */
export interface RecordedRequest {
  method: string;
  /** The path as sent, ids still encoded. */
  path: string;
  body: unknown;
}

/** A refusal: an answer that is not a success. */
class Refusal {
  readonly body: ProblemBody;

  constructor(body: ProblemBody) {
    this.body = body;
  }
}

function refuse(code: string, params?: Record<string, string>): never {
  throw new Refusal(problem(code, params));
}

/** Russian collation, letter case and «ё» ignored, then by id (api-contract § 0.6). */
function byName<T extends { name: string; id: string }>(first: T, second: T): number {
  return (
    first.name.localeCompare(second.name, 'ru', { sensitivity: 'base' }) ||
    first.id.localeCompare(second.id)
  );
}

function sameName(first: string, second: string): boolean {
  return first.trim().localeCompare(second.trim(), 'ru', { sensitivity: 'accent' }) === 0;
}

/** api-contract § 0: the tree is at most 10 levels deep. */
const MAX_DEPTH = 10;

const KEY_PATTERN = /^[A-Z][A-Z0-9]{1,9}$/;

/** feat-028 BR-OL-3: a description is at most 2 000 characters. */
const DESCRIPTION_MAX_LENGTH = 2000;

let createdCount = 0;

function newId(prefix: string): string {
  createdCount += 1;
  return `${prefix}0000000-0000-4000-9000-${String(createdCount).padStart(12, '0')}`;
}

function nameReasons(name: unknown): string[] {
  const length = typeof name === 'string' ? name.trim().length : 0;
  return length >= 1 && length <= 100 ? [] : ['organizations.name_length'];
}

function descriptionReasons(description: unknown): string[] {
  return typeof description === 'string' && description.length > DESCRIPTION_MAX_LENGTH
    ? ['organizations.description_length']
    : [];
}

/** The description as the API stores it: as typed, an empty one being none (feat-028 § 0.2). */
function descriptionOf(description: unknown): string | null {
  return typeof description === 'string' && description !== '' ? description : null;
}

function validate(reasons: Record<string, string[]>): void {
  const failing = Object.fromEntries(
    Object.entries(reasons).filter(([, codes]) => codes.length > 0),
  );
  if (Object.keys(failing).length > 0) {
    throw new Refusal(validationProblem(failing));
  }
}

function text(value: unknown): string {
  return typeof value === 'string' ? value : '';
}

/** A list field of a body: absent or `null` is empty, duplicates collapse (feat-028 § 0.2). */
function idList(value: unknown): string[] {
  return Array.isArray(value)
    ? [...new Set(value.filter((entry): entry is string => typeof entry === 'string'))]
    : [];
}

/**
 * The in-memory API. `serveOrganizations()` installs its handlers; a test
 * reads `requests` and the dataset, or puts a one-off answer in front with
 * `server.use` (MSW takes the newest handler first).
 */
export class OrganizationsTestApi {
  readonly data: Dataset;
  readonly requests: RecordedRequest[] = [];

  constructor(data: Dataset = exampleDataset()) {
    this.data = data;
    this.data.links ??= [];
    this.ensureProjectNodes();
  }

  /** Every project has its node (ADR-0031): the ones a dataset lacks stand at the top level. */
  private ensureProjectNodes(): void {
    for (const project of this.data.projects) {
      const has = this.data.areas.some(
        (area) => area.type === 'project' && area.projectId === project.id,
      );
      if (!has) {
        this.data.areas.push(projectNode(newId('e'), project.organizationId, project.id, null));
      }
    }
  }

  // ---- lookups, from the outside in (api-contract § 0.3)

  private organization(id: string | undefined, allowDeactivated = false): OrganizationRecord {
    const found = this.data.organizations.find((candidate) => candidate.id === id?.toLowerCase());
    if (!found || (!found.active && !allowDeactivated)) {
      refuse('organizations.organization.not_found');
    }
    return found;
  }

  private project(
    organizationId: string | undefined,
    id: string | undefined,
    allowDeactivated = false,
  ): ProjectRecord {
    const organization = this.organization(organizationId);
    const found = this.data.projects.find(
      (candidate) =>
        candidate.id === id?.toLowerCase() && candidate.organizationId === organization.id,
    );
    if (!found || (!found.active && !allowDeactivated)) {
      refuse('organizations.project.not_found');
    }
    return found;
  }

  /** An active team of the organization, or `team.not_found` (a deleted, unknown or foreign one alike). */
  private team(organization: OrganizationRecord, id: string | undefined | null): TeamRecord {
    const found = this.data.teams.find(
      (candidate) =>
        typeof id === 'string' &&
        candidate.id === id.toLowerCase() &&
        candidate.organizationId === organization.id &&
        !candidate.deleted,
    );
    if (!found) {
      refuse('organizations.team.not_found');
    }
    return found;
  }

  /** An active project of the organization named in a body, or `project.not_found`. */
  private activeProject(organization: OrganizationRecord, id: string): ProjectRecord {
    const found = this.data.projects.find(
      (candidate) =>
        candidate.id === id.toLowerCase() &&
        candidate.organizationId === organization.id &&
        candidate.active,
    );
    if (!found) {
      refuse('organizations.project.not_found');
    }
    return found;
  }

  private isLinked(projectId: string, teamId: string): boolean {
    return this.data.links.some((link) => link.projectId === projectId && link.teamId === teamId);
  }

  private link(projectId: string, teamId: string): void {
    if (!this.isLinked(projectId, teamId)) {
      this.data.links.push({ projectId, teamId });
    }
  }

  /** An area of the organization, or `area.not_found` (an unknown, a foreign or a malformed id alike). */
  private area(organization: OrganizationRecord, id: string | undefined | null): AreaRecord {
    const found = this.data.areas.find(
      (candidate) =>
        typeof id === 'string' &&
        candidate.id === id.toLowerCase() &&
        candidate.organizationId === organization.id,
    );
    if (!found) {
      refuse('organizations.area.not_found');
    }
    return found;
  }

  private projectOf(area: AreaRecord): ProjectRecord | undefined {
    return area.projectId === null
      ? undefined
      : this.data.projects.find((project) => project.id === area.projectId);
  }

  /** A project node is named as its project (api-contract § 4). */
  private nodeName(area: AreaRecord): string {
    return area.type === 'project' ? (this.projectOf(area)?.name ?? area.name) : area.name;
  }

  private nodeDescription(area: AreaRecord): string | null {
    return area.type === 'project'
      ? (this.projectOf(area)?.description ?? null)
      : (area.description ?? null);
  }

  /** `null` for a department; `false`: the node's project is deactivated, the node is read-only. */
  private projectActive(area: AreaRecord): boolean | null {
    return area.type === 'department' ? null : (this.projectOf(area)?.active ?? true);
  }

  /** The children of a node (or the top level), by name. */
  private children(organizationId: string, parentId: string | null): AreaRecord[] {
    return this.data.areas
      .filter(
        (candidate) =>
          candidate.organizationId === organizationId && candidate.parentId === parentId,
      )
      .sort(
        (first, second) =>
          this.nodeName(first).localeCompare(this.nodeName(second), 'ru', {
            sensitivity: 'base',
          }) || first.id.localeCompare(second.id),
      );
  }

  private pathOf(area: AreaRecord): string {
    const names: string[] = [];
    let current: AreaRecord | undefined = area;
    while (current) {
      names.unshift(this.nodeName(current));
      const parentId: string | null = current.parentId;
      current = parentId === null ? undefined : this.data.areas.find((c) => c.id === parentId);
    }
    return names.join(' / ');
  }

  private depthOf(area: AreaRecord): number {
    return this.pathOf(area).split(' / ').length - 1;
  }

  private subtreeIds(area: AreaRecord): Set<string> {
    const subtree = new Set([area.id]);
    let grew = true;
    while (grew) {
      grew = false;
      for (const candidate of this.data.areas) {
        if (candidate.parentId && subtree.has(candidate.parentId) && !subtree.has(candidate.id)) {
          subtree.add(candidate.id);
          grew = true;
        }
      }
    }
    return subtree;
  }

  /** The height of the subtree under `area`, the area itself counting as 1. */
  private heightOf(area: AreaRecord): number {
    return (
      1 +
      Math.max(
        0,
        ...this.children(area.organizationId, area.id).map((child) => this.heightOf(child)),
      )
    );
  }

  // ---- answers

  activeOrganizations(): OrganizationListItem[] {
    return this.data.organizations
      .filter((organization) => organization.active)
      .sort(byName)
      .map(({ id, name }) => ({ id, name }));
  }

  private organizationList(active: boolean): OrganizationListItem[] {
    return this.data.organizations
      .filter((organization) => organization.active === active)
      .sort(byName)
      .map(({ id, name }) => ({ id, name }));
  }

  private projectList(organization: OrganizationRecord, active: boolean): ProjectListItem[] {
    return this.data.projects
      .filter((project) => project.organizationId === organization.id && project.active === active)
      .sort(byName)
      .map(({ id, name, key }) => ({ id, name, key }));
  }

  private projectAnswer(project: ProjectRecord): Project {
    return {
      id: project.id,
      organizationId: project.organizationId,
      name: project.name,
      key: project.key,
      description: project.description ?? null,
    };
  }

  private teamAnswer(team: TeamRecord): Team {
    return { id: team.id, name: team.name, description: team.description ?? null };
  }

  /** The active projects a team is linked to, by name (feat-028 § 1.2). */
  private activeProjectsOf(team: TeamRecord): ProjectRecord[] {
    return this.data.links
      .filter((link) => link.teamId === team.id)
      .map((link) => this.data.projects.find((project) => project.id === link.projectId))
      .filter((project): project is ProjectRecord => project !== undefined && project.active)
      .sort(byName);
  }

  /** Whether the acting user is in the team (mod-016 § 3.3); a dataset without team members makes nobody a member. */
  private isMember(team: TeamRecord): boolean {
    const actingUserId = this.data.actingUserId;
    return (
      actingUserId !== undefined &&
      (this.data.teamMembers ?? []).some(
        (member) => member.teamId === team.id && member.userId === actingUserId,
      )
    );
  }

  /**
   * The organization's list (feat-028 § 2.1): active teams by name, each with its active projects and,
   * since feat-040 (api-contract § 4.1), whether the caller is in it.
   */
  private teamList(organization: OrganizationRecord): TeamListItem[] {
    return this.data.teams
      .filter((team) => team.organizationId === organization.id && !team.deleted)
      .sort(byName)
      .map((team) => ({
        ...this.teamAnswer(team),
        projects: this.activeProjectsOf(team).map(({ id, name }) => ({ id, name })),
        isMember: this.isMember(team),
      }));
  }

  private teamProjects(team: TeamRecord): TeamProject[] {
    return this.activeProjectsOf(team).map(({ id, name, key }) => ({ id, name, key }));
  }

  /** The project's active linked teams, by name (feat-028 § 1.3). */
  private linkedTeams(project: ProjectRecord): TeamRecord[] {
    return this.data.links
      .filter((link) => link.projectId === project.id)
      .map((link) => this.data.teams.find((team) => team.id === link.teamId))
      .filter((team): team is TeamRecord => team !== undefined && !team.deleted)
      .sort(byName);
  }

  /**
   * The project's list (feat-028 api-contract § 1.3): linked teams by name, the named
   * team — else the only one — carrying the mark, each saying whether the acting user is in
   * it (mod-016 § 3.3). A dataset without team members makes nobody a member.
   */
  private projectTeamList(project: ProjectRecord): ProjectTeam[] {
    const linked = this.linkedTeams(project);
    const named = linked.find((candidate) => candidate.id === project.defaultTeamId);
    return linked.map((team) => ({
      id: team.id,
      name: team.name,
      defaultMark: named
        ? named.id === team.id
          ? ('named' as const)
          : null
        : linked.length === 1
          ? ('onlyTeam' as const)
          : null,
      isMember: this.isMember(team),
    }));
  }

  private owned(projectId: string, teamId: string): number {
    return this.data.ownedItemCounts?.[ownedKey(projectId, teamId)] ?? 0;
  }

  private areaAnswer(area: AreaRecord): Area {
    return {
      id: area.id,
      parentId: area.parentId,
      type: area.type,
      projectId: area.projectId,
      name: this.nodeName(area),
      description: this.nodeDescription(area),
      path: this.pathOf(area),
    };
  }

  private node(area: AreaRecord): AreaNode {
    return {
      id: area.id,
      parentId: area.parentId,
      type: area.type,
      projectId: area.projectId,
      projectActive: this.projectActive(area),
      name: this.nodeName(area),
      description: this.nodeDescription(area),
      path: this.pathOf(area),
      depth: this.depthOf(area) + 1,
    };
  }

  /** The tree in pre-order from the given top nodes, siblings by name (api-contract § 3). */
  private preOrder(organizationId: string, tops: AreaRecord[]): AreaNode[] {
    const nodes: AreaNode[] = [];
    const visit = (area: AreaRecord) => {
      nodes.push(this.node(area));
      for (const child of this.children(organizationId, area.id)) {
        visit(child);
      }
    };
    for (const top of tops) {
      visit(top);
    }
    return nodes;
  }

  private tree(organization: OrganizationRecord): AreaNode[] {
    return this.preOrder(organization.id, this.children(organization.id, null));
  }

  /** api-contract § 4: the project's node and the sub-areas below it. */
  private projectTree(project: ProjectRecord): AreaNode[] {
    const root = this.data.areas.find(
      (area) => area.type === 'project' && area.projectId === project.id,
    );
    return root ? this.preOrder(project.organizationId, [root]) : [];
  }

  private areaPage(area: AreaRecord): AreaPage {
    const parent = area.parentId ? this.data.areas.find((c) => c.id === area.parentId) : undefined;
    return {
      ...this.areaAnswer(area),
      projectActive: this.projectActive(area),
      parent: parent
        ? { id: parent.id, name: this.nodeName(parent), path: this.pathOf(parent) }
        : null,
      children: this.children(area.organizationId, area.id).map((child) => ({
        id: child.id,
        type: child.type,
        name: this.nodeName(child),
        description: this.nodeDescription(child),
      })),
    };
  }

  private linkedTeamsOfArea(area: AreaRecord): LinkedTeam[] {
    return (this.data.areaTeams ?? [])
      .filter((link) => link.areaId === area.id)
      .map((link) => this.data.teams.find((team) => team.id === link.targetId))
      .filter((team): team is TeamRecord => team !== undefined && !team.deleted)
      .sort(byName)
      .map(({ id, name }) => ({ id, name }));
  }

  /** api-contract § 5: the departments a team is linked to, by path. */
  private teamAreas(teamId: string): TeamArea[] {
    return (this.data.areaTeams ?? [])
      .filter((link) => link.targetId === teamId)
      .map((link) => this.data.areas.find((area) => area.id === link.areaId))
      .filter((area): area is AreaRecord => area !== undefined)
      .map((area) => ({ id: area.id, name: this.nodeName(area), path: this.pathOf(area) }))
      .sort((first, second) =>
        first.path.localeCompare(second.path, 'ru', { sensitivity: 'base' }),
      );
  }

  /** api-contract § 3: the team links a department's deletion ends. */
  private areaDeletion(area: AreaRecord): AreaDeletion {
    return { teams: this.linkedTeamsOfArea(area) };
  }

  private linkArea(areaId: string, targetId: string): void {
    const links = (this.data.areaTeams ??= []);
    if (!links.some((link) => link.areaId === areaId && link.targetId === targetId)) {
      links.push({ areaId, targetId });
    }
  }

  private unlinkArea(areaId: string, targetId: string): void {
    this.data.areaTeams = (this.data.areaTeams ?? []).filter(
      (link) => !(link.areaId === areaId && link.targetId === targetId),
    );
  }

  // ---- writes

  private createOrganization(body: Record<string, unknown>): Organization {
    const name = text(body.name);
    validate({ name: nameReasons(body.name) });
    const holder = this.data.organizations.find((candidate) => sameName(candidate.name, name));
    if (holder) {
      refuse('organizations.organization.name_taken', { name: holder.name });
    }
    const organization = { id: newId('e'), name: name.trim(), active: true };
    this.data.organizations.push(organization);
    return { id: organization.id, name: organization.name };
  }

  private renameOrganization(organizationId: string | undefined, body: Record<string, unknown>) {
    const organization = this.organization(organizationId);
    const name = text(body.name);
    validate({ name: nameReasons(body.name) });
    const holder = this.data.organizations.find(
      (candidate) => candidate.id !== organization.id && sameName(candidate.name, name),
    );
    if (holder) {
      refuse('organizations.organization.name_taken', { name: holder.name });
    }
    organization.name = name.trim();
    return { id: organization.id, name: organization.name };
  }

  private projectNameConflict(organizationId: string, name: string, exceptId?: string): void {
    const holder = this.data.projects.find(
      (candidate) =>
        candidate.organizationId === organizationId &&
        candidate.id !== exceptId &&
        sameName(candidate.name, name),
    );
    if (holder) {
      refuse(
        holder.active
          ? 'organizations.project.name_taken'
          : 'organizations.project.name_taken_by_deactivated',
      );
    }
  }

  /** feat-028 BR-OL-2: a project is created with no team; `teamIds` links the chosen ones. */
  private createProject(organizationId: string | undefined, body: Record<string, unknown>) {
    const organization = this.organization(organizationId);
    const name = text(body.name);
    const key = text(body.key);
    validate({
      name: nameReasons(body.name),
      key:
        key === ''
          ? ['organizations.project.key_required']
          : KEY_PATTERN.test(key)
            ? []
            : ['organizations.project.key_invalid'],
      description: descriptionReasons(body.description),
    });
    const teamIds = idList(body.teamIds);
    const teams = teamIds.map((teamId) => this.team(organization, teamId));
    const parentArea =
      body.parentAreaId === null || body.parentAreaId === undefined
        ? null
        : this.area(organization, body.parentAreaId as string);
    if (parentArea && parentArea.type !== 'department') {
      refuse('organizations.area.type_not_allowed');
    }
    this.projectNameConflict(organization.id, name);
    this.assertNodeNameFree(organization.id, parentArea?.id ?? null, name.trim());
    if (this.data.projects.some((p) => p.organizationId === organization.id && p.key === key)) {
      refuse('organizations.project.key_taken', { key });
    }
    const project: ProjectRecord = {
      id: newId('f'),
      organizationId: organization.id,
      name: name.trim(),
      key,
      active: true,
      description: descriptionOf(body.description),
    };
    this.data.projects.push(project);
    for (const team of teams) {
      this.link(project.id, team.id);
    }
    this.data.areas.push(
      projectNode(newId('e'), organization.id, project.id, parentArea?.id ?? null),
    );
    return this.projectAnswer(project);
  }

  private renameProject(
    organizationId: string | undefined,
    projectId: string | undefined,
    body: Record<string, unknown>,
  ) {
    const project = this.project(organizationId, projectId);
    const name = text(body.name);
    validate({ name: nameReasons(body.name) });
    this.projectNameConflict(project.organizationId, name, project.id);
    const node = this.data.areas.find(
      (area) => area.type === 'project' && area.projectId === project.id,
    );
    if (node) {
      this.assertNodeNameFree(project.organizationId, node.parentId, name.trim(), node.id);
    }
    project.name = name.trim();
    return this.projectAnswer(project);
  }

  private describeProject(project: ProjectRecord, body: Record<string, unknown>) {
    validate({ description: descriptionReasons(body.description) });
    project.description = descriptionOf(body.description);
    return this.projectAnswer(project);
  }

  /**
   * feat-028 § 2.1: the team, its members and its links in one operation. A refusal creates
   * nothing: the projects are judged first, then the members, then the name.
   */
  private createTeam(organizationId: string | undefined, body: Record<string, unknown>) {
    const organization = this.organization(organizationId);
    const name = text(body.name);
    validate({ name: nameReasons(body.name), description: descriptionReasons(body.description) });
    const projects = idList(body.projectIds).map((id) => this.activeProject(organization, id));
    const areas = idList(body.areaIds).map((areaId) => this.area(organization, areaId));
    if (areas.some((area) => area.type !== 'department')) {
      refuse('organizations.area.link_type_not_allowed');
    }
    const memberIds = idList(body.memberIds);
    const members = this.data.memberships;
    for (const userId of memberIds) {
      const user = this.data.users?.find((candidate) => candidate.id === userId);
      const inOrganization =
        members === undefined ||
        members.some(
          (membership) =>
            membership.organizationId === organization.id && membership.userId === userId,
        );
      if (!user || user.state !== 'active' || !inOrganization) {
        refuse('organizations.team.member_not_in_organization');
      }
    }
    this.assertNameFree(organization, name);
    const team: TeamRecord = {
      id: newId('7'),
      organizationId: organization.id,
      name: name.trim(),
      description: descriptionOf(body.description),
      deleted: false,
    };
    this.data.teams.push(team);
    for (const userId of memberIds) {
      (this.data.teamMembers ??= []).push({ teamId: team.id, userId });
    }
    for (const project of projects) {
      this.link(project.id, team.id);
    }
    for (const area of areas) {
      this.linkArea(area.id, team.id);
    }
    return this.teamAnswer(team);
  }

  private assertNameFree(organization: OrganizationRecord, name: string, exceptId?: string): void {
    if (
      this.data.teams.some(
        (other) =>
          other.organizationId === organization.id &&
          other.id !== exceptId &&
          !other.deleted &&
          sameName(other.name, name),
      )
    ) {
      refuse('organizations.team.name_taken');
    }
  }

  private renameTeam(
    organization: OrganizationRecord,
    team: TeamRecord,
    body: Record<string, unknown>,
  ) {
    const name = text(body.name);
    validate({ name: nameReasons(body.name) });
    this.assertNameFree(organization, name, team.id);
    team.name = name.trim();
    return this.teamAnswer(team);
  }

  private describeTeam(team: TeamRecord, body: Record<string, unknown>) {
    validate({ description: descriptionReasons(body.description) });
    team.description = descriptionOf(body.description);
    return this.teamAnswer(team);
  }

  /**
   * feat-028 § 0.3: the team named in `PUT …/default-team`. An id that names nothing, a deleted
   * team's or another organization's: 404; an active team of the organization that is not linked
   * to the project: 409; `null` or none clears the setting.
   */
  private setDefaultTeam(
    organization: OrganizationRecord,
    project: ProjectRecord,
    body: Record<string, unknown>,
  ): void {
    const named = body.teamId;
    if (named === null || named === undefined) {
      project.defaultTeamId = null;
      return;
    }
    const found = this.team(organization, typeof named === 'string' ? named : null);
    if (!this.isLinked(project.id, found.id)) {
      refuse('organizations.team.not_in_project');
    }
    project.defaultTeamId = found.id;
  }

  /** feat-028 § 1.5: the items the team owns in each project it is linked to, deactivated ones included. */
  private deletionCounts(team: TeamRecord): DeletionCounts {
    const projects = this.data.links
      .filter((link) => link.teamId === team.id)
      .map((link) => this.data.projects.find((project) => project.id === link.projectId))
      .filter((project): project is ProjectRecord => project !== undefined)
      .sort(byName)
      .map((project) => ({
        projectId: project.id,
        name: project.name,
        active: project.active,
        count: this.owned(project.id, team.id),
      }));
    return { projects };
  }

  /** feat-028 § 2.1: the items go to none, the settings naming the team are cleared, the links and memberships end. */
  private deleteTeam(team: TeamRecord): void {
    team.deleted = true;
    for (const project of this.data.projects) {
      if (project.defaultTeamId === team.id) {
        project.defaultTeamId = null;
      }
      if (this.data.ownedItemCounts) {
        this.data.ownedItemCounts[ownedKey(project.id, team.id)] = 0;
      }
    }
    this.data.links = this.data.links.filter((link) => link.teamId !== team.id);
    this.data.areaTeams = (this.data.areaTeams ?? []).filter((link) => link.targetId !== team.id);
    this.data.teamMembers = (this.data.teamMembers ?? []).filter(
      (member) => member.teamId !== team.id,
    );
  }

  /** feat-028 § 2.2: all or nothing; a linked pair stays as it is. */
  private linkProjectsToTeam(
    organization: OrganizationRecord,
    team: TeamRecord,
    body: Record<string, unknown>,
  ): void {
    const projects = idList(body.projectIds).map((id) => this.activeProject(organization, id));
    for (const project of projects) {
      this.link(project.id, team.id);
    }
  }

  /** feat-028 § 2.4: all or nothing; a team that is not an active team of the organization is 404. */
  private linkTeamsToProject(
    organization: OrganizationRecord,
    project: ProjectRecord,
    body: Record<string, unknown>,
  ): void {
    const teams = idList(body.teamIds).map((id) => this.team(organization, id));
    for (const team of teams) {
      this.link(project.id, team.id);
    }
  }

  /** feat-028 § 2.4 (SC-09): the destination wrapper and its key are required (D-8). */
  private unlinkTeam(
    organization: OrganizationRecord,
    project: ProjectRecord,
    team: TeamRecord,
    body: Record<string, unknown>,
  ): void {
    const destination = body.destination;
    if (
      destination === null ||
      typeof destination !== 'object' ||
      !('teamId' in (destination as object))
    ) {
      throw new Refusal(
        validationProblem({ destination: ['organizations.team.destination_required'] }),
      );
    }
    const named = (destination as { teamId: unknown }).teamId;
    const target =
      named === null ? null : this.team(organization, typeof named === 'string' ? named : null);
    if (!this.isLinked(project.id, team.id)) {
      return;
    }
    if (target !== null && (target.id === team.id || !this.isLinked(project.id, target.id))) {
      refuse('organizations.team.not_in_project');
    }
    const moved = this.owned(project.id, team.id);
    if (this.data.ownedItemCounts) {
      this.data.ownedItemCounts[ownedKey(project.id, team.id)] = 0;
      if (target !== null) {
        const key = ownedKey(project.id, target.id);
        this.data.ownedItemCounts[key] = (this.data.ownedItemCounts[key] ?? 0) + moved;
      }
    }
    if (project.defaultTeamId === team.id) {
      project.defaultTeamId = null;
    }
    this.data.links = this.data.links.filter(
      (link) => !(link.projectId === project.id && link.teamId === team.id),
    );
  }

  /** A write on a node of a deactivated project is `project.not_found` (api-contract § 3). */
  private writable(area: AreaRecord): void {
    if (this.projectActive(area) === false) {
      refuse('organizations.project.not_found');
    }
  }

  private assertDepartment(area: AreaRecord): void {
    if (area.type !== 'department') {
      refuse('organizations.area.link_type_not_allowed');
    }
  }

  private assertNodeNameFree(
    organizationId: string,
    parentId: string | null,
    name: string,
    exceptId?: string,
  ): void {
    if (
      this.children(organizationId, parentId).some(
        (child) => child.id !== exceptId && sameName(this.nodeName(child), name),
      )
    ) {
      refuse('organizations.area.name_taken');
    }
  }

  /**
   * api-contract § 3: no parent creates a department (teams may be linked in the same step); with
   * a parent a sub-area under a project node or a sub-area. 404s first, then the type rules.
   */
  private createArea(organization: OrganizationRecord, body: Record<string, unknown>) {
    validate({
      name: nameReasons(body.name),
      description: descriptionReasons(body.description),
    });
    const parent =
      body.parentId === null || body.parentId === undefined
        ? null
        : this.area(organization, body.parentId as string);
    if (parent) {
      this.writable(parent);
    }
    const teams = idList(body.teamIds).map((id) => this.team(organization, id));
    const name = text(body.name).trim();
    if (parent && parent.type === 'department') {
      refuse('organizations.area.type_not_allowed');
    }
    if (parent && teams.length > 0) {
      refuse('organizations.area.link_type_not_allowed');
    }
    if (parent && this.depthOf(parent) + 1 >= MAX_DEPTH) {
      refuse('organizations.area.depth_exceeded');
    }
    this.assertNodeNameFree(organization.id, parent?.id ?? null, name);
    const area: AreaRecord = {
      id: newId('5'),
      organizationId: organization.id,
      type: parent ? 'area' : 'department',
      projectId: parent?.projectId ?? null,
      parentId: parent?.id ?? null,
      name,
      description: descriptionOf(body.description),
    };
    this.data.areas.push(area);
    for (const team of teams) {
      this.linkArea(area.id, team.id);
    }
    return this.areaAnswer(area);
  }

  private renameArea(area: AreaRecord, body: Record<string, unknown>) {
    this.writable(area);
    validate({ name: nameReasons(body.name) });
    if (area.type === 'project') {
      refuse('organizations.area.root_is_fixed');
    }
    const name = text(body.name).trim();
    this.assertNodeNameFree(area.organizationId, area.parentId, name, area.id);
    area.name = name;
    return this.areaAnswer(area);
  }

  private describeArea(area: AreaRecord, body: Record<string, unknown>) {
    this.writable(area);
    validate({ description: descriptionReasons(body.description) });
    const project = this.projectOf(area);
    if (area.type === 'project' && project) {
      project.description = descriptionOf(body.description);
    } else {
      area.description = descriptionOf(body.description);
    }
    return this.areaAnswer(area);
  }

  /** api-contract § 3, "PUT …/parent": the type rules, then the cycle, the depth and the name. */
  private moveArea(
    organization: OrganizationRecord,
    area: AreaRecord,
    body: Record<string, unknown>,
  ) {
    const parent =
      body.parentId === null || body.parentId === undefined
        ? null
        : this.area(organization, body.parentId as string);
    this.writable(area);
    if (area.type === 'department') {
      // To the top it is a no-op; under another department the type forbids it (backend QA D-2).
      if (parent === null) {
        return this.areaAnswer(area);
      }
      refuse('organizations.area.type_not_allowed');
    }
    if (area.type === 'project') {
      if (parent && this.subtreeIds(area).has(parent.id)) {
        refuse('organizations.area.move_into_own_subtree');
      }
      if (parent && parent.type !== 'department') {
        refuse('organizations.area.type_not_allowed');
      }
    } else {
      if (parent === null || parent.type === 'department') {
        refuse('organizations.area.type_not_allowed');
      }
      if (parent.projectId !== area.projectId) {
        refuse('organizations.area.outside_project');
      }
      if (this.subtreeIds(area).has(parent.id)) {
        refuse('organizations.area.move_into_own_subtree');
      }
    }
    const base = parent ? this.depthOf(parent) + 1 : 0;
    if (base + this.heightOf(area) > MAX_DEPTH) {
      refuse('organizations.area.depth_exceeded');
    }
    if ((parent?.id ?? null) !== area.parentId) {
      this.assertNodeNameFree(organization.id, parent?.id ?? null, this.nodeName(area), area.id);
    }
    area.parentId = parent?.id ?? null;
    return this.areaAnswer(area);
  }

  /** api-contract § 3: a project node is fixed; a node with a child is refused; a department's links end with it. */
  private deleteArea(area: AreaRecord): void {
    this.writable(area);
    if (area.type === 'project') {
      refuse('organizations.area.root_is_fixed');
    }
    if (this.children(area.organizationId, area.id).length > 0) {
      refuse('organizations.area.not_empty');
    }
    this.data.areas.splice(this.data.areas.indexOf(area), 1);
    this.data.areaTeams = (this.data.areaTeams ?? []).filter((l) => l.areaId !== area.id);
  }

  // ---- MSW

  handlers(): HttpHandler[] {
    type Params = Record<string, string | readonly string[] | undefined>;
    const one = (params: Params, name: string) => {
      const value = params[name];
      return typeof value === 'string' ? value : undefined;
    };
    const answer =
      (status: number, run: (params: Params, body: Record<string, unknown>) => unknown) =>
      async ({ request, params }: { request: Request; params: Params }) => {
        const raw = request.method === 'GET' ? '' : await request.text();
        const body = raw ? (JSON.parse(raw) as Record<string, unknown>) : {};
        this.requests.push({
          method: request.method,
          path: new URL(request.url).pathname,
          body: raw ? body : undefined,
        });
        try {
          const result = run(params, body);
          return status === 204
            ? new HttpResponse(null, { status })
            : HttpResponse.json(result as object, { status });
        } catch (error) {
          if (error instanceof Refusal) {
            return HttpResponse.json(error.body, {
              status: error.body.status,
              headers: PROBLEM_HEADERS,
            });
          }
          throw error;
        }
      };
    const o = (p: Params) => one(p, 'organizationId');
    const pr = (p: Params) => this.project(o(p), one(p, 'projectId'));
    /** The team of an organization's address, from the outside in. */
    const tm = (p: Params) => {
      const organization = this.organization(o(p));
      return { organization, team: this.team(organization, one(p, 'teamId')) };
    };
    /** The pair of a project's link: the project, then an active team of the organization. */
    const pair = (p: Params) => {
      const project = pr(p);
      const organization = this.organization(o(p));
      return { organization, project, team: this.team(organization, one(p, 'teamId')) };
    };
    /** A team of a project's address that must also be linked to it (feat-028 § 0.3). */
    const linked = (p: Params) => {
      const found = pair(p);
      if (!this.isLinked(found.project.id, found.team.id)) {
        refuse('organizations.team.not_found');
      }
      return found;
    };
    const ar = (p: Params) => {
      const organization = this.organization(o(p));
      return { organization, area: this.area(organization, one(p, 'areaId')) };
    };
    const teamsOf = (organization: OrganizationRecord, body: Record<string, unknown>) =>
      idList(body.teamIds).map((id) => this.team(organization, id));
    const areasOf = (organization: OrganizationRecord, body: Record<string, unknown>) =>
      idList(body.areaIds).map((id) => this.area(organization, id));

    return [
      http.get(
        ORGANIZATIONS_URL,
        answer(200, () => this.organizationList(true)),
      ),
      http.get(
        `${ORGANIZATIONS_URL}/deactivated`,
        answer(200, () => this.organizationList(false)),
      ),
      http.post(
        ORGANIZATIONS_URL,
        answer(201, (_p, body) => this.createOrganization(body)),
      ),
      http.get(
        ORGANIZATION_URL,
        answer(200, (p) => {
          const { id, name } = this.organization(o(p));
          return { id, name };
        }),
      ),
      // The caller's served access (mod-016 api-contract § 1.1): the dataset's
      // acting user is the administrator of every active organization, and an
      // unknown or deactivated one is the 404 «Организация не найдена» — the
      // same answer a non-member gets — so every flow, "unknown organization →
      // «Организация не найдена»" included, keeps its meaning under the gate.
      http.get(
        `${ORGANIZATION_URL}/access`,
        answer(200, (p) => {
          this.organization(o(p));
          return servedFor('admin');
        }),
      ),
      http.put(
        `${ORGANIZATION_URL}/name`,
        answer(200, (p, b) => this.renameOrganization(o(p), b)),
      ),
      http.post(
        `${ORGANIZATION_URL}/deactivate`,
        answer(204, (p) => {
          this.organization(o(p)).active = false;
        }),
      ),
      http.post(
        `${ORGANIZATION_URL}/restore`,
        answer(200, (p) => {
          const organization = this.organization(o(p), true);
          organization.active = true;
          return { id: organization.id, name: organization.name };
        }),
      ),
      http.get(
        PROJECTS_URL,
        answer(200, (p) => this.projectList(this.organization(o(p)), true)),
      ),
      http.get(
        `${PROJECTS_URL}/deactivated`,
        answer(200, (p) => this.projectList(this.organization(o(p)), false)),
      ),
      http.post(
        PROJECTS_URL,
        answer(201, (p, b) => this.createProject(o(p), b)),
      ),
      http.get(
        PROJECT_URL,
        answer(200, (p) => this.projectAnswer(pr(p))),
      ),
      http.put(
        `${PROJECT_URL}/name`,
        answer(200, (p, b) => this.renameProject(o(p), one(p, 'projectId'), b)),
      ),
      http.put(
        `${PROJECT_URL}/description`,
        answer(200, (p, b) => this.describeProject(pr(p), b)),
      ),
      http.post(
        `${PROJECT_URL}/deactivate`,
        answer(204, (p) => {
          pr(p).active = false;
        }),
      ),
      http.post(
        `${PROJECT_URL}/restore`,
        answer(200, (p) => {
          const project = this.project(o(p), one(p, 'projectId'), true);
          project.active = true;
          return this.projectAnswer(project);
        }),
      ),
      // The project's end of the links (feat-028 § 2.4).
      http.get(
        TEAMS_URL,
        answer(200, (p) => this.projectTeamList(pr(p))),
      ),
      http.post(
        TEAMS_URL,
        answer(204, (p, b) => {
          const project = pr(p);
          this.linkTeamsToProject(this.organization(o(p)), project, b);
        }),
      ),
      http.get(
        `${PROJECT_TEAM_URL}/unlink-count`,
        answer(200, (p) => {
          const { project, team } = linked(p);
          return { count: this.owned(project.id, team.id) };
        }),
      ),
      http.post(
        `${PROJECT_TEAM_URL}/unlink`,
        answer(204, (p, b) => {
          const { organization, project, team } = pair(p);
          this.unlinkTeam(organization, project, team, b);
        }),
      ),
      http.put(
        `${PROJECT_URL}/default-team`,
        answer(204, (p, b) => this.setDefaultTeam(this.organization(o(p)), pr(p), b)),
      ),
      // The organization's teams (feat-028 § 2.1–2.2).
      http.get(
        ORG_TEAMS_URL,
        answer(200, (p) => this.teamList(this.organization(o(p)))),
      ),
      http.post(
        ORG_TEAMS_URL,
        answer(201, (p, b) => this.createTeam(o(p), b)),
      ),
      http.get(
        ORG_TEAM_URL,
        answer(200, (p) => this.teamAnswer(tm(p).team)),
      ),
      http.put(
        `${ORG_TEAM_URL}/name`,
        answer(200, (p, b) => {
          const { organization, team } = tm(p);
          return this.renameTeam(organization, team, b);
        }),
      ),
      http.put(
        `${ORG_TEAM_URL}/description`,
        answer(200, (p, b) => this.describeTeam(tm(p).team, b)),
      ),
      http.get(
        `${ORG_TEAM_URL}/deletion-counts`,
        answer(200, (p) => this.deletionCounts(tm(p).team)),
      ),
      http.delete(
        ORG_TEAM_URL,
        answer(204, (p) => this.deleteTeam(tm(p).team)),
      ),
      http.get(
        `${ORG_TEAM_URL}/projects`,
        answer(200, (p) => this.teamProjects(tm(p).team)),
      ),
      http.post(
        `${ORG_TEAM_URL}/projects`,
        answer(204, (p, b) => {
          const { organization, team } = tm(p);
          this.linkProjectsToTeam(organization, team, b);
        }),
      ),
      // mod-018 api-contract §§ 2-3: the organization's area tree.
      http.get(
        AREAS_URL,
        answer(200, (p) => this.tree(this.organization(o(p)))),
      ),
      http.post(
        AREAS_URL,
        answer(201, (p, b) => this.createArea(this.organization(o(p)), b)),
      ),
      http.get(
        AREA_URL,
        answer(200, (p) => this.areaPage(ar(p).area)),
      ),
      http.put(
        `${AREA_URL}/name`,
        answer(200, (p, b) => this.renameArea(ar(p).area, b)),
      ),
      http.put(
        `${AREA_URL}/description`,
        answer(200, (p, b) => this.describeArea(ar(p).area, b)),
      ),
      http.put(
        `${AREA_URL}/parent`,
        answer(200, (p, b) => {
          const { organization, area } = ar(p);
          return this.moveArea(organization, area, b);
        }),
      ),
      http.get(
        `${AREA_URL}/deletion`,
        answer(200, (p) => this.areaDeletion(ar(p).area)),
      ),
      http.delete(
        AREA_URL,
        answer(204, (p) => {
          this.deleteArea(ar(p).area);
        }),
      ),
      http.get(
        `${AREA_URL}/teams`,
        answer(200, (p) => this.linkedTeamsOfArea(ar(p).area)),
      ),
      http.post(
        `${AREA_URL}/teams`,
        answer(204, (p, b) => {
          const { organization, area } = ar(p);
          const teams = teamsOf(organization, b);
          this.assertDepartment(area);
          for (const team of teams) {
            this.linkArea(area.id, team.id);
          }
        }),
      ),
      http.delete(
        `${AREA_URL}/teams/:teamId`,
        answer(204, (p) => {
          const { organization, area } = ar(p);
          const team = this.team(organization, one(p, 'teamId'));
          this.unlinkArea(area.id, team.id);
        }),
      ),
      // The project's own subtree, and the team's end of the department links.
      http.get(
        PROJECT_AREAS_URL,
        answer(200, (p) => this.projectTree(pr(p))),
      ),
      http.get(
        TEAM_AREAS_URL,
        answer(200, (p) => this.teamAreas(tm(p).team.id)),
      ),
      http.post(
        TEAM_AREAS_URL,
        answer(204, (p, b) => {
          const { organization, team } = tm(p);
          const areas = areasOf(organization, b);
          for (const area of areas) {
            this.assertDepartment(area);
          }
          for (const area of areas) {
            this.linkArea(area.id, team.id);
          }
        }),
      ),
      http.delete(
        TEAM_AREA_URL,
        answer(204, (p) => {
          const { organization, team } = tm(p);
          const area = this.area(organization, one(p, 'areaId'));
          this.unlinkArea(area.id, team.id);
        }),
      ),
    ];
  }

  /** The writes received, reads left out. */
  writes(): RecordedRequest[] {
    return this.requests.filter((request) => request.method !== 'GET');
  }
}

/** Serves the dataset (the stories' example by default) for the rest of the test. */
export function serveOrganizations(data?: Dataset): OrganizationsTestApi {
  const api = new OrganizationsTestApi(data);
  server.use(...api.handlers());
  return api;
}

/** Answers every request to `url` (an MSW pattern) with the problem body, until the test ends. */
export function serveProblem(
  method: 'get' | 'post' | 'put' | 'delete',
  url: string,
  body: ProblemBody,
  options: { once?: boolean } = {},
): void {
  server.use(
    http[method](
      url,
      () => HttpResponse.json(body, { status: body.status, headers: PROBLEM_HEADERS }),
      { once: options.once ?? false },
    ),
  );
}
