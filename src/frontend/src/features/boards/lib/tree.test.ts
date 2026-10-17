import { describe, expect, it } from 'vitest';
import type { BacklogRow, BacklogTreeItem } from '../api/boardsApi';
import {
  DRAWN_ROWS_LIMIT,
  collapseOneLevel,
  drawnCount,
  expandOneLevel,
  expandedWith,
  firstExpansion,
  hasChildren,
  maxDepth,
  pathTo,
  toggled,
  treeLines,
} from './tree';

// feat-001 BR-H-10..-12 and feat-027 BR-T-4..-8: the lines the backlog table
// draws from the tree the server sends and the panel's expanded set, and how
// the two header buttons and the first expansion change that set.

function node(identifier: string, children: BacklogTreeItem[] = []): BacklogTreeItem {
  return {
    id: identifier,
    identifier,
    number: 0,
    type: { id: 't', key: 'K', name: 'Тип', appearance: 'epic' },
    title: identifier,
    assignee: null,
    team: null,
    state: { id: 's', name: 'Новая', category: { code: 'Proposed', label: 'Новые' } },
    priority: 2,
    effort: null,
    businessValue: null,
    valueArea: null,
    parent: null,
    children,
  };
}

function row(order: number, item: BacklogTreeItem): BacklogRow {
  return { ...item, order };
}

/** Top-level rows numbered 1..n, as the read answers them. */
function rowsOf(...items: BacklogTreeItem[]): BacklogRow[] {
  return items.map((item, index) => row(index + 1, item));
}

/**
 * feat-027 US-2's «Эпики» tree of «Платформа»: MSK-EPIC-1 → MSK-FEAT-8 →
 * MSK-FEAT-23; MSK-FEAT-6 → MSK-US-2 → MSK-BUG-5, MSK-TASK-3; MSK-FEAT-6 →
 * MSK-BUG-4; MSK-EPIC-1 → MSK-US-22; and MSK-EPIC-20 alone.
 */
const rows = rowsOf(
  node('EPIC-1', [
    node('FEAT-8', [node('FEAT-23')]),
    node('FEAT-6', [node('US-2', [node('BUG-5'), node('TASK-3')]), node('BUG-4')]),
    node('US-22'),
  ]),
  node('EPIC-20'),
);

function read(expanded: string[]) {
  return treeLines(rows, new Set(expanded)).map(
    (line) => `${line.block}:${'  '.repeat(line.tier)}${line.item.identifier}`,
  );
}

/** The identifiers drawn with `expanded`, in order. */
function drawn(expanded: ReadonlySet<string>): string[] {
  return treeLines(rows, expanded).map((line) => line.item.identifier);
}

const EVERY_ROW_WITH_CHILDREN = ['EPIC-1', 'FEAT-8', 'FEAT-6', 'US-2'];

describe('treeLines', () => {
  it('draws only the top-level rows while nothing is expanded (BR-T-7)', () => {
    expect(read([])).toEqual(['0:EPIC-1', '1:EPIC-20']);
  });

  it('draws an expanded row’s children beneath it, one tier deeper, in their order, in its block', () => {
    expect(read(['EPIC-1'])).toEqual([
      '0:EPIC-1',
      '0:  FEAT-8',
      '0:  FEAT-6',
      '0:  US-22',
      '1:EPIC-20',
    ]);
  });

  it('goes down every expanded tier, and a collapsed parent hides an expanded child', () => {
    expect(read(['EPIC-1', 'FEAT-6'])).toEqual([
      '0:EPIC-1',
      '0:  FEAT-8',
      '0:  FEAT-6',
      '0:    US-2',
      '0:    BUG-4',
      '0:  US-22',
      '1:EPIC-20',
    ]);
    expect(read(['FEAT-6'])).toEqual(['0:EPIC-1', '1:EPIC-20']);
  });

  it('reaches the tasks: four tiers down, the children of each row beneath it (feat-027 US-2)', () => {
    expect(read(EVERY_ROW_WITH_CHILDREN)).toEqual([
      '0:EPIC-1',
      '0:  FEAT-8',
      '0:    FEAT-23',
      '0:  FEAT-6',
      '0:    US-2',
      '0:      BUG-5',
      '0:      TASK-3',
      '0:    BUG-4',
      '0:  US-22',
      '1:EPIC-20',
    ]);
  });

  it('ignores an expanded id with nothing beneath it', () => {
    expect(read(['EPIC-20', 'FEAT-8'])).toEqual(['0:EPIC-1', '1:EPIC-20']);
  });

  it('draws a tree of any depth without a recursion of its own', () => {
    let chain = node('D-10000');
    for (let depth = 9_999; depth >= 0; depth -= 1) {
      chain = node(`D-${depth}`, [chain]);
    }
    const everyId = new Set(Array.from({ length: 10_001 }, (_, depth) => `D-${depth}`));

    const lines = treeLines(rowsOf(chain), everyId);

    expect(lines).toHaveLength(10_001);
    expect(lines.at(-1)).toMatchObject({ tier: 10_000, block: 0 });
    expect(lines.at(-1)?.item.identifier).toBe('D-10000');
  });
});

