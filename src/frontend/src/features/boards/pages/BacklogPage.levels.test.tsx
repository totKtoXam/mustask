import { afterEach, describe, expect, it } from 'vitest';
import { fireEvent, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { focusManager } from '@tanstack/react-query';
import { Route, useNavigate } from 'react-router-dom';
import { authValue, renderShell } from '../../../test/renderShell';
import { BACKLOG_ROUTE, BOARD_ROUTE } from '../paths';
import { serveBoards } from '../testApi';
import { findBacklogTable, identifiers } from '../testBacklog';
import { cardsIn, findBoard } from '../testBoard';
import { platformItem, storiesDataset } from '../testFixtures';
import { chooseLevel, levelButton, levelMenu } from '../testLevels';
import { backlogAddress, boardAddress, mskForm, renderBoards, teamContext } from '../testRender';
import { BacklogPage } from './BacklogPage';
import { BoardPage } from './BoardPage';

// feat-001 US-6: one level drop-down on «Бэклоги» and «Канбан» in place of
// mod-006's tabs, and the board named «Канбан» (BR-H-15, BR-H-16; spec
// § The rename, § Level drop-down). The data is mod-006's stories' example
// with an epic and a feature added, so every level has a row.

afterEach(() => {
  focusManager.setFocused(undefined);
});

function dataset() {
  const data = storiesDataset(['US-3', 'BUG-5']);
  data.items.push(
    platformItem(1, 'EPIC', 'Единый вход'),
    platformItem(6, 'FEAT', 'Вход по ссылке'),
  );
  return data;
}

async function backlogIdentifiers(): Promise<string[]> {
  return identifiers(await findBacklogTable());
}

/** The browser's back, as a test presses it: the router's own history step. */
function BackProbe() {
  const navigate = useNavigate();
  return <button type="button" data-testid="history-back" hidden onClick={() => navigate(-1)} />;
}

function renderWithBack(path: string) {
  return renderShell({
    path,
    auth: authValue([]),
    contextSource: teamContext(),
    routes: (
      <>
        <Route
          path={BACKLOG_ROUTE}
          element={
            <>
              <BacklogPage />
              <BackProbe />
            </>
          }
        />
        <Route path={BOARD_ROUTE} element={<BoardPage />} />
      </>
    ),
  });
}

describe('US-6 Scenario: The drop-down replaces the tabs on «Бэклоги» (feat-027 BR-T-16: at the tab row’s right end)', () => {
  it('reads «Истории» named «Уровень: Истории» at the right end of the tab row, offers the three levels with «Истории» current, and «Фичи» opens its own address with focus kept on the button', async () => {
    serveBoards(dataset());
    renderBoards(backlogAddress('stories'));

    expect(await backlogIdentifiers()).toEqual(['MSK-US-3', 'MSK-BUG-5']);
    // One tab, «Бэклог»: the level is no tab but the drop-down beside it.
    expect(screen.getAllByRole('tab').map((tab) => tab.textContent)).toEqual(['Бэклог']);
    const button = levelButton();
    expect(button).toHaveAccessibleName('Уровень: Истории');
    expect(button).toHaveTextContent('Истории');
    expect(button.parentElement).toBe(
      screen.getByRole('tablist').closest('.MuiTabs-root')?.parentElement,
    );
    expect(button).toHaveAttribute('aria-haspopup', 'menu');
    expect(button).toHaveAttribute('aria-expanded', 'false');
    expect(button.querySelector('[data-testid="ExpandMoreIcon"]')).not.toBeNull();
    // Under the h1, after the command bar: the next control in document order.
    const heading = screen.getByRole('heading', { level: 1, name: 'Платформа' });
    const toBoard = screen.getByRole('link', { name: 'Показать канбан' });
    expect(heading.compareDocumentPosition(button) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
    expect(toBoard.compareDocumentPosition(button) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();

    await userEvent.click(button);
    const menu = await levelMenu();
    expect(button).toHaveAttribute('aria-expanded', 'true');
    expect(document.getElementById(button.getAttribute('aria-controls') ?? '')).toContainElement(
      menu,
    );
    const items = within(menu).getAllByRole('menuitem');
    expect(items.map((element) => element.textContent)).toEqual(['Эпики', 'Фичи', 'Истории']);
    expect(items.map((element) => element.getAttribute('aria-current'))).toEqual([
      null,
      null,
      'page',
    ]);
    expect(items[2]).toHaveClass('Mui-selected');
    await waitFor(() => expect(items[2]).toHaveFocus());

    await userEvent.click(within(menu).getByRole('menuitem', { name: 'Фичи' }));

    expect(screen.getByTestId('location')).toHaveTextContent(backlogAddress('features'));
    expect(await backlogIdentifiers()).toEqual(['MSK-FEAT-6']);
    expect(levelButton()).toHaveAccessibleName('Уровень: Фичи');
    await waitFor(() => expect(levelButton()).toHaveFocus());
    expect(screen.queryByRole('menu')).toBeNull();
  });
});

describe('US-6 Scenario: The same drop-down on «Канбан»', () => {
  it('`<team>/boards/epics` reads the team, the tab «Канбан» and «Уровень: Эпики»; «Истории» opens `<team>/boards/stories`', async () => {
    serveBoards(dataset());
    renderBoards(boardAddress('epics'));

    expect(cardsIn(await findBoard(), 'Новая')).toEqual(['MSK-EPIC-1']);
    expect(screen.getByRole('heading', { level: 1, name: 'Платформа' })).toBeInTheDocument();
    expect(screen.getAllByRole('tab').map((tab) => tab.textContent)).toEqual(['Канбан']);
    expect(levelButton()).toHaveAccessibleName('Уровень: Эпики');

    await chooseLevel('Истории');

    expect(screen.getByTestId('location')).toHaveTextContent(boardAddress('stories'));
    await waitFor(async () =>
      expect(cardsIn(await findBoard(), 'Новая')).toEqual(['MSK-US-3', 'MSK-BUG-5']),
    );
    expect(levelButton()).toHaveAccessibleName('Уровень: Истории');
  });
});

describe('US-6 Scenario: Back returns to the previous level', () => {
  it('from «Истории» to «Фичи» with the drop-down, back shows «Истории» and the button reads it', async () => {
    serveBoards(dataset());
    renderWithBack(backlogAddress('stories'));
    expect(await backlogIdentifiers()).toEqual(['MSK-US-3', 'MSK-BUG-5']);

    await chooseLevel('Фичи');
    expect(await backlogIdentifiers()).toEqual(['MSK-FEAT-6']);

    fireEvent.click(screen.getByTestId('history-back'));

    expect(screen.getByTestId('location')).toHaveTextContent(backlogAddress('stories'));
    await waitFor(async () =>
      expect(await backlogIdentifiers()).toEqual(['MSK-US-3', 'MSK-BUG-5']),
    );
    expect(levelButton()).toHaveAccessibleName('Уровень: Истории');
  });
});

describe('US-6 Scenario: The drop-down works by keyboard', () => {
  it('ArrowDown opens on «Истории»; ArrowUp twice and Enter shows «Эпики»; Escape closes with focus back on the button', async () => {
    serveBoards(dataset());
    renderBoards(backlogAddress('stories'));
    expect(await backlogIdentifiers()).toEqual(['MSK-US-3', 'MSK-BUG-5']);

    levelButton().focus();
    await userEvent.keyboard('{ArrowDown}');
    const menu = await levelMenu();
    await waitFor(() =>
      expect(within(menu).getByRole('menuitem', { name: 'Истории' })).toHaveFocus(),
    );
    await userEvent.keyboard('{ArrowUp}{ArrowUp}');
    expect(within(menu).getByRole('menuitem', { name: 'Эпики' })).toHaveFocus();
    await userEvent.keyboard('{Enter}');

    expect(screen.getByTestId('location')).toHaveTextContent(backlogAddress('epics'));
    expect(await backlogIdentifiers()).toEqual(['MSK-EPIC-1']);
    expect(levelButton()).toHaveAccessibleName('Уровень: Эпики');
    await waitFor(() => expect(levelButton()).toHaveFocus());

    // Enter and Space open it too; End and Home jump; Escape closes, focus back.
    await userEvent.keyboard('{Enter}');
    const again = await levelMenu();
    await waitFor(() =>
      expect(within(again).getByRole('menuitem', { name: 'Эпики' })).toHaveFocus(),
    );
    await userEvent.keyboard('{End}');
    expect(within(again).getByRole('menuitem', { name: 'Истории' })).toHaveFocus();
    await userEvent.keyboard('{Home}');
    expect(within(again).getByRole('menuitem', { name: 'Эпики' })).toHaveFocus();
    await userEvent.keyboard('{Escape}');
    await waitFor(() => expect(screen.queryByRole('menu')).toBeNull());
    expect(levelButton()).toHaveFocus();
    expect(screen.getByTestId('location')).toHaveTextContent(backlogAddress('epics'));

    await userEvent.keyboard(' ');
    expect(await levelMenu()).toBeInTheDocument();
  });
});

describe('US-6 Scenario: Each level keeps its own address', () => {
  it('`<team>/backlogs/epics` opened directly reads «Уровень: Эпики»; `<team>/backlogs/tasks` is «Страница не найдена»', async () => {
    serveBoards(dataset());
    const view = renderBoards(backlogAddress('epics'));

    expect(await backlogIdentifiers()).toEqual(['MSK-EPIC-1']);
    expect(levelButton()).toHaveAccessibleName('Уровень: Эпики');
    view.unmount();

    renderBoards(backlogAddress('tasks'));
    expect(
      await screen.findByRole('heading', { level: 1, name: 'Страница не найдена' }),
    ).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /^Уровень: / })).toBeNull();
  });
});

