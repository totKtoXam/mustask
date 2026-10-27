import { describe, expect, it } from 'vitest';
import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { HttpResponse, delay, http } from 'msw';
import { theme } from '../../../app/theme';
import { server } from '../../../test/mocks/server';
import { serveBoards } from '../testApi';
import { findBacklogTable, identifiers } from '../testBacklog';
import { findBoard } from '../testBoard';
import { BACKLOG_URL, ids, storiesDataset } from '../testFixtures';
import { levelButton } from '../testLevels';
import { backlogAddress, boardAddress, renderBoards, teamContext } from '../testRender';

// feat-027 US-1: the head of «Бэклоги» and «Канбан» as the reference screen
// has it (BR-T-16; design § 3): the team's name with its drop-down, one tab, the
// level drop-down at the tab row's right end, the primary and the grey button.

function teamButton(): HTMLElement {
  return screen.getByRole('button', { name: 'Сменить команду' });
}

describe('US-1 Scenario: The page head (feat-027 BR-T-16)', () => {
  it('reads the team’s name as the h1 with «Сменить команду» beside it, and the browser’s tab keeps «Бэклоги — Mustask»', async () => {
    serveBoards(storiesDataset());
    renderBoards(backlogAddress('epics'));
    await screen.findByRole('heading', { level: 2, name: 'Бэклог пуст' });

    const heading = screen.getByRole('heading', { level: 1, name: 'Платформа' });
    expect(heading.compareDocumentPosition(teamButton()) & Node.DOCUMENT_POSITION_FOLLOWING).toBe(
      Node.DOCUMENT_POSITION_FOLLOWING,
    );
    expect(heading.parentElement).toBe(teamButton().parentElement);
    await waitFor(() => expect(document.title).toBe('Бэклоги — Mustask'));
  });

  it('stands one tab «Бэклог» under it, selected, with «Эпики» and its chevron at the right end of the tab row, named «Уровень: Эпики»', async () => {
    serveBoards(storiesDataset());
    renderBoards(backlogAddress('epics'));
    await screen.findByRole('heading', { level: 2, name: 'Бэклог пуст' });

    const tabs = screen.getByRole('tablist', { name: 'Вид страницы' });
    const tab = within(tabs).getByRole('tab', { name: 'Бэклог' });
    expect(within(tabs).getAllByRole('tab')).toHaveLength(1);
    expect(tab).toHaveAttribute('aria-selected', 'true');
    expect(tab).toHaveAttribute('href', backlogAddress('epics'));
    const level = levelButton();
    expect(level).toHaveAccessibleName('Уровень: Эпики');
    expect(level).toHaveTextContent('Эпики');
    expect(level.querySelector('[data-testid="ExpandMoreIcon"]')).not.toBeNull();
    // The right end of the tab row.
    const row = tabs.closest('.MuiTabs-root')?.parentElement as HTMLElement;
    expect(row.lastElementChild).toBe(level);
    expect(getComputedStyle(row).justifyContent).toBe('space-between');
  });

  it('has «Новый рабочий элемент» as the one blue primary button — a leading plus, no trailing chevron — and «Показать канбан» as a grey button with an icon', async () => {
    serveBoards(storiesDataset());
    renderBoards(backlogAddress('stories'));
    await findBacklogTable();

    const primary = await screen.findByRole('button', { name: 'Новый рабочий элемент' });
    expect(primary).toHaveAttribute('aria-haspopup', 'menu');
    expect(primary.querySelector('[data-testid="AddIcon"]')).not.toBeNull();
    expect(primary.querySelector('[data-testid="ExpandMoreIcon"]')).toBeNull();
    expect(getComputedStyle(primary).getPropertyValue('--variant-containedBg')).toBe(
      theme.palette.primary.main,
    );
    const toBoard = screen.getByRole('link', { name: 'Показать канбан' });
    expect(toBoard).toHaveAttribute('href', boardAddress('stories'));
    expect(toBoard.querySelector('[data-testid="ArrowCircleRightOutlinedIcon"]')).not.toBeNull();
    expect(getComputedStyle(toBoard).getPropertyValue('--variant-containedBg')).toBe(
      theme.palette.shell.buttonNeutral,
    );
    // The primary stands before the grey button.
    expect(
      primary.compareDocumentPosition(toBoard) & Node.DOCUMENT_POSITION_FOLLOWING,
    ).toBeTruthy();
  });

  it('shows no planning pane, column options, tags, filter, full screen, «Аналитика» tab or favourite star', async () => {
    serveBoards(storiesDataset());
    renderBoards(backlogAddress('stories'));
    await findBacklogTable();

    expect(screen.queryByRole('tab', { name: /Аналитика/ })).toBeNull();
    expect(screen.queryByRole('button', { name: /избранн|звезд|участник/i })).toBeNull();
    expect(
      screen.queryByRole('button', { name: /фильтр|параметры|столбц|полный экран/i }),
    ).toBeNull();
    expect(screen.queryByRole('columnheader', { name: /Теги/ })).toBeNull();
  });
});