describe('hasChildren and toggled', () => {
  it('a row has an expand control exactly when it has a child (BR-T-7)', () => {
    expect(hasChildren(node('EPIC-1', [node('FEAT-8')]))).toBe(true);
    expect(hasChildren(node('EPIC-20'))).toBe(false);
  });

  it('toggles one id into a new set, the old one unchanged', () => {
    const before = new Set(['EPIC-1']);
    const after = toggled(before, 'FEAT-6');
    expect([...after]).toEqual(['EPIC-1', 'FEAT-6']);
    expect([...toggled(after, 'EPIC-1')]).toEqual(['FEAT-6']);
    expect([...before]).toEqual(['EPIC-1']);
  });
});

describe('maxDepth', () => {
  it('is the depth of the deepest row, collapsed ones counted: 3 for the tree to the tasks', () => {
    expect(maxDepth(rows)).toBe(3);
  });

  it('is 0 when no top-level row has a child, and for an empty backlog', () => {
    expect(maxDepth(rowsOf(node('EPIC-20'), node('EPIC-21')))).toBe(0);
    expect(maxDepth([])).toBe(0);
  });

  it('counts a same-level chain like any other: a feature under a feature under a feature', () => {
    expect(maxDepth(rowsOf(node('FEAT-1', [node('FEAT-2', [node('FEAT-3')])])))).toBe(2);
  });
});

describe('drawnCount', () => {
  it('equals the number of lines treeLines draws, for any expanded set', () => {
    for (const expanded of [
      [],
      ['EPIC-1'],
      ['EPIC-1', 'FEAT-6'],
      ['FEAT-6'],
      ['EPIC-20', 'FEAT-8'],
      EVERY_ROW_WITH_CHILDREN,
    ]) {
      const set = new Set(expanded);
      expect(drawnCount(rows, set)).toBe(treeLines(rows, set).length);
    }
  });

  it('counts the top-level rows alone while nothing is expanded, and every row when all are', () => {
    expect(drawnCount(rows, new Set())).toBe(2);
    expect(drawnCount(rows, new Set(EVERY_ROW_WITH_CHILDREN))).toBe(10);
    expect(drawnCount([], new Set())).toBe(0);
  });
});

describe('expandOneLevel (BR-T-8, feat-027 US-3)', () => {
  it('each call opens one more depth: EPIC-1, then FEAT-8 and FEAT-6, then US-2, then nothing', () => {
    const first = expandOneLevel(rows, new Set());
    expect(first && [...first]).toEqual(['EPIC-1']);
    expect(drawn(first ?? new Set())).toEqual(['EPIC-1', 'FEAT-8', 'FEAT-6', 'US-22', 'EPIC-20']);

    const second = expandOneLevel(rows, first ?? new Set());
    expect(second && [...second].sort()).toEqual(['EPIC-1', 'FEAT-6', 'FEAT-8']);
    expect(drawn(second ?? new Set())).toContain('FEAT-23');
    expect(drawn(second ?? new Set())).toContain('US-2');
    expect(drawn(second ?? new Set())).not.toContain('BUG-5');

    const third = expandOneLevel(rows, second ?? new Set());
    expect(third && [...third].sort()).toEqual(['EPIC-1', 'FEAT-6', 'FEAT-8', 'US-2']);
    expect(drawn(third ?? new Set())).toContain('BUG-5');
    expect(drawn(third ?? new Set())).toContain('TASK-3');

    expect(expandOneLevel(rows, third ?? new Set())).toBeNull();
  });

  it('completes a level opened in part: FEAT-8 joins FEAT-6, and US-2 stays collapsed', () => {
    const next = expandOneLevel(rows, new Set(['EPIC-1', 'FEAT-6']));

    expect(next && [...next].sort()).toEqual(['EPIC-1', 'FEAT-6', 'FEAT-8']);
  });

  it('looks only at shown rows: an expanded row beneath a collapsed one does not move the level', () => {
    // US-2 is expanded but hidden beneath the collapsed FEAT-6: depth 1 is still the one to open.
    const next = expandOneLevel(rows, new Set(['EPIC-1', 'US-2']));

    expect(next && [...next].sort()).toEqual(['EPIC-1', 'FEAT-6', 'FEAT-8', 'US-2']);
  });

  it('is null when no row has a child, and for an empty backlog', () => {
    expect(expandOneLevel(rowsOf(node('FEAT-21')), new Set())).toBeNull();
    expect(expandOneLevel([], new Set())).toBeNull();
  });

  it('returns a new set and leaves the old one as it was', () => {
    const before = new Set<string>();

    const after = expandOneLevel(rows, before);

    expect(after).not.toBe(before);
    expect(before.size).toBe(0);
  });
});

