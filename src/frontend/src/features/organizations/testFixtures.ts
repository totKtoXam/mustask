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
export const TEAMS_URL = `${PROJECT_URL}/teams`;
export const TEAM_URL = `${TEAMS_URL}/:teamId`;
export const AREAS_URL = `${PROJECT_URL}/areas`;
export const AREA_URL = `${AREAS_URL}/:areaId`;

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
}

export interface TeamRecord {
  id: string;
  projectId: string;
  name: string;
  isFirstTeam: boolean;
  deleted: boolean;
  /** `null` once deleted (api-contract § 7). */
  defaultAreaId: string | null;
}

export interface AreaRecord {
  id: string;
  projectId: string;
  parentId: string | null;
  name: string;
  ownerTeamId: string | null;
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
  areas: AreaRecord[];
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
  /** The caller the handlers answer as (the token's `sub`); needed with `teamMembers` and `memberships`. */
  actingUserId?: string;
}

/**
 * The stories' example (design § 17): «Альфа» with «Пример проекта» (`PRM`),
 * «Аналитика» and the deactivated «Старый проект»; «Бета» with
 * «Отчётность»; the deactivated «Гамма». In «Пример проекта» the team
 * «Платформа» owns «Платформа» and «Платформа / API» explicitly (its
 * default) and «Платформа / API / Шлюз» by inheritance (US-4); «Мобильное
 * приложение» was left to the first team by a deleted team.
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
      team(ids.prmTeam, ids.prm, 'Пример проекта', true, ids.prmRoot),
      team(ids.platformTeam, ids.prm, 'Платформа', false, ids.apiArea),
      team(ids.anlTeam, ids.anl, 'Аналитика', true, ids.anlRoot),
      team(ids.repTeam, ids.rep, 'Отчётность', true, ids.repRoot),
      team(ids.oldTeam, ids.old, 'Старый проект', true, ids.oldRoot),
      team(ids.arcTeam, ids.arc, 'Архив', true, ids.arcRoot),
    ],
    areas: [
      area(ids.prmRoot, ids.prm, null, 'Пример проекта', ids.prmTeam),
      area(ids.platformArea, ids.prm, ids.prmRoot, 'Платформа', ids.platformTeam),
      area(ids.apiArea, ids.prm, ids.platformArea, 'API', ids.platformTeam),
      area(ids.gatewayArea, ids.prm, ids.apiArea, 'Шлюз', null),
      area(ids.mobileArea, ids.prm, ids.prmRoot, 'Мобильное приложение', ids.prmTeam),
      area(ids.reportsArea, ids.prm, ids.prmRoot, 'Отчёты', null),
      area(ids.anlRoot, ids.anl, null, 'Аналитика', ids.anlTeam),
      area(ids.repRoot, ids.rep, null, 'Отчётность', ids.repTeam),
      area(ids.oldRoot, ids.old, null, 'Старый проект', ids.oldTeam),
      area(ids.arcRoot, ids.arc, null, 'Архив', ids.arcTeam),
    ],
  };
}

/** An instance with no organization at all (US-1 «No organization yet»). */
export function emptyDataset(): Dataset {
  return { organizations: [], projects: [], teams: [], areas: [] };
}

function team(
  id: string,
  projectId: string,
  name: string,
  isFirstTeam: boolean,
  defaultAreaId: string,
): TeamRecord {
  return { id, projectId, name, isFirstTeam, deleted: false, defaultAreaId };
}

function area(
  id: string,
  projectId: string,
  parentId: string | null,
  name: string,
  ownerTeamId: string | null,
): AreaRecord {
  return { id, projectId, parentId, name, ownerTeamId };
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
