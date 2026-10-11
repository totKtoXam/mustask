import { describe, expect, it } from 'vitest';
import { ApiError, apiFetch } from '../../../shared/api/httpClient';
import type { WorkItem } from '../../workitems';
import { processType, stateId } from '../../workitems/testFixtures';
import { serveBoards } from '../testApi';
import { API, fullTreeDataset, ids, item, mskRecord } from '../testFixtures';
import { getBacklog, getBoard, type BacklogTreeItem } from './boardsApi';

// feat-027 api-contract § 2 (the backlog at full depth) and § 3 (the create's
// `parentId`) through the panel's own API layer, against the in-memory API the
// component tests use — which is what keeps that API to the contract. US-1's
// «Data»: «Платформа»'s tree from MSK-EPIC-1 down to MSK-TASK-3, MSK-FEAT-21
// and MSK-TASK-7 in «Мастаск»'s area.

const ORG = ids.example;
const MSK = ids.msk;
const PLATFORM = ids.platformTeam;
const WORK_ITEMS = `/api/organizations/${ORG}/projects/${MSK}/workitems`;

/** The identifiers of a tree, one per line, two spaces per depth. */
function outline(rows: readonly BacklogTreeItem[], depth = 0): string[] {
  return rows.flatMap((row) => [
    `${'  '.repeat(depth)}${row.identifier}`,
    ...outline(row.children, depth + 1),
  ]);
}

async function read(level: 'epics' | 'features' | 'stories', team: string = PLATFORM) {
  return getBacklog(ORG, MSK, team, level);
}

