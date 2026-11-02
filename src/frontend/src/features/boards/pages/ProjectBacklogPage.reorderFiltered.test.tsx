import { afterEach, describe, expect, it } from 'vitest';
import { act, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { focusManager } from '@tanstack/react-query';
import { serveBoards } from '../testApi';
import {
  CELL,
  actions,
  cellOf,
  findBacklogTable,
  identifiers,
  rowOf,
  rowsOf,
} from '../testBacklog';
import { dataTransfer, fireDrag } from '../testDrag';
import { ids, item, projectDataset } from '../testFixtures';
import { projectBacklogAddress, renderBoards } from '../testRender';

// mod-017 US-5 once reordered a filtered project backlog against the full list; feat-043 US-4 (Q-9 A)
// retires that: a backlog narrowed by a chosen team keeps its order and offers no reorder. `projectDataset` puts MSK-US-2 on «Мастаск», MSK-US-3 on «Платформа» and MSK-US-9 on
// «Аналитика»; MSK-US-5 is a second «Мастаск» story the filter hides. The project's order starts 2, 3, 5, 9.

const HINT = 'Пока выбор сужает список, порядок не меняется. Выберите «все», чтобы менять его.';

afterEach(() => {
  focusManager.setFocused(undefined);
});

async function chooseTeams(...names: string[]) {
  const band = screen.getByRole('group', { name: 'Фильтры' });
  await userEvent.click(within(band).getByRole('combobox', { name: 'Команда' }));
  for (const name of names) {
    await userEvent.click(await screen.findByRole('option', { name }));
  }
  await userEvent.keyboard('{Escape}');
  await waitFor(() => expect(screen.queryByRole('listbox')).not.toBeInTheDocument());
}

describe('feat-043 US-4 Scenario: A narrowed project backlog keeps its order and cannot be reordered (Q-9 A)', () => {
  it('under a chosen team the hint and the tag show, the row menu offers no move and a drag draws no line; clearing the filter gives the move back', async () => {
    const data = projectDataset();
    data.items.push(item(ids.msk, 5, 'US', 'Скрытая', { teamId: ids.mskTeam }));
    const api = serveBoards(data);
    renderBoards(projectBacklogAddress());
    const table = await findBacklogTable();
    expect(screen.queryByText(HINT)).not.toBeInTheDocument();

    await chooseTeams('Платформа', 'Аналитика');
    await waitFor(() => expect(identifiers(table)).toEqual(['MSK-US-3', 'MSK-US-9']));

    expect(screen.getByText(HINT)).toBeVisible();
    expect(screen.getByText('сужено: команда Платформа, Аналитика')).toBeVisible();

    await userEvent.click(actions('MSK-US-9'));
    const menu = await screen.findByRole('menu', { name: 'Ещё действия: MSK-US-9' });
    expect(within(menu).queryByRole('menuitem', { name: 'В начало' })).not.toBeInTheDocument();
    expect(within(menu).queryByRole('menuitem', { name: 'Ниже' })).not.toBeInTheDocument();
    expect(within(menu).getByRole('menuitem', { name: 'Изменить родителя…' })).toBeVisible();
    await userEvent.keyboard('{Escape}');
    await waitFor(() => expect(screen.queryByRole('menu')).not.toBeInTheDocument());

    // No drag at all: no grip, rows not draggable (design § 7.1); nothing is offered between blocks.
    expect(table.querySelectorAll('[data-grip]')).toHaveLength(0);
    expect(rowOf(table, 'MSK-US-9')).not.toHaveAttribute('draggable', 'true');
    const transfer = dataTransfer();
    const source = rowOf(table, 'MSK-US-9');
    await act(async () => fireDrag('dragStart', source, transfer));
    await act(async () =>
      fireDrag('dragOver', cellOf(rowOf(table, 'MSK-US-3'), CELL.title), transfer, 'upper'),
    );
    expect(rowsOf(table).filter((row) => row.hasAttribute('data-drop-line'))).toHaveLength(0);
    await act(async () => fireDrag('dragEnd', source, transfer));
    expect(api.writes().filter((request) => request.path.includes('/reorder'))).toEqual([]);
    expect(api.projectOrder(ids.msk)).toEqual(['MSK-US-2', 'MSK-US-3', 'MSK-US-5', 'MSK-US-9']);

    await userEvent.click(screen.getByRole('button', { name: 'Очистить фильтры' }));
    await waitFor(() => expect(screen.queryByText(HINT)).not.toBeInTheDocument());
    await waitFor(() => expect(identifiers(table)).toHaveLength(4));
    await userEvent.click(actions('MSK-US-9'));
    expect(await screen.findByRole('menuitem', { name: 'В начало' })).toBeVisible();
  });
});

describe('mod-017 US-5 Scenario: A chosen team deleted meanwhile (design § 5.2, BR-OT-12)', () => {
  it('stays in the filter and selects nothing; «Очистить фильтры» removes it and the team’s items stand under «Без команды»', async () => {
    const api = serveBoards(projectDataset());
    renderBoards(projectBacklogAddress());
    const table = await findBacklogTable();
    await chooseTeams('Платформа');
    await waitFor(() => expect(identifiers(table)).toEqual(['MSK-US-3']));

    api.deleteTeam(ids.platformTeam);
    act(() => {
      focusManager.setFocused(false);
      focusManager.setFocused(true);
    });

    expect((await screen.findAllByText('Ничего не найдено')).length).toBeGreaterThan(0);
    expect(api.reads('/backlogs/stories').at(-1)?.path).toContain(`teamId=${ids.platformTeam}`);

    await userEvent.click(screen.getByRole('button', { name: 'Очистить фильтры' }));

    const again = await findBacklogTable();
    await waitFor(() => expect(identifiers(again)).toEqual(['MSK-US-2', 'MSK-US-3', 'MSK-US-9']));
    expect(within(rowOf(again, 'MSK-US-3')).getByText('Без команды')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Очистить фильтры' })).toBeNull();
  });
});
