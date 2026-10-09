// Test data of this slice: the stories' dataset (US-1..US-6 «Data»), the API's
// URLs as MSW patterns, and problem bodies with the catalogs' own Russian
// texts. Every test of the slice takes its data from here; `testApi.ts`
// serves it per api-contract.
import { readFileSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { sampleProcessDefinition } from '../process/testFixtures';
import type { WorkItemType } from '../process';
import type { WorkItemFields } from './api/workItemsApi';
import { WORK_ITEM_FIELD_NAMES, fieldsOf } from './lib/fields';

/** The base URL is `test.env.VITE_API_BASE_URL` in vite.config.ts. */
export const API = 'http://localhost:5000';

/** The routes as MSW path patterns (api-contract § 5, § 3). */
export const ORGANIZATION_URL = `${API}/api/organizations/:organizationId`;
export const PROCESS_URL = `${ORGANIZATION_URL}/process`;
export const PROJECT_URL = `${ORGANIZATION_URL}/projects/:projectId`;
export const AREAS_URL = `${PROJECT_URL}/areas`;
export const WORK_ITEMS_URL = `${PROJECT_URL}/workitems`;
export const WORK_ITEM_URL = `${WORK_ITEMS_URL}/:identifier`;
export const CANDIDATES_URL = `${ORGANIZATION_URL}/workitems/assignee-candidates`;
/** feat-001 api-contract § 3.4: «Родитель»'s search. */
export const PARENT_CANDIDATES_URL = `${WORK_ITEM_URL}/parent-candidates`;
/** feat-001 api-contract § 3.2: the parent write on its own. */
export const WORK_ITEM_PARENT_URL = `${WORK_ITEM_URL}/parent`;

/** Stable ids: canonical lowercase uuids, never shown on screen. */
export const ids = {
  example: 'e0000000-0000-4000-8000-000000000001',
  other: 'e0000000-0000-4000-8000-000000000002',
  msk: 'f0000000-0000-4000-8000-000000000001',
  ops: 'f0000000-0000-4000-8000-000000000002',
  otherMsk: 'f0000000-0000-4000-8000-000000000003',
  mskRoot: 'a1000000-0000-4000-8000-000000000001',
  platform: 'a1000000-0000-4000-8000-000000000002',
  api: 'a1000000-0000-4000-8000-000000000003',
  reports: 'a1000000-0000-4000-8000-000000000004',
  opsRoot: 'a1000000-0000-4000-8000-000000000005',
  warehouse: 'a1000000-0000-4000-8000-000000000006',
  otherRoot: 'a1000000-0000-4000-8000-000000000007',
  anna: 'u0000000-anna-smirnova',
  annaK: 'u0000000-anna-kuznetsova',
  petr: 'u0000000-petr-ivanov',
  ghost: 'u0000000-not-in-directory',
} as const;

/** The seeded process every organization of the dataset has (mod-001 api-contract § 1.4). */
export const process = sampleProcessDefinition;

export function processType(key: string): WorkItemType {
  const type = process.workItemTypes.find((candidate) => candidate.key === key);
  if (!type) {
    throw new Error(`No type ${key} in the process fixture.`);
  }
  return type;
}

export function stateId(typeKey: string, stateName: string): string {
  const state = processType(typeKey).states.find((candidate) => candidate.name === stateName);
  if (!state) {
    throw new Error(`No state «${stateName}» of ${typeKey} in the process fixture.`);
  }
  return state.id;
}

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

export interface AreaRecord {
  id: string;
  projectId: string;
  parentId: string | null;
  name: string;
}

export interface UserRecord {
  id: string;
  displayName: string;
  email: string;
  active: boolean;
}

export interface WorkItemRecord {
  id: string;
  projectId: string;
  number: number;
  typeId: string;
  title: string;
  description: string;
  stateId: string;
  assigneeId: string | null;
  areaId: string;
  createdAt: string;
  changedAt: string;
  requestId: string | null;
  /** feat-001: the stored parent's record id; absent or `null` is none. */
  parentId?: string | null;
  /** feat-022: all 17 fields, `null` where the type lacks one (api-contract § 2). */
  fields: WorkItemFields;
}

/** A record's overrides: any field, and the fields per type one by one. */
export type WorkItemOverrides = Partial<Omit<WorkItemRecord, 'fields'>> & {
  fields?: Partial<WorkItemFields>;
};

/**
 * feat-022 api-contract § 3.1 (BR-M3-37): a new item's fields — «Приоритет» 2,
 * a bug's «Серьёзность» 3, «Вид ценности» `Business` where the type has it,
 * the type's texts `""`, everything else `null`.
 */
export function defaultFields(appearance: string): WorkItemFields {
  const has = new Set(fieldsOf(appearance));
  const fields = Object.fromEntries(WORK_ITEM_FIELD_NAMES.map((name) => [name, null])) as Record<
    keyof WorkItemFields,
    unknown
  >;
  fields.priority = 2;
  if (has.has('severity')) fields.severity = 3;
  if (has.has('valueArea')) fields.valueArea = 'Business';
  for (const text of ['acceptanceCriteria', 'reproSteps', 'systemInfo'] as const) {
    if (has.has(text)) fields[text] = '';
  }
  return fields as unknown as WorkItemFields;
}

export interface Dataset {
  organizations: OrganizationRecord[];
  projects: ProjectRecord[];
  areas: AreaRecord[];
  users: UserRecord[];
  items: WorkItemRecord[];
}

/** «05.10.2026 14:30» in whatever zone the test runs: the panel shows local time. */
export const SAVED_AT = new Date(2026, 9, 5, 14, 30).toISOString();
export const SAVED_AT_TEXT = '05.10.2026 14:30';

let itemCount = 0;

/** An item record with the dataset's defaults: «Новая», no assignee, the root area. */
export function item(
  projectId: string,
  number: number,
  typeKey: string,
  title: string,
  overrides: WorkItemOverrides = {},
): WorkItemRecord {
  itemCount += 1;
  const type = processType(typeKey);
  const { fields, ...rest } = overrides;
  return {
    id: `b1000000-0000-4000-8000-${String(itemCount).padStart(12, '0')}`,
    projectId,
    number,
    typeId: type.id,
    title,
    description: '',
    stateId: stateId(typeKey, 'Новая'),
    assigneeId: null,
    areaId: projectId === ids.ops ? ids.opsRoot : ids.mskRoot,
    createdAt: SAVED_AT,
    changedAt: SAVED_AT,
    requestId: null,
    ...rest,
    fields: { ...defaultFields(type.appearance), ...fields },
  };
}

/**
 * The stories' example: «Пример организации» with «Мастаск» (`MSK`, root
 * «Мастаск», «Платформа», «Платформа / API», «Отчёты») and «Операции»
 * (`OPS`, «Склад»), no item yet; «Орг Б» with its own `MSK`. The directory
 * holds «Анна Смирнова» and «Анна Кузнецова», active, and «Пётр Иванов»,
 * inactive (US-4).
 */
export function emptyDataset(): Dataset {
  return {
    organizations: [
      { id: ids.example, name: 'Пример организации', active: true },
      { id: ids.other, name: 'Орг Б', active: true },
    ],
    projects: [
      { id: ids.msk, organizationId: ids.example, name: 'Мастаск', key: 'MSK', active: true },
      { id: ids.ops, organizationId: ids.example, name: 'Операции', key: 'OPS', active: true },
      { id: ids.otherMsk, organizationId: ids.other, name: 'Мастаск Б', key: 'MSK', active: true },
    ],
    areas: [
      { id: ids.mskRoot, projectId: ids.msk, parentId: null, name: 'Мастаск' },
      { id: ids.platform, projectId: ids.msk, parentId: ids.mskRoot, name: 'Платформа' },
      { id: ids.api, projectId: ids.msk, parentId: ids.platform, name: 'API' },
      { id: ids.reports, projectId: ids.msk, parentId: ids.mskRoot, name: 'Отчёты' },
      { id: ids.opsRoot, projectId: ids.ops, parentId: null, name: 'Операции' },
      { id: ids.warehouse, projectId: ids.ops, parentId: ids.opsRoot, name: 'Склад' },
      { id: ids.otherRoot, projectId: ids.otherMsk, parentId: null, name: 'Мастаск Б' },
    ],
    users: [
      {
        id: ids.anna,
        displayName: 'Анна Смирнова',
        email: 'anna.smirnova@example.com',
        active: true,
      },
      {
        id: ids.annaK,
        displayName: 'Анна Кузнецова',
        email: 'anna.kuznetsova@example.com',
        active: true,
      },
      { id: ids.petr, displayName: 'Пётр Иванов', email: 'petr.ivanov@example.com', active: false },
    ],
    items: [],
  };
}

/**
 * The dataset with US-2's and US-3's items in «Мастаск»: MSK-BUG-4 «Падение
 * при входе», assigned to «Анна Смирнова», «Активна», «Платформа / API»,
 * saved 05.10.2026 14:30; MSK-TASK-5 in «Новая»; and an item of «Операции».
 */
export function exampleDataset(): Dataset {
  const data = emptyDataset();
  data.items.push(
    item(ids.msk, 4, 'BUG', 'Падение при входе', {
      assigneeId: ids.anna,
      stateId: stateId('BUG', 'Активна'),
      areaId: ids.api,
      changedAt: SAVED_AT,
    }),
    item(ids.msk, 5, 'TASK', 'Настроить сборку', {
      changedAt: new Date(2026, 9, 4, 9, 0).toISOString(),
    }),
    item(ids.ops, 1, 'BUG', 'Склад не отвечает', {
      changedAt: new Date(2026, 9, 6, 9, 0).toISOString(),
    }),
  );
  return data;
}

/**
 * feat-001's stories' dataset (US-1 «Data», US-2's chain): in «Мастаск»'s
 * «Платформа» MSK-EPIC-1 «Вход и регистрация», MSK-US-2 «Письмо со ссылкой»,
 * MSK-TASK-3 «Шаблон письма», MSK-BUG-4 «Ссылка истекает раньше срока»,
 * MSK-FEAT-6 «Вход по ссылке», MSK-FEAT-8 «Вход через SSO», none with a parent,
 * each changed a minute after the one before in that order; MSK-TASK-10 →
 * MSK-TASK-11 → MSK-TASK-12, each the parent of the next, changed earlier; and
 * OPS-EPIC-2 «Склад», an epic of another project («Операции» stands for the
 * story's «Склад»).
 */
export function hierarchyDataset(): Dataset {
  const data = emptyDataset();
  const at = (minute: number) => new Date(Date.UTC(2026, 9, 5, 9, minute)).toISOString();
  const platform = (
    number: number,
    typeKey: string,
    title: string,
    minute: number,
    overrides: WorkItemOverrides = {},
  ) =>
    item(ids.msk, number, typeKey, title, {
      areaId: ids.platform,
      changedAt: at(minute),
      ...overrides,
    });
  const task10 = platform(10, 'TASK', 'Подготовить окружение', 1);
  const task11 = platform(11, 'TASK', 'Настроить почтовый сервис', 2, { parentId: task10.id });
  const task12 = platform(12, 'TASK', 'Проверить доставку', 3, { parentId: task11.id });
  data.items.push(
    task10,
    task11,
    task12,
    platform(1, 'EPIC', 'Вход и регистрация', 11),
    platform(2, 'US', 'Письмо со ссылкой', 12),
    platform(3, 'TASK', 'Шаблон письма', 13),
    platform(4, 'BUG', 'Ссылка истекает раньше срока', 14),
    platform(6, 'FEAT', 'Вход по ссылке', 16),
    platform(8, 'FEAT', 'Вход через SSO', 18),
    item(ids.ops, 2, 'EPIC', 'Склад', { changedAt: at(30) }),
  );
  return data;
}

/** The record of «Мастаск»'s item `number` in `data`. */
export function recordOf(data: Dataset, number: number): WorkItemRecord {
  const found = data.items.find(
    (candidate) => candidate.projectId === ids.msk && candidate.number === number,
  );
  if (!found) {
    throw new Error(`No item ${number} in «Мастаск».`);
  }
  return found;
}

interface Catalog {
  codes: Record<string, { statuses: number[]; ru: string }>;
}

function readCatalog(name: string): Catalog {
  return JSON.parse(
    readFileSync(
      path.resolve(
        path.dirname(fileURLToPath(import.meta.url)),
        `../../../../../contracts/errors/${name}.json`,
      ),
      'utf8',
    ),
  ) as Catalog;
}

/** The three catalogs this slice's answers come from (ADR-0006), read once. */
const CATALOG: Catalog['codes'] = {
  ...readCatalog('workitems').codes,
  ...readCatalog('organizations').codes,
  ...readCatalog('user_directory').codes,
};

/** The catalog's Russian text of `code`. */
export function catalogText(code: string): string {
  const entry = CATALOG[code];
  if (!entry) {
    throw new Error(`No code ${code} in the catalogs.`);
  }
  return entry.ru;
}

/** A problem body as the API answers it (api-contract § 0.5). */
export interface ProblemBody {
  status: number;
  title: string;
  detail: string;
  errorCode: string;
  errors?: Record<string, { code: string; detail: string }[]>;
  /** feat-001 § 0.5: only `workitems.link.cycle` carries any. */
  params?: Record<string, string>;
}

/** The answer for `code`, with the catalog's status and text. */
export function problem(code: string): ProblemBody {
  const entry = CATALOG[code];
  if (!entry) {
    throw new Error(`No code ${code} in the catalogs.`);
  }
  return {
    status: entry.statuses[0] ?? 500,
    title: code,
    detail: entry.ru,
    errorCode: code,
  };
}

/** The answer for `code` whose text the catalog renders with `params` (`{name}` placeholders). */
export function problemWithParams(code: string, params: Record<string, string>): ProblemBody {
  const body = problem(code);
  return {
    ...body,
    detail: body.detail.replace(/\{(\w+)\}/g, (placeholder: string, name: string) =>
      Object.prototype.hasOwnProperty.call(params, name) ? (params[name] ?? '') : placeholder,
    ),
    params,
  };
}

/** `workitems.validation_failed` with its field reasons, one per field (§ 0.5). */
export function validationProblem(reasons: Record<string, string>): ProblemBody {
  return {
    ...problem('workitems.validation_failed'),
    errors: Object.fromEntries(
      Object.entries(reasons).map(([field, code]) => [
        field,
        [{ code, detail: catalogText(code) }],
      ]),
    ),
  };
}

/** The headers of a problem answer. */
export const PROBLEM_HEADERS = { 'Content-Type': 'application/problem+json' };
