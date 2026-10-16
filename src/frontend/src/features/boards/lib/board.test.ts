import { describe, expect, it } from 'vitest';
import type { BoardCardItem, Board, BoardColumn } from '../api/boardsApi';
import {
  applyCardMove,
  columnOf,
  columnTarget,
  emptyBoardHint,
  isBoardEmpty,
  moveTargets,
} from './board';

// The seeded «Истории» board (spec § Model; api-contract § 3.3; bug-004 D-3):
// the columns are headed by the names of the states they map — «Новая»,
// «Активна», «Разрешена», «Закрыта» — and «Разрешена» maps both types.

const US = 'type-us';
const BUG = 'type-bug';

function card(identifier: string, order: number, typeId: string, stateName: string): BoardCardItem {
  return {
    order,
    id: `id-${identifier}`,
    identifier,
    number: order,
    type: {
      id: typeId,
      key: typeId === US ? 'US' : 'BUG',
      name: typeId === US ? 'Пользовательская история' : 'Баг',
      appearance: typeId === US ? 'story' : 'bug',
    },
    title: identifier,
    assignee: null,
    state: { id: `${typeId}-${stateName}`, name: stateName, category: { code: 'X', label: 'X' } },
    priority: 2,
    checklist: [],
  };
}

function column(
  key: string,
  header: string,
  targets: Record<string, string | null>,
  items: BoardCardItem[],
): BoardColumn {
  return {
    key,
    header,
    // The column's category stays in the answer; its label is no header.
    category: { code: key.split('-')[0] ?? '', label: `Категория ${key}` },
    targets: Object.entries(targets).map(([typeId, stateId]) => ({ typeId, stateId })),
    items,
  };
}

function storiesBoard(): Pick<Board, 'columns'> {
  return {
    columns: [
      column('Proposed-1', 'Новая', { [US]: 'us-new', [BUG]: 'bug-new' }, [
        card('MSK-US-9', 2, US, 'Новая'),
        card('MSK-BUG-5', 4, BUG, 'Новая'),
      ]),
      column('InProgress-1', 'Активна', { [US]: 'us-active', [BUG]: 'bug-active' }, [
        card('MSK-US-3', 1, US, 'Активна'),
        card('MSK-US-11', 3, US, 'Активна'),
      ]),
      column('Resolved-1', 'Разрешена', { [US]: 'us-resolved', [BUG]: 'bug-resolved' }, []),
      column('Completed-1', 'Закрыта', { [US]: 'us-closed', [BUG]: 'bug-closed' }, []),
    ],
  };
}

/**
 * A process the seed does not write (doc-007's customization is not built): a state only the
 * story has, so its column maps none for the bug. The seeded process has no such column.
 */
function reviewBoard(): Pick<Board, 'columns'> {
  return {
    columns: [
      column('Proposed-1', 'Новая', { [US]: 'us-new', [BUG]: 'bug-new' }, [
        card('MSK-US-9', 2, US, 'Новая'),
        card('MSK-BUG-5', 4, BUG, 'Новая'),
      ]),
      column('Resolved-1', 'Проверка', { [US]: 'us-review', [BUG]: null }, []),
      column('Completed-1', 'Закрыта', { [US]: 'us-closed', [BUG]: 'bug-closed' }, []),
    ],
  };
}

const headers = (columns: readonly BoardColumn[]) => columns.map((entry) => entry.header);

describe('US-5 Scenario: The card’s menu moves it by keyboard — the columns it offers', () => {
  it('offers MSK-BUG-5 in «Новая» «Активна», «Разрешена», «Закрыта»: not its own column', () => {
    const board = storiesBoard();
    const bug = columnOf(board, 'id-MSK-BUG-5')?.items.find((item) => item.id === 'id-MSK-BUG-5');
    if (!bug) throw new Error('No MSK-BUG-5.');

    expect(headers(moveTargets(board, bug))).toEqual(['Активна', 'Разрешена', 'Закрыта']);
  });

  it('offers a story in «Активна» «Новая», «Разрешена», «Закрыта» — the same «Разрешена» a bug is offered', () => {
    const board = storiesBoard();
    const story = board.columns[1]?.items[0];
    if (!story) throw new Error('No story.');

    expect(headers(moveTargets(board, story))).toEqual(['Новая', 'Разрешена', 'Закрыта']);
  });

  it('offers a column only to the types that map a state in it (BR-M4-18)', () => {
    const board = reviewBoard();
    const [story, bug] = ['id-MSK-US-9', 'id-MSK-BUG-5'].map((id) =>
      columnOf(board, id)?.items.find((item) => item.id === id),
    );
    if (!story || !bug) throw new Error('No cards.');

    expect(headers(moveTargets(board, story))).toEqual(['Проверка', 'Закрыта']);
    expect(headers(moveTargets(board, bug))).toEqual(['Закрыта']);
  });
});

