import { matchPath } from 'react-router-dom';
import { organizationsPermissions } from '../../features/organizations';

/**
 * The address model of feat-043, in one place (ADR-0032 §§ 1, 2, 4; design § 10): the left
 * menu's table by the most specific chosen object, and the table of retired addresses. The
 * route-inventory test and the retired-address test read this file; nothing else lists a menu
 * segment or a retired shape, so a line that adds a section adds one row here.
 */

/** What the menu is built from: the organization when nothing is chosen, else the primary object. */
export type MenuKind = 'organization' | 'department' | 'team' | 'project';

/**
 * The query names the other chosen selectors ride in (ADR-0032 § 1, the form mod-020 founded).
 * `project` is read only from an address with no object prefix of its own, such as the
 * organization's settings pages, where nothing else can name the project (QA F-3).
 */
export const SELECTOR_QUERY = {
  team: 'teamId',
  department: 'departmentId',
  project: 'projectId',
} as const;

/** The segment under an object's prefix that is its «Параметры» (ADR-0032 § 2). */
export const OBJECT_SETTINGS_SEGMENT = 'settings';

/** The level a «Канбан» or «Бэклог» entry lands on (boards' landing level, a test pins the equality). */
export const LANDING_LEVEL_SEGMENT = 'stories';

export interface MenuRow {
  id: string;
  /** The entry's label, final. */
  label: string;
  /**
   * The path under the object's prefix. `:level` is a board or backlog level (the entry lands
   * on `LANDING_LEVEL_SEGMENT`); «Структура» sits on the area prefix, whatever the menu.
   */
  segment: string;
  /** The read code the page asks (until mod-019 the organization role's bundle). */
  code: string;
}

const read = organizationsPermissions;

/**
 * The code of an entry whose section is decided at a project or a team (projects, work items, boards,
 * backlogs, a project's pages): the organization answer does not carry such a code (BR-SA-11, BR-SA-18) and
 * every member reads those lists (mod-019 design § 3), so the entry asks the organization read every member
 * holds. The object's own page and the server decide the rest.
 */
export const SECTION_ENTRY_CODE = read.organizationRead;
const ENTRY = SECTION_ENTRY_CODE;

/** The table of ADR-0032 § 2, rows in menu order. «Настройки» is not a row: the shell derives it. */
export const MENU_TABLE: Record<MenuKind, readonly MenuRow[]> = {
  organization: [
    { id: 'overview', label: 'Обзор', segment: 'overview', code: read.organizationRead },
    { id: 'areas', label: 'Структура', segment: 'areas', code: read.areaRead },
    { id: 'projects', label: 'Проекты', segment: 'projects', code: ENTRY },
    { id: 'teams', label: 'Команды', segment: 'teams', code: read.teamRead },
    {
      id: 'workitems',
      label: 'Рабочие элементы',
      segment: 'workitems',
      code: ENTRY,
    },
    {
      id: 'boards',
      label: 'Канбан',
      segment: 'boards/:level',
      code: ENTRY,
    },
    {
      id: 'backlogs',
      label: 'Бэклог',
      segment: 'backlogs/:level',
      code: ENTRY,
    },
    { id: 'members', label: 'Участники', segment: 'members', code: read.membershipRead },
  ],
  department: [
    { id: 'overview', label: 'Обзор', segment: 'overview', code: read.areaRead },
    { id: 'projects', label: 'Проекты', segment: 'projects', code: ENTRY },
    { id: 'teams', label: 'Команды', segment: 'teams', code: read.teamRead },
    {
      id: 'workitems',
      label: 'Рабочие элементы',
      segment: 'workitems',
      code: ENTRY,
    },
    {
      id: 'boards',
      label: 'Канбан',
      segment: 'boards/:level',
      code: ENTRY,
    },
    {
      id: 'backlogs',
      label: 'Бэклог',
      segment: 'backlogs/:level',
      code: ENTRY,
    },
    { id: 'members', label: 'Участники', segment: 'members', code: read.areaRead },
  ],
  team: [
    { id: 'overview', label: 'Обзор', segment: 'overview', code: read.teamRead },
    {
      id: 'boards',
      label: 'Канбан',
      segment: 'boards/:level',
      code: ENTRY,
    },
    {
      id: 'backlogs',
      label: 'Бэклог',
      segment: 'backlogs/:level',
      code: ENTRY,
    },
    {
      id: 'workitems',
      label: 'Рабочие элементы',
      segment: 'workitems',
      code: ENTRY,
    },
    { id: 'sprints', label: 'Спринты', segment: 'sprints', code: ENTRY },
    { id: 'projects', label: 'Проекты', segment: 'projects', code: ENTRY },
    { id: 'members', label: 'Участники и доступ', segment: 'members', code: read.teamRead },
  ],
  project: [
    { id: 'general', label: 'Общие', segment: 'general', code: ENTRY },
    { id: 'structure', label: 'Структура', segment: 'areas/:areaId/overview', code: read.areaRead },
    {
      id: 'workitems',
      label: 'Рабочие элементы',
      segment: 'workitems',
      code: ENTRY,
    },
    {
      id: 'boards',
      label: 'Канбан',
      segment: 'boards/:level',
      code: ENTRY,
    },
    {
      id: 'backlogs',
      label: 'Бэклог',
      segment: 'backlogs/:level',
      code: ENTRY,
    },
    { id: 'teams', label: 'Команды', segment: 'teams', code: read.teamRead },
    { id: 'access', label: 'Участники и доступ', segment: 'access', code: ENTRY },
  ],
};

