// A move inside a group of a by-project backlog (feat-048 design § 6, OQ-6 A): which items of
// «Переместить» are enabled when only part of the project's backlog is loaded, the order after a
// move, and where «В конец» goes. The reorder body itself is the shipped one (`lib/reorder.ts`).
import type { ReorderBody } from '../api/boardsApi';
import { MOVE_ENTRIES, entryMove, type MoveEntry } from './reorder';

export interface MoveItem {
  entry: MoveEntry;
  enabled: boolean;
  /** «Ниже» on the last loaded row of a part-loaded group says why it is off. */
  needsMore: boolean;
}

/** The four items for the row at `index` of `count` loaded rows; `partial`: more rows remain. */
export function moveItems(index: number, count: number, partial: boolean): MoveItem[] {
  return MOVE_ENTRIES.map((entry) => {
    switch (entry) {
      case 'top':
      case 'up':
        return { entry, enabled: index > 0, needsMore: false };
      case 'down':
        return {
          entry,
          enabled: index < count - 1,
          needsMore: partial && index === count - 1,
        };
      case 'bottom':
        // «В конец» is the project's end: on a part-loaded group every row can go there.
        return { entry, enabled: partial || index < count - 1, needsMore: false };
    }
  });
}

/** The reorder to send; `null` when the item is off. */
export function moveBody(
  rows: readonly { id: string }[],
  index: number,
  entry: MoveEntry,
  partial: boolean,
): ReorderBody | null {
  const row = rows[index];
  if (!row) return null;
  if (entry === 'bottom') {
    return partial || index < rows.length - 1 ? { itemId: row.id, position: 'bottom' } : null;
  }
  return entryMove(rows, index, entry);
}

/**
 * The loaded rows after the move at once. «В конец» on a part-loaded group is not here: the row
 * leaves the loaded set (the caller drops it by id) and the count stays unchanged.
 */
export function movedRows<T>(rows: readonly T[], index: number, entry: MoveEntry): T[] {
  const next = [...rows];
  const [row] = next.splice(index, 1);
  if (row === undefined) return next;
  const target =
    entry === 'top' ? 0 : entry === 'up' ? index - 1 : entry === 'down' ? index + 1 : next.length;
  next.splice(Math.max(0, target), 0, row);
  return next;
}
