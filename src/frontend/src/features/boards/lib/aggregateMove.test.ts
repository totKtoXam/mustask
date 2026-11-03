import { describe, expect, it } from 'vitest';
import { moveBody, moveItems, movedRows } from './aggregateMove';

const rows = [{ id: 'a' }, { id: 'b' }, { id: 'c' }];
const enabled = (index: number, count: number, partial: boolean) =>
  Object.fromEntries(moveItems(index, count, partial).map((item) => [item.entry, item.enabled]));

describe('feat-048 OQ-6 A: the move menu of a by-project group', () => {
  it('on a fully loaded group works as the shipped menu does', () => {
    expect(enabled(0, 3, false)).toEqual({ top: false, up: false, down: true, bottom: true });
    expect(enabled(2, 3, false)).toEqual({ top: true, up: true, down: false, bottom: false });
  });

  it('on a part-loaded group keeps «В конец» on every row, and says «Ниже» needs more on the last loaded one', () => {
    expect(enabled(2, 3, true)).toEqual({ top: true, up: true, down: false, bottom: true });
    const down = moveItems(2, 3, true).find((item) => item.entry === 'down');
    expect(down?.needsMore).toBe(true);
    const inner = moveItems(1, 3, true).find((item) => item.entry === 'down');
    expect(inner?.needsMore).toBe(false);
  });

  it('sends «В конец» of a part-loaded group as the project’s end, and nothing for an off item', () => {
    expect(moveBody(rows, 2, 'bottom', true)).toEqual({ itemId: 'c', position: 'bottom' });
    expect(moveBody(rows, 2, 'bottom', false)).toBeNull();
    expect(moveBody(rows, 9, 'top', false)).toBeNull();
  });

  it('reorders the loaded rows at once', () => {
    const ids = (list: { id: string }[]) => list.map((row) => row.id);
    expect(ids(movedRows(rows, 2, 'top'))).toEqual(['c', 'a', 'b']);
    expect(ids(movedRows(rows, 1, 'up'))).toEqual(['b', 'a', 'c']);
    expect(ids(movedRows(rows, 0, 'down'))).toEqual(['b', 'a', 'c']);
    expect(ids(movedRows(rows, 0, 'bottom'))).toEqual(['b', 'c', 'a']);
  });
});