describe('US-1 Scenario: The team drop-down moves to another team of the organization (feat-040 BR-TS-11)', () => {
  it('«Команды» lists the organization’s teams with «Платформа» selected and `aria-current="page"`, and no «Весь проект»; «Мастаск» opens the same page of that team, its h1 reading «Мастаск»', async () => {
    serveBoards(storiesDataset());
    renderBoards(backlogAddress('epics'));
    await screen.findByRole('heading', { level: 2, name: 'Бэклог пуст' });

    const button = teamButton();
    expect(button).toHaveAttribute('aria-haspopup', 'menu');
    expect(button).toHaveAttribute('aria-expanded', 'false');
    await userEvent.click(button);
    const menu = await screen.findByRole('menu', { name: 'Команды' });
    expect(button).toHaveAttribute('aria-expanded', 'true');
    const entries = within(menu).getAllByRole('menuitem');
    // feat-040 BR-TS-11: the switcher's team level, no heading, no divider, and «Весь проект» is gone.
    expect(entries.map((entry) => entry.textContent)).toEqual([
      'Мастаск',
      'Платформа',
      'Аналитика',
    ]);
    expect(within(menu).queryByRole('separator')).toBeNull();
    expect(entries.map((entry) => entry.getAttribute('aria-current'))).toEqual([
      null,
      'page',
      null,
    ]);
    expect(entries[1]).toHaveClass('Mui-selected');
    expect(entries[0]).toHaveAttribute('href', backlogAddress('epics', ids.mskTeam));

    await userEvent.click(within(menu).getByRole('menuitem', { name: 'Мастаск' }));

    expect(screen.getByTestId('location')).toHaveTextContent(backlogAddress('epics', ids.mskTeam));
    expect(await screen.findByRole('heading', { level: 1, name: 'Мастаск' })).toBeInTheDocument();
  });

  it('works by keyboard: ArrowDown opens it, Escape closes it with focus back on the button', async () => {
    serveBoards(storiesDataset());
    renderBoards(backlogAddress('epics'));
    await screen.findByRole('heading', { level: 2, name: 'Бэклог пуст' });

    teamButton().focus();
    await userEvent.keyboard('{ArrowDown}');
    const menu = await screen.findByRole('menu', { name: 'Команды' });
    await waitFor(() =>
      expect(within(menu).getByRole('menuitem', { name: 'Платформа' })).toHaveFocus(),
    );
    await userEvent.keyboard('{Escape}');

    await waitFor(() => expect(screen.queryByRole('menu')).toBeNull());
    expect(teamButton()).toHaveFocus();
  });

  it('choosing a team by keyboard leaves focus on «Сменить команду» of the new page, not on the document (F-9)', async () => {
    serveBoards(storiesDataset());
    renderBoards(backlogAddress('epics'));
    await screen.findByRole('heading', { level: 2, name: 'Бэклог пуст' });

    teamButton().focus();
    await userEvent.keyboard('{ArrowDown}');
    const menu = await screen.findByRole('menu', { name: 'Команды' });
    await waitFor(() =>
      expect(within(menu).getByRole('menuitem', { name: 'Платформа' })).toHaveFocus(),
    );
    await userEvent.keyboard('{ArrowUp}{Enter}');

    expect(await screen.findByRole('heading', { level: 1, name: 'Мастаск' })).toBeInTheDocument();
    expect(screen.getByTestId('location')).toHaveTextContent(backlogAddress('epics', ids.mskTeam));
    await waitFor(() => expect(teamButton()).toHaveFocus());
  });

  it('a team chosen by the pointer does the same', async () => {
    serveBoards(storiesDataset());
    renderBoards(backlogAddress('epics'));
    await screen.findByRole('heading', { level: 2, name: 'Бэклог пуст' });

    await userEvent.click(teamButton());
    await userEvent.click(
      within(await screen.findByRole('menu', { name: 'Команды' })).getByRole('menuitem', {
        name: 'Аналитика',
      }),
    );

    expect(await screen.findByRole('heading', { level: 1, name: 'Аналитика' })).toBeInTheDocument();
    await waitFor(() => expect(teamButton()).toHaveFocus());
  });
});

describe('US-1 Scenario: An organization with one team still has the drop-down', () => {
  it('the h1 reads «Мастаск» with «Сменить команду» beside it, and its menu lists «Мастаск», selected, alone', async () => {
    serveBoards(storiesDataset());
    const context = teamContext({ teamId: ids.mskTeam });
    const [organization] = context.organizations;
    if (!organization) throw new Error('No organization.');
    renderBoards(backlogAddress('epics', ids.mskTeam), {
      context: {
        ...context,
        organizations: [{ ...organization, teams: [{ id: ids.mskTeam, name: 'Мастаск' }] }],
      },
    });
    await screen.findByRole('heading', { level: 2, name: 'Бэклог пуст' });

    expect(screen.getByRole('heading', { level: 1, name: 'Мастаск' })).toBeInTheDocument();
    await userEvent.click(teamButton());

    const menu = await screen.findByRole('menu', { name: 'Команды' });
    const entries = within(menu).getAllByRole('menuitem');
    expect(entries.map((entry) => entry.textContent)).toEqual(['Мастаск']);
    expect(entries[0]).toHaveAttribute('aria-current', 'page');
    expect(entries[0]).toHaveClass('Mui-selected');
  });
});

