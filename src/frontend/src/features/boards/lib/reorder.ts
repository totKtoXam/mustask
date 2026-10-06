import type { BacklogItem, ReorderBody } from '../api/boardsApi';

// How the backlog's two gestures become a reorder (spec § Page «Бэклоги»;
// api-contract § 3.2 "How the panel's gestures map"; BR-M4-8, BR-M4-25), and
// the order the panel shows at once while the reorder is saved.

/** The handle menu's entries, in the menu's order. */
export const MOVE_ENTRIES = ['top', 'up', 'down', 'bottom'] as const;

export type MoveEntry = (typeof MOVE_ENTRIES)[number];

export const MOVE_ENTRY_LABELS: Readonly<Record<MoveEntry, string>> = {
  top: 'В начало',
  up: 'Выше',
  down: 'Ниже',
  bottom: 'В конец',
};

/** The part of a row the mapping reads. */
type Row = Pick<BacklogItem, 'id'>;

/**
 * Whether an entry moves the row at `index` of `count` (mod-015 BR-9): «В
 * начало» and «Выше» are disabled on the first row, «Ниже» and «В конец» on
 * the last; a backlog of one row offers none.
 */
export function isEntryEnabled(entry: MoveEntry, index: number, count: number): boolean {
  switch (entry) {
    case 'top':
    case 'up':
      return index > 0 && index < count;
    case 'down':
    case 'bottom':
      return index >= 0 && index < count - 1;
  }
}

function after(item: Row, anchor: Row): ReorderBody {
  return { itemId: item.id, position: 'after', afterItemId: anchor.id };
}

/**
 * The reorder a menu entry asks for the row at `index`, or `null` when the
 * entry is disabled there: «В начало» the top; «Выше» directly after the row
 * two above, the top on the second row; «Ниже» directly after the row below;
 * «В конец» the bottom.
 */
export function entryMove(
  rows: readonly Row[],
  index: number,
  entry: MoveEntry,
): ReorderBody | null {
  const item = rows[index];
  if (!item || !isEntryEnabled(entry, index, rows.length)) {
    return null;
  }
  switch (entry) {
    case 'top':
      return { itemId: item.id, position: 'top' };
    case 'up': {
      const anchor = rows[index - 2];
      return anchor ? after(item, anchor) : { itemId: item.id, position: 'top' };
    }
    case 'down': {
      const anchor = rows[index + 1];
      return anchor ? after(item, anchor) : null;
    }
    case 'bottom':
      return { itemId: item.id, position: 'bottom' };
  }
}

/**
 * The reorder a pointer drop asks for. `gap` is where the drop line stands:
 * 0 above the first row, `rows.length` below the last, `k` between rows
 * `k - 1` and `k`. Above the first row is the top, below the last the bottom,
 * elsewhere directly after the row above the line. A line on either edge of
 * the dragged row itself moves nothing: `null`.
 */
export function dropMove(rows: readonly Row[], from: number, gap: number): ReorderBody | null {
  const item = rows[from];
  if (!item || gap < 0 || gap > rows.length || gap === from || gap === from + 1) {
    return null;
  }
  if (gap === 0) {
    return { itemId: item.id, position: 'top' };
  }
  if (gap === rows.length) {
    return { itemId: item.id, position: 'bottom' };
  }
  const anchor = rows[gap - 1];
  return anchor ? after(item, anchor) : null;
}

/** Whether a drop line at `gap` would move the row at `from` (it is drawn only then). */
export function isEffectiveGap(count: number, from: number, gap: number): boolean {
  return gap >= 0 && gap <= count && gap !== from && gap !== from + 1;
}

/**
 * The order after `move`, as the server will hold it (§ 3.2 step 4), shown at
 * once while the reorder is saved: the moved row taken out and put at the
 * top, the bottom or directly after the named row, «Порядок» renumbered 1..n.
 * A move that names no row of this order leaves the order as it is.
 */
export function applyMove<T extends BacklogItem>(items: readonly T[], move: ReorderBody): T[] {
  const moved = items.find((item) => item.id === move.itemId);
  if (!moved) {
    return [...items];
  }
  const rest = items.filter((item) => item.id !== move.itemId);
  let at: number;
  if (move.position === 'after') {
    const { afterItemId } = move;
    const anchor = rest.findIndex((item) => item.id === afterItemId);
    if (anchor === -1) {
      return [...items];
    }
    at = anchor + 1;
  } else {
    at = move.position === 'top' ? 0 : rest.length;
  }
  rest.splice(at, 0, moved);
  return rest.map((item, index) => ({ ...item, order: index + 1 }));
}
