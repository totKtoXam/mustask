import { describe, expect, it, vi } from 'vitest';
import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { serveBoards } from '../testApi';
import {
  announcer,
  expandOneLevel,
  findBacklogTable,
  identifiers,
  rowOf,
  rowsOf,
} from '../testBacklog';
import { boardsDataset, ids, item } from '../testFixtures';
import { projectBacklogAddress, renderBoards } from '../testRender';

// feat-039 US-1 "A large project backlog" and design § 7 "Size" (D-4): a project's level can pass
// the drawn-row cap on its top-level rows alone (feat-027 BR-T-7, 2 000 rows; `tree.test.ts` pins
// the number and the rule). Then every top-level row is drawn, none is expanded, no row is withheld,
// and there is no banner, no «Показать ещё» and no notice about the cap; the header buttons work
// from what that leaves. The page is drawn here with a cap of 5, so that 6 top-level rows stand for
// the 5 000 stories of the story: the page’s path is the same, and a 2 001-row table takes a minute to draw.

const { CAP } = vi.hoisted(() => ({ CAP: 5 }));

vi.mock('../lib/tree', async (importOriginal) => {
  const actual = await importOriginal<typeof import('../lib/tree')>();
  return {
    ...actual,
    firstExpansion: (rows: Parameters<typeof actual.firstExpansion>[0]) =>
      actual.firstExpansion(rows, CAP),
  };
});

const TOP_LEVEL_ROWS = CAP + 1;

describe('US-1 Scenario: A large project backlog (spec NFR: client load)', () => {
  it('draws every top-level row, expands none, and says nothing about the cap; «Развернуть на один уровень» then shows the child and announces «Показано строк: 7.»', async () => {
    const data = boardsDataset();
    for (let number = 1; number <= TOP_LEVEL_ROWS; number += 1) {
      data.items.push(item(ids.msk, number, 'US', `Элемент ${number}`, { areaId: ids.mskRoot }));
    }
    const first = data.items[0];
    if (!first) throw new Error('No first story.');
    data.items.push(
      item(ids.msk, TOP_LEVEL_ROWS + 1, 'TASK', 'Задача первой', { parentId: first.id }),
    );
    const api = serveBoards(data);
    renderBoards(projectBacklogAddress());

    const table = await findBacklogTable();

    expect(rowsOf(table)).toHaveLength(TOP_LEVEL_ROWS);
    expect(identifiers(table)[0]).toBe('MSK-US-1');
    expect(identifiers(table).at(-1)).toBe(`MSK-US-${TOP_LEVEL_ROWS}`);
    expect(() => rowOf(table, `MSK-TASK-${TOP_LEVEL_ROWS + 1}`)).toThrow();
    expect(screen.queryByRole('alert')).toBeNull();
    expect(screen.queryByText(/Показать ещё/)).toBeNull();
    expect(screen.queryByText(/Показаны все строки/)).toBeNull();
    // One read, whole: no paging.
    expect(api.reads('/backlogs/stories')).toHaveLength(1);

    await userEvent.click(expandOneLevel());

    await waitFor(() => expect(rowsOf(table)).toHaveLength(TOP_LEVEL_ROWS + 1));
    expect(announcer()).toHaveTextContent(`Показано строк: ${TOP_LEVEL_ROWS + 1}.`);
    expect(rowOf(table, `MSK-TASK-${TOP_LEVEL_ROWS + 1}`)).toBeInTheDocument();
  });
});
