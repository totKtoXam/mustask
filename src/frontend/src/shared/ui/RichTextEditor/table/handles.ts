import type { Editor } from '@tiptap/core';
import { CellSelection, TableMap, isInTable, rowIsHeader, selectedRect } from '@tiptap/pm/tables';

/**
 * The on-table handles (design § Tables): the pure part. What shows is read
 * from the editor's state, where it sits from the DOM rects of the active cell,
 * its table and its scroll wrapper. Nothing here is a document node.
 */

export type HandleKind =
  'rowAbove' | 'rowBelow' | 'columnLeft' | 'columnRight' | 'deleteRow' | 'deleteColumn';

const COLUMN_SIDE: HandleKind[] = ['columnLeft', 'columnRight', 'deleteColumn'];

export interface HandleSpot {
  kind: HandleKind;
  /** The handle's centre, px, relative to the overlay root. */
  x: number;
  y: number;
}

export interface HandleLayout {
  handles: HandleSpot[];
}

/** The visible dot's size and the hit target round it, px (design § Tables). */
export const HANDLE_DOT = 16;
export const HANDLE_HIT = 20;

/** Which handles the caret's cell offers: the same rules as the «…» menu's items. */
export function offeredHandles(editor: Editor): HandleKind[] | null {
  const { state } = editor;
  if (!editor.isEditable || !editor.isFocused) return null;
  if (state.selection instanceof CellSelection || !isInTable(state)) return null;
  const rect = selectedRect(state);
  const inHeaderRow = rect.top === 0 && rowIsHeader(rect.map, rect.table, 0);
  const kinds: HandleKind[] = [];
  if (!inHeaderRow) kinds.push('rowAbove');
  kinds.push('rowBelow', 'columnLeft', 'columnRight');
  if (rect.map.height > 1) kinds.push('deleteRow');
  if (rect.map.width > 1) kinds.push('deleteColumn');
  return kinds;
}

function sameRange(value: number, low: number, high: number): boolean {
  return value >= low - 1 && value <= high + 1;
}

/** The handles to draw and where, or `null` when none shows. */
export function measureHandles(editor: Editor, root: HTMLElement): HandleLayout | null {
  const kinds = offeredHandles(editor);
  if (!kinds) return null;
  const { $head } = editor.state.selection;
  let cell: Node | null = null;
  for (let depth = $head.depth; depth > 0 && !cell; depth--) {
    const name = $head.node(depth).type.name;
    if (name === 'tableCell' || name === 'tableHeader') {
      cell = editor.view.nodeDOM($head.before(depth));
    }
  }
  if (!(cell instanceof HTMLElement)) return null;
  const table = cell.closest('table');
  if (!table) return null;
  const scroll = cell.closest('.rt-table-scroll') ?? table;

  const origin = root.getBoundingClientRect();
  const body = editor.view.dom.getBoundingClientRect();
  const c = cell.getBoundingClientRect();
  const t = table.getBoundingClientRect();
  const w = scroll.getBoundingClientRect();

  const left = Math.max(t.left, w.left) - origin.left;
  const right = Math.min(t.right, w.right) - origin.left;
  const top = t.top - origin.top;
  const cellLeft = c.left - origin.left;
  const cellRight = c.right - origin.left;
  const cellTop = c.top - origin.top;
  const cellBottom = c.bottom - origin.top;
  const visibleX = (x: number) => sameRange(x, w.left - origin.left, w.right - origin.left);
  const visibleY = (y: number) => sameRange(y, body.top - origin.top, body.bottom - origin.top);

  // The first cell of a table: the row «+» above and the column «+» on the left
  // would share the corner, so the row one steps out of the border.
  const corner = Math.abs(c.top - t.top) < 1 && Math.abs(c.left - t.left) < 1;
  const rowAboveX = corner ? left - HANDLE_DOT * 0.75 : left;

  const spots: Record<HandleKind, HandleSpot> = {
    rowAbove: { kind: 'rowAbove', x: rowAboveX, y: cellTop },
    rowBelow: { kind: 'rowBelow', x: left, y: cellBottom },
    deleteRow: { kind: 'deleteRow', x: right, y: (cellTop + cellBottom) / 2 },
    columnLeft: { kind: 'columnLeft', x: cellLeft, y: top },
    columnRight: { kind: 'columnRight', x: cellRight, y: top },
    deleteColumn: { kind: 'deleteColumn', x: (cellLeft + cellRight) / 2, y: top },
  };

  const handles = kinds
    .map((kind) => spots[kind])
    .filter(
      (spot) => (COLUMN_SIDE.includes(spot.kind) ? visibleX(spot.x) : true) && visibleY(spot.y),
    );
  return handles.length > 0 ? { handles } : null;
}

export function sameLayout(a: HandleLayout | null, b: HandleLayout | null): boolean {
  if (a === b) return true;
  if (!a || !b || a.handles.length !== b.handles.length) return false;
  return a.handles.every((spot, index) => {
    const other = b.handles[index] as HandleSpot;
    return (
      spot.kind === other.kind &&
      Math.round(spot.x) === Math.round(other.x) &&
      Math.round(spot.y) === Math.round(other.y)
    );
  });
}

/**
 * Runs one handle's command: the tiptap table commands the «…» menu runs, so
 * the header-row rules are theirs. After an add the selection maps along with
 * its cell; after a delete the caret goes to the neighbour that took its place.
 */
export function runHandle(editor: Editor, kind: HandleKind): void {
  if (!offeredHandles(editor)?.includes(kind)) return;
  const rect = selectedRect(editor.state);
  const { tableStart } = rect;
  const column = rect.left;
  let target: { row: number; column: number } | null = null;
  if (kind === 'deleteRow') {
    const row = rect.top < rect.map.height - 1 ? rect.top : rect.top - 1;
    target = { row, column };
  } else if (kind === 'deleteColumn') {
    target = { row: rect.top, column: column < rect.map.width - 1 ? column : column - 1 };
  }

  const chain = editor.chain();
  switch (kind) {
    case 'rowAbove':
      chain.addRowBefore().run();
      break;
    case 'rowBelow':
      chain.addRowAfter().run();
      break;
    case 'columnLeft':
      chain.addColumnBefore().run();
      break;
    case 'columnRight':
      chain.addColumnAfter().run();
      break;
    case 'deleteRow':
      chain.deleteRow().run();
      break;
    case 'deleteColumn':
      chain.deleteColumn().run();
      break;
  }

  if (target) {
    const table = editor.state.doc.resolve(tableStart).parent;
    const map = TableMap.get(table);
    const cellPos = map.map[target.row * map.width + target.column];
    // The cell's first paragraph starts two positions in (cell open, paragraph open).
    if (cellPos !== undefined) editor.commands.setTextSelection(tableStart + cellPos + 2);
  }
}
