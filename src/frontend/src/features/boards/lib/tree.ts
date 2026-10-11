import type { BacklogRow, BacklogTreeItem } from '../api/boardsApi';

// How the backlog's tree is drawn (feat-001 US-4; feat-027 BR-T-4..-8, spec
// § Page «Бэклоги»): the top-level rows in mod-006's order, each followed —
// while it is expanded — by its children one tier deeper, and so on down, to
// any depth. The server sends the whole tree and collapses nothing; what is
// expanded is the panel's, by item, and changes only what is shown. A row's
// "depth" in feat-027's rules is a line's `tier` here: 0 at the top.
//
// Every walk keeps its own stack: a chain of parents holds at most 64 items, a
// bound the writes keep (feat-027 api-contract § 1, D-9), so the tree is at most
// 64 rows deep — but a chain stored before the bound can be longer, and no walk
// may recurse once per level.

/** One line of the table: a row or a nested row. */
export interface TreeLine {
  item: BacklogTreeItem;
  /** 0 for a top-level row, 1 for its children, 2 for theirs. */
  tier: number;
  /** The index of the top-level row whose block the line is part of. */
  block: number;
}

/**
 * The most lines the table draws at once (spec § NFR; feat-027 D-6 OQ-2 (d)):
 * the first expansion stops at the deepest level whose total stays within it.
 */
export const DRAWN_ROWS_LIMIT = 2000;

/** Whether a row has an expand control: it has a child to show (BR-T-7). */
export function hasChildren(item: Pick<BacklogTreeItem, 'children'>): boolean {
  return item.children.length > 0;
}

/**
 * The lines the table draws: every top-level row, and beneath each expanded
 * one its children in their own order (BR-T-6), recursively. A collapsed row's
 * children are not drawn at all (spec § NFR: nested rows are rendered only
 * while shown).
 */
export function treeLines(rows: readonly BacklogRow[], expanded: ReadonlySet<string>): TreeLine[] {
  const lines: TreeLine[] = [];
  rows.forEach((row, block) => {
    // A stack, so the line popped next is the first of the pending ones.
    const pending: TreeLine[] = [{ item: row, tier: 0, block }];
    for (let line = pending.pop(); line !== undefined; line = pending.pop()) {
      lines.push(line);
      if (expanded.has(line.item.id)) {
        const { children } = line.item;
        for (let index = children.length - 1; index >= 0; index -= 1) {
          pending.push({ item: children[index] as BacklogTreeItem, tier: line.tier + 1, block });
        }
      }
    }
  });
  return lines;
}

/** A row met on a walk of the whole tree, shown or not. */
interface Visit {
  item: BacklogTreeItem;
  depth: number;
  /** Every row above it is expanded, so the table draws it (or it is top-level). */
  shown: boolean;
}

/** Every row of the tree, collapsed ones' descendants included, in no promised order. */
function visits(rows: readonly BacklogRow[], expanded: ReadonlySet<string>): Visit[] {
  const found: Visit[] = [];
  const pending: Visit[] = rows.map((item) => ({ item, depth: 0, shown: true }));
  for (let visit = pending.pop(); visit !== undefined; visit = pending.pop()) {
    found.push(visit);
    const open = visit.shown && expanded.has(visit.item.id);
    for (const child of visit.item.children) {
      pending.push({ item: child, depth: visit.depth + 1, shown: open });
    }
  }
  return found;
}

/**
 * The depth of the deepest row of the tree, collapsed rows' descendants
 * counted: 0 when no top-level row has a child (BR-T-8's "depth").
 */
export function maxDepth(rows: readonly BacklogRow[]): number {
  return visits(rows, new Set()).reduce((deepest, visit) => Math.max(deepest, visit.depth), 0);
}

/**
 * How many lines the table draws with `expanded` — `treeLines(rows, expanded)
 * .length` without building them (the status region's «Показано строк: <m>.»,
 * and the 2 000-row cap of the first expansion).
 */
export function drawnCount(rows: readonly BacklogRow[], expanded: ReadonlySet<string>): number {
  return visits(rows, expanded).filter((visit) => visit.shown).length;
}

