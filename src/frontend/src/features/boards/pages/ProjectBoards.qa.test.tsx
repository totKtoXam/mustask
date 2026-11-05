import { afterEach, describe, expect, it } from 'vitest';
import { act, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { focusManager } from '@tanstack/react-query';
import { HttpResponse, http } from 'msw';
import { server } from '../../../test/mocks/server';
import { WORK_ITEM_URL } from '../../workitems/testFixtures';
import { serveBoards } from '../testApi';
import { CELL, cellOf, findBacklogTable, identifiers, outline, rowOf } from '../testBacklog';
import { cardMenuButton, cardsIn, findBoard } from '../testBoard';
import { dataTransfer, fireDrag } from '../testDrag';
import { PROBLEM_HEADERS, ids, problem, projectDataset } from '../testFixtures';
import { parentWrites, openParentDialog, pickParent, saveButton } from '../testParentDialog';
import { projectBacklogAddress, projectBoardAddress, renderBoards } from '../testRender';

// QA of feat-039 (qa.md, Frontend): what the developer's page tests leave unpinned. The data is
// US-1's: MSK-US-2 (the first team's area), MSK-US-3 («Платформа»), MSK-US-9 («Аналитика»), all
// «Новая»; OPS-US-1 is another project's.

afterEach(() => {
  focusManager.setFocused(undefined);
});

describe('OQ-5 / BR-PB-3: the row actions of the team backlog work on the project’s backlog', () => {
  // A drop onto another row is a parent write (feat-027 BR-T-11), never a place (US-2, "from any
  // point of the row"): the project’s own two views are read again, and no order is written.
  it('a story dropped onto another story is the parent write, names no team, writes no place, and the project’s backlog is read again', async () => {
    const api = serveBoards(projectDataset());
    renderBoards(projectBacklogAddress());
    const table = await findBacklogTable();
    expect(identifiers(table)).toEqual(['MSK-US-2', 'MSK-US-3', 'MSK-US-9']);
    const readsBefore = api.reads('/backlogs/stories').length;

    const transfer = dataTransfer();
    const source = rowOf(table, 'MSK-US-9');
    const over = cellOf(rowOf(table, 'MSK-US-2'), CELL.title);
    await act(async () => fireDrag('dragStart', source, transfer));
    await act(async () => fireDrag('dragOver', over, transfer));
    await act(async () => fireDrag('drop', over, transfer));
    await act(async () => fireDrag('dragEnd', source, transfer));

    expect(await screen.findByRole('alert')).toHaveTextContent(
      'У MSK-US-9 теперь родитель MSK-US-2.',
    );
    expect(parentWrites(api).map((request) => request.body)).toMatchObject([
      { parentId: api.item(ids.msk, 2).id },
    ]);
    expect(api.writes().filter((request) => request.path.endsWith('/reorder'))).toEqual([]);
    expect(api.data.projectPlaces).toEqual([]);
    expect(api.data.places).toEqual([]);
    await waitFor(() => expect(api.reads('/backlogs/stories').length).toBeGreaterThan(readsBefore));
    // Every read of the page was the project's own, none a team's.
    expect(api.reads('/teams/')).toEqual([]);
    await waitFor(() => expect(outline(table)).toEqual(['MSK-US-2', '  MSK-US-9', 'MSK-US-3']));
  });

  it('«Изменить родителя…» from a row’s «Ещё действия» writes the parent too', async () => {
    const api = serveBoards(projectDataset());
    renderBoards(projectBacklogAddress());
    await findBacklogTable();

    const dialog = await openParentDialog('MSK-US-9');
    await pickParent(dialog, 'MSK-US-3');
    await userEvent.click(saveButton(dialog));

    await waitFor(() => expect(parentWrites(api)).toHaveLength(1));
    expect(parentWrites(api).map((request) => request.body)).toMatchObject([
      { parentId: api.item(ids.msk, 3).id },
    ]);
    expect(api.writes().filter((request) => request.path.endsWith('/reorder'))).toEqual([]);
    expect(api.reads('/teams/')).toEqual([]);
  });
});

describe('BR-PB-5 / mod-016 design § 2.4: a card move the scoped check refuses, on the project’s board', () => {
  it('shows «Недостаточно прав» at once and the card is back in its column', async () => {
    serveBoards(projectDataset());
    renderBoards(projectBoardAddress(), { role: 'member' });
    const board = await findBoard();
    server.use(
      http.patch(WORK_ITEM_URL, () =>
        HttpResponse.json(problem('organizations.access.forbidden'), {
          status: 403,
          headers: PROBLEM_HEADERS,
        }),
      ),
    );

    await userEvent.click(cardMenuButton('MSK-US-3'));
    const menu = await screen.findByRole('menu', { name: 'Переместить в колонку' });
    await userEvent.click(within(menu).getByRole('menuitem', { name: 'Активна' }));

    expect(await screen.findByText('Недостаточно прав')).toBeVisible();
    await waitFor(() => expect(cardsIn(board, 'Новая')).toContain('MSK-US-3'));
    expect(cardsIn(board, 'Активна')).not.toContain('MSK-US-3');
  });
});