describe('collapseOneLevel (BR-T-8, feat-027 US-3)', () => {
  it('each call closes the deepest open depth: US-2, then FEAT-8 and FEAT-6, then EPIC-1, then nothing', () => {
    const everything = new Set(EVERY_ROW_WITH_CHILDREN);

    const first = collapseOneLevel(rows, everything);
    expect(first && [...first].sort()).toEqual(['EPIC-1', 'FEAT-6', 'FEAT-8']);
    expect(drawn(first ?? new Set())).not.toContain('BUG-5');
    expect(drawn(first ?? new Set())).toContain('US-2');

    const second = collapseOneLevel(rows, first ?? new Set());
    expect(second && [...second]).toEqual(['EPIC-1']);

    const third = collapseOneLevel(rows, second ?? new Set());
    expect(third && [...third]).toEqual([]);
    expect(drawn(third ?? new Set())).toEqual(['EPIC-1', 'EPIC-20']);

    expect(collapseOneLevel(rows, third ?? new Set())).toBeNull();
  });

  it('closes a level opened in part: FEAT-6 collapses, EPIC-1 stays', () => {
    const next = collapseOneLevel(rows, new Set(['EPIC-1', 'FEAT-6']));

    expect(next && [...next]).toEqual(['EPIC-1']);
  });

  it('closes the depth and everything deeper, a hidden expanded row included', () => {
    // US-2 is expanded but hidden beneath the collapsed FEAT-6: the deepest shown open row is EPIC-1.
    const next = collapseOneLevel(rows, new Set(['EPIC-1', 'US-2']));

    expect(next && [...next]).toEqual([]);
  });

  it('is null while nothing shown is expanded — an expanded id with nothing beneath it does not count', () => {
    expect(collapseOneLevel(rows, new Set())).toBeNull();
    expect(collapseOneLevel(rows, new Set(['EPIC-20', 'US-22']))).toBeNull();
    expect(collapseOneLevel([], new Set())).toBeNull();
  });

  it('returns a new set and leaves the old one as it was', () => {
    const before = new Set(['EPIC-1']);

    const after = collapseOneLevel(rows, before);

    expect(after).not.toBe(before);
    expect([...before]).toEqual(['EPIC-1']);
  });
});

