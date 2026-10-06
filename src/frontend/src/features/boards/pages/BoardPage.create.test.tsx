import { describe, expect, it } from 'vitest';
import { fireEvent, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { serveBoards } from '../testApi';
import { cardsIn, columnBox, columnNames, findBoard } from '../testBoard';
import { boardsDataset, ids, item, platformItem, storiesDataset } from '../testFixtures';
import { boardAddress, renderBoards } from '../testRender';

// US-6: creating from a team's board with mod-003's dialog, as US-3's from
// the backlog (spec § Dialog; BR-M4-15, BR-M4-21).

async function dialog() {
  return screen.findByRole('dialog', { name: 'Новый рабочий элемент' });
}

async function create(title: string) {
  const found = await dialog();
  await userEvent.type(within(found).getByRole('textbox', { name: 'Заголовок' }), title);
  await userEvent.click(within(found).getByRole('button', { name: 'Создать' }));
  return found;
}

describe('US-6 Scenario: «Новый элемент» sits at the top of the first column', () => {
  it('is the first control in «Новые» and opens the menu of «Пользовательская история» and «Баг»', async () => {
    serveBoards(storiesDataset(['US-3']));
    renderBoards(boardAddress());
    const board = await findBoard();

    const first = within(columnBox(board, 'Новые')).getAllByRole('button')[0];
    expect(first).toHaveTextContent('Новый элемент');
    expect(first?.querySelector('[data-testid="AddIcon"]')).not.toBeNull();
    expect(first).toHaveAttribute('aria-haspopup', 'menu');
    expect(within(columnBox(board, 'В работе')).queryByText('Новый элемент')).toBeNull();
    if (!first) throw new Error('No control.');
    await userEvent.click(first);

    const menu = await screen.findByRole('menu', { name: 'Новый рабочий элемент' });
    expect(
      within(menu)
        .getAllByRole('menuitem')
        .map((entry) => entry.textContent),
    ).toEqual(['Пользовательская история', 'Баг']);
    expect(first).toHaveAttribute('aria-expanded', 'true');
  });

  it('on the «Фичи» board it opens the dialog at once with «Тип» «Фича»', async () => {
    const data = boardsDataset();
    data.items.push(platformItem(6, 'FEAT', 'Отчёты'));
    serveBoards(data);
    renderBoards(boardAddress('features'));
    const board = await findBoard();

    const button = within(columnBox(board, 'Новые')).getByRole('button', { name: 'Новый элемент' });
    expect(button).not.toHaveAttribute('aria-haspopup');
    await userEvent.click(button);

    expect(screen.queryByRole('menu')).toBeNull();
    const select = within(await dialog()).getByRole('combobox', { name: 'Тип' });
    expect(select).toHaveTextContent('Фича');
    fireEvent.mouseDown(select);
    expect(
      within(await screen.findByRole('listbox'))
        .getAllByRole('option')
        .map((option) => option.textContent),
    ).toEqual(['Фича']);
  });
});

describe('US-6 Scenario: The new card is the last of «Новые», in the team’s area', () => {
  it('a «Фича» «Отчёты по командам» from «Новый элемент»: the notice, then MSK-FEAT-6 and the new card in «Новые»', async () => {
    const data = boardsDataset();
    data.items.push(platformItem(6, 'FEAT', 'Отчёты'));
    const api = serveBoards(data);
    renderBoards(boardAddress('features'));
    const board = await findBoard();

    await userEvent.click(
      within(columnBox(board, 'Новые')).getByRole('button', { name: 'Новый элемент' }),
    );
    const found = await dialog();
    expect(
      within(found).getByText(
        'Рабочий элемент будет создан в статусе «Новая» в области «Платформа» и встанет в конец бэклога команды «Платформа». Остальные поля заполняются в его форме.',
      ),
    ).toBeInTheDocument();
    await create('Отчёты по командам');

    expect(await screen.findByText('Рабочий элемент MSK-FEAT-7 создан.')).toBeInTheDocument();
    await waitFor(() => expect(cardsIn(board, 'Новые')).toEqual(['MSK-FEAT-6', 'MSK-FEAT-7']));
    expect(api.item(ids.msk, 7).areaId).toBe(ids.platform);
    expect(api.order(ids.platformTeam, 'features')).toEqual(['MSK-FEAT-6', 'MSK-FEAT-7']);
    const post = api.writes().find((request) => request.path.endsWith('/workitems'));
    expect((post?.body as { teamId: string }).teamId).toBe(ids.platformTeam);
  });
});

describe('US-6 Scenario: On the first team’s board the area is the root', () => {
  it('a «Баг» on «Мастаск»’s «Истории» board takes «Мастаск» and is the last card in «Новые»', async () => {
    const data = boardsDataset();
    data.items.push(item(ids.msk, 5, 'US', 'Сводка', { areaId: ids.mskRoot }));
    const api = serveBoards(data);
    renderBoards(boardAddress('stories', ids.mskTeam));
    const board = await findBoard();

    await userEvent.click(
      within(columnBox(board, 'Новые')).getByRole('button', { name: 'Новый элемент' }),
    );
    await userEvent.click(await screen.findByRole('menuitem', { name: 'Баг' }));
    await create('Сбой');

    await waitFor(() => expect(cardsIn(board, 'Новые')).toEqual(['MSK-US-5', 'MSK-BUG-6']));
    expect(api.item(ids.msk, 6).areaId).toBe(ids.mskRoot);
  });
});

describe('US-6 Scenario: Creating from the empty board', () => {
  it('«Аналитика»’s «Эпики» frame opens the dialog with «Эпик»; after «Миграция данных» the four columns show, its card in «Новые»', async () => {
    serveBoards(storiesDataset());
    renderBoards(boardAddress('epics', ids.analyticsTeam));
    await screen.findByRole('heading', { level: 2, name: 'На доске нет рабочих элементов' });

    await userEvent.click(screen.getByRole('button', { name: 'Новый рабочий элемент' }));
    expect(within(await dialog()).getByRole('combobox', { name: 'Тип' })).toHaveTextContent('Эпик');
    await create('Миграция данных');

    const board = await findBoard();
    expect(columnNames(board)).toEqual([
      'Новые, рабочих элементов: 1',
      'В работе, рабочих элементов: 0',
      'Разрешена, рабочих элементов: 0',
      'Завершённые, рабочих элементов: 0',
    ]);
    expect(cardsIn(board, 'Новые')).toEqual(['MSK-EPIC-12']);
    expect(
      screen.queryByRole('heading', { level: 2, name: 'На доске нет рабочих элементов' }),
    ).toBeNull();
  });
});

describe('US-6 Scenario: The team was deleted while the dialog was open', () => {
  it('shows «Команда не найдена.» in the dialog, creates nothing, and closing shows the frame', async () => {
    const api = serveBoards(storiesDataset());
    renderBoards(boardAddress());
    const board = await findBoard();
    await userEvent.click(
      within(columnBox(board, 'Новые')).getByRole('button', { name: 'Новый элемент' }),
    );
    await userEvent.click(
      await screen.findByRole('menuitem', { name: 'Пользовательская история' }),
    );

    api.data.teams = api.data.teams.map((team) =>
      team.id === ids.platformTeam ? { ...team, active: false } : team,
    );
    const found = await create('Экспорт');

    expect(await within(found).findByRole('alert')).toHaveTextContent('Команда не найдена.');
    expect(api.data.items).toHaveLength(4);
    await userEvent.click(within(found).getByRole('button', { name: 'Отмена' }));

    expect(
      await screen.findByRole('heading', { level: 1, name: 'Команда не найдена' }),
    ).toBeInTheDocument();
  });
});
