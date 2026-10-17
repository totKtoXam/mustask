// Test data of this slice: the stories' dataset (US-1 «Data»: «Мастаск» with
// its first team «Мастаск», the team «Платформа» owning «Платформа», «Платформа
// / API» and «Отчёты» with no owner of their own), the API's URLs as MSW
// patterns, and problem bodies with the catalogs' own Russian texts. mod-003's
// fixtures supply the organizations, projects, areas, users, the seeded
// process and the item builder; this file adds the teams, the area owners and
// the backlog places. `testApi.ts` serves it per api-contract.
import { readFileSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import {
  API,
  PROJECT_URL,
  emptyDataset,
  process,
  ids as workItemIds,
  item,
  processType,
  stateId,
  type Dataset,
  type WorkItemOverrides,
  type WorkItemRecord,
} from '../workitems/testFixtures';

export { item, stateId };

/** The routes as MSW path patterns (api-contract § 3, § 5). */
export const TEAM_URL = `${PROJECT_URL}/teams/:teamId`;
export const BACKLOG_URL = `${TEAM_URL}/backlogs/:level`;
export const REORDER_URL = `${BACKLOG_URL}/reorder`;
export const BOARD_URL = `${TEAM_URL}/boards/:level`;
/** feat-039 api-contract § 3, § 5: the project's own, under the project and no team. */
export const PROJECT_BACKLOG_URL = `${PROJECT_URL}/backlogs/:level`;
export const PROJECT_REORDER_URL = `${PROJECT_BACKLOG_URL}/reorder`;
export const PROJECT_BOARD_URL = `${PROJECT_URL}/boards/:level`;
export { API };

/** Stable ids: mod-003's, and this slice's teams. */
export const ids = {
  ...workItemIds,
  mskTeam: 'c2000000-0000-4000-8000-000000000001',
  platformTeam: 'c2000000-0000-4000-8000-000000000002',
  analyticsTeam: 'c2000000-0000-4000-8000-000000000003',
  opsTeam: 'c2000000-0000-4000-8000-000000000004',
  analyticsArea: 'a1000000-0000-4000-8000-000000000010',
  boris: 'u0000000-boris-petrov',
} as const;

export interface TeamRecord {
  id: string;
  projectId: string;
  name: string;
  defaultAreaId: string;
  /** Owns the project's root; never deleted. */
  first: boolean;
  active: boolean;
}

/** One item's one place (BR-M4-24): on one team's backlog of one level. */
export interface PlaceRecord {
  itemId: string;
  teamId: string;
  level: string;
  position: number;
}

/** One item's one place on its project's backlog of one level (feat-039 api-contract § 6). */
export interface ProjectPlaceRecord {
  itemId: string;
  level: string;
  position: number;
}

export interface BoardsDataset extends Dataset {
  teams: TeamRecord[];
  /** An area's explicit owning team; an area absent here inherits its parent's. */
  owners: Record<string, string>;
  places: PlaceRecord[];
  /** The project's own order, apart from every team's (feat-039 BR-PB-4). */
  projectPlaces: ProjectPlaceRecord[];
}

/**
 * US-1's data with no item: «Пример организации» / «Мастаск» (`MSK`), the
 * first team «Мастаск» (the root, its default), «Платформа» (owns «Платформа»,
 * its default), «Аналитика» (added today with its own area «Аналитика», its
 * default, which holds nothing); and «Операции» (`OPS`) with its first team.
 * «Борис Петров» joins the directory.
 */
export function boardsDataset(): BoardsDataset {
  const data = emptyDataset();
  data.areas.push({
    id: ids.analyticsArea,
    projectId: ids.msk,
    parentId: ids.mskRoot,
    name: 'Аналитика',
  });
  data.users.push({
    id: ids.boris,
    displayName: 'Борис Петров',
    email: 'boris.petrov@example.com',
    active: true,
  });
  return {
    ...data,
    teams: [
      {
        id: ids.mskTeam,
        projectId: ids.msk,
        name: 'Мастаск',
        defaultAreaId: ids.mskRoot,
        first: true,
        active: true,
      },
      {
        id: ids.platformTeam,
        projectId: ids.msk,
        name: 'Платформа',
        defaultAreaId: ids.platform,
        first: false,
        active: true,
      },
      {
        id: ids.analyticsTeam,
        projectId: ids.msk,
        name: 'Аналитика',
        defaultAreaId: ids.analyticsArea,
        first: false,
        active: true,
      },
      {
        id: ids.opsTeam,
        projectId: ids.ops,
        name: 'Операции',
        defaultAreaId: ids.opsRoot,
        first: true,
        active: true,
      },
    ],
    owners: { [ids.platform]: ids.platformTeam, [ids.analyticsArea]: ids.analyticsTeam },
    places: [],
    projectPlaces: [],
  };
}

/**
 * A process the seed does not write (doc-007's customization is not built): «Пользовательская
 * история» gains a state «Проверка» in the Resolved category, after «Разрешена», which «Баг» does
 * not have. The seeded «Истории» columns map both types, so this is the one way a test gets a
 * column that maps a state of the story and none of the bug (BR-M4-18). The fixture is shared by
 * the file's tests: `restore()` puts the states back, and every test that calls this must call it.
 */
export function addStoryOnlyReviewState(): { stateId: string; restore(): void } {
  const story = processType('US');
  const saved = structuredClone(story.states);
  const removed = story.states[story.states.length - 1];
  if (!removed) {
    throw new Error('«Пользовательская история» has no states in the process fixture.');
  }
  const id = '10000000-0000-4000-8000-0000000000a1';
  story.states.splice(story.states.length - 1, 0, {
    id,
    name: 'Проверка',
    order: removed.order,
    isInitial: false,
    category: { code: 'Resolved', label: 'Решённые' },
  });
  removed.order += 1;
  return {
    stateId: id,
    restore: () => {
      story.states.splice(0, story.states.length, ...saved);
    },
  };
}

/**
 * feat-032 BR-C-11: the fixture process that has a test-case type, which the seeded one does not
 * until feat-031 is in main. «Тест-кейс» (`TC`, appearance `testcase`) joins «Задачи» with
 * the states «Новая», «Готов» and «Закрыта», and «Удалена» as every type has it. Like
 * `addStoryOnlyReviewState`, the fixture is shared by the file's tests: `restore()` takes the
 * type away again, and every test that calls this must call it.
 */
export function addTestcaseType(): { typeId: string; restore(): void } {
  const typeId = '10000000-0000-4000-8000-0000000000b1';
  const tasks = process.backlogLevels.find((candidate) => candidate.name === 'Задачи');
  if (!tasks) {
    throw new Error('No «Задачи» level in the process fixture.');
  }
  const state = (
    offset: number,
    name: string,
    code: string,
    label: string,
    isInitial: boolean,
  ) => ({
    id: `10000000-0000-4000-8000-0000000000b${offset + 2}`,
    name,
    order: offset,
    isInitial,
    category: { code, label },
  });
  process.workItemTypes.push({
    id: typeId,
    key: 'TC',
    name: 'Тест-кейс',
    appearance: 'testcase',
    backlogLevel: { id: tasks.id, name: tasks.name },
    states: [
      state(1, 'Новая', 'Proposed', 'Новые', true),
      state(2, 'Готов', 'InProgress', 'В работе', false),
      state(3, 'Закрыта', 'Completed', 'Завершённые', false),
      state(4, 'Удалена', 'Removed', 'Удалённые', false),
    ],
  } as unknown as (typeof process.workItemTypes)[number]);
  return {
    typeId,
    restore: () => {
      const at = process.workItemTypes.findIndex((candidate) => candidate.id === typeId);
      if (at >= 0) {
        process.workItemTypes.splice(at, 1);
      }
    },
  };
}

/** A «Мастаск» item in «Платформа» (the team's own area), «Новая» unless overridden. */
export function platformItem(
  number: number,
  typeKey: string,
  title: string,
  overrides: WorkItemOverrides = {},
): WorkItemRecord {
  return item(ids.msk, number, typeKey, title, { areaId: ids.platform, ...overrides });
}

/**
 * The dataset with «Платформа»'s «Истории» backlog never reordered and
 * holding `numbers` as user stories (US-2's MSK-US-3, MSK-BUG-5, MSK-US-9,
 * MSK-US-11 by default) — a number listed as `BUG-<n>` is a bug.
 */
export function storiesDataset(
  entries: readonly string[] = ['US-3', 'BUG-5', 'US-9', 'US-11'],
): BoardsDataset {
  const data = boardsDataset();
  for (const entry of entries) {
    const [key = 'US', number = '0'] = entry.split('-');
    data.items.push(platformItem(Number(number), key, `Элемент ${entry}`));
  }
  return data;
}

/**
 * feat-039 US-1's «Data» in «Мастаск»: three stories on three teams' surfaces — MSK-US-2 «Вход по
 * паролю» in the root area, which the first team «Мастаск» owns, MSK-US-3 «Выход» in «Платформа»'s,
 * MSK-US-9 «Отчёт» in «Аналитика»'s — and OPS-US-1 «Приход товара» in the project «Операции», which
 * no «Мастаск» view holds. All «Новая», nothing reordered, so the project's order is by number.
 */
export function projectDataset(): BoardsDataset {
  const data = boardsDataset();
  data.items.push(
    item(ids.msk, 2, 'US', 'Вход по паролю', { areaId: ids.mskRoot }),
    platformItem(3, 'US', 'Выход'),
    item(ids.msk, 9, 'US', 'Отчёт', { areaId: ids.analyticsArea }),
    item(ids.ops, 1, 'US', 'Приход товара'),
  );
  return data;
}

/**
 * feat-001's US-4 «Data» on «Платформа»: US-1's MSK-EPIC-1 «Вход и
 * регистрация», MSK-US-2 «Письмо со ссылкой», MSK-TASK-3 «Шаблон письма»,
 * MSK-BUG-4 «Ссылка истекает раньше срока», MSK-FEAT-6 «Вход по ссылке»,
 * MSK-FEAT-8 «Вход через SSO»; MSK-EPIC-20 «Отчётность», MSK-FEAT-23 «Вход по
 * коду», MSK-US-22 «Запомнить устройство»; and MSK-FEAT-21 «Аудит входа» in
 * «Мастаск», team «Мастаск». MSK-EPIC-1 is the parent of MSK-FEAT-6,
 * MSK-FEAT-8, MSK-FEAT-21 and MSK-US-22; MSK-FEAT-6 of MSK-US-2 and MSK-BUG-4;
 * MSK-FEAT-8 of MSK-FEAT-23. «Фичи» of «Платформа» is placed MSK-FEAT-8,
 * MSK-FEAT-6, MSK-FEAT-23; the other backlogs were never reordered.
 */
export function treeDataset(): BoardsDataset {
  const data = boardsDataset();
  const epic1 = platformItem(1, 'EPIC', 'Вход и регистрация');
  const feat6 = platformItem(6, 'FEAT', 'Вход по ссылке', { parentId: epic1.id });
  const feat8 = platformItem(8, 'FEAT', 'Вход через SSO', { parentId: epic1.id });
  const feat23 = platformItem(23, 'FEAT', 'Вход по коду', { parentId: feat8.id });
  data.items.push(
    epic1,
    platformItem(2, 'US', 'Письмо со ссылкой', { parentId: feat6.id }),
    platformItem(3, 'TASK', 'Шаблон письма'),
    platformItem(4, 'BUG', 'Ссылка истекает раньше срока', { parentId: feat6.id }),
    feat6,
    feat8,
    platformItem(20, 'EPIC', 'Отчётность'),
    item(ids.msk, 21, 'FEAT', 'Аудит входа', { areaId: ids.mskRoot, parentId: epic1.id }),
    platformItem(22, 'US', 'Запомнить устройство', { parentId: epic1.id }),
    feat23,
  );
  [feat8, feat6, feat23].forEach((record, index) =>
    data.places.push({
      itemId: record.id,
      teamId: ids.platformTeam,
      level: 'features',
      position: index + 1,
    }),
  );
  return data;
}

/**
 * feat-027's US-1 «Data»: `treeDataset`'s items with the planning values and
 * the deeper links of the stories — MSK-EPIC-1 «Вход и регистрация» of
 * «Трудоёмкость» 20, «Бизнес-ценность» 300, «Бизнес» and «Приоритет» 1;
 * MSK-EPIC-20 «Отчётность» «Архитектура» of «Приоритет» 2; MSK-FEAT-8 of
 * «Трудоёмкость» 2,5; MSK-FEAT-6 in «Платформа / API»; MSK-TASK-3 «Шаблон
 * письма» of «Приоритет» 3. MSK-US-2 is the parent of MSK-BUG-5 «Письмо не
 * доходит», MSK-TASK-3 and MSK-TASK-7 «Настроить SMTP», the last in «Мастаск»'s
 * area (another team's, so beneath no row of «Платформа»).
 */
export function fullTreeDataset(): BoardsDataset {
  const data = treeDataset();
  const us2 = mskRecord(data, 2);
  Object.assign(mskRecord(data, 1).fields, {
    effort: 20,
    businessValue: 300,
    valueArea: 'Business',
    priority: 1,
  });
  Object.assign(mskRecord(data, 20).fields, { valueArea: 'Architectural', priority: 2 });
  Object.assign(mskRecord(data, 8).fields, { effort: 2.5 });
  mskRecord(data, 6).areaId = ids.api;
  const task3 = mskRecord(data, 3);
  task3.parentId = us2.id;
  task3.fields.priority = 3;
  data.items.push(
    platformItem(5, 'BUG', 'Письмо не доходит', { parentId: us2.id }),
    item(ids.msk, 7, 'TASK', 'Настроить SMTP', { areaId: ids.mskRoot, parentId: us2.id }),
  );
  return data;
}

/**
 * feat-032 US-1's «Data»: «Платформа»'s «Истории» board with MSK-US-2 «Повторная отправка
 * письма» in «Активна», assigned to «Анна Смирнова», under MSK-FEAT-11 «Вход по ссылке»
 * («Фичи»), which also has MSK-BUG-1 «Активна» (US-1 numbers it 4, which MSK-TASK-4 holds: a number is the project's, and so MSK-FEAT-6 is MSK-FEAT-11 here, MSK-TASK-6 holding 6); MSK-US-9 «Новая» with nobody assigned and no
 * children. MSK-US-2's children: the tasks MSK-TASK-3 «be» («Закрыта», Анна), MSK-TASK-4 «fe»
 * («Новая», nobody), MSK-TASK-6 «qa» («Активна», Борис) and MSK-TASK-7 «release» (seeded
 * directly into «Удалена», Борис); the bugs MSK-BUG-5 «Неверный счётчик» («Закрыта», Борис)
 * and MSK-BUG-8 «Дубль» («Разрешена», Анна) — each also a card of its own on the board.
 */
export function checklistDataset(): BoardsDataset {
  const data = boardsDataset();
  const feat6 = platformItem(11, 'FEAT', 'Вход по ссылке');
  const us2 = platformItem(2, 'US', 'Повторная отправка письма', {
    stateId: stateId('US', 'Активна'),
    assigneeId: ids.anna,
    parentId: feat6.id,
  });
  const child = (
    number: number,
    typeKey: string,
    title: string,
    state: string,
    assigneeId: string | null,
  ) =>
    platformItem(number, typeKey, title, {
      parentId: us2.id,
      stateId: stateId(typeKey, state),
      assigneeId,
    });
  data.items.push(
    feat6,
    us2,
    platformItem(9, 'US', 'Выход из системы'),
    platformItem(1, 'BUG', 'Ссылка истекает раньше срока', {
      parentId: feat6.id,
      stateId: stateId('BUG', 'Активна'),
    }),
    child(3, 'TASK', 'be', 'Закрыта', ids.anna),
    child(4, 'TASK', 'fe', 'Новая', null),
    child(6, 'TASK', 'qa', 'Активна', ids.boris),
    child(7, 'TASK', 'release', 'Удалена', ids.boris),
    child(5, 'BUG', 'Неверный счётчик', 'Закрыта', ids.boris),
    child(8, 'BUG', 'Дубль', 'Разрешена', ids.anna),
  );
  return data;
}

/**
 * Adds US-1's fixture test cases to `checklistDataset()`: MSK-TC-10 «Письмо приходит»
 * («Готов», Анна) and MSK-TC-12 «Письмо не дублируется» («Закрыта», Анна), children of MSK-US-2.
 * Call `addTestcaseType()` first.
 */
export function addTestCases(data: BoardsDataset): void {
  const us2 = mskRecord(data, 2);
  for (const [number, title, state] of [
    [10, 'Письмо приходит', 'Готов'],
    [12, 'Письмо не дублируется', 'Закрыта'],
  ] as const) {
    data.items.push(
      platformItem(number, 'TC', title, {
        parentId: us2.id,
        stateId: stateId('TC', state),
        assigneeId: ids.anna,
      }),
    );
  }
}

/** The record of «Мастаск»'s item `number` in `data`. */
export function mskRecord(data: Pick<Dataset, 'items'>, number: number): WorkItemRecord {
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

/** The catalogs this slice's answers come from (ADR-0006), read once. */
const CATALOG: Catalog['codes'] = {
  ...readCatalog('boards').codes,
  ...readCatalog('workitems').codes,
  ...readCatalog('organizations').codes,
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
}

/** The answer for `code`, with the catalog's status and text. */
export function problem(code: string): ProblemBody {
  const entry = CATALOG[code];
  if (!entry) {
    throw new Error(`No code ${code} in the catalogs.`);
  }
  return { status: entry.statuses[0] ?? 500, title: code, detail: entry.ru, errorCode: code };
}

/** The headers of a problem answer. */
export const PROBLEM_HEADERS = { 'Content-Type': 'application/problem+json' };
