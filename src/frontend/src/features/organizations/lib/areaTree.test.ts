import { describe, expect, it } from 'vitest';
import { getAreaTree, type AreaNode } from '../api/organizationsApi';
import { ids } from '../testFixtures';
import { serveOrganizations } from '../testApi';
import {
  descendantCount,
  expandableIds,
  hasChildren,
  initialExpansion,
  moveOptions,
  narrow,
  parentOptions,
  subtreeIds,
} from './areaTree';

/** The example tree of «Альфа», as the read answers it (pre-order). */
async function exampleTree(): Promise<AreaNode[]> {
  serveOrganizations();
  return getAreaTree(ids.alfa);
}

function manyAreas(count: number): AreaNode[] {
  return Array.from({ length: count }, (_, index) => ({
    id: `a${index}`,
    parentId: index === 0 ? null : 'a0',
    name: `Область ${index}`,
    description: null,
    path: index === 0 ? 'Область 0' : `Область 0 / Область ${index}`,
    depth: index === 0 ? 0 : 1,
  }));
}

describe('mod-018 the example tree as the read gives it', () => {
  it('is in pre-order, siblings alphabetical, paths from the top level', async () => {
    const tree = await exampleTree();

    expect(tree.map((node) => [node.path, node.depth])).toEqual([
      ['Мобильное приложение', 0],
      ['Отчёты', 0],
      ['Платформа', 0],
      ['Платформа / API', 1],
      ['Платформа / API / Шлюз', 2],
    ]);
  });
});

describe('mod-018 design § 5.3 and D-5: the places a new or a moved area can take', () => {
  it('offers «Верхний уровень» first and only with `area.create`', async () => {
    const tree = await exampleTree();

    expect(parentOptions(tree, true)[0]).toEqual({ id: null, label: 'Верхний уровень' });
    expect(
      parentOptions(tree, true)
        .map((option) => option.label)
        .slice(1),
    ).toEqual(tree.map((node) => node.path));
    expect(parentOptions(tree, false).map((option) => option.id)).toEqual(
      tree.map((node) => node.id),
    );
  });

  it('leaves the node, its subtree and its current parent out of a move, in tree order', async () => {
    const tree = await exampleTree();

    expect(moveOptions(tree, ids.apiArea, true).map((option) => option.label)).toEqual([
      'Верхний уровень',
      'Мобильное приложение',
      'Отчёты',
    ]);
    expect(subtreeIds(tree, ids.platformArea)).toEqual(
      new Set([ids.platformArea, ids.apiArea, ids.gatewayArea]),
    );
    expect(descendantCount(tree, ids.platformArea)).toBe(2);
  });

  it('offers «Верхний уровень» to an area below the top, and to no top-level area (it is there already)', async () => {
    const tree = await exampleTree();

    expect(moveOptions(tree, ids.reportsArea, true).some((option) => option.id === null)).toBe(
      false,
    );
    expect(moveOptions(tree, ids.apiArea, false).some((option) => option.id === null)).toBe(false);
  });
});

describe('mod-018 D-3: the initial expansion', () => {
  it('expands every area that has children while there are at most 100 areas', async () => {
    const tree = await exampleTree();

    expect(initialExpansion(tree)).toEqual(new Set([ids.platformArea, ids.apiArea]));
    expect(expandableIds(tree)).toEqual(new Set([ids.platformArea, ids.apiArea]));
    expect(hasChildren(tree, ids.apiArea)).toBe(true);
    expect(hasChildren(tree, ids.gatewayArea)).toBe(false);
  });

  it('shows the top level alone above 100 areas', () => {
    expect(initialExpansion(manyAreas(100))).toEqual(new Set(['a0']));
    expect(initialExpansion(manyAreas(101)).size).toBe(0);
  });
});

describe('mod-018 D-1: the local filter of a picker', () => {
  it('narrows by any of the texts, ignoring case', () => {
    const options = [
      { name: 'Альфа', key: 'ALP' },
      { name: 'Бета', key: 'BET' },
    ];

    expect(narrow(options, 'bet', (o) => [o.name, o.key]).shown).toEqual([options[1]]);
    expect(narrow(options, ' АЛЬФ ', (o) => [o.name, o.key]).shown).toEqual([options[0]]);
    expect(narrow(options, '', (o) => [o.name]).total).toBe(2);
  });

  it('draws at most 50 and counts every match', () => {
    const options = Array.from({ length: 312 }, (_, index) => `Область ${index}`);

    const { shown, total } = narrow(options, '', (option) => [option]);

    expect(shown).toHaveLength(50);
    expect(total).toBe(312);
  });
});
