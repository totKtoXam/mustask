import { afterEach, describe, expect, it, vi } from 'vitest';
import { act, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { focusManager } from '@tanstack/react-query';
import { serveBoards, type BoardsTestApi } from '../testApi';
import { cardMenuButton, cardOf, cardsIn, columnBox, findBoard } from '../testBoard';
import { checkboxOf, counterOf, listOf } from '../testChecklist';
import { dataTransfer, fireDrag } from '../testDrag';
import { acrossProjectsDataset, createdAt, ids, item, stateId } from '../testFixtures';
import { backlogAddress, boardAddress, renderBoards } from '../testRender';
import { chooseProjects, filterBand, here, projectField } from '../testProjectFilter';

// feat-040 US-1..US-4 on a team's board: «Платформа» works on «Мастаск» (`MSK`) and «Операции» (`OPS`)
// and owns MSK-US-3, OPS-US-1 and OPS-US-2 (`acrossProjectsDataset`).

vi.setConfig({ testTimeout: 60_000 });

afterEach(() => {
  focusManager.setFocused(undefined);
});

const TEAM_ADDRESS = `/api/organizations/${ids.example}/teams/${ids.platformTeam}/boards/stories`;

function patches(api: BoardsTestApi) {
  return api.writes().filter((request) => request.method === 'PATCH');
}

/** The card dragged onto the column by the browser's own drag and drop. */
async function dragCard(board: HTMLElement, identifier: string, header: string) {
  const transfer = dataTransfer();
  const card = cardOf(board, identifier);
  const target = columnBox(board, header);
  await act(async () => fireDrag('dragStart', card, transfer));
  await act(async () => fireDrag('dragOver', target, transfer));
  await act(async () => fireDrag('drop', target, transfer));
  await act(async () => fireDrag('dragEnd', card, transfer));
}

describe('feat-040 US-1: a team’s board holds the team’s cards of every project, at the team’s address', () => {
  it('lists MSK-US-3, OPS-US-1 and OPS-US-2 in «Новая», and no card of another team; the read names the team alone', async () => {
    const api = serveBoards(acrossProjectsDataset());
    renderBoards(boardAddress());

    const board = await findBoard();

    expect(cardsIn(board, 'Новая')).toEqual(['MSK-US-3', 'OPS-US-1', 'OPS-US-2']);
    expect(api.reads('/boards/stories').map((request) => request.path)).toEqual([TEAM_ADDRESS]);
  });

  it('names the project at the right end of the assignee row, under the team above it, with «Проект: » for assistive technology (D-1)', async () => {
    serveBoards(acrossProjectsDataset());
    renderBoards(boardAddress());
    const board = await findBoard();

    for (const [identifier, project] of [
      ['MSK-US-3', 'Мастаск'],
      ['OPS-US-1', 'Операции'],
    ] as const) {
      const card = cardOf(board, identifier);
      expect(within(card).getByText('Проект:')).toBeInTheDocument();
      expect(card).toHaveTextContent(`Проект: ${project}`);
      expect(card).toHaveTextContent('Команда: Платформа');
      // The third row: the assignee, the priority, then the project; the card has no row of its own for it.
      const third = card.children[2] as HTMLElement;
      expect(third).toHaveTextContent(new RegExp(`Приоритет: 2Проект: ${project}$`));
      const state = card.children[1] as HTMLElement;
      expect(state).toHaveTextContent('Команда: Платформа');
      expect(state).not.toHaveTextContent('Проект:');
    }
  });

  it('keeps the project’s name as it stands now', async () => {
    const data = acrossProjectsDataset();
    const ops = data.projects.find((project) => project.id === ids.ops);
    if (!ops) throw new Error('No project.');
    ops.name = 'Склад и логистика';
    serveBoards(data);
    renderBoards(boardAddress());
    const board = await findBoard();

    expect(cardOf(board, 'OPS-US-2')).toHaveTextContent('Проект: Склад и логистика');
  });

  it('shows the project’s own board as it did, with the project on each card too (BR-TS-3)', async () => {
    serveBoards(acrossProjectsDataset());
    renderBoards(`/organizations/${ids.example}/projects/${ids.ops}/boards/stories`);
    const board = await findBoard();

    expect(cardsIn(board, 'Новая')).toEqual(['OPS-US-1', 'OPS-US-2', 'OPS-US-3', 'OPS-US-4']);
    expect(cardOf(board, 'OPS-US-3')).toHaveTextContent('Проект: Операции');
    expect(cardOf(board, 'OPS-US-3')).toHaveTextContent('Команда: Операции');
  });
});

describe('feat-040 US-2: the project filter on the board (BR-TS-8)', () => {
  it('narrows the columns and their counters, carries the choice in the address and to the read, and clears', async () => {
    const api = serveBoards(acrossProjectsDataset());
    renderBoards(boardAddress());
    const board = await findBoard();

    await chooseProjects('Операции');

    await waitFor(() => expect(cardsIn(board, 'Новая')).toEqual(['OPS-US-1', 'OPS-US-2']));
    expect(here()).toEqual({ pathname: boardAddress(), search: `?projectId=${ids.ops}` });
    expect(api.reads('/boards/stories').at(-1)?.path).toBe(`${TEAM_ADDRESS}?projectId=${ids.ops}`);
    expect(screen.getByRole('list', { name: 'Новая, рабочих элементов: 2' })).toBeInTheDocument();

    await userEvent.click(within(filterBand()).getByRole('button', { name: 'Очистить фильтры' }));

    await waitFor(() => expect(here().search).toBe(''));
    await waitFor(() => expect(cardsIn(board, 'Новая')).toHaveLength(3));
  });

  it('is kept by the tab, the level menu and «Показать бэклог»', async () => {
    serveBoards(acrossProjectsDataset());
    renderBoards(`${boardAddress()}?projectId=${ids.msk}`);
    await findBoard();
    const filter = `?projectId=${ids.msk}`;

    expect(screen.getByRole('tab', { name: 'Канбан' })).toHaveAttribute(
      'href',
      `${boardAddress()}${filter}`,
    );
    expect(screen.getByRole('link', { name: 'Показать бэклог' })).toHaveAttribute(
      'href',
      `${backlogAddress()}${filter}`,
    );
    expect(projectField()).toHaveTextContent('Мастаск');
  });

  it('a value that names no project of the team shows the filtered-empty frame and «Сбросить фильтр»', async () => {
    serveBoards(acrossProjectsDataset());
    renderBoards(`${boardAddress()}?projectId=not-a-project`);

    expect(
      await screen.findByRole('heading', { level: 2, name: 'Ничего не найдено' }),
    ).toBeVisible();
    expect(screen.getByText('Фильтр задан')).toBeVisible();

    await userEvent.click(screen.getByRole('button', { name: 'Сбросить фильтр' }));

    const board = await findBoard();
    expect(cardsIn(board, 'Новая')).toHaveLength(3);
    expect(here().search).toBe('');
  });
});

describe('feat-040 US-4: a card is moved and ticked in its own project (BR-TS-5)', () => {
  it('a card of «Операции» moved with its menu writes the state in «Операции», touching no place', async () => {
    const api = serveBoards(acrossProjectsDataset());
    renderBoards(boardAddress());
    const board = await findBoard();

    await userEvent.click(cardMenuButton('OPS-US-1'));
    await userEvent.click(await screen.findByRole('menuitem', { name: 'Активна' }));

    await waitFor(() => expect(cardsIn(board, 'Активна')).toEqual(['OPS-US-1']));
    await waitFor(() => expect(cardMenuButton('OPS-US-1')).not.toHaveAttribute('aria-disabled'));
    expect(patches(api).map((request) => [request.path, request.body])).toEqual([
      [
        `/api/organizations/${ids.example}/projects/${ids.ops}/workitems/OPS-US-1`,
        { stateId: stateId('US', 'Активна') },
      ],
    ]);
    expect(api.item(ids.ops, 1).stateId).toBe(stateId('US', 'Активна'));
    expect(api.data.places).toEqual([]);
    expect(api.data.projectPlaces).toEqual([]);
  });

  it('a drag writes the state of the card’s own project too, for a card of each project', async () => {
    const api = serveBoards(acrossProjectsDataset());
    renderBoards(boardAddress());
    const board = await findBoard();

    await dragCard(board, 'MSK-US-3', 'Разрешена');
    await waitFor(() => expect(cardMenuButton('MSK-US-3')).not.toHaveAttribute('aria-disabled'));
    await dragCard(board, 'OPS-US-2', 'Разрешена');
    await waitFor(() => expect(cardMenuButton('OPS-US-2')).not.toHaveAttribute('aria-disabled'));

    expect(patches(api).map((request) => request.path)).toEqual([
      `/api/organizations/${ids.example}/projects/${ids.msk}/workitems/MSK-US-3`,
      `/api/organizations/${ids.example}/projects/${ids.ops}/workitems/OPS-US-2`,
    ]);
    expect(cardsIn(board, 'Разрешена')).toEqual(['MSK-US-3', 'OPS-US-2']);
  });

  it('a checklist tick on a card of «Операции» writes the child in «Операции»', async () => {
    const data = acrossProjectsDataset();
    const story = data.items.find((record) => record.projectId === ids.ops && record.number === 1);
    if (!story) throw new Error('No story.');
    data.items.push(
      item(ids.ops, 10, 'TASK', 'Проверить склад', {
        parentId: story.id,
        teamId: ids.platformTeam,
        createdAt: createdAt(20),
      }),
    );
    const api = serveBoards(data);
    renderBoards(boardAddress());
    const board = await findBoard();
    await waitFor(() => expect(counterOf(board, 'OPS-US-1', 'task')).toBeInTheDocument());

    await userEvent.click(counterOf(board, 'OPS-US-1', 'task'));
    await userEvent.click(checkboxOf(listOf(board, 'OPS-US-1', 'Задача'), 'OPS-TASK-10'));

    await waitFor(() => expect(patches(api)).toHaveLength(1));
    expect(patches(api)[0]?.path).toBe(
      `/api/organizations/${ids.example}/projects/${ids.ops}/workitems/OPS-TASK-10`,
    );
    expect(api.item(ids.ops, 10).stateId).toBe(stateId('TASK', 'Закрыта'));
  });

  it('a title opens the card over the board and reads it in the card’s own project', async () => {
    const api = serveBoards(acrossProjectsDataset());
    renderBoards(boardAddress());
    const board = await findBoard();

    await userEvent.click(
      within(cardOf(board, 'OPS-US-2')).getByRole('link', { name: 'Отгрузка' }),
    );

    const dialog = await screen.findByRole('dialog', { name: 'Пользовательская история OPS-US-2' });
    expect(within(dialog).getByRole('textbox', { name: 'Проект' })).toHaveValue('Операции');
    expect(api.reads('/workitems/OPS-US-2').map((request) => request.path)).toEqual([
      `/api/organizations/${ids.example}/projects/${ids.ops}/workitems/OPS-US-2`,
    ]);
  });
});

describe('feat-040 US-5 and BR-TS-2: creating from the board, and a team on no project', () => {
  it('the first column’s «Новый элемент» opens the dialog with «Проект» first', async () => {
    serveBoards(acrossProjectsDataset());
    renderBoards(boardAddress());
    await findBoard();

    await userEvent.click(await screen.findByRole('button', { name: 'Новый элемент' }));
    await userEvent.click(
      await screen.findByRole('menuitem', { name: 'Пользовательская история' }),
    );

    const dialog = await screen.findByRole('dialog', { name: 'Новый рабочий элемент' });
    await waitFor(() =>
      expect(within(dialog).getByRole('combobox', { name: 'Проект' })).toBeVisible(),
    );
    expect(within(dialog).getAllByRole('combobox')[0]).toBe(
      within(dialog).getByRole('combobox', { name: 'Проект' }),
    );
  });

  it('a team that works on no active project reads «Канбан пуст» with the hint, no action, no filter row and no create command', async () => {
    const data = acrossProjectsDataset();
    const ops = data.projects.find((project) => project.id === ids.ops);
    if (!ops) throw new Error('No project.');
    ops.active = false;
    serveBoards(data);
    renderBoards(boardAddress('stories', ids.opsTeam));

    expect(await screen.findByRole('heading', { level: 2, name: 'Канбан пуст' })).toBeVisible();
    expect(screen.getByText('Команда не участвует ни в одном проекте.')).toBeVisible();
    expect(screen.queryByRole('button', { name: 'Новый рабочий элемент' })).toBeNull();
    expect(screen.queryByRole('button', { name: 'Новый элемент' })).toBeNull();
    expect(screen.queryByRole('group', { name: 'Фильтры' })).toBeNull();
  });

  it('says the same for a reader: nothing of why', async () => {
    const data = acrossProjectsDataset();
    const ops = data.projects.find((project) => project.id === ids.ops);
    if (!ops) throw new Error('No project.');
    ops.active = false;
    serveBoards(data);
    renderBoards(boardAddress('stories', ids.opsTeam), { role: 'viewer' });

    expect(await screen.findByRole('heading', { level: 2, name: 'Канбан пуст' })).toBeVisible();
    expect(screen.getByText('Команда не участвует ни в одном проекте.')).toBeVisible();
  });

  it('a team that owns nothing at the level while working on projects reads today’s frame, with the action for a writer', async () => {
    serveBoards(acrossProjectsDataset());
    renderBoards(boardAddress('epics'));

    expect(await screen.findByRole('heading', { level: 2, name: 'Канбан пуст' })).toBeVisible();
    expect(screen.queryByText('Команда не участвует ни в одном проекте.')).toBeNull();
    expect(screen.getAllByRole('button', { name: 'Новый рабочий элемент' }).length).toBeGreaterThan(
      0,
    );
    // The filter row is there to filter by: the team works on two projects.
    expect(within(filterBand()).getByRole('combobox', { name: 'Проект' })).toBeVisible();
  });
});