describe('US-1 Scenario: The heading while the team list is not read', () => {
  it('reads «Бэклоги» while nothing names the team, then the team’s name from the page’s own read; there is no drop-down while the list is loading', async () => {
    serveBoards(storiesDataset());
    let release: () => void = () => {};
    const held = new Promise<void>((resolve) => {
      release = resolve;
    });
    server.use(
      http.get(BACKLOG_URL, async () => {
        await held;
        await delay(0);
        return undefined;
      }),
    );
    renderBoards(backlogAddress('stories'), {
      context: { ...teamContext(), status: 'loading', organizations: [] },
    });

    expect(await screen.findByRole('heading', { level: 1, name: 'Бэклоги' })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Сменить команду' })).toBeNull();

    release();
    expect(identifiers(await findBacklogTable())).toEqual([
      'MSK-US-3',
      'MSK-BUG-5',
      'MSK-US-9',
      'MSK-US-11',
    ]);
    expect(screen.getByRole('heading', { level: 1, name: 'Платформа' })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Сменить команду' })).toBeNull();
  });

  it('keeps the head complete while the backlog is read: the team, the tab, the level and the grey button — the primary waits for the level’s types', async () => {
    serveBoards(storiesDataset());
    let release: () => void = () => {};
    const held = new Promise<void>((resolve) => {
      release = resolve;
    });
    server.use(
      http.get(BACKLOG_URL, async () => {
        await held;
        await delay(0);
        return HttpResponse.json({}, { status: 500 });
      }),
    );
    renderBoards(backlogAddress('stories'));

    expect(await screen.findByRole('status', { name: 'Загрузка данных' })).toBeInTheDocument();
    expect(screen.getByRole('heading', { level: 1, name: 'Платформа' })).toBeInTheDocument();
    expect(teamButton()).toBeInTheDocument();
    expect(screen.getByRole('tab', { name: 'Бэклог' })).toBeInTheDocument();
    expect(levelButton()).toHaveAccessibleName('Уровень: Истории');
    expect(screen.getByRole('link', { name: 'Показать канбан' })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Новый рабочий элемент' })).toBeNull();
    release();
  });
});

describe('US-1 Scenario: «Канбан» has the same head', () => {
  it('shows the team’s h1, the tab «Канбан», «Эпики» at the right end of the tab row, «Показать бэклог» as a grey button and no primary; the browser’s tab reads «Канбан — Mustask»', async () => {
    serveBoards(storiesDataset());
    renderBoards(boardAddress('stories'));
    await findBoard();

    expect(screen.getByRole('heading', { level: 1, name: 'Платформа' })).toBeInTheDocument();
    expect(teamButton()).toBeInTheDocument();
    const tabs = screen.getByRole('tablist', { name: 'Вид страницы' });
    const tab = within(tabs).getByRole('tab', { name: 'Канбан' });
    expect(tab).toHaveAttribute('aria-selected', 'true');
    expect(tab).toHaveAttribute('href', boardAddress('stories'));
    const level = levelButton();
    expect(level).toHaveAccessibleName('Уровень: Истории');
    expect(level).toHaveTextContent('Истории');
    expect(tabs.closest('.MuiTabs-root')?.parentElement?.lastElementChild).toBe(level);
    const toBacklog = screen.getByRole('link', { name: 'Показать бэклог' });
    expect(toBacklog).toHaveAttribute('href', backlogAddress('stories'));
    expect(toBacklog.querySelector('[data-testid="ArrowCircleLeftOutlinedIcon"]')).not.toBeNull();
    expect(getComputedStyle(toBacklog).getPropertyValue('--variant-containedBg')).toBe(
      theme.palette.shell.buttonNeutral,
    );
    // A board has no primary in its head (mod-015 § 10.4).
    expect(screen.queryByRole('button', { name: 'Новый рабочий элемент' })).toBeNull();
    await waitFor(() => expect(document.title).toBe('Канбан — Mustask'));
  });

  it('its team drop-down opens the same board of another team at the same level', async () => {
    serveBoards(storiesDataset());
    renderBoards(boardAddress('stories'));
    await findBoard();

    await userEvent.click(teamButton());
    await userEvent.click(
      within(await screen.findByRole('menu', { name: 'Команды' })).getByRole('menuitem', {
        name: 'Мастаск',
      }),
    );

    expect(screen.getByTestId('location')).toHaveTextContent(boardAddress('stories', ids.mskTeam));
    expect(await screen.findByRole('heading', { level: 1, name: 'Мастаск' })).toBeInTheDocument();
    // «Канбан» wears the same head: focus is on its «Сменить команду» too (F-9).
    await waitFor(() => expect(teamButton()).toHaveFocus());
  });
});
