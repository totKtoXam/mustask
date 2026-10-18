import { afterEach, describe, expect, it, vi } from 'vitest';
import { act, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { focusManager } from '@tanstack/react-query';
import { serveBoards } from '../testApi';
import { findBacklogTable, identifiers, rowOf } from '../testBacklog';
import { cardsIn, findBoard } from '../testBoard';
import { ids, projectDataset } from '../testFixtures';
import { chooseLevel } from '../testLevels';
import {
  backlogAddress,
  boardAddress,
  projectBacklogAddress,
  projectBoardAddress,
  renderBoards,
} from '../testRender';

// mod-020 (the human's answer to mod-017 OQ-9 B): the team filter of the project's backlog and board
// lives in the page address as the repeated `teamId` (api-contract § 3), so a link carries it, and a
// re-read and a level change keep it (BR-OT-8). `projectDataset` puts MSK-US-2 on «Мастаск», MSK-US-3 on
// «Платформа» and MSK-US-9 on «Аналитика». The card inside the first test is slow in jsdom.

vi.setConfig({ testTimeout: 60_000 });

afterEach(() => {
  focusManager.setFocused(undefined);
});

const WITH_PLATFORM = `?teamId=${ids.platformTeam}`;

/** The router's pathname and search, which the shell’s location probe keeps. */
function here(): { pathname: string; search: string } {
  const probe = screen.getByTestId('location');
  return { pathname: probe.textContent ?? '', search: probe.getAttribute('data-search') ?? '' };
}

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

describe('mod-020 Scenario: the project backlog’s team filter lives in the address', () => {
  it('writes a choice into the address, one teamId per value, and «Очистить фильтры» takes it out', async () => {
    serveBoards(projectDataset());
    renderBoards(projectBacklogAddress());
    const table = await findBacklogTable();

    await chooseTeams('Платформа', 'Аналитика');

    await waitFor(() =>
      expect(here().search).toBe(`?teamId=${ids.platformTeam}&teamId=${ids.analyticsTeam}`),
    );
    expect(here().pathname).toBe(projectBacklogAddress());
    await userEvent.click(within(band()).getByRole('button', { name: 'Очистить фильтры' }));
    await waitFor(() => expect(here().search).toBe(''));
    await waitFor(() => expect(identifiers(table)).toHaveLength(3));
  });

  it('a link that carries the filter opens the backlog narrowed: every read carries it, the field shows it', async () => {
    const api = serveBoards(projectDataset());
    renderBoards(`${projectBacklogAddress()}${WITH_PLATFORM}`);

    const table = await findBacklogTable();

    expect(identifiers(table)).toEqual(['MSK-US-3']);
    expect(filterField()).toHaveTextContent('Платформа');
    expect(within(band()).getByRole('button', { name: 'Очистить фильтры' })).toBeVisible();
    expect(api.reads('/backlogs/stories').length).toBeGreaterThan(0);
    for (const read of api.reads('/backlogs/stories')) {
      expect(read.path).toContain(`teamId=${ids.platformTeam}`);
    }
  });

  it('keeps the filter in the address across a re-read and a level change; the other page starts unfiltered', async () => {
    serveBoards(projectDataset());
    renderBoards(`${projectBacklogAddress()}${WITH_PLATFORM}`);
    const table = await findBacklogTable();

    act(() => {
      focusManager.setFocused(false);
      focusManager.setFocused(true);
    });
    await waitFor(() => expect(identifiers(table)).toEqual(['MSK-US-3']));
    expect(here().search).toBe(WITH_PLATFORM);

    await chooseLevel('Фичи');
    await screen.findByRole('heading', { level: 2, name: 'Ничего не найдено' });
    expect(here()).toEqual({ pathname: projectBacklogAddress('features'), search: WITH_PLATFORM });
    expect(filterField()).toHaveTextContent('Платформа');

    await userEvent.click(screen.getByRole('link', { name: 'Показать канбан' }));
    await screen.findByRole('heading', { level: 2, name: 'Канбан пуст' });
    expect(here()).toEqual({ pathname: projectBoardAddress('features'), search: '' });
    expect(screen.getByText('Все команды')).toBeInTheDocument();
  });

  it('«Сбросить фильтр» of a link whose filter selects nothing takes it out of the address', async () => {
    serveBoards(projectDataset());
    renderBoards(`${projectBacklogAddress('epics')}${WITH_PLATFORM}`);
    await screen.findByRole('heading', { level: 2, name: 'Ничего не найдено' });

    await userEvent.click(screen.getByRole('button', { name: 'Сбросить фильтр' }));

    expect(await screen.findByRole('heading', { level: 2, name: 'Бэклог пуст' })).toBeVisible();
    expect(here().search).toBe('');
  });

  it('opening a card adds `workitem` beside the filter and closing it leaves the filter', async () => {
    serveBoards(projectDataset());
    renderBoards(`${projectBacklogAddress()}${WITH_PLATFORM}`);
    const table = await findBacklogTable();

    await userEvent.click(within(rowOf(table, 'MSK-US-3')).getByRole('link', { name: 'Выход' }));

    await screen.findByRole('dialog', { name: 'Пользовательская история MSK-US-3' });
    expect(here().search).toBe(`${WITH_PLATFORM}&workitem=MSK-US-3`);

    await userEvent.keyboard('{Escape}');

    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
    expect(here().search).toBe(WITH_PLATFORM);
    expect(filterField()).toHaveTextContent('Платформа');
  });
});

describe('mod-020 Scenario: the project board’s team filter lives in the address', () => {
  it('writes a choice into the address and a link that carries it opens the board narrowed', async () => {
    serveBoards(projectDataset());
    renderBoards(projectBoardAddress());
    const board = await findBoard();
    expect(cardsIn(board, 'Новая')).toEqual(['MSK-US-2', 'MSK-US-3', 'MSK-US-9']);

    await chooseTeams('Мастаск');
    await waitFor(() => expect(cardsIn(board, 'Новая')).toEqual(['MSK-US-2']));
    expect(here().search).toBe(`?teamId=${ids.mskTeam}`);
  });

  it('a link with the filter opens the board narrowed, and a level change keeps it', async () => {
    const api = serveBoards(projectDataset());
    renderBoards(`${projectBoardAddress()}${WITH_PLATFORM}`);

    const board = await findBoard();

    expect(cardsIn(board, 'Новая')).toEqual(['MSK-US-3']);
    expect(filterField()).toHaveTextContent('Платформа');
    for (const read of api.reads('/boards/stories')) {
      expect(read.path).toContain(`teamId=${ids.platformTeam}`);
    }

    await chooseLevel('Фичи');
    await screen.findByRole('heading', { level: 2, name: 'Ничего не найдено' });
    expect(here()).toEqual({ pathname: projectBoardAddress('features'), search: WITH_PLATFORM });
  });
});

// feat-040 BR-TS-8 gives a team's views a filter of their own, the project's (US-2); the team is
// still no filter on them, whatever their address carries.
describe('mod-020 Scenario: a team’s own views have no team filter, whatever their address carries', () => {
  it('a team’s backlog ignores a teamId in its address: its rows, no «Команда» filter, nothing sent', async () => {
    const api = serveBoards(projectDataset());
    renderBoards(`${backlogAddress()}?teamId=${ids.mskTeam}`);

    const table = await findBacklogTable();

    expect(identifiers(table)).toEqual(['MSK-US-3']);
    expect(screen.queryByRole('combobox', { name: 'Команда' })).toBeNull();
    for (const read of api.reads('/backlogs/stories')) {
      expect(read.path).not.toContain('teamId=');
    }
  });

  it('a team’s board ignores it too, and its level links carry nothing of it', async () => {
    const api = serveBoards(projectDataset());
    renderBoards(`${boardAddress()}?teamId=${ids.mskTeam}`);

    const board = await findBoard();

    expect(cardsIn(board, 'Новая')).toEqual(['MSK-US-3']);
    expect(screen.queryByRole('combobox', { name: 'Команда' })).toBeNull();
    for (const read of api.reads('/boards/stories')) {
      expect(read.path).not.toContain('teamId=');
    }
    await chooseLevel('Фичи');
    await waitFor(() => expect(here()).toEqual({ pathname: boardAddress('features'), search: '' }));
  });
});

// CR-2 (code review, mod-020): a link to the project page's own address keeps its filter, as a click on
// it did before the filter moved to the address. The page's own tab is the selected one, and «Весь
// проект» is the selected entry of the team menu; the nav entry is the only such link that drops it.
// feat-040 BR-TS-11 retires, by name, ««Весь проект», the selected entry on the project page, keeps the filter»
// and «a team’s own tab and its «Весь проект» carry no filter, whatever its address holds»: the head has
// no «Весь проект» any more, and a team's own tab carries nothing of a team filter (the scenario above).
describe('mod-020 Scenario: the project page’s own tab keeps the filter', () => {
  it('the backlog’s tab is selected, links to the page with its filter, and a click keeps it', async () => {
    serveBoards(projectDataset());
    renderBoards(`${projectBacklogAddress()}${WITH_PLATFORM}`);
    const table = await findBacklogTable();
    const tab = screen.getByRole('tab', { name: 'Бэклог' });

    expect(tab).toHaveAttribute('aria-selected', 'true');
    expect(tab).toHaveAttribute('href', `${projectBacklogAddress()}${WITH_PLATFORM}`);

    await userEvent.click(tab);

    expect(here()).toEqual({ pathname: projectBacklogAddress(), search: WITH_PLATFORM });
    expect(identifiers(table)).toEqual(['MSK-US-3']);
    expect(filterField()).toHaveTextContent('Платформа');
  });

  it('the board’s tab keeps several values, `none` among them, in the address’s order', async () => {
    serveBoards(projectDataset());
    const filter = `?teamId=none&teamId=${ids.platformTeam}`;
    renderBoards(`${projectBoardAddress()}${filter}`);
    await findBoard();
    const tab = screen.getByRole('tab', { name: 'Канбан' });

    expect(tab).toHaveAttribute('aria-selected', 'true');
    expect(tab).toHaveAttribute('href', `${projectBoardAddress()}${filter}`);

    await userEvent.click(tab);

    expect(here()).toEqual({ pathname: projectBoardAddress(), search: filter });
  });

  it('a page without a filter has a tab without one', async () => {
    serveBoards(projectDataset());
    renderBoards(projectBacklogAddress());
    await findBacklogTable();

    expect(screen.getByRole('tab', { name: 'Бэклог' })).toHaveAttribute(
      'href',
      projectBacklogAddress(),
    );
  });
});
