import type { WorkItemStateReference } from '../../workitems';
import type { BacklogItem, Board, BoardCardItem, BoardColumn } from '../api/boardsApi';

// Where a card may go on the board and how the board looks after it goes
// (spec § Model "The board's columns and what a card move writes"; BR-M4-3,
// BR-M4-15, BR-M4-18; api-contract § 3.3 `targets`).

/**
 * The state a card of `typeId` takes when dropped in `column` — the
 * column's `targets` entry for the type — or `null`: the column maps no state
 * of the type, so it is no drop target for it and is not in its menu.
 */
export function columnTarget(column: Pick<BoardColumn, 'targets'>, typeId: string): string | null {
  return column.targets.find((target) => target.typeId === typeId)?.stateId ?? null;
}

/** The column a card is in, by the card's id. */
export function columnOf(board: Pick<Board, 'columns'>, cardId: string): BoardColumn | undefined {
  return board.columns.find((column) => column.items.some((card) => card.id === cardId));
}

/**
 * The columns a card may move to (spec § Page «Доски», «Card menu»): every
 * other column that maps a state of the card's type, in board order. Its own
 * column is never one.
 */
export function moveTargets(board: Pick<Board, 'columns'>, card: BacklogItem): BoardColumn[] {
  const own = columnOf(board, card.id);
  return board.columns.filter(
    (column) => column.key !== own?.key && columnTarget(column, card.type.id) !== null,
  );
}

/**
 * The board after the card moves to the column `toKey`, shown at once while
 * the move is saved: the card leaves its column and stands in the new one at
 * its backlog rank (`order`, BR-M4-15), with `state` when it is known — the
 * name comes from the process, read beside the board — else as it was until
 * the board is read again.
 */
export function applyCardMove<T extends Pick<Board, 'columns'>>(
  board: T,
  cardId: string,
  toKey: string,
  state: WorkItemStateReference | undefined,
): T {
  const from = columnOf(board, cardId);
  const card = from?.items.find((candidate) => candidate.id === cardId);
  if (!from || !card || from.key === toKey) {
    return board;
  }
  const moved: BoardCardItem = state ? { ...card, state } : card;
  return {
    ...board,
    columns: board.columns.map((column) => {
      if (column.key === from.key) {
        return { ...column, items: column.items.filter((candidate) => candidate.id !== cardId) };
      }
      if (column.key === toKey) {
        return {
          ...column,
          items: [...column.items, moved].sort((first, second) => first.order - second.order),
        };
      }
      return column;
    }),
  };
}

/**
 * The hint of the empty frame of a team that works on no active project (feat-040 design § 10,
 * state 10 (a)): the same for every role, saying nothing of why a project is gone.
 */
export const NO_PROJECT_HINT = 'Команда не участвует ни в одном проекте.';

/** Whether no column holds a card: the board shows its empty frame (spec § Page «Доски»). */
export function isBoardEmpty(board: Pick<Board, 'columns'>): boolean {
  return board.columns.every((column) => column.items.length === 0);
}

/**
 * The empty frame's hint (spec § Page «Доски»): a new work item takes its type's initial state,
 * which the board's first column maps. The column is named by its own header — the state's name
 * (bug-004 D-3), «Новая» on the seeded process — never by a word held here.
 */
export function emptyBoardHint(board: Pick<Board, 'columns'>): string {
  const first = board.columns[0]?.header;
  return first
    ? `Новые рабочие элементы этого уровня появятся в колонке «${first}».`
    : 'Новые рабочие элементы этого уровня появятся на доске.';
}
