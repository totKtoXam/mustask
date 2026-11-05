import { describe, expect, it } from 'vitest';
import type { BacklogItem } from '../api/boardsApi';
import {
  MOVE_ENTRIES,
  applyMove,
  dropMove,
  entryMove,
  isEffectiveGap,
  isEntryEnabled,
  type MoveEntry,
} from './reorder';

// US-2's backlog: MSK-US-3, MSK-BUG-5, MSK-US-9, MSK-US-11 (api-contract § 3.2
// "How the panel's gestures map").

function row(identifier: string, order: number): BacklogItem {
  return {
    order,
    id: `id-${identifier}`,
    identifier,
    number: order,
    type: { id: 'us', key: 'US', name: 'Пользовательская история', appearance: 'story' },
    title: identifier,
    assignee: null,
    team: null,
    project: { id: 'prj-1', name: 'Мастаск' },
    state: { id: 'new', name: 'Новая', category: { code: 'Proposed', label: 'Новые' } },
    priority: 2,
  };
}

const ROWS = ['MSK-US-3', 'MSK-BUG-5', 'MSK-US-9', 'MSK-US-11'].map((identifier, index) =>
  row(identifier, index + 1),
);

const id = (identifier: string) => `id-${identifier}`;

function identifiers(items: readonly BacklogItem[]): string[] {
  return items.map((item) => item.identifier);
}

describe('US-2 Scenario Outline: The handle’s menu moves a row by keyboard — the mapping', () => {
  it.each<[MoveEntry, unknown, string[]]>([
    [
      'top',
      { itemId: id('MSK-BUG-5'), position: 'top' },
      ['MSK-BUG-5', 'MSK-US-3', 'MSK-US-9', 'MSK-US-11'],
    ],
    // «Выше» on the second row is the top: there is no row two above.
    [
      'up',
      { itemId: id('MSK-BUG-5'), position: 'top' },
      ['MSK-BUG-5', 'MSK-US-3', 'MSK-US-9', 'MSK-US-11'],
    ],
    [
      'down',
      { itemId: id('MSK-BUG-5'), position: 'after', afterItemId: id('MSK-US-9') },
      ['MSK-US-3', 'MSK-US-9', 'MSK-BUG-5', 'MSK-US-11'],
    ],
    [
      'bottom',
      { itemId: id('MSK-BUG-5'), position: 'bottom' },
      ['MSK-US-3', 'MSK-US-9', 'MSK-US-11', 'MSK-BUG-5'],
    ],
  ])(
    '«%s» on MSK-BUG-5, the second row, sends %j and reads the outline’s order',
    (entry, body, order) => {
      const move = entryMove(ROWS, 1, entry);

      expect(move).toEqual(body);
      expect(identifiers(applyMove(ROWS, move as never))).toEqual(order);
    },
  );

  it('«Выше» below the second row goes directly after the row two above', () => {
    expect(entryMove(ROWS, 3, 'up')).toEqual({
      itemId: id('MSK-US-11'),
      position: 'after',
      afterItemId: id('MSK-BUG-5'),
    });
    expect(entryMove(ROWS, 2, 'up')).toEqual({
      itemId: id('MSK-US-9'),
      position: 'after',
      afterItemId: id('MSK-US-3'),
    });
  });

  it('«Ниже» on the next-to-last row goes after the last one', () => {
    expect(entryMove(ROWS, 2, 'down')).toEqual({
      itemId: id('MSK-US-9'),
      position: 'after',
      afterItemId: id('MSK-US-11'),
    });
  });
});

describe('US-2 Scenario: Entries that would not move a row are disabled', () => {
  it('disables «В начало» and «Выше» on the first row, «Ниже» and «В конец» on the last', () => {
    const enabledOn = (index: number) =>
      MOVE_ENTRIES.filter((entry) => isEntryEnabled(entry, index, ROWS.length));

    expect(enabledOn(0)).toEqual(['down', 'bottom']);
    expect(enabledOn(1)).toEqual(['top', 'up', 'down', 'bottom']);
    expect(enabledOn(3)).toEqual(['top', 'up']);
    expect(MOVE_ENTRIES.filter((entry) => isEntryEnabled(entry, 0, 1))).toEqual([]);
  });

  it('asks nothing for a disabled entry', () => {
    expect(entryMove(ROWS, 0, 'top')).toBeNull();
    expect(entryMove(ROWS, 0, 'up')).toBeNull();
    expect(entryMove(ROWS, 3, 'down')).toBeNull();
    expect(entryMove(ROWS, 3, 'bottom')).toBeNull();
  });
});

describe('US-2 Scenarios: A drag between two rows persists; A drop at either end — the mapping', () => {
  it('a drop between MSK-US-3 and MSK-BUG-5 goes after MSK-US-3', () => {
    const move = dropMove(ROWS, 3, 1);

    expect(move).toEqual({
      itemId: id('MSK-US-11'),
      position: 'after',
      afterItemId: id('MSK-US-3'),
    });
    const after = applyMove(ROWS, move as never);
    expect(identifiers(after)).toEqual(['MSK-US-3', 'MSK-US-11', 'MSK-BUG-5', 'MSK-US-9']);
    expect(after.map((item) => item.order)).toEqual([1, 2, 3, 4]);
  });

  it('above the first row is the top, below the last the bottom', () => {
    expect(dropMove(ROWS, 2, 0)).toEqual({ itemId: id('MSK-US-9'), position: 'top' });
    expect(dropMove(ROWS, 2, 4)).toEqual({ itemId: id('MSK-US-9'), position: 'bottom' });
  });

  it('a line on either edge of the dragged row moves nothing, and is not drawn', () => {
    expect(dropMove(ROWS, 2, 2)).toBeNull();
    expect(dropMove(ROWS, 2, 3)).toBeNull();
    expect(isEffectiveGap(4, 2, 2)).toBe(false);
    expect(isEffectiveGap(4, 2, 3)).toBe(false);
    expect(isEffectiveGap(4, 2, 1)).toBe(true);
    expect(isEffectiveGap(4, 2, 4)).toBe(true);
  });

  it('a drop below a later row goes after it: the moved row is not counted twice', () => {
    expect(dropMove(ROWS, 0, 3)).toEqual({
      itemId: id('MSK-US-3'),
      position: 'after',
      afterItemId: id('MSK-US-9'),
    });
    expect(identifiers(applyMove(ROWS, dropMove(ROWS, 0, 3) as never))).toEqual([
      'MSK-BUG-5',
      'MSK-US-9',
      'MSK-US-3',
      'MSK-US-11',
    ]);
  });
});

describe('applyMove: the order shown while a reorder is saved', () => {
  it('leaves the order as it is for a move that names no row of it', () => {
    expect(identifiers(applyMove(ROWS, { itemId: 'id-gone', position: 'top' }))).toEqual(
      identifiers(ROWS),
    );
    expect(
      identifiers(
        applyMove(ROWS, { itemId: id('MSK-US-9'), position: 'after', afterItemId: 'id-gone' }),
      ),
    ).toEqual(identifiers(ROWS));
  });

  it('a row named after itself stays where it is (§ 3.2 step 4)', () => {
    expect(
      identifiers(
        applyMove(ROWS, {
          itemId: id('MSK-US-9'),
          position: 'after',
          afterItemId: id('MSK-US-9'),
        }),
      ),
    ).toEqual(identifiers(ROWS));
  });
});
