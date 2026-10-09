import { describe, expect, it } from 'vitest';
import type { BacklogRow, BacklogTreeItem } from '../api/boardsApi';
import { hasChildren, toggled, treeLines } from './tree';

// feat-001 BR-H-10..-12: the lines the backlog table draws from the tree the
// server sends and the panel's expanded set.

function node(identifier: string, children: BacklogTreeItem[] = []): BacklogTreeItem {
  return {
    id: identifier,
    identifier,
    number: 0,
    type: { id: 't', key: 'K', name: 'Тип', appearance: 'epic' },
    title: identifier,
    assignee: null,
    state: { id: 's', name: 'Новая', category: { code: 'Proposed', label: 'Новые' } },
    priority: 2,
    parent: null,
    children,
  };
}

function row(order: number, item: BacklogTreeItem): BacklogRow {
  return { ...item, order };
}

const rows = [
  row(1, node('EPIC-1', [node('FEAT-8'), node('FEAT-6', [node('US-2'), node('BUG-4')])])),
  row(2, node('EPIC-20')),
];

function read(expanded: string[]) {
  return treeLines(rows, new Set(expanded)).map(
    (line) => `${line.block}:${'  '.repeat(line.tier)}${line.item.identifier}`,
  );
}

describe('treeLines', () => {
  it('draws only the top-level rows while nothing is expanded (BR-H-12)', () => {
    expect(read([])).toEqual(['0:EPIC-1', '1:EPIC-20']);
  });

  it('draws an expanded row’s children beneath it, one tier deeper, in their order, in its block', () => {
    expect(read(['EPIC-1'])).toEqual(['0:EPIC-1', '0:  FEAT-8', '0:  FEAT-6', '1:EPIC-20']);
  });

  it('goes down every expanded tier, and a collapsed parent hides an expanded child', () => {
    expect(read(['EPIC-1', 'FEAT-6'])).toEqual([
      '0:EPIC-1',
      '0:  FEAT-8',
      '0:  FEAT-6',
      '0:    US-2',
      '0:    BUG-4',
      '1:EPIC-20',
    ]);
    expect(read(['FEAT-6'])).toEqual(['0:EPIC-1', '1:EPIC-20']);
  });

  it('ignores an expanded id with nothing beneath it', () => {
    expect(read(['EPIC-20', 'FEAT-8'])).toEqual(['0:EPIC-1', '1:EPIC-20']);
  });
});

describe('hasChildren and toggled', () => {
  it('a row has an expand control exactly when it has a child (BR-H-12)', () => {
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
