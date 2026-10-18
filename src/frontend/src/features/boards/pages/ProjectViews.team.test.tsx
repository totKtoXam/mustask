import { afterEach, describe, expect, it } from 'vitest';
import { act, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { focusManager } from '@tanstack/react-query';
import { HttpResponse, http } from 'msw';
import { gate } from '../../../test/gate';
import { server } from '../../../test/mocks/server';
import { serveBoards } from '../testApi';
import {
  CELL,
  cellText,
  findBacklogTable,
  identifiers,
  orders,
  outline,
  rowOf,
} from '../testBacklog';
import { cardOf, cardsIn, columnNames, findBoard } from '../testBoard';
import {
  PROBLEM_HEADERS,
  PROJECT_BACKLOG_URL,
  ids,
  item,
  platformItem,
  problem,
  projectDataset,
} from '../testFixtures';
import { chooseLevel } from '../testLevels';
import {
  backlogAddress,
  boardAddress,
  projectBacklogAddress,
  projectBoardAddress,
  renderBoards,
} from '../testRender';

// mod-017 US-4, US-5 (design § 5.2, § 6): the owning team on the backlog's column and the board's
// card, and the team filter of the project's views. `projectDataset` puts MSK-US-2 on «Мастаск»,
// MSK-US-3 on «Платформа» and MSK-US-9 on «Аналитика»; OPS-US-1 is another project's.

afterEach(() => {
  focusManager.setFocused(undefined);
});

function band(): HTMLElement {
  return screen.getByRole('group', { name: 'Фильтры' });
}

function filterField(): HTMLElement {
  return within(band()).getByRole('combobox', { name: 'Команда' });
}

async function chooseTeams(...names: string[]) {
  await userEvent.click(filterField());
  for (const name of names) {
    await userEvent.click(await screen.findByRole('option', { name }));
  }
  await userEvent.keyboard('{Escape}');
  await waitFor(() => expect(screen.queryByRole('listbox')).not.toBeInTheDocument());
}

function returnToTab() {
  act(() => {
    focusManager.setFocused(false);
    focusManager.setFocused(true);
  });
}

describe('mod-017 BR-OT-7 Scenario: the team on the backlog row and the board card', () => {
  it('the column «Команда» follows «Статус» and names the team, or «Без команды» in the secondary colour — on a team’s views and the project’s', async () => {
    const data = projectDataset();
    data.items.push(item(ids.msk, 12, 'US', 'Ничья', { teamId: null }));
    serveBoards(data);
    renderBoards(projectBacklogAddress());

    const table = await findBacklogTable();

    expect(cellText(rowOf(table, 'MSK-US-2'), CELL.team)).toBe('Мастаск');
    expect(cellText(rowOf(table, 'MSK-US-3'), CELL.team)).toBe('Платформа');
    const none = within(rowOf(table, 'MSK-US-12')).getByText('Без команды');
    expect(getComputedStyle(none).color).not.toBe(getComputedStyle(rowOf(table, 'MSK-US-3')).color);
  });

  it('a team’s backlog shows its team and its project on every row too, and has the project filter row, not the team one (feat-040 BR-TS-3, BR-TS-8)', async () => {
    serveBoards(projectDataset());
    renderBoards(backlogAddress());

    const table = await findBacklogTable();

    expect(identifiers(table)).toEqual(['MSK-US-3']);
    expect(cellText(rowOf(table, 'MSK-US-3'), CELL.team)).toBe('Платформа');
    expect(cellText(rowOf(table, 'MSK-US-3'), CELL.project)).toBe('Мастаск');
    // The team is the filter on its own views; the project is the one a team's views have.
    const filters = screen.getByRole('group', { name: 'Фильтры' });
    expect(within(filters).getByRole('combobox', { name: 'Проект' })).toBeVisible();
    expect(within(filters).queryByRole('combobox', { name: 'Команда' })).toBeNull();
  });

  it('a board card names its team at the right of its state, with «Команда: » for assistive technology; a team’s board has no filter row', async () => {
    const data = projectDataset();
    data.items.push(item(ids.msk, 12, 'US', 'Ничья', { teamId: null }));
    serveBoards(data);
    renderBoards(projectBoardAddress());

    const board = await findBoard();

    expect(within(cardOf(board, 'MSK-US-3')).getByText('Команда:')).toBeInTheDocument();
    expect(cardOf(board, 'MSK-US-3')).toHaveTextContent('Команда: Платформа');
    expect(cardOf(board, 'MSK-US-12')).toHaveTextContent('Команда: Без команды');
    expect(
      within(screen.getByRole('group', { name: 'Фильтры' })).getByRole('combobox'),
    ).toBeVisible();
  });

  it('a team’s board shows the team line and the project at the right end of the assignee row, and the project filter row, not the team one (feat-040)', async () => {
    serveBoards(projectDataset());
    renderBoards(boardAddress());

    const board = await findBoard();

    expect(cardOf(board, 'MSK-US-3')).toHaveTextContent('Команда: Платформа');
    expect(cardOf(board, 'MSK-US-3')).toHaveTextContent('Проект: Мастаск');
    const filters = screen.getByRole('group', { name: 'Фильтры' });
    expect(within(filters).getByRole('combobox', { name: 'Проект' })).toBeVisible();
    expect(within(filters).queryByRole('combobox', { name: 'Команда' })).toBeNull();
  });
});

describe('mod-017 US-5 Scenario: the team filter on the project’s backlog', () => {
  it('offers the teams by name, then «Без команды»; a choice narrows the rows and renumbers them, the request carries `teamId`', async () => {
    const api = serveBoards(projectDataset());
    renderBoards(projectBacklogAddress());
    const table = await findBacklogTable();
    expect(identifiers(table)).toEqual(['MSK-US-2', 'MSK-US-3', 'MSK-US-9']);

    await userEvent.click(filterField());
    expect(
      within(await screen.findByRole('listbox'))
        .getAllByRole('option')
        .map((option) => option.textContent),
    ).toEqual(['Аналитика', 'Мастаск', 'Платформа', 'Без команды']);
    await userEvent.keyboard('{Escape}');
    await chooseTeams('Платформа', 'Аналитика');

    await waitFor(() => expect(identifiers(table)).toEqual(['MSK-US-3', 'MSK-US-9']));
    expect(orders(table)).toEqual(['1', '2']);
    expect(api.reads('/backlogs/stories').at(-1)?.path).toContain(`teamId=${ids.platformTeam}`);
    expect(api.reads('/backlogs/stories').at(-1)?.path).toContain(`teamId=${ids.analyticsTeam}`);
  });

  it('«Без команды» selects the items of none; «Очистить фильтры» brings every row back', async () => {
    const data = projectDataset();
    data.items.push(item(ids.msk, 12, 'US', 'Ничья', { teamId: null }));
    serveBoards(data);
    renderBoards(projectBacklogAddress());
    const table = await findBacklogTable();

    await chooseTeams('Без команды');
    await waitFor(() => expect(identifiers(table)).toEqual(['MSK-US-12']));
    await userEvent.click(within(band()).getByRole('button', { name: 'Очистить фильтры' }));

    await waitFor(() => expect(identifiers(table)).toHaveLength(4));
    expect(within(band()).queryByRole('button', { name: 'Очистить фильтры' })).toBeNull();
  });

  it('a child whose parent does not match is a top-level row, numbered among the visible ones', async () => {
    const data = projectDataset();
    const parent = platformItem(21, 'FEAT', 'Родитель', { teamId: ids.mskTeam });
    const child = platformItem(22, 'FEAT', 'Потомок', { parentId: parent.id });
    data.items.push(parent, child);
    serveBoards(data);
    renderBoards(projectBacklogAddress('features'));
    const table = await findBacklogTable();
    expect(outline(table)).toEqual(['MSK-FEAT-21', '  MSK-FEAT-22']);

    await chooseTeams('Платформа');

    await waitFor(() => expect(outline(table)).toEqual(['MSK-FEAT-22']));
    expect(orders(table)).toEqual(['1']);
  });

  it('keeps the filter across a re-read and a level change, and drops it with the page: the board opens unfiltered', async () => {
    serveBoards(projectDataset());
    renderBoards(projectBacklogAddress());
    const table = await findBacklogTable();
    await chooseTeams('Платформа');
    await waitFor(() => expect(identifiers(table)).toEqual(['MSK-US-3']));

    returnToTab();
    await waitFor(() => expect(identifiers(table)).toEqual(['MSK-US-3']));
    expect(filterField()).toHaveTextContent('Платформа');

    await chooseLevel('Фичи');
    await screen.findByRole('heading', { level: 2, name: 'Ничего не найдено' });
    expect(filterField()).toHaveTextContent('Платформа');

    await userEvent.click(screen.getByRole('link', { name: 'Показать канбан' }));
    await screen.findByRole('heading', { level: 2, name: 'Канбан пуст' });
    expect(filterField()).not.toHaveTextContent('Платформа');
    expect(screen.getByText('Все команды')).toBeInTheDocument();
  });

  it('keeps the previous rows, busy, while a change is read; a failed read replaces them with the banner and «Повторить»', async () => {
    serveBoards(projectDataset());
    renderBoards(projectBacklogAddress());
    const table = await findBacklogTable();
    const held = gate();
    let fail = true;
    server.use(
      http.get(PROJECT_BACKLOG_URL, async ({ request }) => {
        if (new URL(request.url).searchParams.has('teamId')) {
          await held.opened;
          if (fail) {
            fail = false;
            return HttpResponse.json(problem('organizations.access.forbidden'), {
              status: 500,
              headers: PROBLEM_HEADERS,
            });
          }
        }
        return undefined;
      }),
    );

    await chooseTeams('Платформа');

    await waitFor(() => expect(table).toHaveAttribute('aria-busy', 'true'));
    expect(identifiers(table)).toHaveLength(3);
    expect(screen.queryByRole('status', { name: 'Загрузка данных' })).toBeNull();

    held.open();
    await userEvent.click(await screen.findByRole('button', { name: 'Повторить' }));
    await waitFor(async () => expect(identifiers(await findBacklogTable())).toEqual(['MSK-US-3']));
  });

  it('shows «Ничего не найдено» after the filter, the row kept, and «Сбросить фильтр» brings the rows back', async () => {
    serveBoards(projectDataset());
    renderBoards(projectBacklogAddress('epics'));
    await screen.findByRole('heading', { level: 2, name: 'Бэклог пуст' });

    await chooseTeams('Платформа');

    expect(
      await screen.findByRole('heading', { level: 2, name: 'Ничего не найдено' }),
    ).toBeVisible();
    expect(
      screen.getByText('Ни один рабочий элемент не подходит под заданные условия фильтра.'),
    ).toBeInTheDocument();
    expect(within(band()).getByRole('button', { name: 'Очистить фильтры' })).toBeVisible();

    await userEvent.click(screen.getByRole('button', { name: 'Сбросить фильтр' }));

    expect(await screen.findByRole('heading', { level: 2, name: 'Бэклог пуст' })).toBeVisible();
  });

  it('a reader filters too', async () => {
    serveBoards(projectDataset());
    renderBoards(projectBacklogAddress(), { role: 'viewer' });
    const table = await findBacklogTable();

    await chooseTeams('Аналитика');

    await waitFor(() => expect(identifiers(table)).toEqual(['MSK-US-9']));
  });
});

describe('mod-017 US-5 Scenario: the team filter on the project’s board', () => {
  it('narrows the columns and their counters, and leaves a card’s checklist whole', async () => {
    const data = projectDataset();
    data.items.push(
      item(ids.msk, 14, 'TASK', 'Задача мастера', { parentId: data.items[0]?.id, teamId: null }),
    );
    serveBoards(data);
    renderBoards(projectBoardAddress());
    const board = await findBoard();
    expect(cardsIn(board, 'Новая')).toEqual(['MSK-US-2', 'MSK-US-3', 'MSK-US-9']);

    await chooseTeams('Мастаск');

    await waitFor(() => expect(cardsIn(board, 'Новая')).toEqual(['MSK-US-2']));
    expect(columnNames(board)).toContain('Новая, рабочих элементов: 1');
    // The checklist of MSK-US-2 still counts the task of no team (BR-OT-8).
    expect(cardOf(board, 'MSK-US-2')).toHaveTextContent('0/1');
  });

  it('shows «Ничего не найдено» when nothing matches, with «Сбросить фильтр»', async () => {
    serveBoards(projectDataset());
    renderBoards(projectBoardAddress('epics'));
    await screen.findByRole('heading', { level: 2, name: 'Канбан пуст' });

    await chooseTeams('Платформа');

    expect(
      await screen.findByRole('heading', { level: 2, name: 'Ничего не найдено' }),
    ).toBeVisible();
    await userEvent.click(screen.getByRole('button', { name: 'Сбросить фильтр' }));
    expect(await screen.findByRole('heading', { level: 2, name: 'Канбан пуст' })).toBeVisible();
  });
});

describe('mod-017 US-2 Scenario: the project’s views create, starting with the project’s default team', () => {
  async function openDialog(address: string, data = projectDataset()) {
    const api = serveBoards(data);
    renderBoards(address);
    return api;
  }

  it('the backlog’s «Новый рабочий элемент» starts with the default «Мастаск», sends it, and a creation the filter hides is noticed', async () => {
    const data = projectDataset();
    data.projects.find((project) => project.id === ids.msk)!.defaultTeamId = ids.mskTeam;
    const api = await openDialog(projectBacklogAddress(), data);
    await findBacklogTable();
    await chooseTeams('Платформа');

    await userEvent.click(await screen.findByRole('button', { name: 'Новый рабочий элемент' }));
    await userEvent.click(await screen.findByRole('menuitem', { name: 'Баг' }));
    const dialog = await screen.findByRole('dialog', { name: 'Новый рабочий элемент' });
    expect(await within(dialog).findByRole('combobox', { name: 'Команда' })).toHaveValue('Мастаск');
    await userEvent.type(within(dialog).getByRole('textbox', { name: 'Заголовок' }), 'Сбой');
    await userEvent.click(within(dialog).getByRole('button', { name: 'Создать' }));

    expect(
      await screen.findByText(
        'Рабочий элемент MSK-BUG-10 создан. Он не подходит под текущий фильтр.',
      ),
    ).toBeInTheDocument();
    const post = api.writes().find((request) => request.path.endsWith('/workitems'));
    expect((post?.body as { team: { teamId: string } }).team).toEqual({ teamId: ids.mskTeam });
  });

  it('the board’s first column draws «Новый элемент», and the dialog starts with «Без команды» when no default is named', async () => {
    await openDialog(projectBoardAddress());
    const board = await findBoard();

    await userEvent.click(within(board).getByRole('button', { name: 'Новый элемент' }));
    await userEvent.click(
      await screen.findByRole('menuitem', { name: 'Пользовательская история' }),
    );
    const dialog = await screen.findByRole('dialog', { name: 'Новый рабочий элемент' });

    expect(await within(dialog).findByRole('combobox', { name: 'Команда' })).toHaveValue(
      'Без команды',
    );
    expect(within(dialog).getByText('Попадёт только в бэклог проекта.')).toBeVisible();
  });
});
