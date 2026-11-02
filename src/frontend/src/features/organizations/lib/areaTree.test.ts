import { describe, expect, it } from 'vitest';
import { getAreaTree, type AreaNode } from '../api/organizationsApi';
import { ids } from '../testFixtures';
import { serveOrganizations } from '../testApi';
import {
  areaMoveOptions,
  descendantCount,
  departmentsOf,
  expandableIds,
  hasChildren,
  initialExpansion,
  isReadOnly,
  narrow,
  placeOfProjectNode,
  placementOptions,
  projectMoveOptions,
  subAreaParentOptions,
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
    type: index === 0 ? ('department' as const) : ('project' as const),
    projectId: index === 0 ? null : `p${index}`,
    projectActive: index === 0 ? null : true,
    name: `Область ${index}`,
    description: null,
    path: index === 0 ? 'Область 0' : `Область 0 / Область ${index}`,
    depth: index === 0 ? 1 : 2,
  }));
}

describe('mod-018 the example tree as the read gives it', () => {
  it('is in pre-order, siblings alphabetical, project nodes named as their projects', async () => {
    const tree = await exampleTree();

    expect(tree.map((node) => [node.type, node.path, node.depth])).toEqual([
      ['project', 'Аналитика', 1],
      ['department', 'Отчёты', 1],
      ['department', 'Платформа', 1],
      ['project', 'Платформа / Пример проекта', 2],
      ['area', 'Платформа / Пример проекта / Мобильное приложение', 3],
      ['area', 'Платформа / Пример проекта / API', 3],
      ['area', 'Платформа / Пример проекта / API / Шлюз', 4],
      ['project', 'Старый проект', 1],
    ]);
    expect(isReadOnly(tree[tree.length - 1]!)).toBe(true);
    expect(isReadOnly(tree[0]!)).toBe(false);
  });
});

describe('mod-018 design §§ 5.2, 5.4 and D-5: the places a new or a moved node can take', () => {
  it('offers «Организация» first, then every department, for a new project', async () => {
    const tree = await exampleTree();

    expect(departmentsOf(tree).map((node) => node.name)).toEqual(['Отчёты', 'Платформа']);
    expect(placementOptions(tree)).toEqual([
      { id: null, label: 'Организация' },
      { id: ids.reportsArea, label: 'Отчёты' },
      { id: ids.platformArea, label: 'Платформа' },
    ]);
  });

  it('offers a project every place but its current one', async () => {
    const tree = await exampleTree();

    expect(projectMoveOptions(tree, ids.platformArea).map((option) => option.label)).toEqual([
      'Организация',
      'Отчёты',
    ]);
    expect(projectMoveOptions(tree, null).map((option) => option.label)).toEqual([
      'Отчёты',
      'Платформа',
    ]);
  });

  it('offers a sub-area its project’s subtree, minus itself, its subtree and its parent', async () => {
    const tree = await exampleTree();
    const subtree = tree.filter((node) => node.projectId === ids.prm);

    expect(subAreaParentOptions(subtree).map((option) => option.label)).toEqual([
      'Платформа / Пример проекта',
      'Платформа / Пример проекта / Мобильное приложение',
      'Платформа / Пример проекта / API',
      'Платформа / Пример проекта / API / Шлюз',
    ]);
    expect(
      areaMoveOptions(subtree, ids.apiArea, ids.prmRoot).map((option) => option.label),
    ).toEqual(['Платформа / Пример проекта / Мобильное приложение']);
    expect(subtreeIds(tree, ids.apiArea)).toEqual(new Set([ids.apiArea, ids.gatewayArea]));
    expect(descendantCount(tree, ids.platformArea)).toBe(4);
  });

  it('reads a project’s place off its node: its department, or «Организация»', async () => {
    const tree = await exampleTree();
    const node = (id: string) => tree.find((entry) => entry.id === id)!;

    expect(placeOfProjectNode(node(ids.prmRoot))).toEqual({
      id: ids.platformArea,
      label: 'Платформа',
    });
    expect(placeOfProjectNode(node(ids.anlRoot))).toEqual({ id: null, label: 'Организация' });
  });
});

describe('mod-018 D-3: the initial expansion', () => {
  it('expands every node that has children while there are at most 100 nodes', async () => {
    const tree = await exampleTree();
    const expandable = new Set([ids.platformArea, ids.prmRoot, ids.apiArea]);

    expect(initialExpansion(tree)).toEqual(expandable);
    expect(expandableIds(tree)).toEqual(expandable);
    expect(hasChildren(tree, ids.apiArea)).toBe(true);
    expect(hasChildren(tree, ids.gatewayArea)).toBe(false);
  });

  it('shows the top level alone above 100 nodes', () => {
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
