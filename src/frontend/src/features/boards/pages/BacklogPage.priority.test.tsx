import { describe, expect, it } from 'vitest';
import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { Route } from 'react-router-dom';
import { delay, http } from 'msw';
import { server } from '../../../test/mocks/server';
import { authValue, renderShell } from '../../../test/renderShell';
import { WORK_ITEM_ROUTE, WorkItemFormPage } from '../../workitems';
import { serveWorkItems } from '../../workitems/testApi';
import { serveBoards } from '../testApi';
import { CELL, cellText, findBacklogTable, rowOf, rowsOf } from '../testBacklog';
import { cardOf, findBoard } from '../testBoard';
import { chooseLevel } from '../testLevels';
import {
  BACKLOG_URL,
  boardsDataset,
  mskRecord,
  platformItem,
  treeDataset,
  type BoardsDataset,
} from '../testFixtures';
import { backlogAddress, boardAddress, boardsRoutes, teamContext } from '../testRender';

// feat-022 US-6 (BR-M3-39; spec § Backlog and board): «Приоритет» on every
// backlog row, nested ones included, and on every card.

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

function headersOf(table: HTMLElement): string[] {
  return within(table)
    .getAllByRole('columnheader')
    .map((header) => header.textContent ?? '');
}

describe('feat-022 US-6 Scenario: The backlog shows a «Приоритет» column', () => {
  it('«Истории» has «Приоритет» right after «Статус»: MSK-US-12 reads «1», MSK-BUG-4 «3»; «Эпики» and «Фичи» too', async () => {
    serveBoards(priorityDataset());
    renderShell({
      path: backlogAddress('stories'),
      auth: authValue([]),
      contextSource: teamContext(),
      routes: boardsRoutes(),
    });

    const table = await findBacklogTable();
    const headers = headersOf(table);
    expect(headers[headers.indexOf('Статус') + 1]).toBe('Приоритет');
    expect(cellText(rowOf(table, 'MSK-US-12'), CELL.priority)).toBe('1');
    expect(cellText(rowOf(table, 'MSK-BUG-4'), CELL.priority)).toBe('3');

    await chooseLevel('Эпики');
    await waitFor(async () =>
      expect(cellText(rowOf(await findBacklogTable(), 'MSK-EPIC-1'), CELL.priority)).toBe('4'),
    );
    expect(headersOf(await findBacklogTable())).toContain('Приоритет');

    await chooseLevel('Фичи');
    await waitFor(async () =>
      expect(cellText(rowOf(await findBacklogTable(), 'MSK-FEAT-6'), CELL.priority)).toBe('2'),
    );
  });

  it('a nested row shows its own «Приоритет» (feat-001’s tree)', async () => {
    const data = treeDataset();
    mskRecord(data, 6).fields.priority = 1;
    serveBoards(data);
    renderShell({
      path: backlogAddress('epics'),
      auth: authValue([]),
      contextSource: teamContext(),
      routes: boardsRoutes(),
    });
    const table = await findBacklogTable();

    await userEvent.click(screen.getByRole('button', { name: 'Развернуть MSK-EPIC-1' }));

    await waitFor(() => expect(rowsOf(table).length).toBeGreaterThan(1));
    expect(cellText(rowOf(table, 'MSK-FEAT-6'), CELL.priority)).toBe('1');
    expect(cellText(rowOf(table, 'MSK-FEAT-8'), CELL.priority)).toBe('2');
  });
});

describe('feat-022 US-6 Scenario: The backlog skeleton names the new column', () => {
  it('the skeleton’s headers include «Приоритет» after «Статус»', async () => {
    serveBoards(priorityDataset());
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
    renderShell({
      path: backlogAddress('stories'),
      auth: authValue([]),
      contextSource: teamContext(),
      routes: boardsRoutes(),
    });

    const skeleton = await screen.findByRole('status', { name: 'Загрузка данных' });
    const headers = headersOf(skeleton);
    expect(headers[headers.indexOf('Статус') + 1]).toBe('Приоритет');
    release();
    expect(await findBacklogTable()).toBeInTheDocument();
  });
});