describe('US-6 Scenario: The board is named «Канбан» everywhere it is named', () => {
  it('the entry, the tab, the browser tab, the region and «Показать канбан» read «Канбан»; nothing reads «Доски» or «Показать доску»', async () => {
    serveBoards(dataset());
    renderBoards(mskForm('MSK-US-3'));

    const navigation = screen.getByRole('navigation', { name: 'Основное меню' });
    const entry = within(navigation).getByRole('link', { name: 'Канбан' });
    expect(entry).toHaveAttribute('href', boardAddress('stories'));
    expect(entry.querySelector('[data-testid="ViewWeekOutlinedIcon"]')).not.toBeNull();
    await userEvent.click(entry);

    expect(await findBoard()).toBeInTheDocument();
    expect(screen.getByRole('heading', { level: 1, name: 'Платформа' })).toBeInTheDocument();
    expect(screen.getByRole('tab', { name: 'Канбан' })).toHaveAttribute('aria-selected', 'true');
    await waitFor(() => expect(document.title).toBe('Канбан — Mustask'));
    expect(document.body).not.toHaveTextContent(/Доски|Показать доску/);

    await chooseLevel('Фичи');
    await userEvent.click(screen.getByRole('link', { name: 'Показать бэклог' }));
    expect(await backlogIdentifiers()).toEqual(['MSK-FEAT-6']);
    const toBoard = screen.getByRole('link', { name: 'Показать канбан' });
    expect(toBoard).toHaveAttribute('href', boardAddress('features'));
    expect(toBoard.querySelector('[data-testid="ArrowCircleRightOutlinedIcon"]')).not.toBeNull();
    expect(document.body).not.toHaveTextContent(/Доски|Показать доску/);

    await userEvent.click(toBoard);
    expect(screen.getByTestId('location')).toHaveTextContent(boardAddress('features'));
    expect(cardsIn(await findBoard(), 'Новая')).toEqual(['MSK-FEAT-6']);
  });

  it('a level with no item shows «Канбан пуст» with mod-006’s hint and «Новый рабочий элемент»', async () => {
    serveBoards(storiesDataset());
    renderBoards(boardAddress('epics'));

    expect(await screen.findByRole('heading', { level: 2, name: 'Канбан пуст' })).toBeVisible();
    expect(
      screen.getByText('Новые рабочие элементы этого уровня появятся в колонке «Новая».'),
    ).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Новый рабочий элемент' })).toBeInTheDocument();
    expect(document.body).not.toHaveTextContent(/Доски|На доске нет рабочих элементов/);
  });
});
