import type { BacklogTreeItem } from '../api/boardsApi';

// What dropping a row onto another row does (feat-027 US-5; BR-T-11, BR-T-12;
// design § 4.9): the parent write, decided here on the read the page shows — the
// parent write's own order (feat-001 § 3.2): the type, then the descendants. A
// refusal the server alone can see (a descendant on another team, a parent
// changed or deleted meanwhile) is its code's text.

/** The notice of a drop onto a type BR-H-2 does not allow above the item's (spec § Notices on «Бэклоги»). */
export const LEVEL_REFUSAL_TEXT =
  'Родителем может быть только рабочий элемент того же или более высокого уровня.';

/** The notice of a drop onto a descendant (spec § Notices on «Бэклоги»): `path` runs from the item down to the target. */
export function cycleRefusalText(target: string, item: string, path: readonly string[]): string {
  return `Нельзя выбрать родителем ${target}: он вложен в ${item} (${path.join(' → ')}).`;
}

/** Why a target is refused: a type BR-H-2 does not allow, or one of the item's own descendants. */
export type DropRefusal = 'level' | 'descendant';

/** What a drag finds under the pointer, on a row. */
export type DropVerdict =
  /** The item itself or its current parent: no target, nothing is drawn or sent. */
  | { kind: 'none' }
  | { kind: 'allowed' }
  /** Refused here: releasing sends nothing, and `refusalNotice` says why. */
  | { kind: 'refused'; reason: DropRefusal };

/** The part of a row a verdict reads. */
type DraggedRow = Pick<BacklogTreeItem, 'id' | 'identifier' | 'type' | 'parent' | 'children'>;
type TargetRow = Pick<BacklogTreeItem, 'id' | 'identifier' | 'type'>;

/**
 * The level rank of each type of the process, by the type's id (mod-001
 * `backlogLevels[].rank`; a higher rank is a higher level) — the rule BR-H-2
 * reads. A type the process does not list has none.
 */
export function typeRanks(process: {
  backlogLevels: readonly { id: string; rank: number }[];
  workItemTypes: readonly { id: string; backlogLevel: { id: string } }[];
}): ReadonlyMap<string, number> {
  const ranks = new Map<string, number>();
  for (const type of process.workItemTypes) {
    const level = process.backlogLevels.find((candidate) => candidate.id === type.backlogLevel.id);
    if (level) {
      ranks.set(type.id, level.rank);
    }
  }
  return ranks;
}

/** The ids of every descendant of `row` in the read — collapsed rows' included — by an iterative walk, so no depth can exhaust the stack. */
export function descendantIds(row: Pick<BacklogTreeItem, 'children'>): ReadonlySet<string> {
  const found = new Set<string>();
  const pending = [...row.children];
  for (let next = pending.pop(); next !== undefined; next = pending.pop()) {
    found.add(next.id);
    pending.push(...next.children);
  }
  return found;
}

/**
 * The identifiers from `from` down to `to` through the read's children, both
 * included, or `null` when `to` is no descendant of `from`: «MSK-FEAT-8 →
 * MSK-FEAT-23» in the descendant's notice.
 */
export function pathDown(
  from: Pick<BacklogTreeItem, 'id' | 'identifier' | 'children'>,
  to: Pick<BacklogTreeItem, 'id'>,
): string[] | null {
  // For each row met below `from`: its own identifier and the id of the row above it.
  const above = new Map<string, { identifier: string; parentId: string }>();
  const pending: { row: BacklogTreeItem; parentId: string }[] = from.children.map((row) => ({
    row,
    parentId: from.id,
  }));
  for (let next = pending.pop(); next !== undefined; next = pending.pop()) {
    above.set(next.row.id, { identifier: next.row.identifier, parentId: next.parentId });
    if (next.row.id === to.id) {
      break;
    }
    for (const child of next.row.children) {
      pending.push({ row: child, parentId: next.row.id });
    }
  }
  if (!above.has(to.id)) {
    return null;
  }
  const path: string[] = [];
  for (let step = above.get(to.id); step !== undefined; step = above.get(step.parentId)) {
    path.push(step.identifier);
  }
  path.push(from.identifier);
  return path.reverse();
}

/**
 * What a drag of `dragged` finds on the row `target` (BR-T-12): the item
 * itself and its current parent are no target; a target of a type BR-H-2 does
 * not allow above the item's — by the process's level ranks — is refused, then
 * one of the item's own `descendants`; any other is allowed. With the process
 * not read yet there is no rank to refuse by, and the server decides.
 */
export function dropVerdict(
  dragged: DraggedRow,
  target: TargetRow,
  context: { ranks: ReadonlyMap<string, number>; descendants: ReadonlySet<string> },
): DropVerdict {
  if (target.id === dragged.id || target.id === dragged.parent?.id) {
    return { kind: 'none' };
  }
  const own = context.ranks.get(dragged.type.id);
  const above = context.ranks.get(target.type.id);
  if (own !== undefined && above !== undefined && above < own) {
    return { kind: 'refused', reason: 'level' };
  }
  if (context.descendants.has(target.id)) {
    return { kind: 'refused', reason: 'descendant' };
  }
  return { kind: 'allowed' };
}

/**
 * The notice a refused drop shows (spec § Notices on «Бэклоги»). The path of a
 * descendant's is walked here, once, on the release — not on every pointer move.
 */
export function refusalNotice(reason: DropRefusal, dragged: DraggedRow, target: TargetRow): string {
  if (reason === 'level') {
    return LEVEL_REFUSAL_TEXT;
  }
  const path = pathDown(dragged, target) ?? [dragged.identifier, target.identifier];
  return cycleRefusalText(target.identifier, dragged.identifier, path);
}
