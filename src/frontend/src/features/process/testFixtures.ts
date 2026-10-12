// Test data of this slice: the full answer api-contract § 1.4 fixes for every
// seeded process (five types, 24 states, four levels; only the ids differ
// from one process to another) and the read's URL for MSW handlers. Every
// test of the slice imports from here instead of typing the body again.
import type {
  BacklogLevel,
  CategoryCode,
  ProcessDefinitionResponse,
  StateDefinition,
  WorkItemType,
} from './api/processApi';

/** The base URL is `test.env.VITE_API_BASE_URL` in vite.config.ts. */
const API = 'http://localhost:5000';

/** The read as an MSW path pattern (api-contract § 1). */
export const PROCESS_DEFINITION_URL = `${API}/api/organizations/:organizationId/process`;

/** The URL the panel requests for `organizationId`, re-encoded as the address carried it. */
export function processDefinitionRequestUrl(organizationId: string): string {
  return `${API}/api/organizations/${encodeURIComponent(organizationId)}/process`;
}

/** The Development stand's demonstration organization (api-contract § 4, D-4). */
export const DEMONSTRATION_ORGANIZATION_ID = 'd0000000-0000-4000-8000-000000000001';

let lastId = 0;

/** A synthetic, stable uuid: never shown on the screen. */
function nextId(): string {
  lastId += 1;
  return `10000000-0000-4000-8000-${String(lastId).padStart(12, '0')}`;
}

const CATEGORY_LABELS: Record<CategoryCode, string> = {
  Proposed: 'Новые',
  InProgress: 'В работе',
  Resolved: 'Решённые',
  Completed: 'Завершённые',
  Removed: 'Удалённые',
};

function states(rows: readonly (readonly [string, CategoryCode])[]): StateDefinition[] {
  return rows.map(([name, code], index) => ({
    id: nextId(),
    name,
    order: index + 1,
    isInitial: index === 0,
    category: { code, label: CATEGORY_LABELS[code] },
  }));
}

/**
 * The states of Эпик, Фича, Пользовательская история and Баг: one name, one meaning
 * (bug-004 D-3) — «Разрешена» is Resolved on every type that has it.
 */
const STANDARD_STATES = [
  ['Новая', 'Proposed'],
  ['Активна', 'InProgress'],
  ['Разрешена', 'Resolved'],
  ['Закрыта', 'Completed'],
  ['Удалена', 'Removed'],
] as const;

const TASK_STATES = [
  ['Новая', 'Proposed'],
  ['Активна', 'InProgress'],
  ['Закрыта', 'Completed'],
  ['Удалена', 'Removed'],
] as const;

const backlogLevels: BacklogLevel[] = [
  { id: nextId(), name: 'Эпики', rank: 4, kind: 'portfolio' },
  { id: nextId(), name: 'Фичи', rank: 3, kind: 'portfolio' },
  { id: nextId(), name: 'Истории', rank: 2, kind: 'requirement' },
  { id: nextId(), name: 'Задачи', rank: 1, kind: 'task' },
];

function level(name: string): { id: string; name: string } {
  const found = backlogLevels.find((candidate) => candidate.name === name);
  if (!found) {
    throw new Error(`No backlog level «${name}» in the fixture.`);
  }
  return { id: found.id, name: found.name };
}

const workItemTypes: WorkItemType[] = [
  {
    id: nextId(),
    key: 'EPIC',
    name: 'Эпик',
    appearance: 'epic',
    backlogLevel: level('Эпики'),
    states: states(STANDARD_STATES),
  },
  {
    id: nextId(),
    key: 'FEAT',
    name: 'Фича',
    appearance: 'feature',
    backlogLevel: level('Фичи'),
    states: states(STANDARD_STATES),
  },
  {
    id: nextId(),
    key: 'US',
    name: 'Пользовательская история',
    appearance: 'story',
    backlogLevel: level('Истории'),
    states: states(STANDARD_STATES),
  },
  {
    id: nextId(),
    key: 'BUG',
    name: 'Баг',
    appearance: 'bug',
    backlogLevel: level('Истории'),
    states: states(STANDARD_STATES),
  },
  {
    id: nextId(),
    key: 'TASK',
    name: 'Задача',
    appearance: 'task',
    backlogLevel: level('Задачи'),
    states: states(TASK_STATES),
  },
];

/** The seeded process as the read answers it (api-contract § 1.4), in display order. */
export const sampleProcessDefinition: ProcessDefinitionResponse = {
  id: '0199a3f2-5b7c-7d10-9a4e-3c2b1a0f9e8d',
  backlogLevels,
  workItemTypes,
};

/** A deep copy of the sample a test may change without touching the shared one. */
export function cloneProcessDefinition(): ProcessDefinitionResponse {
  return structuredClone(sampleProcessDefinition);
}

/** The five type names in the order the read gives them (design DD-2). */
export const TYPE_NAMES_IN_ORDER = workItemTypes.map((type) => type.name);