/** The route prefix of a kind under `/organizations/:organizationId`. */
export const KIND_ROUTE_PREFIX: Record<MenuKind, string> = {
  organization: '/organizations/:organizationId',
  department: '/organizations/:organizationId/areas/:areaId',
  team: '/organizations/:organizationId/teams/:teamId',
  project: '/organizations/:organizationId/projects/:projectId',
};

/** The entries of the menu for a kind, in order. */
export function menuFor(kind: MenuKind): readonly MenuRow[] {
  return MENU_TABLE[kind];
}

/** The row's route pattern, e.g. `/organizations/:organizationId/teams/:teamId/boards/:level`. */
export function rowRoute(kind: MenuKind, row: MenuRow): string {
  // «Структура» is the node's page, on the area prefix (ADR-0031 rule 5), whatever the menu.
  const prefix = row.id === 'structure' ? KIND_ROUTE_PREFIX.organization : KIND_ROUTE_PREFIX[kind];
  return `${prefix}/${row.segment}`;
}

/** Whether a row's page is a board or a backlog view (it takes a level). */
export function rowTakesLevel(row: MenuRow): boolean {
  return row.segment.includes(':level');
}

/**
 * The retired addresses (spec BR-NV-6, ADR-0032 § 4): panel routes, no API route. Each answers
 * «Страница не найдена», redirects nowhere and is the target of no link. `…/projects/:p/settings`
 * itself is the new «Параметры» and is not here.
 */
export interface RetiredAddress {
  /** What the address was, for a test's name. */
  was: string;
  /** The earlier line that built it. */
  owner: string;
  /** The route pattern, without a query. */
  pattern: string;
}

export const RETIRED_ADDRESSES: readonly RetiredAddress[] = [
  { was: 'team list', owner: 'feat-028', pattern: '/organizations/:organizationId/settings/teams' },
  {
    was: 'team page',
    owner: 'feat-028',
    pattern: '/organizations/:organizationId/settings/teams/:teamId',
  },
  { was: 'area tree', owner: 'mod-018', pattern: '/organizations/:organizationId/settings/areas' },
  {
    was: 'area page',
    owner: 'mod-018',
    pattern: '/organizations/:organizationId/settings/areas/:areaId',
  },
  {
    was: 'project list',
    owner: 'mod-002',
    pattern: '/organizations/:organizationId/settings/projects',
  },
  {
    was: 'deactivated projects',
    owner: 'mod-002',
    pattern: '/organizations/:organizationId/settings/projects/deactivated',
  },
  {
    was: 'members page',
    owner: 'mod-016',
    pattern: '/organizations/:organizationId/settings/members',
  },
  {
    was: 'project general settings',
    owner: 'mod-002',
    pattern: '/organizations/:organizationId/projects/:projectId/settings/general',
  },
  {
    was: 'project teams settings',
    owner: 'feat-028',
    pattern: '/organizations/:organizationId/projects/:projectId/settings/teams',
  },
  {
    was: 'project areas settings',
    owner: 'mod-018',
    pattern: '/organizations/:organizationId/projects/:projectId/settings/areas',
  },
];

/** Whether `pathname` is a retired address, matched as the router matches (case and encoding). */
export function isRetiredAddress(pathname: string): boolean {
  return RETIRED_ADDRESSES.some(
    (retired) => matchPath({ path: retired.pattern, end: true }, pathname) !== null,
  );
}