describe('feat-022 US-6 Scenario: A card shows the priority', () => {
  it('MSK-US-12’s card reads «Приоритет: 1» as a caption on its third line, after the assignee', async () => {
    serveBoards(priorityDataset());
    renderShell({
      path: boardAddress('stories'),
      auth: authValue([]),
      contextSource: teamContext(),
      routes: boardsRoutes(),
    });

    const card = cardOf(await findBoard(), 'MSK-US-12');
    const third = card.children[2] as HTMLElement;
    expect(third).toHaveTextContent(/^Не назначенПриоритет: 1$/);
    expect(within(third).getByText('Приоритет: 1')).toHaveClass('MuiTypography-caption');
    expect(within(cardOf(await findBoard(), 'MSK-BUG-4')).getByText('Приоритет: 3')).toBeVisible();
  });
});

describe('feat-022 US-6 Scenario: A change on the form shows on the backlog and the board', () => {
  it('MSK-US-12 saved with «Приоритет» 4 on its form reads «4» on the backlog and «Приоритет: 4» on the board', async () => {
    const data = priorityDataset();
    serveBoards(data);
    // The form's reads and save, over the same items.
    serveWorkItems(data);
    renderShell({
      path: backlogAddress('stories'),
      auth: authValue([]),
      contextSource: teamContext(),
      routes: (
        <>
          <Route path={WORK_ITEM_ROUTE} element={<WorkItemFormPage />} />
          {boardsRoutes()}
        </>
      ),
    });
    const table = await findBacklogTable();
    expect(cellText(rowOf(table, 'MSK-US-12'), CELL.priority)).toBe('1');

    await userEvent.click(within(rowOf(table, 'MSK-US-12')).getByRole('link'));
    await userEvent.click(await screen.findByRole('combobox', { name: 'Приоритет' }));
    await userEvent.click(screen.getByRole('option', { name: '4' }));
    await userEvent.click(screen.getByRole('button', { name: 'Сохранить' }));
    expect(await screen.findByText('Изменения сохранены.')).toBeInTheDocument();

    const navigation = screen.getByRole('navigation', { name: 'Основное меню' });
    await userEvent.click(within(navigation).getByRole('link', { name: 'Бэклоги' }));
    await waitFor(async () =>
      expect(cellText(rowOf(await findBacklogTable(), 'MSK-US-12'), CELL.priority)).toBe('4'),
    );

    await userEvent.click(within(navigation).getByRole('link', { name: 'Канбан' }));
    expect(within(cardOf(await findBoard(), 'MSK-US-12')).getByText('Приоритет: 4')).toBeVisible();
  });
});

describe('feat-022 US-6 Scenario: Nothing else on the backlog or the board changes', () => {
  it('moving a row sends no priority and leaves every «Приоритет» as it was', async () => {
    const data = priorityDataset();
    const api = serveBoards(data);
    renderShell({
      path: backlogAddress('stories'),
      auth: authValue([]),
      contextSource: teamContext(),
      routes: boardsRoutes(),
    });
    const table = await findBacklogTable();

    expect(rowsOf(table).map((row) => cellText(row, CELL.identifier))).toEqual([
      'MSK-BUG-4',
      'MSK-US-12',
    ]);

    await userEvent.click(screen.getByRole('button', { name: 'Переместить MSK-BUG-4' }));
    await userEvent.click(await screen.findByRole('menuitem', { name: 'В конец' }));

    await waitFor(() =>
      expect(rowsOf(table).map((row) => cellText(row, CELL.identifier))).toEqual([
        'MSK-US-12',
        'MSK-BUG-4',
      ]),
    );
    expect(api.writes().map((request) => Object.keys(request.body as object))).toEqual([
      ['itemId', 'position'],
    ]);
    expect(rowsOf(table).map((row) => cellText(row, CELL.priority))).toEqual(['1', '3']);
    expect(mskRecord(data, 4).fields.priority).toBe(3);
  });
});
