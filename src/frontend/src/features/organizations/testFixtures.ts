// Test data of this slice: the stories' dataset (design § 17), the API's
// URLs as MSW patterns, and problem bodies per code with the catalog's own
// Russian texts. Every test of the slice takes its data from here instead of
// typing it again; `testApi.ts` serves it per api-contract.
import { readFileSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

/** The base URL is `test.env.VITE_API_BASE_URL` in vite.config.ts. */
export const API = 'http://localhost:5000';

/** The module's routes as MSW path patterns (api-contract § 5). */
export const ORGANIZATIONS_URL = `${API}/api/organizations`;
export const ORGANIZATION_URL = `${ORGANIZATIONS_URL}/:organizationId`;
export const PROJECTS_URL = `${ORGANIZATION_URL}/projects`;
export const PROJECT_URL = `${PROJECTS_URL}/:projectId`;
/** The project's linked teams, and the link's pair (feat-028 api-contract § 2.4). */
export const TEAMS_URL = `${PROJECT_URL}/teams`;
export const TEAM_LINKS_URL = `${PROJECT_URL}/team-links`;
export const PROJECT_TEAM_URL = `${TEAMS_URL}/:teamId`;
/** The organization's teams (feat-028 api-contract § 2.1): a team's address is the organization's. */
export const ORG_TEAMS_URL = `${ORGANIZATION_URL}/teams`;
export const ORG_TEAM_URL = `${ORG_TEAMS_URL}/:teamId`;
/** mod-018: the organization's typed area tree, the project's subtree and the team's department links. */
export const AREAS_URL = `${ORGANIZATION_URL}/areas`;
export const AREA_URL = `${AREAS_URL}/:areaId`;
export const PROJECT_AREAS_URL = `${PROJECT_URL}/areas`;
export const TEAM_AREAS_URL = `${ORG_TEAM_URL}/areas`;
export const TEAM_AREA_URL = `${TEAM_AREAS_URL}/:areaId`;

/** Stable ids of the dataset: canonical lowercase uuids, never shown on screen. */
export const ids = {
  alfa: 'a0000000-0000-4000-8000-000000000001',
  beta: 'a0000000-0000-4000-8000-000000000002',
  gamma: 'a0000000-0000-4000-8000-000000000003',
  prm: 'b0000000-0000-4000-8000-000000000001',
  anl: 'b0000000-0000-4000-8000-000000000002',
  old: 'b0000000-0000-4000-8000-000000000003',
  rep: 'b0000000-0000-4000-8000-000000000004',
  arc: 'b0000000-0000-4000-8000-000000000005',
  prmTeam: 'c0000000-0000-4000-8000-000000000001',
  platformTeam: 'c0000000-0000-4000-8000-000000000002',
  anlTeam: 'c0000000-0000-4000-8000-000000000003',
  repTeam: 'c0000000-0000-4000-8000-000000000004',
  oldTeam: 'c0000000-0000-4000-8000-000000000005',
  arcTeam: 'c0000000-0000-4000-8000-000000000006',
  prmRoot: 'd0000000-0000-4000-8000-000000000001',
  platformArea: 'd0000000-0000-4000-8000-000000000002',
  apiArea: 'd0000000-0000-4000-8000-000000000003',
  gatewayArea: 'd0000000-0000-4000-8000-000000000004',
  mobileArea: 'd0000000-0000-4000-8000-000000000005',
  reportsArea: 'd0000000-0000-4000-8000-000000000006',
  anlRoot: 'd0000000-0000-4000-8000-000000000007',
  repRoot: 'd0000000-0000-4000-8000-000000000008',
  oldRoot: 'd0000000-0000-4000-8000-000000000009',
  arcRoot: 'd0000000-0000-4000-8000-000000000010',
} as const;

export interface OrganizationRecord {
  id: string;
  name: string;
  active: boolean;
}

export interface ProjectRecord {
  id: string;
  organizationId: string;
  name: string;
  key: string;
  active: boolean;
  /** The team the project's settings name (mod-017 BR-OT-4); absent or `null`: none named. */
  defaultTeamId?: string | null;
  /** feat-028 BR-OL-3: as typed; absent or `null`: none. */
  description?: string | null;
}

/** A team is the organization's (feat-028 BR-OL-1); the projects it works on are `Dataset.links`. */
export interface TeamRecord {
  id: string;
  organizationId: string;
  name: string;
  description?: string | null;
  deleted: boolean;
}

/** One project–team link (feat-028 BR-OL-4): at most one per pair. */
export interface LinkRecord {
  projectId: string;
  teamId: string;
}

/**
 * mod-018 (ADR-0031): a node of the organization's typed tree. A project node takes its name and
 * its description from its project.
 */
export interface AreaRecord {
  id: string;
  organizationId: string;
  type: 'department' | 'project' | 'area';
  /** The project of a project node or a sub-area; `null` for a department. */
  projectId: string | null;
  parentId: string | null;
  name: string;
  description?: string | null;
}

/** One department–team link: at most one per pair. */
export interface AreaLinkRecord {
  areaId: string;
  targetId: string;
}

/** A user in a team (mod-016 api-contract § 2): the team list's `isMember` reads it. */
export interface TeamMemberRecord {
  teamId: string;
  userId: string;
}

/** A person as the user projection holds them (mod-016 api-contract § 0.4). */
export interface UserRecord {
  id: string;
  displayName: string | null;
  email: string | null;
  state: 'active' | 'inactive' | 'unknown';
}

/** A user's role in an organization (mod-016 api-contract § 1.2). */
export interface MembershipRecord {
  organizationId: string;
  userId: string;
  role: 'admin' | 'member' | 'viewer';
  addedAt?: string;
  addedBy?: string;
  roleChangedAt?: string | null;
  roleChangedBy?: string | null;
}

export interface Dataset {
  organizations: OrganizationRecord[];
  projects: ProjectRecord[];
  teams: TeamRecord[];
  /** The project–team links; a team with none works on no project, a project with none has no team. */
  links: LinkRecord[];
  areas: AreaRecord[];
  /** The department–team links; absent: none. */
  areaTeams?: AreaLinkRecord[];
  /** The projection's users (mod-016 § 0.4); empty by default. */
  users?: UserRecord[];
  /**
   * Who is in which organization, in which role. Absent: the acting user is the
   * administrator of every active organization, which is what the slice's
   * existing flows assume; present (even empty): exactly these, and
   * `serveMemberships` answers by them.
   */
  memberships?: MembershipRecord[];
  /** Who is in which team; empty by default, so no team is the acting user's own. */
  teamMembers?: TeamMemberRecord[];
  /**
   * How many items a team owns in a project, in any state (feat-028 api-contract § 1.5), keyed by
   * `ownedKey(projectId, teamId)`; absent: none.
   */
  ownedItemCounts?: Record<string, number>;
  /** The caller the handlers answer as (the token's `sub`); needed with `teamMembers` and `memberships`. */
  actingUserId?: string;
}

/** The key of `Dataset.ownedItemCounts`: the items a team owns in one project. */
export function ownedKey(projectId: string, teamId: string): string {
  return `${projectId}:${teamId}`;
}

/**
 * The stories' example (design § 17): «Альфа» with «Пример проекта» (`PRM`),
 * «Аналитика» and the deactivated «Старый проект»; «Бета» with
 * «Отчётность»; the deactivated «Гамма». The teams are the organizations': «Альфа» has
 * «Платформа», «Пример проекта», «Аналитика» and «Старый проект». «Пример проекта» is linked
 * to two teams, «Платформа» and «Пример проекта», and has no named default (so neither carries
 * a mark); every other project is linked to one team, which is its default as the only one
 * (feat-028 BR-OL-7). The area tree has no owners (BR-OT-10).
 */
export function exampleDataset(): Dataset {
  return {
    organizations: [
      { id: ids.alfa, name: 'Альфа', active: true },
      { id: ids.beta, name: 'Бета', active: true },
      { id: ids.gamma, name: 'Гамма', active: false },
    ],
    projects: [
      { id: ids.prm, organizationId: ids.alfa, name: 'Пример проекта', key: 'PRM', active: true },
      { id: ids.anl, organizationId: ids.alfa, name: 'Аналитика', key: 'ANL', active: true },
      { id: ids.old, organizationId: ids.alfa, name: 'Старый проект', key: 'OLD', active: false },
      { id: ids.rep, organizationId: ids.beta, name: 'Отчётность', key: 'REP', active: true },
      { id: ids.arc, organizationId: ids.gamma, name: 'Архив', key: 'ARC', active: true },
    ],
    teams: [
      team(ids.prmTeam, ids.alfa, 'Пример проекта'),
      team(ids.platformTeam, ids.alfa, 'Платформа'),
      team(ids.anlTeam, ids.alfa, 'Аналитика'),
      team(ids.repTeam, ids.beta, 'Отчётность'),
      team(ids.oldTeam, ids.alfa, 'Старый проект'),
      team(ids.arcTeam, ids.gamma, 'Архив'),
    ],
    links: [
      { projectId: ids.prm, teamId: ids.prmTeam },
      { projectId: ids.prm, teamId: ids.platformTeam },
      { projectId: ids.anl, teamId: ids.anlTeam },
      { projectId: ids.rep, teamId: ids.repTeam },
      { projectId: ids.old, teamId: ids.oldTeam },
      { projectId: ids.arc, teamId: ids.arcTeam },
    ],
    areas: [
      department(ids.platformArea, ids.alfa, 'Платформа', 'Платформенные сервисы'),
      department(ids.reportsArea, ids.alfa, 'Отчёты'),
      projectNode(ids.prmRoot, ids.alfa, ids.prm, ids.platformArea),
      projectNode(ids.anlRoot, ids.alfa, ids.anl, null),
      projectNode(ids.oldRoot, ids.alfa, ids.old, null),
      projectNode(ids.repRoot, ids.beta, ids.rep, null),
      projectNode(ids.arcRoot, ids.gamma, ids.arc, null),
      subArea(ids.apiArea, ids.alfa, ids.prm, ids.prmRoot, 'API'),
      subArea(ids.gatewayArea, ids.alfa, ids.prm, ids.apiArea, 'Шлюз'),
      subArea(ids.mobileArea, ids.alfa, ids.prm, ids.prmRoot, 'Мобильное приложение'),
    ],
    areaTeams: [{ areaId: ids.platformArea, targetId: ids.platformTeam }],
  };
}

/** An instance with no organization at all (US-1 «No organization yet»). */
export function emptyDataset(): Dataset {
  return { organizations: [], projects: [], teams: [], links: [], areas: [] };
}

/** The state right after the upgrade (mod-018 US-5): the example with no area anywhere. */
export function noAreasDataset(): Dataset {
  const example = exampleDataset();
  return {
    ...example,
    areas: example.areas
      .filter((node) => node.type === 'project')
      .map((node) => ({ ...node, parentId: null })),
    areaTeams: [],
  };
}

/**
 * The right after the upgrade state of feat-028 US-7: the example's organizations and projects,
 * no team anywhere and no link — every project has none.
 */
export function noTeamsDataset(): Dataset {
  return { ...exampleDataset(), teams: [], links: [] };
}

/** A team of an organization, active (feat-028: the organization's, with no project). */
export function team(
  id: string,
  organizationId: string,
  name: string,
  description: string | null = null,
): TeamRecord {
  return { id, organizationId, name, description, deleted: false };
}

export function department(
  id: string,
  organizationId: string,
  name: string,
  description: string | null = null,
): AreaRecord {
  return {
    id,
    organizationId,
    type: 'department',
    projectId: null,
    parentId: null,
    name,
    description,
  };
}

/** A project's node; its name and description are the project's. */
export function projectNode(
  id: string,
  organizationId: string,
  projectId: string,
  parentId: string | null,
): AreaRecord {
  return { id, organizationId, type: 'project', projectId, parentId, name: '' };
}

export function subArea(
  id: string,
  organizationId: string,
  projectId: string,
  parentId: string,
  name: string,
  description: string | null = null,
): AreaRecord {
  return { id, organizationId, type: 'area', projectId, parentId, name, description };
}

/** The organizations module's error catalog (ADR-0006), read once. */
const CATALOG = JSON.parse(
  readFileSync(
    path.resolve(
      path.dirname(fileURLToPath(import.meta.url)),
      '../../../../../contracts/errors/organizations.json',
    ),
    'utf8',
  ),
) as { codes: Record<string, { statuses: number[]; ru: string }> };

/** The catalog's Russian text of `code`, its `{param}`s filled in. */
export function catalogText(code: string, params: Record<string, string> = {}): string {
  const entry = CATALOG.codes[code];
  if (!entry) {
    throw new Error(`No code ${code} in contracts/errors/organizations.json.`);
  }
  return entry.ru.replace(/\{(\w+)\}/g, (_match, name: string) => params[name] ?? `{${name}}`);
}

/** A problem body as the API answers it (api-contract § 0.5). */
export interface ProblemBody {
  status: number;
  title: string;
  detail: string;
  errorCode: string;
  params?: Record<string, { value: string; type: 'string' }>;
  errors?: Record<string, { code: string; detail: string }[]>;
}

/** The answer for `code`, with the catalog's status and text. */
export function problem(code: string, params: Record<string, string> = {}): ProblemBody {
  const entry = CATALOG.codes[code];
  if (!entry) {
    throw new Error(`No code ${code} in contracts/errors/organizations.json.`);
  }
  const body: ProblemBody = {
    status: entry.statuses[0] ?? 500,
    title: code,
    detail: catalogText(code, params),
    errorCode: code,
  };
  if (Object.keys(params).length > 0) {
    body.params = Object.fromEntries(
      Object.entries(params).map(([name, value]) => [name, { value, type: 'string' as const }]),
    );
  }
  return body;
}

/** `organizations.validation_failed` with its field reasons (api-contract § 0.5). */
export function validationProblem(reasons: Record<string, readonly string[]>): ProblemBody {
  return {
    ...problem('organizations.validation_failed'),
    errors: Object.fromEntries(
      Object.entries(reasons).map(([field, fieldCodes]) => [
        field,
        fieldCodes.map((code) => ({ code, detail: catalogText(code) })),
      ]),
    ),
  };
}

/** The headers of a problem answer. */
export const PROBLEM_HEADERS = { 'Content-Type': 'application/problem+json' };

/** An organization with no department and no project: the area tree is empty (mod-018 US-5). */
export function emptyTreeDataset(): Dataset {
  const data = exampleDataset();
  return {
    ...data,
    projects: data.projects.filter((project) => project.organizationId !== ids.alfa),
    areas: [],
  };
}