/**
 * «Развернуть на один уровень» (BR-T-8): the expanded set with every row with
 * children expanded at the smallest depth where a shown such row is collapsed;
 * `null` when every row with children is already expanded — the button is then
 * unavailable and changes nothing.
 */
export function expandOneLevel(
  rows: readonly BacklogRow[],
  expanded: ReadonlySet<string>,
): ReadonlySet<string> | null {
  const all = visits(rows, expanded);
  const collapsed = all.filter(
    (visit) => visit.shown && hasChildren(visit.item) && !expanded.has(visit.item.id),
  );
  if (collapsed.length === 0) {
    return null;
  }
  const depth = collapsed.reduce((least, visit) => Math.min(least, visit.depth), Infinity);
  const next = new Set(expanded);
  for (const visit of all) {
    if (visit.depth === depth && hasChildren(visit.item)) {
      next.add(visit.item.id);
    }
  }
  return next;
}

/**
 * «Свернуть на один уровень» (BR-T-8): the expanded set with every row at the
 * greatest depth where a shown row is expanded, and every row deeper,
 * collapsed; `null` when no shown row is expanded.
 */
export function collapseOneLevel(
  rows: readonly BacklogRow[],
  expanded: ReadonlySet<string>,
): ReadonlySet<string> | null {
  const all = visits(rows, expanded);
  const open = all.filter(
    (visit) => visit.shown && hasChildren(visit.item) && expanded.has(visit.item.id),
  );
  if (open.length === 0) {
    return null;
  }
  const depth = open.reduce((greatest, visit) => Math.max(greatest, visit.depth), 0);
  const next = new Set(expanded);
  for (const visit of all) {
    if (visit.depth >= depth) {
      next.delete(visit.item.id);
    }
  }
  return next;
}

/**
 * The tree's first state (feat-027 D-6 OQ-2 (d)): collapsed, then expanded one
 * level at a time — BR-T-8's «Развернуть на один уровень», repeated — for as
 * long as the lines drawn stay within `limit`. A level that would take the
 * total past it is not opened, nor any below; with the top-level rows alone
 * past it, nothing is expanded.
 */
export function firstExpansion(
  rows: readonly BacklogRow[],
  limit: number = DRAWN_ROWS_LIMIT,
): ReadonlySet<string> {
  let expanded: ReadonlySet<string> = new Set();
  for (
    let next = expandOneLevel(rows, expanded);
    next !== null;
    next = expandOneLevel(rows, expanded)
  ) {
    if (drawnCount(rows, next) > limit) {
      break;
    }
    expanded = next;
  }
  return expanded;
}

/**
 * The ids from the top-level row down to the row `id` itself, collapsed rows'
 * children searched too — the rows that must be expanded for `id`'s parent to
 * be drawn and `id` beneath it; `[]` when the tree has no such row. An
 * iterative walk, with the parent of every row it meets.
 */
export function pathTo(rows: readonly BacklogRow[] | undefined, id: string): string[] {
  const parentOf = new Map<string, string | null>();
  const pending = (rows ?? []).map((item) => ({
    item: item as BacklogTreeItem,
    parent: null as string | null,
  }));
  for (let next = pending.pop(); next !== undefined; next = pending.pop()) {
    parentOf.set(next.item.id, next.parent);
    if (next.item.id === id) {
      const path: string[] = [];
      for (let step: string | null = id; step !== null; step = parentOf.get(step) ?? null) {
        path.unshift(step);
      }
      return path;
    }
    for (const child of next.item.children) {
      pending.push({ item: child, parent: next.item.id });
    }
  }
  return [];
}

/** The expanded set with every one of `ids` expanded; the same set when they all are. */
export function expandedWith(
  expanded: ReadonlySet<string>,
  ids: readonly string[],
): ReadonlySet<string> {
  return ids.every((id) => expanded.has(id)) ? expanded : new Set([...expanded, ...ids]);
}

/** The expanded set with `id` toggled: a new set, so a state update sees the change. */
export function toggled(expanded: ReadonlySet<string>, id: string): ReadonlySet<string> {
  const next = new Set(expanded);
  if (!next.delete(id)) {
    next.add(id);
  }
  return next;
}