describe('firstExpansion (feat-027 D-6 OQ-2 (d))', () => {
  /** `parents` rows, each with `children` children, each of those with `grandchildren`. */
  function generated(parents: number, children: number, grandchildren: number): BacklogRow[] {
    return rowsOf(
      ...Array.from({ length: parents }, (_, parent) =>
        node(
          `P${parent}`,
          Array.from({ length: children }, (_, child) =>
            node(
              `P${parent}C${child}`,
              Array.from({ length: grandchildren }, (_, grandchild) =>
                node(`P${parent}C${child}G${grandchild}`),
              ),
            ),
          ),
        ),
      ),
    );
  }

  it('names the cap the table draws at once: 2 000 rows', () => {
    expect(DRAWN_ROWS_LIMIT).toBe(2000);
  });

  it('opens a small tree down to its last level', () => {
    const expanded = firstExpansion(rows);

    expect([...expanded].sort()).toEqual(['EPIC-1', 'FEAT-6', 'FEAT-8', 'US-2']);
    expect(drawnCount(rows, expanded)).toBe(10);
  });

  it('opens nothing for a backlog with no child, and for an empty one', () => {
    expect(firstExpansion(rowsOf(node('FEAT-21'))).size).toBe(0);
    expect(firstExpansion([]).size).toBe(0);
  });

  it('opens level by level while the lines drawn stay within the cap, and stops before the level that passes it', () => {
    // 4 rows + 8 + 16 = 28 lines when fully open.
    const tree = generated(4, 2, 2);

    expect(drawnCount(tree, firstExpansion(tree, 28))).toBe(28);
    expect(drawnCount(tree, firstExpansion(tree, 27))).toBe(12);
    expect(drawnCount(tree, firstExpansion(tree, 12))).toBe(12);
    expect(drawnCount(tree, firstExpansion(tree, 11))).toBe(4);
    expect(drawnCount(tree, firstExpansion(tree, 4))).toBe(4);
  });

  it('opens nothing when the top-level rows alone pass the cap', () => {
    expect(firstExpansion(generated(4, 2, 2), 3).size).toBe(0);
  });

  it('takes a level that lands exactly on 2 000 and refuses one that lands on 2 100', () => {
    // 100 + 100 × 19 = 2 000 lines after the first level.
    const exact = generated(100, 19, 1);
    expect(drawnCount(exact, firstExpansion(exact))).toBe(2000);

    // 100 + 100 × 20 = 2 100 lines after the first level: nothing is opened.
    const over = generated(100, 20, 1);
    expect(firstExpansion(over).size).toBe(0);
    expect(drawnCount(over, firstExpansion(over))).toBe(100);
  });

  it('stops at the second level for 10 epics × 20 features × 10 stories: 210 lines, not 2 210', () => {
    const tree = generated(10, 20, 10);

    const expanded = firstExpansion(tree);

    expect(drawnCount(tree, expanded)).toBe(210);
    expect(expanded.size).toBe(10);
    const deeper = expandOneLevel(tree, expanded);
    expect(deeper && drawnCount(tree, deeper)).toBe(2210);
  });

  it('is the set the header buttons then work from', () => {
    const narrowed = collapseOneLevel(rows, firstExpansion(rows));

    expect(narrowed && [...narrowed].sort()).toEqual(['EPIC-1', 'FEAT-6', 'FEAT-8']);
  });
});

describe('pathTo (feat-027 US-6: the rows to open so a moved row is drawn)', () => {
  it('is the ids from the top-level row down to the row itself', () => {
    expect(pathTo(rows, 'EPIC-20')).toEqual(['EPIC-20']);
    expect(pathTo(rows, 'FEAT-23')).toEqual(['EPIC-1', 'FEAT-8', 'FEAT-23']);
    expect(pathTo(rows, 'TASK-3')).toEqual(['EPIC-1', 'FEAT-6', 'US-2', 'TASK-3']);
    expect(pathTo(rows, 'BUG-4')).toEqual(['EPIC-1', 'FEAT-6', 'BUG-4']);
  });

  it('is empty for a row the tree does not have, and for no read at all', () => {
    expect(pathTo(rows, 'MISSING')).toEqual([]);
    expect(pathTo([], 'EPIC-1')).toEqual([]);
    expect(pathTo(undefined, 'EPIC-1')).toEqual([]);
  });

  it('walks a chain of 10 000 links without recursing', () => {
    let chain = node('N10000');
    for (let depth = 9999; depth >= 1; depth -= 1) {
      chain = node(`N${depth}`, [chain]);
    }

    const path = pathTo(rowsOf(chain), 'N10000');

    expect(path).toHaveLength(10_000);
    expect(path[0]).toBe('N1');
    expect(path[9999]).toBe('N10000');
  });
});

describe('expandedWith', () => {
  it('adds the ids that are missing, as a new set', () => {
    const open = new Set(['EPIC-1']);

    const next = expandedWith(open, ['EPIC-1', 'FEAT-8']);

    expect([...next].sort()).toEqual(['EPIC-1', 'FEAT-8']);
    expect([...open]).toEqual(['EPIC-1']);
  });

  it('answers the same set when they are all there, so a state update changes nothing', () => {
    const open = new Set(['EPIC-1', 'FEAT-8']);

    expect(expandedWith(open, ['FEAT-8'])).toBe(open);
    expect(expandedWith(open, [])).toBe(open);
  });
});
