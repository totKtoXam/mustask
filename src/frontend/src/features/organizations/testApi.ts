// MSW handlers over an in-memory copy of the stories' dataset, answering per
// api-contract: lists in display order, not-found from the outside in, the
// order of answers of § 0.4 and the refusals the screens place. Flow tests
// (create → listed → switcher) run without a handler per step. It keeps the
// contract's answers, not every rule of the backend: what a test needs.
import { HttpResponse, http, type HttpHandler } from 'msw';
import { server } from '../../test/mocks/server';
import { servedFor } from '../../test/organizationRoles';
import type {
  AreaNode,
  Organization,
  OrganizationListItem,
  Project,
  ProjectListItem,
  Team,
  TeamInList,
} from './api/organizationsApi';
import {
  AREAS_URL,
  AREA_URL,
  ORGANIZATIONS_URL,
  ORGANIZATION_URL,
  PROBLEM_HEADERS,
  PROJECTS_URL,
  PROJECT_URL,
  TEAMS_URL,
  TEAM_URL,
  exampleDataset,
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

const KEY_PATTERN = /^[A-Z][A-Z0-9]{1,9}$/;

let createdCount = 0;

function newId(prefix: string): string {
  createdCount += 1;
  return `${prefix}0000000-0000-4000-9000-${String(createdCount).padStart(12, '0')}`;
}

function nameReasons(name: unknown): string[] {
  const length = typeof name === 'string' ? name.trim().length : 0;
  return length >= 1 && length <= 100 ? [] : ['organizations.name_length'];
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

  private team(project: ProjectRecord, id: string | undefined | null): TeamRecord {
    const found = this.data.teams.find(
      (candidate) =>
        typeof id === 'string' &&
        candidate.id === id.toLowerCase() &&
        candidate.projectId === project.id &&
        !candidate.deleted,
    );
    if (!found) {
      refuse('organizations.team.not_found');
    }
    return found;
  }

  private area(project: ProjectRecord, id: string | undefined | null): AreaRecord {
    const found = this.data.areas.find(
      (candidate) =>
        typeof id === 'string' &&
        candidate.id === id.toLowerCase() &&
        candidate.projectId === project.id,
    );
    if (!found) {
      refuse('organizations.area.not_found');
    }
    return found;
  }

  private children(parentId: string): AreaRecord[] {
    return this.data.areas.filter((candidate) => candidate.parentId === parentId);
  }

  private pathOf(area: AreaRecord): string {
    if (area.parentId === null) {
      return area.name;
    }
    const names: string[] = [];
    let current: AreaRecord | undefined = area;
    while (current && current.parentId !== null) {
      names.unshift(current.name);
      const parentId: string = current.parentId;
      current = this.data.areas.find((candidate) => candidate.id === parentId);
    }
    return names.join(' / ');
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
    };
  }

  /** mod-017 api-contract § 1.3: the named team, else the only active team, else none carries the mark. */
  private teamAnswer(team: TeamRecord): Team {
    const project = this.data.projects.find((candidate) => candidate.id === team.projectId);
    const active = this.data.teams.filter(
      (candidate) => candidate.projectId === team.projectId && !candidate.deleted,
    );
    const named = active.find((candidate) => candidate.id === project?.defaultTeamId);
    const mark = named
      ? named.id === team.id
        ? ('named' as const)
        : null
      : active.length === 1 && active[0]?.id === team.id
        ? ('onlyTeam' as const)
        : null;
    return { id: team.id, projectId: team.projectId, name: team.name, defaultMark: mark };
  }

  /**
   * The project's team list (api-contract § 1.3): by name in Russian collation,
   * no team first, each saying whether the acting user is in it (mod-016
   * § 3.3). A dataset without team members makes nobody a member.
   */
  private teamList(project: ProjectRecord): TeamInList[] {
    const active = this.data.teams
      .filter((team) => team.projectId === project.id && !team.deleted)
      .sort(byName);
    const actingUserId = this.data.actingUserId;
    return active.map((team) => ({
      ...this.teamAnswer(team),
      isMember:
        actingUserId !== undefined &&
        (this.data.teamMembers ?? []).some(
          (member) => member.teamId === team.id && member.userId === actingUserId,
        ),
    }));
  }

  private areaAnswer(area: AreaRecord) {
    return {
      id: area.id,
      projectId: area.projectId,
      parentId: area.parentId,
      name: area.name,
    };
  }

  private tree(project: ProjectRecord): AreaNode[] {
    const nodes: AreaNode[] = [];
    const visit = (area: AreaRecord, depth: number) => {
      nodes.push({
        id: area.id,
        parentId: area.parentId,
        name: area.name,
        path: this.pathOf(area),
        depth,
      });
      for (const child of this.children(area.id).sort(byName)) {
        visit(child, depth + 1);
      }
    };
    const root = this.data.areas.find(
      (area) => area.projectId === project.id && area.parentId === null,
    );
    if (root) {
      visit(root, 0);
    }
    return nodes;
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
    });
    this.projectNameConflict(organization.id, name);
    if (this.data.projects.some((p) => p.organizationId === organization.id && p.key === key)) {
      refuse('organizations.project.key_taken', { key });
    }
    const project: ProjectRecord = {
      id: newId('f'),
      organizationId: organization.id,
      name: name.trim(),
      key,
      active: true,
    };
    // BR-OT-11: the project's team is an ordinary one; the root area is owned by nobody.
    const first: TeamRecord = {
      id: newId('9'),
      projectId: project.id,
      name: project.name,
      deleted: false,
    };
    const root: AreaRecord = {
      id: newId('8'),
      projectId: project.id,
      parentId: null,
      name: project.name,
    };
    this.data.projects.push(project);
    this.data.teams.push(first);
    this.data.areas.push(root);
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
    project.name = name.trim();
    const root = this.data.areas.find(
      (area) => area.projectId === project.id && area.parentId === null,
    );
    if (root) {
      root.name = project.name;
    }
    return this.projectAnswer(project);
  }

  private addTeam(
    organizationId: string | undefined,
    projectId: string | undefined,
    body: Record<string, unknown>,
  ) {
    const project = this.project(organizationId, projectId);
    const name = text(body.name);
    validate({ name: nameReasons(body.name) });
    if (
      this.data.teams.some(
        (team) => team.projectId === project.id && !team.deleted && sameName(team.name, name),
      )
    ) {
      refuse('organizations.team.name_taken');
    }
    // BR-OT-11: adding a team creates no area.
    const team: TeamRecord = {
      id: newId('7'),
      projectId: project.id,
      name: name.trim(),
      deleted: false,
    };
    this.data.teams.push(team);
    return this.teamAnswer(team);
  }

  private renameTeam(team: TeamRecord, project: ProjectRecord, body: Record<string, unknown>) {
    const name = text(body.name);
    validate({ name: nameReasons(body.name) });
    if (
      this.data.teams.some(
        (other) =>
          other.projectId === project.id &&
          other.id !== team.id &&
          !other.deleted &&
          sameName(other.name, name),
      )
    ) {
      refuse('organizations.team.name_taken');
    }
    team.name = name.trim();
    return this.teamAnswer(team);
  }

  /**
   * mod-017 api-contract § 0.2, § 2.8: the team named in `PUT …/default-team`. An id that
   * names nothing, a deleted team's or another organization's: 404; an active team of
   * another active project of the organization: 409; `null` or none clears the setting.
   */
  private setDefaultTeam(project: ProjectRecord, body: Record<string, unknown>): void {
    const named = body.teamId;
    if (named === null || named === undefined) {
      project.defaultTeamId = null;
      return;
    }
    const found = this.data.teams.find(
      (candidate) => typeof named === 'string' && candidate.id === named.toLowerCase(),
    );
    const owner = found && this.data.projects.find((candidate) => candidate.id === found.projectId);
    if (
      !found ||
      found.deleted ||
      !owner ||
      !owner.active ||
      owner.organizationId !== project.organizationId
    ) {
      refuse('organizations.team.not_found');
    }
    if (found.projectId !== project.id) {
      refuse('organizations.team.not_in_project');
    }
    project.defaultTeamId = found.id;
  }

  /** mod-017 api-contract § 2.9: what a deletion would reassign, in any state. */
  private ownedCount(team: TeamRecord): { count: number } {
    return { count: this.data.ownedItemCounts?.[team.id] ?? 0 };
  }

  /** mod-017 api-contract § 2.7: the items go to «Без команды», the setting naming the team is cleared. */
  private deleteTeam(team: TeamRecord, project: ProjectRecord): void {
    const active = this.data.teams.filter(
      (candidate) => candidate.projectId === project.id && !candidate.deleted,
    );
    if (active.length <= 1) {
      refuse('organizations.team.last_team_cannot_be_deleted');
    }
    team.deleted = true;
    if (project.defaultTeamId === team.id) {
      project.defaultTeamId = null;
    }
    if (this.data.ownedItemCounts) {
      this.data.ownedItemCounts[team.id] = 0;
    }
  }

  private createArea(project: ProjectRecord, body: Record<string, unknown>) {
    validate({ name: nameReasons(body.name) });
    const parent = this.area(project, body.parentId as string | null | undefined);
    const name = text(body.name).trim();
    if (this.children(parent.id).some((child) => sameName(child.name, name))) {
      refuse('organizations.area.name_taken');
    }
    const area: AreaRecord = {
      id: newId('5'),
      projectId: project.id,
      parentId: parent.id,
      name,
    };
    this.data.areas.push(area);
    return this.areaAnswer(area);
  }

  private renameArea(area: AreaRecord, body: Record<string, unknown>) {
    if (area.parentId === null) {
      refuse('organizations.area.root_is_fixed');
    }
    validate({ name: nameReasons(body.name) });
    const name = text(body.name).trim();
    if (
      this.children(area.parentId).some(
        (child) => child.id !== area.id && sameName(child.name, name),
      )
    ) {
      refuse('organizations.area.name_taken');
    }
    area.name = name;
    return this.areaAnswer(area);
  }

  private moveArea(area: AreaRecord, project: ProjectRecord, body: Record<string, unknown>) {
    if (area.parentId === null) {
      refuse('organizations.area.root_is_fixed');
    }
    const parent = this.area(project, body.parentId as string | null | undefined);
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
    if (subtree.has(parent.id)) {
      refuse('organizations.area.move_into_own_subtree');
    }
    if (
      parent.id !== area.parentId &&
      this.children(parent.id).some((child) => sameName(child.name, area.name))
    ) {
      refuse('organizations.area.name_taken');
    }
    area.parentId = parent.id;
    return this.areaAnswer(area);
  }

  private deleteArea(area: AreaRecord): void {
    if (area.parentId === null) {
      refuse('organizations.area.root_is_fixed');
    }
    if (this.children(area.id).length > 0) {
      refuse('organizations.area.not_empty');
    }
    this.data.areas.splice(this.data.areas.indexOf(area), 1);
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
    const tm = (p: Params) => {
      const project = pr(p);
      return { project, team: this.team(project, one(p, 'teamId')) };
    };
    const ar = (p: Params) => {
      const project = pr(p);
      return { project, area: this.area(project, one(p, 'areaId')) };
    };

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
      http.get(
        TEAMS_URL,
        answer(200, (p) => this.teamList(pr(p))),
      ),
      http.post(
        TEAMS_URL,
        answer(201, (p, b) => this.addTeam(o(p), one(p, 'projectId'), b)),
      ),
      http.get(
        TEAM_URL,
        answer(200, (p) => this.teamAnswer(tm(p).team)),
      ),
      http.put(
        `${TEAM_URL}/name`,
        answer(200, (p, b) => {
          const { project, team } = tm(p);
          return this.renameTeam(team, project, b);
        }),
      ),
      http.put(
        `${PROJECT_URL}/default-team`,
        answer(204, (p, b) => this.setDefaultTeam(pr(p), b)),
      ),
      http.get(
        `${TEAM_URL}/owned-items/count`,
        answer(200, (p) => this.ownedCount(tm(p).team)),
      ),
      http.delete(
        TEAM_URL,
        answer(204, (p) => {
          const { project, team } = tm(p);
          this.deleteTeam(team, project);
        }),
      ),
      http.get(
        AREAS_URL,
        answer(200, (p) => this.tree(pr(p))),
      ),
      http.post(
        AREAS_URL,
        answer(201, (p, b) => this.createArea(pr(p), b)),
      ),
      http.put(
        `${AREA_URL}/name`,
        answer(200, (p, b) => this.renameArea(ar(p).area, b)),
      ),
      http.put(
        `${AREA_URL}/parent`,
        answer(200, (p, b) => {
          const { project, area } = ar(p);
          return this.moveArea(area, project, b);
        }),
      ),
      http.delete(
        AREA_URL,
        answer(204, (p) => {
          this.deleteArea(ar(p).area);
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
