import { describe, expect, it } from 'vitest';
import { ApiError } from '../../../shared/api/httpClient';
import { serveBoards } from '../testApi';
import { ids, item, platformItem, projectDataset, stateId } from '../testFixtures';
import {
  getBacklog,
  getBoard,
  getProjectBacklog,
  getProjectBoard,
  reorderProjectBacklog,
} from './boardsApi';

// feat-039 api-contract § 1..§ 3 through the panel's own API layer, against the in-memory API
// the component tests use — which is what keeps that API to the contract. `projectDataset`:
// MSK-US-2, MSK-US-3 and MSK-US-9 on three teams’ surfaces of «Мастаск», OPS-US-1 in «Операции».

const ORG = ids.example;
const MSK = ids.msk;

async function failure(promise: Promise<unknown>): Promise<ApiError> {
  try {
    await promise;
  } catch (error) {
    if (error instanceof ApiError) return error;
    throw error;
  }
  throw new Error('The call did not fail.');
}

describe('getProjectBacklog: every item of the project at the level (api-contract § 1, § 3.1)', () => {
  it('answers the project’s id and name, the level, and the stories of every team in the project’s order, «Порядок» 1..n', async () => {
    serveBoards(projectDataset());

    const backlog = await getProjectBacklog(ORG, MSK, 'stories');

    expect(backlog.project).toEqual({ id: MSK, name: 'Мастаск' });
    expect(backlog.level.code).toBe('stories');
    expect(backlog.items.map((row) => [row.order, row.identifier])).toEqual([
      [1, 'MSK-US-2'],
      [2, 'MSK-US-3'],
      [3, 'MSK-US-9'],
    ]);
    expect(backlog).not.toHaveProperty('team');
  });

  it('puts the placed items first, then the unplaced ones by creation time, then by number', async () => {
    const data = projectDataset();
    for (const record of data.items) {
      record.createdAt = '2026-10-01T10:00:00Z';
    }
    const earlier = platformItem(12, 'US', 'Раньше', { createdAt: '2026-10-01T08:00:00Z' });
    const middle = platformItem(10, 'US', 'Позже', { createdAt: '2026-10-01T09:00:00Z' });
    const same = platformItem(11, 'US', 'Одновременно', { createdAt: '2026-10-01T09:00:00Z' });
    data.items.push(middle, same, earlier);
    const us9 = data.items.find((record) => record.number === 9);
    if (!us9) throw new Error('No MSK-US-9.');
    data.projectPlaces.push({ itemId: us9.id, level: 'stories', position: 1 });
    serveBoards(data);

    const { items } = await getProjectBacklog(ORG, MSK, 'stories');

    // The placed MSK-US-9; then 08:00, the two of 09:00 by number, and the two of 10:00 by number.
    expect(items.map((row) => row.identifier)).toEqual([
      'MSK-US-9',
      'MSK-US-12',
      'MSK-US-10',
      'MSK-US-11',
      'MSK-US-2',
      'MSK-US-3',
    ]);
  });

  it('leaves out an item in the Removed category and an item of another level', async () => {
    const data = projectDataset();
    data.items.push(
      platformItem(7, 'US', 'Удалённая', { stateId: stateId('US', 'Удалена') }),
      item(ids.msk, 8, 'FEAT', 'Фича', { areaId: ids.analyticsArea }),
    );
    serveBoards(data);

    const { items } = await getProjectBacklog(ORG, MSK, 'stories');

    expect(items.map((row) => row.identifier)).toEqual(['MSK-US-2', 'MSK-US-3', 'MSK-US-9']);
  });

  it('takes one request, to the project’s own route', async () => {
    const api = serveBoards(projectDataset());

    await getProjectBacklog(ORG, MSK, 'epics');

    expect(api.requests.map((request) => `${request.method} ${request.path}`)).toEqual([
      `GET /api/organizations/${ORG}/projects/${MSK}/backlogs/epics`,
    ]);
  });

  it('a team’s backlog is still its own: «Платформа» reads MSK-US-3 and the answer names the team', async () => {
    serveBoards(projectDataset());

    const backlog = await getBacklog(ORG, MSK, ids.platformTeam, 'stories');

    expect(backlog.items.map((row) => row.identifier)).toEqual(['MSK-US-3']);
    expect(backlog.team.name).toBe('Платформа');
  });
});

