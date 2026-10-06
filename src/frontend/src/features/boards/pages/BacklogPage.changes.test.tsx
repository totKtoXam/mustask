import { afterEach, describe, expect, it } from 'vitest';
import { act, screen, waitFor, within } from '@testing-library/react';
import { focusManager } from '@tanstack/react-query';
import { serveBoards, type BoardsTestApi } from '../testApi';
import { boardsDataset, ids, item, platformItem, type BoardsDataset } from '../testFixtures';
import { backlogAddress, renderBoards } from '../testRender';

// US-7 (pass A's scenarios): changes made elsewhere — on a form, in «Области»,
// a team deleted — show on the next read, with no notice (BR-M4-28). The
// change is made on the in-memory API as another screen's request would make
// it; the page shows it once it reads again — here when its tab regains focus.

afterEach(() => {
  focusManager.setFocused(undefined);
});

async function backlogTable() {
  return screen.findByRole('table', { name: 'Бэклог' });
}

function identifiers(table: HTMLElement): string[] {
  return within(table)
    .getAllByRole('row')
    .slice(1)
    .map((row) => within(row).getAllByRole('cell')[2]?.textContent ?? '');
}

function returnToTab() {
  act(() => {
    focusManager.setFocused(false);
    focusManager.setFocused(true);
  });
}

function place(data: BoardsDataset, teamId: string, numbers: readonly number[]) {
  numbers.forEach((number, index) => {
    const record = data.items.find((candidate) => candidate.number === number);
    if (!record) throw new Error(`No item ${number}.`);
    data.places.push({ itemId: record.id, teamId, level: 'stories', position: index + 1 });
  });
}

/** US-7's data: «Мастаск»'s «Истории» reordered to MSK-US-5, MSK-US-7. */
function changesDataset(): BoardsDataset {
  const data = boardsDataset();
  data.items.push(
    item(ids.msk, 5, 'US', 'Сводка проекта', { areaId: ids.mskRoot }),
    item(ids.msk, 7, 'US', 'Отчёт за квартал', { areaId: ids.reports }),
  );
  place(data, ids.mskTeam, [5, 7]);
  return data;
}

function record(api: BoardsTestApi, number: number) {
  return api.item(ids.msk, number);
}

describe('US-7 Scenario: A new Область moves the item to the other team’s backlog', () => {
  it('MSK-US-3 set to «Отчёты» leaves «Платформа» on the next read and is last on «Мастаск»', async () => {
    const data = changesDataset();
    data.items.push(platformItem(3, 'US', 'Экспорт'));
    const api = serveBoards(data);
    renderBoards(backlogAddress());
    const table = await backlogTable();
    expect(identifiers(table)).toEqual(['MSK-US-3']);

    record(api, 3).areaId = ids.reports;
    expect(identifiers(table)).toEqual(['MSK-US-3']);
    expect(screen.queryByRole('alert')).toBeNull();
    returnToTab();

    expect(await screen.findByRole('heading', { level: 2, name: 'Бэклог пуст' })).toBeVisible();
    expect(api.order(ids.mskTeam)).toEqual(['MSK-US-5', 'MSK-US-7', 'MSK-US-3']);
  });
});

describe('US-7 Scenario: An area given another owner takes its items along', () => {
  it('«Мастаск» made the owner of «Платформа / API» takes MSK-US-3 and MSK-BUG-4 to its backlog, last', async () => {
    const data = changesDataset();
    data.items.push(
      platformItem(3, 'US', 'Экспорт', { areaId: ids.api }),
      platformItem(4, 'BUG', 'Падение', { areaId: ids.api }),
      platformItem(9, 'US', 'Остаётся'),
    );
    const api = serveBoards(data);
    renderBoards(backlogAddress());
    const table = await backlogTable();
    expect(identifiers(table)).toEqual(['MSK-US-3', 'MSK-BUG-4', 'MSK-US-9']);

    api.data.owners[ids.api] = ids.mskTeam;
    returnToTab();

    await waitFor(() => expect(identifiers(table)).toEqual(['MSK-US-9']));
    expect(api.order(ids.mskTeam)).toEqual(['MSK-US-5', 'MSK-US-7', 'MSK-US-3', 'MSK-BUG-4']);
  });
});