describe('US-5 Scenario: A column is a drop target for the types that map a state in it — the targets', () => {
  it('maps a story and a bug each to its own state of «Разрешена» (bug-004 D-3)', () => {
    const resolved = storiesBoard().columns[2];
    if (!resolved) throw new Error('No «Разрешена».');

    expect(columnTarget(resolved, US)).toBe('us-resolved');
    expect(columnTarget(resolved, BUG)).toBe('bug-resolved');
    expect(columnTarget(resolved, 'type-unknown')).toBeNull();
  });

  it('maps no state of a type that has none in the column (BR-M4-18)', () => {
    const review = reviewBoard().columns[1];
    if (!review) throw new Error('No «Проверка».');

    expect(columnTarget(review, US)).toBe('us-review');
    expect(columnTarget(review, BUG)).toBeNull();
  });
});

describe('US-5 Scenario: The card lands at its backlog place in the new column', () => {
  it('MSK-US-9 dropped at the top of «Активна» stands between MSK-US-3 and MSK-US-11 by «Порядок»', () => {
    const board = storiesBoard();
    const state = {
      id: 'us-active',
      name: 'Активна',
      category: { code: 'InProgress', label: 'В работе' },
    };

    const after = applyCardMove(board, 'id-MSK-US-9', 'InProgress-1', state);

    expect(after.columns[1]?.items.map((item) => item.identifier)).toEqual([
      'MSK-US-3',
      'MSK-US-9',
      'MSK-US-11',
    ]);
    expect(after.columns[0]?.items.map((item) => item.identifier)).toEqual(['MSK-BUG-5']);
    expect(after.columns[1]?.items[1]?.state).toEqual(state);
    expect(after.columns[1]?.items[1]?.order).toBe(2);
  });

  it('a story and a bug moved to «Разрешена» stand in that one column, by «Порядок»', () => {
    const board = storiesBoard();

    const bugMoved = applyCardMove(board, 'id-MSK-BUG-5', 'Resolved-1', undefined);
    const bothMoved = applyCardMove(bugMoved, 'id-MSK-US-9', 'Resolved-1', undefined);

    expect(bothMoved.columns[2]?.items.map((item) => item.identifier)).toEqual([
      'MSK-US-9',
      'MSK-BUG-5',
    ]);
    expect(bothMoved.columns[0]?.items).toEqual([]);
  });

  it('keeps the card’s state as it was while its name is not known, and changes nothing for its own column', () => {
    const board = storiesBoard();

    const moved = applyCardMove(board, 'id-MSK-BUG-5', 'Resolved-1', undefined);
    expect(moved.columns[2]?.items[0]?.state.name).toBe('Новая');
    expect(applyCardMove(board, 'id-MSK-BUG-5', 'Proposed-1', undefined)).toBe(board);
    expect(applyCardMove(board, 'id-gone', 'Resolved-1', undefined)).toBe(board);
  });
});

describe('US-4 Scenario: A level with no items shows the empty frame — when', () => {
  it('is empty only when every column is', () => {
    const board = storiesBoard();
    expect(isBoardEmpty(board)).toBe(false);
    expect(isBoardEmpty({ columns: board.columns.map((entry) => ({ ...entry, items: [] })) })).toBe(
      true,
    );
  });

  it('says where a new item will appear by the first column’s own header, not by a word held here', () => {
    expect(emptyBoardHint(storiesBoard())).toBe(
      'Новые рабочие элементы этого уровня появятся в колонке «Новая».',
    );
    expect(
      emptyBoardHint({ columns: [column('Proposed-1', 'Черновик', { [US]: 'us-draft' }, [])] }),
    ).toBe('Новые рабочие элементы этого уровня появятся в колонке «Черновик».');
    expect(emptyBoardHint({ columns: [] })).toBe(
      'Новые рабочие элементы этого уровня появятся на доске.',
    );
  });
});