describe('getProjectBoard: the same items in the process’s columns (api-contract § 3.3)', () => {
  it('answers the project and the columns with the cards of every team, each once', async () => {
    serveBoards(projectDataset());

    const board = await getProjectBoard(ORG, MSK, 'stories');

    expect(board.project).toEqual({ id: MSK, name: 'Мастаск' });
    const first = board.columns[0];
    expect(first?.items.map((card) => card.identifier)).toEqual([
      'MSK-US-2',
      'MSK-US-3',
      'MSK-US-9',
    ]);
    expect(board.columns.flatMap((column) => column.items)).toHaveLength(3);
    expect(board).not.toHaveProperty('team');
  });

  it('a team’s board still holds its team’s cards only', async () => {
    serveBoards(projectDataset());

    const board = await getBoard(ORG, MSK, ids.platformTeam, 'stories');

    expect(board.columns.flatMap((column) => column.items.map((card) => card.identifier))).toEqual([
      'MSK-US-3',
    ]);
  });
});

describe('reorderProjectBacklog: one item moves on the project’s backlog (api-contract § 3.2)', () => {
  it('answers 204 and writes the project’s places only, none of a team’s', async () => {
    const api = serveBoards(projectDataset());

    await reorderProjectBacklog(ORG, MSK, 'stories', {
      itemId: api.item(MSK, 9).id,
      position: 'top',
    });

    expect(api.projectOrder(MSK)).toEqual(['MSK-US-9', 'MSK-US-2', 'MSK-US-3']);
    expect(api.data.places).toEqual([]);
    expect(api.requests.map((request) => `${request.method} ${request.path}`)).toEqual([
      `POST /api/organizations/${ORG}/projects/${MSK}/backlogs/stories/reorder`,
    ]);
  });

  it('refuses an item of another project like one that names nothing: 409 `boards.backlog.reorder_stale`, nothing changes', async () => {
    const api = serveBoards(projectDataset());

    const error = await failure(
      reorderProjectBacklog(ORG, MSK, 'stories', {
        itemId: api.item(MSK, 2).id,
        position: 'after',
        afterItemId: api.item(ids.ops, 1).id,
      }),
    );

    expect(error.status).toBe(409);
    expect(error.errorCode).toBe('boards.backlog.reorder_stale');
    expect(api.data.projectPlaces).toEqual([]);
  });
});

describe('the answers from the outside in (api-contract § 0.3)', () => {
  it('a project that names nothing is `organizations.project.not_found`, a level the page does not serve `boards.level.not_found`', async () => {
    serveBoards(projectDataset());

    const project = await failure(
      getProjectBacklog(ORG, 'f0000000-0000-4000-8000-0000000000ff', 'stories'),
    );
    const level = await failure(getProjectBoard(ORG, MSK, 'tasks' as unknown as 'stories'));

    expect(project.errorCode).toBe('organizations.project.not_found');
    expect(level.errorCode).toBe('boards.level.not_found');
  });

  it('an organization that names nothing is `organizations.organization.not_found`, told before the project', async () => {
    serveBoards(projectDataset());

    const error = await failure(
      getProjectBacklog('e0000000-0000-4000-8000-0000000000ff', MSK, 'stories'),
    );

    expect(error.errorCode).toBe('organizations.organization.not_found');
  });

  it('a project of another organization answers like one that names nothing', async () => {
    serveBoards(projectDataset());

    const error = await failure(getProjectBacklog(ORG, ids.otherMsk, 'stories'));

    expect(error.errorCode).toBe('organizations.project.not_found');
  });
});
