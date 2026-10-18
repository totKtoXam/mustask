import { afterEach, describe, expect, it, vi } from 'vitest';
import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { focusManager } from '@tanstack/react-query';
import { serveBoards } from '../testApi';
import { findBacklogTable } from '../testBacklog';
import { cardOf, cardsIn, findBoard } from '../testBoard';
import { ids, projectDataset } from '../testFixtures';
import { chooseLevel } from '../testLevels';
import {
  backlogAddress,
  boardAddress,
  projectBacklogAddress,
  projectBoardAddress,
  renderBoards,
} from '../testRender';

// mod-020 QA: the cases the developer's file leaves out — the none value and several values on the
// backlog, a card opened over a filtered board, a value that names nothing on the board, the team
// menu leaving the filter behind (feat-040 retires, by name, «another team, and «Весь проект» from a team,
// are other views and start unfiltered»: the project's head has no menu; a team's own menu starts the
// other team unfiltered, held by the project filter's tests).

vi.setConfig({ testTimeout: 60_000 });

afterEach(() => {
  focusManager.setFocused(undefined);
});

const WITH_PLATFORM = `?teamId=${ids.platformTeam}`;

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

describe('mod-020 QA: the project backlog’s filter in the address', () => {
  it('«Без команды» is `none` in the address and in every read, beside a team, in the order chosen', async () => {
    const api = serveBoards(projectDataset());
    renderBoards(projectBacklogAddress());
    await findBacklogTable();

    await chooseTeams('Без команды', 'Платформа');

    await waitFor(() => expect(here().search).toBe(`?teamId=none&teamId=${ids.platformTeam}`));
    await waitFor(() =>
      expect(
        api
          .reads('/backlogs/stories')
          .some(
            (read) =>
              read.path.includes('teamId=none') && read.path.includes(`teamId=${ids.platformTeam}`),
          ),
      ).toBe(true),
    );
  });
});

describe('mod-020 QA: a team’s own views, whatever their address carries', () => {
  it('an empty team backlog with a teamId in its address is empty, not filtered: no «Сбросить фильтр»', async () => {
    serveBoards(projectDataset());
    renderBoards(`${backlogAddress('epics')}?teamId=${ids.mskTeam}`);

    expect(await screen.findByRole('heading', { level: 2, name: 'Бэклог пуст' })).toBeVisible();
    expect(screen.queryByRole('button', { name: 'Сбросить фильтр' })).toBeNull();
    expect(here().search).toBe(`?teamId=${ids.mskTeam}`);
  });

  it('an empty team board with a teamId in its address is empty, not filtered', async () => {
    serveBoards(projectDataset());
    renderBoards(`${boardAddress('epics')}?teamId=${ids.mskTeam}`);

    expect(await screen.findByRole('heading', { level: 2, name: 'Канбан пуст' })).toBeVisible();
    expect(screen.queryByRole('button', { name: 'Сбросить фильтр' })).toBeNull();
  });
});

describe('mod-020 QA: the project board’s filter in the address', () => {
  it('opening a card adds `workitem` beside the filter and closing it leaves the filter', async () => {
    serveBoards(projectDataset());
    renderBoards(`${projectBoardAddress()}${WITH_PLATFORM}`);
    const board = await findBoard();
    expect(cardsIn(board, 'Новая')).toEqual(['MSK-US-3']);

    await userEvent.click(within(cardOf(board, 'MSK-US-3')).getByRole('link', { name: 'Выход' }));

    await screen.findByRole('dialog', { name: 'Пользовательская история MSK-US-3' });
    expect(here().search).toBe(`${WITH_PLATFORM}&workitem=MSK-US-3`);

    await userEvent.keyboard('{Escape}');

    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
    expect(here().search).toBe(WITH_PLATFORM);
    expect(filterField()).toHaveTextContent('Платформа');
  });

  it('a value that names no team selects nothing, and «Сбросить фильтр» takes it out of the address', async () => {
    serveBoards(projectDataset());
    renderBoards(`${projectBoardAddress()}?teamId=c3000000-0000-4000-8000-000000000099`);

    await screen.findByRole('heading', { level: 2, name: 'Ничего не найдено' });
    expect(here().search).toBe('?teamId=c3000000-0000-4000-8000-000000000099');

    await userEvent.click(screen.getByRole('button', { name: 'Сбросить фильтр' }));

    const board = await findBoard();
    expect(cardsIn(board, 'Новая')).toEqual(['MSK-US-2', 'MSK-US-3', 'MSK-US-9']);
    expect(here().search).toBe('');
  });

  it('a level change keeps several values, `none` among them, in the address’s order', async () => {
    serveBoards(projectDataset());
    const search = `?teamId=none&teamId=${ids.platformTeam}`;
    renderBoards(`${projectBoardAddress()}${search}`);
    await findBoard();

    await chooseLevel('Фичи');

    await waitFor(() => expect(here().pathname).toBe(projectBoardAddress('features')));
    expect(here().search).toBe(search);
  });
});