describe('US-7 Scenario: An area moved under another parent takes its items along', () => {
  it('«Платформа / API» moved under the root takes MSK-US-3 to the end of «Мастаск»', async () => {
    const data = changesDataset();
    data.items.push(platformItem(3, 'US', 'Экспорт', { areaId: ids.api }));
    const api = serveBoards(data);
    renderBoards(backlogAddress('stories', ids.mskTeam));
    const table = await backlogTable();
    expect(identifiers(table)).toEqual(['MSK-US-5', 'MSK-US-7']);

    api.data.areas = api.data.areas.map((area) =>
      area.id === ids.api ? { ...area, parentId: ids.mskRoot } : area,
    );
    returnToTab();

    await waitFor(() => expect(identifiers(table)).toEqual(['MSK-US-5', 'MSK-US-7', 'MSK-US-3']));
  });
});

describe('US-7 Scenario: An item that comes back before anything was ordered returns', () => {
  it('MSK-US-9 away to «Отчёты» and back, with no reorder between, is first on «Платформа» again', async () => {
    const data = changesDataset();
    data.items.push(
      platformItem(3, 'US', 'Три'),
      platformItem(9, 'US', 'Девять'),
      platformItem(11, 'US', 'Одиннадцать'),
    );
    place(data, ids.platformTeam, [9, 3, 11]);
    const api = serveBoards(data);
    renderBoards(backlogAddress());
    const table = await backlogTable();
    expect(identifiers(table)).toEqual(['MSK-US-9', 'MSK-US-3', 'MSK-US-11']);

    record(api, 9).areaId = ids.reports;
    returnToTab();
    await waitFor(() => expect(identifiers(table)).toEqual(['MSK-US-3', 'MSK-US-11']));
    record(api, 9).areaId = ids.platform;
    returnToTab();

    await waitFor(() => expect(identifiers(table)).toEqual(['MSK-US-9', 'MSK-US-3', 'MSK-US-11']));
  });
});

describe('US-7 Scenario: An item placed elsewhere meanwhile comes back last', () => {
  it('MSK-US-9 placed on «Мастаск» while away comes back below the placed rows of «Платформа»', async () => {
    const data = changesDataset();
    data.items.push(
      platformItem(3, 'US', 'Три'),
      platformItem(9, 'US', 'Девять'),
      platformItem(11, 'US', 'Одиннадцать'),
    );
    place(data, ids.platformTeam, [9, 3, 11]);
    const api = serveBoards(data);
    renderBoards(backlogAddress());
    const table = await backlogTable();

    record(api, 9).areaId = ids.reports;
    api.reorderAsAnotherUser(ids.mskTeam, 'stories', {
      itemId: record(api, 9).id,
      position: 'top',
    });
    record(api, 9).areaId = ids.platform;
    returnToTab();

    await waitFor(() => expect(identifiers(table)).toEqual(['MSK-US-3', 'MSK-US-11', 'MSK-US-9']));
  });
});

describe('US-7 Scenario: A deleted team’s items go to the first team, in creation order', () => {
  it('«Аналитика»’s backlog answers «Команда не найдена»; «Мастаск» reads MSK-US-5, MSK-US-7, MSK-US-21, MSK-US-30', async () => {
    const data = changesDataset();
    data.items.push(
      item(ids.msk, 21, 'US', 'Двадцать один', { areaId: ids.analyticsArea }),
      item(ids.msk, 30, 'US', 'Тридцать', { areaId: ids.analyticsArea }),
    );
    place(data, ids.analyticsTeam, [30, 21]);
    const api = serveBoards(data);
    const view = renderBoards(backlogAddress('stories', ids.analyticsTeam));
    expect(identifiers(await backlogTable())).toEqual(['MSK-US-30', 'MSK-US-21']);

    // «Аналитика» is deleted: its areas pass to the first team (mod-002).
    api.data.teams = api.data.teams.map((team) =>
      team.id === ids.analyticsTeam ? { ...team, active: false } : team,
    );
    api.data.owners[ids.analyticsArea] = ids.mskTeam;
    returnToTab();

    expect(
      await screen.findByRole('heading', { level: 1, name: 'Команда не найдена' }),
    ).toBeInTheDocument();
    expect(screen.queryByRole('alert')).toBeNull();

    view.unmount();
    renderBoards(backlogAddress('stories', ids.mskTeam));
    expect(identifiers(await backlogTable())).toEqual([
      'MSK-US-5',
      'MSK-US-7',
      'MSK-US-21',
      'MSK-US-30',
    ]);
  });
});
