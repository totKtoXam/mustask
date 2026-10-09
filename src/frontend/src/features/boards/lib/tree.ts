import type { BacklogRow, BacklogTreeItem } from '../api/boardsApi';

// How the backlog's tree is drawn (feat-001 US-4; BR-H-9..-12, spec § Page
// «Бэклоги»): the top-level rows in mod-006's order, each followed — while it
// is expanded — by its children one tier deeper, and so on down. The server
// sends the whole tree and collapses nothing; what is expanded is the
// panel's, by item, and changes only what is shown.

/** One line of the table: a row or a nested row. */
export interface TreeLine {
  item: BacklogTreeItem;
  /** 0 for a top-level row, 1 for its children, 2 for theirs. */
  tier: number;
  /** The index of the top-level row whose block the line is part of. */
  block: number;
}

/** Whether a row has an expand control: it has a child to show (BR-H-12). */
export function hasChildren(item: Pick<BacklogTreeItem, 'children'>): boolean {
  return item.children.length > 0;
}

/**
 * The lines the table draws: every top-level row, and beneath each expanded
 * one its children in their own backlog's order (BR-H-11), recursively. A
 * collapsed row's children are not drawn at all (spec § NFR: nested rows are
 * rendered only while shown).
 */
export function treeLines(rows: readonly BacklogRow[], expanded: ReadonlySet<string>): TreeLine[] {
  const lines: TreeLine[] = [];
  const visit = (item: BacklogTreeItem, tier: number, block: number) => {
    lines.push({ item, tier, block });
    if (expanded.has(item.id)) {
      for (const child of item.children) {
        visit(child, tier + 1, block);
      }
    }
  };
  rows.forEach((row, block) => visit(row, 0, block));
  return lines;
}

/** The expanded set with `id` toggled: a new set, so a state update sees the change. */
export function toggled(expanded: ReadonlySet<string>, id: string): ReadonlySet<string> {
  const next = new Set(expanded);
  if (!next.delete(id)) {
    next.add(id);
  }
  return next;
}