describe('getBacklog: the whole tree in the one answer (feat-027 api-contract § 1, § 2)', () => {
  it('«Эпики» reaches the tasks: epic → feature → story or bug → task, features before a story, a bug before a task', async () => {
    serveBoards(fullTreeDataset());

    const backlog = await read('epics');

    expect(outline(backlog.items)).toEqual([
      'MSK-EPIC-1',
      '  MSK-FEAT-8',
      '    MSK-FEAT-23',
      '  MSK-FEAT-6',
      '    MSK-US-2',
      '      MSK-BUG-5',
      '      MSK-TASK-3',
      '    MSK-BUG-4',
      '  MSK-US-22',
      'MSK-EPIC-20',
    ]);
  });

  it('answers it in one read: no request per row or per level', async () => {
    const api = serveBoards(fullTreeDataset());

    await read('epics');

    expect(api.reads('/backlogs/')).toHaveLength(1);
  });

  it('numbers only the top-level rows, 1..n; a nested row has no `order` member at all (BR-T-2)', async () => {
    serveBoards(fullTreeDataset());

    const { items } = await read('epics');

    expect(items.map((row) => row.order)).toEqual([1, 2]);
    const everyNested = items.flatMap(function collect(row: BacklogTreeItem): BacklogTreeItem[] {
      return row.children.flatMap((child) => [child, ...collect(child)]);
    });
    expect(everyNested).toHaveLength(8);
    for (const nested of everyNested) {
      expect(Object.keys(nested)).not.toContain('order');
    }
  });

  it('«Фичи»: a feature under a feature stands beneath its parent only, and its «Порядок» is over the two top-level rows', async () => {
    serveBoards(fullTreeDataset());

    const { items } = await read('features');

    expect(outline(items)).toEqual([
      'MSK-FEAT-8',
      '  MSK-FEAT-23',
      'MSK-FEAT-6',
      '  MSK-US-2',
      '    MSK-BUG-5',
      '    MSK-TASK-3',
      '  MSK-BUG-4',
    ]);
    expect(items.map((row) => [row.identifier, row.order])).toEqual([
      ['MSK-FEAT-8', 1],
      ['MSK-FEAT-6', 2],
    ]);
  });

  it('«Истории»: a bug under a story stands beneath it only; its task follows; a story under an epic stands at the top', async () => {
    serveBoards(fullTreeDataset());

    const { items } = await read('stories');

    expect(outline(items)).toEqual([
      'MSK-US-2',
      '  MSK-BUG-5',
      '  MSK-TASK-3',
      'MSK-BUG-4',
      'MSK-US-22',
    ]);
    expect(items.map((row) => row.order)).toEqual([1, 2, 3]);
  });

  it('an item of a level below the page that stands beneath no row is not on the page', async () => {
    const data = fullTreeDataset();
    // MSK-FEAT-6 loses its parent: it is no row of «Эпики», and nor are its stories and tasks.
    mskRecord(data, 6).parentId = null;
    serveBoards(data);

    const { items } = await read('epics');

    expect(outline(items)).toEqual([
      'MSK-EPIC-1',
      '  MSK-FEAT-8',
      '    MSK-FEAT-23',
      '  MSK-US-22',
      'MSK-EPIC-20',
    ]);
  });

  it('another team’s children stand beneath no row of this one: MSK-FEAT-21 and MSK-TASK-7, in «Мастаск»’s area', async () => {
    serveBoards(fullTreeDataset());

    const platform = await read('epics');
    const msk = await read('features', ids.mskTeam);
    const mskStories = await read('stories', ids.mskTeam);

    expect(outline(platform.items)).not.toContain('  MSK-FEAT-21');
    expect(outline(platform.items).join()).not.toContain('MSK-TASK-7');
    expect(outline(msk.items)).toEqual(['MSK-FEAT-21']);
    expect(msk.items[0]?.parent?.identifier).toBe('MSK-EPIC-1');
    expect(outline(mskStories.items)).toEqual([]);
  });

  it('a child in «Удалена» stands beneath no row, and frees its parent’s children', async () => {
    const data = fullTreeDataset();
    mskRecord(data, 5).stateId = stateId('BUG', 'Удалена');
    serveBoards(data);

    const { items } = await read('stories');

    expect(outline(items)).toEqual(['MSK-US-2', '  MSK-TASK-3', 'MSK-BUG-4', 'MSK-US-22']);
  });

  it('a row’s `parent` is the row it stands beneath, whatever its team or level, and `null` for none', async () => {
    serveBoards(fullTreeDataset());

    const { items } = await read('epics');

    const epic = items[0];
    const feature = epic?.children[1];
    const story = feature?.children[0];
    expect(epic?.parent).toBeNull();
    expect(feature?.parent?.identifier).toBe('MSK-EPIC-1');
    expect(story?.parent?.identifier).toBe('MSK-FEAT-6');
    expect(story?.children[1]?.parent?.identifier).toBe('MSK-US-2');
  });

  it('every row carries «Трудоёмкость», «Бизнес-ценность», «Вид ценности» and «Приоритет» as its item stores them, `null` where its type has no such field', async () => {
    serveBoards(fullTreeDataset());

    const { items } = await read('epics');

    const rows = new Map<string, BacklogTreeItem>();
    const collect = (row: BacklogTreeItem) => {
      rows.set(row.identifier, row);
      row.children.forEach(collect);
    };
    items.forEach(collect);
    const values = (identifier: string) => {
      const row = rows.get(identifier);
      return [row?.effort, row?.businessValue, row?.valueArea, row?.priority];
    };
    expect(values('MSK-EPIC-1')).toEqual([20, 300, 'Business', 1]);
    expect(values('MSK-EPIC-20')).toEqual([null, null, 'Architectural', 2]);
    expect(values('MSK-FEAT-8')).toEqual([2.5, null, 'Business', 2]);
    expect(values('MSK-US-2')).toEqual([null, null, 'Business', 2]);
    expect(values('MSK-BUG-4')).toEqual([null, null, null, 2]);
    expect(values('MSK-TASK-3')).toEqual([null, null, null, 3]);
  });

  it('the board is unchanged: cards in one level, no tree, `order` the position in the whole backlog', async () => {
    serveBoards(fullTreeDataset());

    const board = await getBoard(ORG, MSK, PLATFORM, 'stories');

    const cards = board.columns.flatMap((column) => column.items);
    expect(cards.map((card) => [card.identifier, card.order])).toEqual([
      ['MSK-US-2', 1],
      ['MSK-BUG-4', 2],
      ['MSK-BUG-5', 3],
      ['MSK-US-22', 4],
    ]);
    expect(Object.keys(cards[0] ?? {})).not.toContain('children');
    expect(Object.keys(cards[0] ?? {})).not.toContain('parent');
  });
});

