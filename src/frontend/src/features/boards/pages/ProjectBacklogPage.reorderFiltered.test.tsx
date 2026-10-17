import { afterEach, describe, expect, it } from 'vitest';
import { act, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { focusManager } from '@tanstack/react-query';
import { serveBoards } from '../testApi';
import { actions, announcer, findBacklogTable, identifiers, orders, rowOf } from '../testBacklog';
import { ids, item, projectDataset } from '../testFixtures';
import { projectBacklogAddress, renderBoards } from '../testRender';

// mod-017 US-5 (design § 6.3, api-contract § 2.6): a filtered project backlog is reordered against the
// full list. `projectDataset` puts MSK-US-2 on «Мастаск», MSK-US-3 on «Платформа» and MSK-US-9 on
// «Аналитика»; MSK-US-5 is a second «Мастаск» story the filter hides. The project's order starts 2, 3, 5, 9.

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

describe('mod-017 US-5 Scenario: A filtered backlog reorders against the full list (BR-OT-8)', () => {
  it('«В начало» and «Ниже» on the visible rows: the request carries no filter and names rows of the full list; the hidden rows keep their order; the position is told among the visible rows', async () => {
    const data = projectDataset();
    data.items.push(item(ids.msk, 5, 'US', 'Скрытая', { teamId: ids.mskTeam }));
    const api = serveBoards(data);
    renderBoards(projectBacklogAddress());
    const table = await findBacklogTable();
    expect(api.projectOrder(ids.msk)).toEqual(['MSK-US-2', 'MSK-US-3', 'MSK-US-5', 'MSK-US-9']);

    await chooseTeams('Платформа', 'Аналитика');
    await waitFor(() => expect(identifiers(table)).toEqual(['MSK-US-3', 'MSK-US-9']));

    await userEvent.click(actions('MSK-US-9'));
    await userEvent.click(await screen.findByRole('menuitem', { name: 'В начало' }));

    await waitFor(() => expect(identifiers(table)).toEqual(['MSK-US-9', 'MSK-US-3']));
    expect(orders(table)).toEqual(['1', '2']);
    expect(announcer()).toHaveTextContent('MSK-US-9 перемещён: позиция 1 из 2.');
    await waitFor(() => expect(actions('MSK-US-9')).not.toHaveAttribute('aria-disabled'));
    // Against the full list: the hidden MSK-US-2 and MSK-US-5 keep their relative order below the moved row.
    expect(api.projectOrder(ids.msk)).toEqual(['MSK-US-9', 'MSK-US-2', 'MSK-US-3', 'MSK-US-5']);

    await userEvent.click(actions('MSK-US-9'));
    await userEvent.click(await screen.findByRole('menuitem', { name: 'Ниже' }));

    await waitFor(() => expect(identifiers(table)).toEqual(['MSK-US-3', 'MSK-US-9']));
    expect(announcer()).toHaveTextContent('MSK-US-9 перемещён: позиция 2 из 2.');
    await waitFor(() => expect(actions('MSK-US-9')).not.toHaveAttribute('aria-disabled'));
    expect(api.projectOrder(ids.msk)).toEqual(['MSK-US-2', 'MSK-US-3', 'MSK-US-9', 'MSK-US-5']);

    const path = `/api/organizations/${ids.example}/projects/${ids.msk}/backlogs/stories/reorder`;
    expect(
      api
        .writes()
        .filter((request) => request.path.includes('/reorder'))
        .map((request) => [request.path, request.body]),
    ).toEqual([
      [path, { itemId: api.item(ids.msk, 9).id, position: 'top' }],
      [
        path,
        {
          itemId: api.item(ids.msk, 9).id,
          position: 'after',
          afterItemId: api.item(ids.msk, 3).id,
        },
      ],
    ]);
    // The filter outlives the writes: the last read still carries it.
    expect(api.reads('/backlogs/stories').at(-1)?.path).toContain('teamId=');
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
