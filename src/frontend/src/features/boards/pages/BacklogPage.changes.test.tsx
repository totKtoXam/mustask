import { afterEach, describe, expect, it } from 'vitest';
import { act, screen, waitFor } from '@testing-library/react';
import { focusManager } from '@tanstack/react-query';
import { serveBoards, type BoardsTestApi } from '../testApi';
import { findBacklogTable, identifiers } from '../testBacklog';
import { boardsDataset, ids, item, platformItem, type BoardsDataset } from '../testFixtures';
import { backlogAddress, renderBoards } from '../testRender';

// US-7 (pass A's scenarios): changes made elsewhere — on a form, in «Области»,
// a team deleted — show on the next read, with no notice (BR-M4-28). The
// change is made on the in-memory API as another screen's request would make
// it; the page shows it once it reads again — here when its tab regains focus.

afterEach(() => {
  focusManager.setFocused(undefined);
});

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

// mod-017 BR-OT-1, BR-OT-5, BR-OT-6, BR-OT-10: a team's backlog holds what the team owns; an
// area decides nothing. Retired by name (doc-012 P-1; spec "Existing rules"): «An area given
// another owner takes its items along», «An area moved under another parent takes its items
// along», «An item that comes back before anything was ordered returns». Rewritten over the
// owning team: the first and the last two scenarios below.

describe('US-7 Scenario: An item given another owning team moves to the other team’s backlog', () => {
  it('MSK-US-3 given to «Мастаск» leaves «Платформа» on the next read and is last on «Мастаск»', async () => {
    const data = changesDataset();
    data.items.push(platformItem(3, 'US', 'Экспорт'));
    const api = serveBoards(data);
    renderBoards(backlogAddress());
    const table = await findBacklogTable();
    expect(identifiers(table)).toEqual(['MSK-US-3']);

    api.giveTeam(ids.msk, 3, ids.mskTeam);
    expect(identifiers(table)).toEqual(['MSK-US-3']);
    expect(screen.queryByRole('alert')).toBeNull();
    returnToTab();

    expect(await screen.findByRole('heading', { level: 2, name: 'Бэклог пуст' })).toBeVisible();
    expect(api.order(ids.mskTeam)).toEqual(['MSK-US-5', 'MSK-US-7', 'MSK-US-3']);
  });
});

describe('US-7 Scenario: An area moved changes no team', () => {
  it('«Платформа / API» moved under the root leaves MSK-US-3 on «Платформа»’s backlog', async () => {
    const data = changesDataset();
    data.items.push(platformItem(3, 'US', 'Экспорт', { areaId: ids.api }));
    const api = serveBoards(data);
    renderBoards(backlogAddress('stories', ids.mskTeam));
    const table = await findBacklogTable();
    expect(identifiers(table)).toEqual(['MSK-US-5', 'MSK-US-7']);

    api.data.areas = api.data.areas.map((area) =>
      area.id === ids.api ? { ...area, parentId: ids.mskRoot } : area,
    );
    returnToTab();

    await waitFor(() => expect(api.reads('/backlogs/stories').length).toBeGreaterThan(1));
    expect(identifiers(table)).toEqual(['MSK-US-5', 'MSK-US-7']);
    expect(api.order(ids.platformTeam)).toEqual(['MSK-US-3']);
  });
});

describe('US-7 Scenario: An item given another team and given back returns unplaced, last', () => {
  it('MSK-US-9 away to «Мастаск» and back is last on «Платформа»: the team place went with the change (BR-OT-6)', async () => {
    const data = changesDataset();
    data.items.push(
      platformItem(3, 'US', 'Три'),
      platformItem(9, 'US', 'Девять'),
      platformItem(11, 'US', 'Одиннадцать'),
    );
    place(data, ids.platformTeam, [9, 3, 11]);
    const api = serveBoards(data);
    renderBoards(backlogAddress());
    const table = await findBacklogTable();
    expect(identifiers(table)).toEqual(['MSK-US-9', 'MSK-US-3', 'MSK-US-11']);

    api.giveTeam(ids.msk, 9, ids.mskTeam);
    returnToTab();
    await waitFor(() => expect(identifiers(table)).toEqual(['MSK-US-3', 'MSK-US-11']));
    api.giveTeam(ids.msk, 9, ids.platformTeam);
    returnToTab();

    await waitFor(() => expect(identifiers(table)).toEqual(['MSK-US-3', 'MSK-US-11', 'MSK-US-9']));
  });

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
    const table = await findBacklogTable();

    api.giveTeam(ids.msk, 9, ids.mskTeam);
    api.reorderAsAnotherUser(ids.mskTeam, 'stories', {
      itemId: record(api, 9).id,
      position: 'top',
    });
    api.giveTeam(ids.msk, 9, ids.platformTeam);
    returnToTab();

    await waitFor(() => expect(identifiers(table)).toEqual(['MSK-US-3', 'MSK-US-11', 'MSK-US-9']));
  });
});

describe('US-7 Scenario: A deleted team’s items have no team', () => {
  it('«Аналитика»’s backlog answers «Команда не найдена»; «Мастаск» reads MSK-US-5, MSK-US-7 only', async () => {
    const data = changesDataset();
    data.items.push(
      item(ids.msk, 21, 'US', 'Двадцать один', { areaId: ids.analyticsArea }),
      item(ids.msk, 30, 'US', 'Тридцать', { areaId: ids.analyticsArea }),
    );
    place(data, ids.analyticsTeam, [30, 21]);
    const api = serveBoards(data);
    const view = renderBoards(backlogAddress('stories', ids.analyticsTeam));
    expect(identifiers(await findBacklogTable())).toEqual(['MSK-US-30', 'MSK-US-21']);

    // «Аналитика» is deleted: its items go to «Без команды» (mod-017 BR-OT-12), not to a first team.
    api.deleteTeam(ids.analyticsTeam);
    returnToTab();

    expect(
      await screen.findByRole('heading', { level: 1, name: 'Команда не найдена' }),
    ).toBeInTheDocument();
    expect(screen.queryByRole('alert')).toBeNull();

    view.unmount();
    renderBoards(backlogAddress('stories', ids.mskTeam));
    expect(identifiers(await findBacklogTable())).toEqual(['MSK-US-5', 'MSK-US-7']);
    expect(record(api, 21).teamId).toBeNull();
  });
});
