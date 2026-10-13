import { describe, expect, it } from 'vitest';
import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { authValue, renderShell } from '../../../test/renderShell';
import { serveWorkItems } from '../../workitems/testApi';
import { serveBoards } from '../testApi';
import { findBacklogTable, identifiers, openActions, titleLink, rowOf } from '../testBacklog';
import { cardOf, findBoard } from '../testBoard';
import { boardsDataset, mskRecord, platformItem, type BoardsDataset } from '../testFixtures';
import { backlogAddress, boardAddress, boardsRoutes, teamContext } from '../testRender';

// feat-022 US-6 (BR-M3-39; spec § Backlog and board) as feat-027 D-7 amends
// it: «Приоритет» is on every card of the board, and no longer on a backlog
// row — the backlog table's columns are the reference screen's (feat-027
// BR-T-1, BR-T-3), and the priority stays on the card and on the form.

/** US-6's data on «Платформа»: MSK-US-12 of «Приоритет» 1, MSK-BUG-4 of 3, an epic and a feature. */
function priorityDataset(): BoardsDataset {
  const data = boardsDataset();
  data.items.push(
    platformItem(12, 'US', 'Вход по ссылке из письма', { fields: { priority: 1 } }),
    platformItem(4, 'BUG', 'Падение при входе', { fields: { priority: 3 } }),
    platformItem(1, 'EPIC', 'Вход и регистрация', { fields: { priority: 4 } }),
    platformItem(6, 'FEAT', 'Вход по ссылке'),
  );
  return data;
}

function renderBacklog(path: string) {
  return renderShell({
    path,
    auth: authValue([]),
    contextSource: teamContext(),
    routes: boardsRoutes(),
  });
}

describe('feat-027 D-7 Scenario: «Приоритет» is not on the backlog row', () => {
  it('no level of the backlog has a «Приоритет» column, and no row shows one, nested rows included', async () => {
    serveBoards(priorityDataset());
    renderBacklog(backlogAddress('stories'));

    const table = await findBacklogTable();

    expect(
      within(table)
        .getAllByRole('columnheader')
        .map((header) => header.getAttribute('aria-label') ?? header.textContent),
    ).toEqual([
      'Иерархия',
      'Порядок',
      'Тип',
      'Заголовок',
      'Статус',
      'Трудоёмкость',
      'Бизнес-ценность',
      'Вид ценности',
    ]);
    expect(screen.queryByText('Приоритет')).toBeNull();
    // MSK-BUG-4 and MSK-US-12 stand there with their priorities 3 and 1 stored — and unshown.
    expect(identifiers(table)).toEqual(['MSK-BUG-4', 'MSK-US-12']);
    expect(table).not.toHaveTextContent('Приоритет');
  });
});

describe('feat-022 US-6 Scenario: A card shows the priority', () => {
  it('MSK-US-12’s card reads «Приоритет: 1» as a caption on its third line, after the assignee', async () => {
    serveBoards(priorityDataset());
    renderBacklog(boardAddress('stories'));

    const card = cardOf(await findBoard(), 'MSK-US-12');
    const third = card.children[2] as HTMLElement;
    expect(third).toHaveTextContent(/^Не назначенПриоритет: 1$/);
    expect(within(third).getByText('Приоритет: 1')).toHaveClass('MuiTypography-caption');
    expect(within(cardOf(await findBoard(), 'MSK-BUG-4')).getByText('Приоритет: 3')).toBeVisible();
  });
});

describe('feat-022 US-6 Scenario: A change on the form shows on the board', () => {
  it('MSK-US-12 saved with «Приоритет» 4 in its card reads «Приоритет: 4» on the board', async () => {
    const data = priorityDataset();
    serveBoards(data);
    // The card's reads and save, over the same items.
    serveWorkItems(data);
    renderBacklog(backlogAddress('stories'));
    const table = await findBacklogTable();

    // A title opens the card as a dialog over the backlog (feat-029 BR-C-1).
    await userEvent.click(titleLink(rowOf(table, 'MSK-US-12')));
    await userEvent.click(await screen.findByRole('combobox', { name: 'Приоритет' }));
    await userEvent.click(screen.getByRole('option', { name: '4' }));
    await userEvent.click(screen.getByRole('button', { name: 'Сохранить' }));
    expect(await screen.findByText('Изменения сохранены.')).toBeInTheDocument();
    // The saved card keeps the user's place (focus goes to the title), so Esc closes it at once.
    await userEvent.keyboard('{Escape}');
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());

    const navigation = screen.getByRole('navigation', { name: 'Основное меню' });
    await userEvent.click(within(navigation).getByRole('link', { name: 'Канбан' }));
    expect(within(cardOf(await findBoard(), 'MSK-US-12')).getByText('Приоритет: 4')).toBeVisible();
    // The work item’s form is the card of feat-029: its kit fields outlast the default in jsdom.
  }, 120_000);
});

describe('feat-022 US-6 Scenario: Nothing else on the backlog or the board changes', () => {
  it('moving a row sends no priority and leaves every «Приоритет» as it was', async () => {
    const data = priorityDataset();
    const api = serveBoards(data);
    renderBacklog(backlogAddress('stories'));
    const table = await findBacklogTable();
    expect(identifiers(table)).toEqual(['MSK-BUG-4', 'MSK-US-12']);

    const menu = await openActions('MSK-BUG-4');
    await userEvent.click(within(menu).getByRole('menuitem', { name: 'В конец' }));

    await waitFor(() => expect(identifiers(table)).toEqual(['MSK-US-12', 'MSK-BUG-4']));
    expect(api.writes().map((request) => Object.keys(request.body as object))).toEqual([
      ['itemId', 'position'],
    ]);
    expect(mskRecord(data, 4).fields.priority).toBe(3);
    expect(mskRecord(data, 12).fields.priority).toBe(1);
  });
});
