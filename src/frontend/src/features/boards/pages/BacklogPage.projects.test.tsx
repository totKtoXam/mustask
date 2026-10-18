import { afterEach, describe, expect, it, vi } from 'vitest';
import { act, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { focusManager } from '@tanstack/react-query';
import { HttpResponse, delay, http } from 'msw';
import { server } from '../../../test/mocks/server';
import { serveBoards } from '../testApi';
import {
  CELL,
  actions,
  announcer,
  cellOf,
  cellText,
  findBacklogTable,
  identifiers,
  orders,
  rowOf,
  titleLink,
} from '../testBacklog';
import { BACKLOG_URL, acrossProjectsDataset, createdAt, ids, item } from '../testFixtures';
import { chooseLevel } from '../testLevels';
import { backlogAddress, boardAddress, renderBoards } from '../testRender';
import {
  chooseProjects,
  filterBand,
  here,
  projectField,
  projectOptions,
} from '../testProjectFilter';

// feat-040 US-1..US-4 on a team's backlog: «Платформа» works on «Мастаск» (`MSK`) and «Операции» (`OPS`)
// and owns MSK-US-3, OPS-US-1 and OPS-US-2 (`acrossProjectsDataset`); other teams' items and a
// teamless one stand in the same projects. The card inside the first tests is slow in jsdom.

vi.setConfig({ testTimeout: 60_000 });

afterEach(() => {
  focusManager.setFocused(undefined);
});

const TEAM_ADDRESS = `/api/organizations/${ids.example}/teams/${ids.platformTeam}/backlogs/stories`;
const ALL = ['MSK-US-3', 'OPS-US-1', 'OPS-US-2'];

describe('feat-040 US-1: a team’s backlog holds the team’s items of every project, at the team’s address (BR-TS-1, BR-TS-2)', () => {
  it('lists MSK-US-3, OPS-US-1 and OPS-US-2 with «Порядок» 1 to 3, and no item of another team; the read names the team alone', async () => {
    const api = serveBoards(acrossProjectsDataset());
    renderBoards(backlogAddress());

    const table = await findBacklogTable();

    expect(identifiers(table)).toEqual(ALL);
    expect(orders(table)).toEqual(['1', '2', '3']);
    expect(table).not.toHaveTextContent('MSK-US-9');
    expect(table).not.toHaveTextContent('OPS-US-3');
    expect(table).not.toHaveTextContent('OPS-US-4');
    expect(api.reads('/backlogs/stories').map((request) => request.path)).toEqual([TEAM_ADDRESS]);
  });

  it('draws «Команда» and «Проект» on every row, the project as the name it has now', async () => {
    const data = acrossProjectsDataset();
    const ops = data.projects.find((project) => project.id === ids.ops);
    if (!ops) throw new Error('No project.');
    ops.name = 'Склад и логистика';
    serveBoards(data);
    renderBoards(backlogAddress());

    const table = await findBacklogTable();

    expect(cellText(rowOf(table, 'MSK-US-3'), CELL.team)).toBe('Платформа');
    expect(cellText(rowOf(table, 'MSK-US-3'), CELL.project)).toBe('Мастаск');
    expect(cellText(rowOf(table, 'OPS-US-1'), CELL.project)).toBe('Склад и логистика');
    expect(cellText(rowOf(table, 'OPS-US-2'), CELL.team)).toBe('Платформа');
    // A name that does not fit is cut, and the cell keeps it whole as its title.
    expect(cellOf(rowOf(table, 'OPS-US-1'), CELL.project).querySelector('span')).toHaveAttribute(
      'title',
      'Склад и логистика',
    );
  });

  it('puts the unplaced items in one order across projects: creation time, then the project’s key by character code, then the number as a number (BR-TS-6)', async () => {
    const data = acrossProjectsDataset();
    const same = createdAt(30);
    data.items = [
      item(ids.ops, 1, 'US', 'Первая', { teamId: ids.platformTeam, createdAt: same }),
      item(ids.msk, 10, 'US', 'Десятая', { teamId: ids.platformTeam, createdAt: same }),
      item(ids.msk, 9, 'US', 'Девятая', { teamId: ids.platformTeam, createdAt: same }),
      item(ids.ops, 5, 'US', 'Ранняя', { teamId: ids.platformTeam, createdAt: createdAt(1) }),
    ];
    serveBoards(data);
    renderBoards(backlogAddress());

    const table = await findBacklogTable();

    expect(identifiers(table)).toEqual(['OPS-US-5', 'MSK-US-9', 'MSK-US-10', 'OPS-US-1']);
  });

  it('shows the views of a team that works on one project as it did: its items, and the project on each row', async () => {
    serveBoards(acrossProjectsDataset());
    renderBoards(backlogAddress('stories', ids.analyticsTeam));

    const table = await findBacklogTable();

    expect(identifiers(table)).toEqual(['MSK-US-9']);
    expect(cellText(rowOf(table, 'MSK-US-9'), CELL.project)).toBe('Мастаск');
  });
});

describe('feat-040 US-2: the project filter (BR-TS-8)', () => {
  it('offers the team’s projects by name, no counts, and none that is not its own', async () => {
    serveBoards(acrossProjectsDataset());
    renderBoards(backlogAddress());
    await findBacklogTable();

    expect(await projectOptions()).toEqual(['Мастаск', 'Операции']);
    expect(screen.getByText('Все проекты')).toBeInTheDocument();
  });

  it('writes a choice into the address as the repeated `projectId`, narrows and renumbers the rows, and sends it to the read; the team order is untouched', async () => {
    const api = serveBoards(acrossProjectsDataset());
    renderBoards(backlogAddress());
    const table = await findBacklogTable();

    await chooseProjects('Операции');

    await waitFor(() => expect(identifiers(table)).toEqual(['OPS-US-1', 'OPS-US-2']));
    expect(orders(table)).toEqual(['1', '2']);
    expect(here()).toEqual({
      pathname: backlogAddress(),
      search: `?projectId=${ids.ops}`,
    });
    expect(projectField()).toHaveTextContent('Операции');
    expect(api.reads('/backlogs/stories').at(-1)?.path).toBe(
      `${TEAM_ADDRESS}?projectId=${ids.ops}`,
    );
    // The filter narrows the view and changes nothing: the whole order is still there to read.
    expect(api.order(ids.platformTeam)).toEqual(ALL);
  });

  it('keeps several values in the address’s order; any chosen value matches', async () => {
    const api = serveBoards(acrossProjectsDataset());
    renderBoards(`${backlogAddress()}?projectId=${ids.ops}&projectId=${ids.msk}`);
    const table = await findBacklogTable();

    expect(identifiers(table)).toEqual(ALL);
    expect(projectField()).toHaveTextContent('Мастаск, Операции');
    expect(api.reads('/backlogs/stories')[0]?.path).toBe(
      `${TEAM_ADDRESS}?projectId=${ids.ops}&projectId=${ids.msk}`,
    );
  });

  it('is carried by a link and a reload: the address alone narrows the first read', async () => {
    serveBoards(acrossProjectsDataset());
    renderBoards(`${backlogAddress()}?projectId=${ids.msk}`);

    const table = await findBacklogTable();

    expect(identifiers(table)).toEqual(['MSK-US-3']);
    expect(projectField()).toHaveTextContent('Мастаск');
    expect(within(filterBand()).getByRole('button', { name: 'Очистить фильтры' })).toBeVisible();
  });

  it('«Очистить фильтры» takes the values out of the address and brings every row back', async () => {
    serveBoards(acrossProjectsDataset());
    renderBoards(`${backlogAddress()}?projectId=${ids.msk}`);
    const table = await findBacklogTable();

    await userEvent.click(within(filterBand()).getByRole('button', { name: 'Очистить фильтры' }));

    await waitFor(() => expect(here().search).toBe(''));
    await waitFor(() => expect(identifiers(table)).toEqual(ALL));
    expect(screen.queryByRole('button', { name: 'Очистить фильтры' })).toBeNull();
  });

  it('is kept by the level menu, the tab and «Показать канбан», and dropped by the team menu', async () => {
    serveBoards(acrossProjectsDataset());
    renderBoards(`${backlogAddress()}?projectId=${ids.ops}`);
    await findBacklogTable();
    const filter = `?projectId=${ids.ops}`;

    expect(screen.getByRole('tab', { name: 'Бэклог' })).toHaveAttribute(
      'href',
      `${backlogAddress()}${filter}`,
    );
    expect(screen.getByRole('link', { name: 'Показать канбан' })).toHaveAttribute(
      'href',
      `${boardAddress()}${filter}`,
    );
    await chooseLevel('Фичи');
    await waitFor(() =>
      expect(here()).toEqual({ pathname: backlogAddress('features'), search: filter }),
    );

    await userEvent.click(screen.getByRole('button', { name: 'Сменить команду' }));
    const entry = await screen.findByRole('menuitem', { name: 'Аналитика' });
    expect(entry).toHaveAttribute('href', backlogAddress('features', ids.analyticsTeam));
  });

  it('keeps the rows on screen while another filter is read, marked busy, and shows no skeleton', async () => {
    serveBoards(acrossProjectsDataset());
    renderBoards(backlogAddress());
    const table = await findBacklogTable();
    let release: () => void = () => {};
    const held = new Promise<void>((resolve) => {
      release = resolve;
    });
    server.use(
      http.get(BACKLOG_URL, async ({ request }) => {
        if (new URL(request.url).searchParams.has('projectId')) {
          await held;
          await delay(0);
        }
        return undefined;
      }),
    );

    await chooseProjects('Мастаск');

    await waitFor(() => expect(table).toHaveAttribute('aria-busy', 'true'));
    expect(identifiers(table)).toEqual(ALL);
    expect(screen.queryByRole('status', { name: 'Загрузка данных' })).toBeNull();

    release();
    await waitFor(() => expect(identifiers(table)).toEqual(['MSK-US-3']));
    expect(table).not.toHaveAttribute('aria-busy');
  });

  it('shows the error banner in place of the rows when the read of a filter fails', async () => {
    serveBoards(acrossProjectsDataset());
    renderBoards(backlogAddress());
    await findBacklogTable();
    server.use(
      http.get(BACKLOG_URL, ({ request }) =>
        new URL(request.url).searchParams.has('projectId')
          ? HttpResponse.json({ status: 500, title: 'Ошибка' }, { status: 500 })
          : undefined,
      ),
    );

    await chooseProjects('Мастаск');

    expect(await screen.findByRole('alert')).toBeVisible();
    expect(
      within(screen.getByRole('alert')).getByRole('button', { name: 'Повторить' }),
    ).toBeVisible();
    // The filter is still on, and can still be cleared.
    expect(here().search).toBe(`?projectId=${ids.msk}`);
    expect(within(filterBand()).getByRole('button', { name: 'Очистить фильтры' })).toBeVisible();
    // The team's projects do not depend on the filter: the field keeps offering the ones it knew.
    expect(projectField()).toHaveTextContent('Мастаск');
    expect(await projectOptions()).toEqual(['Мастаск', 'Операции']);
    expect(screen.queryByRole('table', { name: 'Бэклог' })).toBeNull();
  });

  it('a value that names no project the team works on selects nothing, names no project and no reason, and can be cleared (alike for unknown, malformed and foreign ids)', async () => {
    serveBoards(acrossProjectsDataset());
    renderBoards(`${backlogAddress()}?projectId=not-a-project&projectId=${ids.otherMsk}`);

    expect(
      await screen.findByRole('heading', { level: 2, name: 'Ничего не найдено' }),
    ).toBeVisible();
    expect(
      screen.getByText('Ни один рабочий элемент не подходит под заданные условия фильтра.'),
    ).toBeVisible();
    expect(screen.getByText('Фильтр задан')).toBeVisible();
    expect(screen.queryByText('Все проекты')).toBeNull();

    await userEvent.click(screen.getByRole('button', { name: 'Сбросить фильтр' }));

    expect(identifiers(await findBacklogTable())).toEqual(ALL);
    expect(here().search).toBe('');
  });

  it('beside a valid value an invalid one adds nothing: the valid project’s items show', async () => {
    serveBoards(acrossProjectsDataset());
    renderBoards(`${backlogAddress()}?projectId=${ids.ops}&projectId=not-a-project`);

    const table = await findBacklogTable();

    expect(identifiers(table)).toEqual(['OPS-US-1', 'OPS-US-2']);
    expect(projectField()).toHaveTextContent('Операции');
    expect(screen.queryByText('Фильтр задан')).toBeNull();
  });

  it('a chosen project deactivated meanwhile stays in the address, is no option, and selects nothing; restored, it selects again', async () => {
    const api = serveBoards(acrossProjectsDataset());
    renderBoards(`${backlogAddress()}?projectId=${ids.ops}`);
    const table = await findBacklogTable();
    expect(identifiers(table)).toEqual(['OPS-US-1', 'OPS-US-2']);

    const ops = api.data.projects.find((project) => project.id === ids.ops);
    if (!ops) throw new Error('No project.');
    ops.active = false;
    act(() => {
      focusManager.setFocused(false);
      focusManager.setFocused(true);
    });

    expect(
      await screen.findByRole('heading', { level: 2, name: 'Ничего не найдено' }),
    ).toBeVisible();
    expect(here().search).toBe(`?projectId=${ids.ops}`);
    expect(screen.getByText('Фильтр задан')).toBeVisible();
    expect(await projectOptions()).toEqual(['Мастаск']);

    ops.active = true;
    act(() => {
      focusManager.setFocused(false);
      focusManager.setFocused(true);
    });

    await waitFor(() => expect(identifiers(table)).toEqual(['OPS-US-1', 'OPS-US-2']));
  });
});

describe('feat-040 US-3: one team order across projects (BR-TS-6, BR-TS-7)', () => {
  it('a move across projects writes the team’s order and no project’s; the same order after a reload', async () => {
    const api = serveBoards(acrossProjectsDataset());
    const view = renderBoards(backlogAddress());
    const table = await findBacklogTable();

    await userEvent.click(actions('OPS-US-2'));
    await userEvent.click(await screen.findByRole('menuitem', { name: 'В начало' }));

    await waitFor(() => expect(identifiers(table)).toEqual(['OPS-US-2', 'MSK-US-3', 'OPS-US-1']));
    expect(announcer()).toHaveTextContent('OPS-US-2 перемещён: позиция 1 из 3.');
    await waitFor(() => expect(actions('OPS-US-2')).not.toHaveAttribute('aria-disabled'));
    expect(api.order(ids.platformTeam)).toEqual(['OPS-US-2', 'MSK-US-3', 'OPS-US-1']);
    expect(api.data.projectPlaces).toEqual([]);
    const reorders = api.writes().filter((request) => request.path.endsWith('/reorder'));
    expect(reorders.map((request) => [request.path, request.body])).toEqual([
      [`${TEAM_ADDRESS}/reorder`, { itemId: api.item(ids.ops, 2).id, position: 'top' }],
    ]);

    view.unmount();
    renderBoards(backlogAddress());
    expect(identifiers(await findBacklogTable())).toEqual(['OPS-US-2', 'MSK-US-3', 'OPS-US-1']);
  });

  it('a move on a filtered backlog is sent against the full list: the request carries no filter, the hidden row keeps its place, the position is told among the visible rows', async () => {
    const api = serveBoards(acrossProjectsDataset());
    renderBoards(`${backlogAddress()}?projectId=${ids.ops}`);
    const table = await findBacklogTable();
    expect(identifiers(table)).toEqual(['OPS-US-1', 'OPS-US-2']);

    await userEvent.click(actions('OPS-US-2'));
    await userEvent.click(await screen.findByRole('menuitem', { name: 'В начало' }));

    await waitFor(() => expect(identifiers(table)).toEqual(['OPS-US-2', 'OPS-US-1']));
    expect(announcer()).toHaveTextContent('OPS-US-2 перемещён: позиция 1 из 2.');
    await waitFor(() => expect(actions('OPS-US-2')).not.toHaveAttribute('aria-disabled'));
    expect(api.order(ids.platformTeam)).toEqual(['OPS-US-2', 'MSK-US-3', 'OPS-US-1']);
    const [reorder] = api.writes().filter((request) => request.path.includes('/reorder'));
    expect(reorder?.path).toBe(`${TEAM_ADDRESS}/reorder`);
    expect(api.reads('/backlogs/stories').at(-1)?.path).toContain(`projectId=${ids.ops}`);
  });
});

describe('feat-040 US-4: an item is read and written in its own project (BR-TS-4, BR-TS-5)', () => {
  it('a title opens the card over the page, and reads it in the item’s own project; the card’s «Проект» is read-only', async () => {
    const api = serveBoards(acrossProjectsDataset());
    renderBoards(backlogAddress());
    const table = await findBacklogTable();

    await userEvent.click(titleLink(rowOf(table, 'OPS-US-1')));

    const dialog = await screen.findByRole('dialog', {
      name: 'Пользовательская история OPS-US-1',
    });
    expect(within(dialog).getByRole('textbox', { name: 'Проект' })).toHaveValue('Операции');
    expect(within(dialog).getByRole('textbox', { name: 'Проект' })).toHaveAttribute('readonly');
    expect(api.reads('/workitems/OPS-US-1').map((request) => request.path)).toEqual([
      `/api/organizations/${ids.example}/projects/${ids.ops}/workitems/OPS-US-1`,
    ]);
    expect(here().pathname).toBe(backlogAddress());
  });

  it('opens by address in the project whose key the identifier carries, whether or not the item is on the view', async () => {
    const api = serveBoards(acrossProjectsDataset());
    renderBoards(`${backlogAddress()}?workitem=OPS-US-3`);

    const dialog = await screen.findByRole('dialog', {
      name: 'Пользовательская история OPS-US-3',
    });

    expect(within(dialog).getByRole('textbox', { name: 'Проект' })).toHaveValue('Операции');
    expect(api.reads('/workitems/OPS-US-3').map((request) => request.path)).toEqual([
      `/api/organizations/${ids.example}/projects/${ids.ops}/workitems/OPS-US-3`,
    ]);
  });

  it.each([
    ['an unknown key', 'ZZZ-US-1'],
    ['a malformed identifier', 'not-an-identifier'],
    ['a key in lower case', 'ops-us-1'],
  ])(
    '%s shows the not-found frame, naming nothing, and sends no request for the item',
    async (_case, identifier) => {
      const api = serveBoards(acrossProjectsDataset());
      renderBoards(`${backlogAddress()}?workitem=${identifier}`);

      expect(await screen.findByText('Рабочий элемент не найден')).toBeVisible();
      expect(screen.getByText('Проверьте адрес или закройте окно.')).toBeVisible();
      expect(screen.queryByText('Проект не найден')).toBeNull();
      expect(api.reads('/workitems/')).toEqual([]);

      // The frame's own action, not the dialog's close icon.
      const close = screen
        .getAllByRole('button', { name: 'Закрыть' })
        .find((button) => button.textContent === 'Закрыть');
      await userEvent.click(close as HTMLElement);
      await waitFor(() => expect(here().search).toBe(''));
    },
  );

  it('a deactivated project’s key shows the same frame, whether or not the item exists', async () => {
    const data = acrossProjectsDataset();
    const ops = data.projects.find((project) => project.id === ids.ops);
    if (!ops) throw new Error('No project.');
    ops.active = false;
    const api = serveBoards(data);
    renderBoards(`${backlogAddress()}?workitem=OPS-US-1`);

    expect(await screen.findByText('Рабочий элемент не найден')).toBeVisible();
    expect(api.reads('/workitems/')).toEqual([]);
  });
});
