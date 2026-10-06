import { describe, expect, it } from 'vitest';
import type { BacklogItem, Board, BoardColumn } from '../api/boardsApi';
import { applyCardMove, columnOf, columnTarget, isBoardEmpty, moveTargets } from './board';

// The seeded «Истории» board (spec § Model; api-contract § 3.3): «Новые»,
// «В работе», «Разрешена» (US only), «Решённые» (BUG only), «Завершённые».

const US = 'type-us';
const BUG = 'type-bug';

function card(identifier: string, order: number, typeId: string, stateName: string): BacklogItem {
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
  };
}

function column(
  key: string,
  header: string,
  targets: Record<string, string | null>,
  items: BacklogItem[],
): BoardColumn {
  return {
    key,
    header,
    category: { code: key.split('-')[0] ?? '', label: header },
    targets: Object.entries(targets).map(([typeId, stateId]) => ({ typeId, stateId })),
    items,
  };
}

function storiesBoard(): Pick<Board, 'columns'> {
  return {
    columns: [
      column('Proposed-1', 'Новые', { [US]: 'us-new', [BUG]: 'bug-new' }, [
        card('MSK-US-9', 2, US, 'Новая'),
        card('MSK-BUG-5', 4, BUG, 'Новая'),
      ]),
      column('InProgress-1', 'В работе', { [US]: 'us-active', [BUG]: 'bug-active' }, [
        card('MSK-US-3', 1, US, 'Активна'),
        card('MSK-US-11', 3, US, 'Активна'),
      ]),
      column('InProgress-2', 'Разрешена', { [US]: 'us-resolved', [BUG]: null }, []),
      column('Resolved-1', 'Решённые', { [US]: null, [BUG]: 'bug-resolved' }, []),
      column('Completed-1', 'Завершённые', { [US]: 'us-closed', [BUG]: 'bug-closed' }, []),
    ],
  };
}

const headers = (columns: readonly BoardColumn[]) => columns.map((entry) => entry.header);

describe('US-5 Scenario: The card’s menu moves it by keyboard — the columns it offers', () => {
  it('offers MSK-BUG-5 in «Новые» «В работе», «Решённые», «Завершённые»: neither «Новые» nor «Разрешена»', () => {
    const board = storiesBoard();
    const bug = columnOf(board, 'id-MSK-BUG-5')?.items.find((item) => item.id === 'id-MSK-BUG-5');
    if (!bug) throw new Error('No MSK-BUG-5.');

    expect(headers(moveTargets(board, bug))).toEqual(['В работе', 'Решённые', 'Завершённые']);
  });

  it('offers a story in «В работе» «Новые», «Разрешена», «Завершённые»: not «Решённые»', () => {
    const board = storiesBoard();
    const story = board.columns[1]?.items[0];
    if (!story) throw new Error('No story.');

    expect(headers(moveTargets(board, story))).toEqual(['Новые', 'Разрешена', 'Завершённые']);
  });
});

describe('US-5 Scenario: A column without a state for the type is no drop target — the targets', () => {
  it('maps each type to its state in the column, or to none (BR-M4-18)', () => {
    const [, , resolvedUs, resolvedBug] = storiesBoard().columns;
    if (!resolvedUs || !resolvedBug) throw new Error('No columns.');

    expect(columnTarget(resolvedUs, US)).toBe('us-resolved');
    expect(columnTarget(resolvedUs, BUG)).toBeNull();
    expect(columnTarget(resolvedBug, BUG)).toBe('bug-resolved');
    expect(columnTarget(resolvedBug, US)).toBeNull();
    expect(columnTarget(resolvedBug, 'type-unknown')).toBeNull();
  });
});

describe('US-5 Scenario: The card lands at its backlog place in the new column', () => {
  it('MSK-US-9 dropped at the top of «В работе» stands between MSK-US-3 and MSK-US-11 by «Порядок»', () => {
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

  it('keeps the card’s state as it was while its name is not known, and changes nothing for its own column', () => {
    const board = storiesBoard();

    const moved = applyCardMove(board, 'id-MSK-BUG-5', 'Resolved-1', undefined);
    expect(moved.columns[3]?.items[0]?.state.name).toBe('Новая');
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
});