describe('the create with a parent (feat-027 api-contract § 3)', () => {
  const requestId = (n: number) => `c4000000-0000-4000-8000-${String(n).padStart(12, '0')}`;
  const TASK = processType('TASK').id;

  /** The raw answer: status and body, which `apiFetch` does not give together. */
  async function post(body: Record<string, unknown>) {
    const response = await fetch(`${API}${WORK_ITEMS}`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(body),
    });
    return { status: response.status, body: (await response.json()) as Record<string, unknown> };
  }

  it('201 creates the item beneath the row, with `parent` its `WorkItemRef`, and `children` none', async () => {
    const data = fullTreeDataset();
    serveBoards(data);
    const us2 = mskRecord(data, 2);

    const created = await apiFetch<WorkItem>(WORK_ITEMS, {
      method: 'POST',
      body: {
        requestId: requestId(1),
        typeId: TASK,
        title: 'Повторная отправка',
        parentId: us2.id,
      },
    });

    expect(created.identifier).toBe('MSK-TASK-24');
    expect(created.parent).toMatchObject({ id: us2.id, identifier: 'MSK-US-2' });
    expect(created.children).toEqual([]);
    expect(created.state.name).toBe('Новая');
    expect(created.assignee).toBeNull();
    expect(created.priority).toBe(2);
  });

  it('takes the parent’s area as stored, so it comes onto the parent’s team’s backlog — last among its parent’s children of its level', async () => {
    const data = fullTreeDataset();
    serveBoards(data);
    const us2 = mskRecord(data, 2);

    await apiFetch(WORK_ITEMS, {
      method: 'POST',
      body: {
        requestId: requestId(1),
        typeId: TASK,
        title: 'Повторная отправка',
        parentId: us2.id,
      },
    });
    const { items } = await read('stories');

    expect(outline(items)).toEqual([
      'MSK-US-2',
      '  MSK-BUG-5',
      '  MSK-TASK-3',
      '  MSK-TASK-24',
      'MSK-BUG-4',
      'MSK-US-22',
    ]);
    const stored = data.items.find((record) => record.title === 'Повторная отправка');
    expect(stored?.areaId).toBe(us2.areaId);
  });

  it('the parent’s «Изменён» moves to the creation time, and nothing else of the parent changes', async () => {
    const data = fullTreeDataset();
    serveBoards(data);
    const us2 = mskRecord(data, 2);
    const before = { ...us2 };

    const created = await apiFetch<WorkItem>(WORK_ITEMS, {
      method: 'POST',
      body: {
        requestId: requestId(1),
        typeId: TASK,
        title: 'Повторная отправка',
        parentId: us2.id,
      },
    });

    expect(us2.changedAt).toBe(created.changedAt);
    expect(us2.changedAt).not.toBe(before.changedAt);
    expect({ ...us2, changedAt: before.changedAt }).toEqual(before);
  });

  it('with `teamId` too the team is still checked, but the area is the parent’s', async () => {
    const data = fullTreeDataset();
    serveBoards(data);
    const feat21 = mskRecord(data, 21);

    const { status, body } = await post({
      requestId: requestId(1),
      typeId: processType('US').id,
      title: 'История «Мастаска»',
      teamId: PLATFORM,
      parentId: feat21.id,
    });

    expect(status).toBe(201);
    expect(body.area).toMatchObject({ id: feat21.areaId });
  });

  it('without `parentId` it behaves as before: no parent, the team’s default area', async () => {
    const data = fullTreeDataset();
    serveBoards(data);

    const { status, body } = await post({
      requestId: requestId(1),
      typeId: TASK,
      title: 'Без родителя',
      teamId: PLATFORM,
    });

    expect(status).toBe(201);
    expect(body.parent).toBeNull();
    expect(body.area).toMatchObject({ id: ids.platform });
  });

  it('a replay of the `requestId` answers 200 with the one item, naming whatever parent the second body names', async () => {
    const data = fullTreeDataset();
    serveBoards(data);
    const us2 = mskRecord(data, 2);
    const first = await post({
      requestId: requestId(1),
      typeId: TASK,
      title: 'Повторная отправка',
      parentId: us2.id,
    });
    const count = data.items.length;

    const replay = await post({
      requestId: requestId(1),
      typeId: TASK,
      title: 'Повторная отправка',
      parentId: 'not-an-id',
    });

    expect(first.status).toBe(201);
    expect(replay.status).toBe(200);
    expect(replay.body.identifier).toBe(first.body.identifier);
    expect(replay.body.parent).toMatchObject({ identifier: 'MSK-US-2' });
    expect(data.items).toHaveLength(count);
  });

  it.each([
    ['a parent that names nothing', 'c4999999-0000-4000-8000-000000000000'],
    ['a malformed parent id', 'not-an-id'],
  ])(
    '%s: 404 `workitems.link.parent_not_found`, and nothing is created',
    async (_why, parentId) => {
      const data = fullTreeDataset();
      serveBoards(data);
      const count = data.items.length;

      const refusal = await apiFetch(WORK_ITEMS, {
        method: 'POST',
        body: { requestId: requestId(1), typeId: TASK, title: 'Задача', parentId },
      }).catch((error: unknown) => error);

      expect(refusal).toBeInstanceOf(ApiError);
      expect((refusal as ApiError).status).toBe(404);
      expect((refusal as ApiError).errorCode).toBe('workitems.link.parent_not_found');
      expect(data.items).toHaveLength(count);
    },
  );

  it('a parent of another project is not found either (BR-H-1)', async () => {
    const data = fullTreeDataset();
    const foreign = item(ids.ops, 90, 'US', 'Чужая');
    data.items.push(foreign);
    serveBoards(data);

    const refusal = await apiFetch(WORK_ITEMS, {
      method: 'POST',
      body: { requestId: requestId(1), typeId: TASK, title: 'Задача', parentId: foreign.id },
    }).catch((error: unknown) => error);

    expect((refusal as ApiError).errorCode).toBe('workitems.link.parent_not_found');
  });

  it('a parent in «Удалена»: 409 `workitems.link.item_deleted`', async () => {
    const data = fullTreeDataset();
    mskRecord(data, 2).stateId = stateId('US', 'Удалена');
    serveBoards(data);

    const refusal = await apiFetch(WORK_ITEMS, {
      method: 'POST',
      body: {
        requestId: requestId(1),
        typeId: TASK,
        title: 'Задача',
        parentId: mskRecord(data, 2).id,
      },
    }).catch((error: unknown) => error);

    expect((refusal as ApiError).status).toBe(409);
    expect((refusal as ApiError).errorCode).toBe('workitems.link.item_deleted');
  });

  it('a parent of a level below the new item’s — a feature under a story: 409 `workitems.link.parent_level_not_allowed`', async () => {
    const data = fullTreeDataset();
    serveBoards(data);

    const refusal = await apiFetch(WORK_ITEMS, {
      method: 'POST',
      body: {
        requestId: requestId(1),
        typeId: processType('FEAT').id,
        title: 'Фича',
        parentId: mskRecord(data, 2).id,
      },
    }).catch((error: unknown) => error);

    expect((refusal as ApiError).status).toBe(409);
    expect((refusal as ApiError).errorCode).toBe('workitems.link.parent_level_not_allowed');
  });

  it('the answers keep the contract’s order: an unknown team before a parent that names nothing, a deleted parent before a level', async () => {
    const data = fullTreeDataset();
    mskRecord(data, 2).stateId = stateId('US', 'Удалена');
    serveBoards(data);

    const team = await apiFetch(WORK_ITEMS, {
      method: 'POST',
      body: {
        requestId: requestId(1),
        typeId: TASK,
        title: 'Задача',
        teamId: 'c2999999-0000-4000-8000-000000000000',
        parentId: 'not-an-id',
      },
    }).catch((error: unknown) => error);
    const deleted = await apiFetch(WORK_ITEMS, {
      method: 'POST',
      body: {
        requestId: requestId(2),
        typeId: processType('FEAT').id,
        title: 'Фича',
        parentId: mskRecord(data, 2).id,
      },
    }).catch((error: unknown) => error);

    expect((team as ApiError).errorCode).toBe('organizations.team.not_found');
    expect((deleted as ApiError).errorCode).toBe('workitems.link.item_deleted');
  });
});
