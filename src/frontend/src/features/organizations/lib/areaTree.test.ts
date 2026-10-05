import { describe, expect, it } from 'vitest';
import { getAreaTree, type AreaNode, type Team } from '../api/organizationsApi';
import { ids } from '../testFixtures';
import { serveOrganizations } from '../testApi';
import {
  INHERIT_OWNER_LABEL,
  defaultAreaOptions,
  moveOptions,
  ownerOptions,
  ownerTeamIdOf,
  rootOf,
  subtreeIds,
} from './areaTree';

/** The example tree of «Пример проекта», as the read answers it (pre-order). */
async function exampleTree(): Promise<AreaNode[]> {
  serveOrganizations();
  return getAreaTree(ids.alfa, ids.prm);
}

describe('mod-002 the example tree as the read gives it', () => {
  it('is in pre-order, siblings alphabetical, paths per DC-9', async () => {
    const tree = await exampleTree();

    expect(tree.map((node) => [node.path, node.depth])).toEqual([
      ['Пример проекта', 0],
      ['Мобильное приложение', 1],
      ['Отчёты', 1],
      ['Платформа', 1],
      ['Платформа / API', 2],
      ['Платформа / API / Шлюз', 3],
    ]);
    expect(rootOf(tree)?.id).toBe(ids.prmRoot);
  });
});

describe('mod-002 US-5 Scenario: Never under itself or its descendant', () => {
  it('leaves the node, its subtree and its current parent out of the move picker, in tree order', async () => {
    const tree = await exampleTree();

    expect(moveOptions(tree, ids.platformArea).map((option) => option.path)).toEqual([
      'Мобильное приложение',
      'Отчёты',
    ]);
    expect(moveOptions(tree, ids.gatewayArea).map((option) => option.path)).toEqual([
      'Пример проекта',
      'Мобильное приложение',
      'Отчёты',
      'Платформа',
    ]);
    expect(subtreeIds(tree, ids.platformArea)).toEqual(
      new Set([ids.platformArea, ids.apiArea, ids.gatewayArea]),
    );
  });

  it('marks the root among the destinations', async () => {
    const tree = await exampleTree();

    const options = moveOptions(tree, ids.apiArea);
    expect(options.find((option) => option.isRoot)?.path).toBe('Пример проекта');
    expect(options.filter((option) => option.isRoot)).toHaveLength(1);
  });
});

describe('mod-002 US-5 Scenario: Never leaves its project', () => {
  it('builds the picker from one project’s tree only', async () => {
    const tree = await exampleTree();

    expect(
      moveOptions(tree, ids.mobileArea).every((option) => tree.some((n) => n.id === option.id)),
    ).toBe(true);
    expect(moveOptions(tree, ids.mobileArea).map((option) => option.id)).not.toContain(ids.anlRoot);
  });
});

describe('mod-002 US-4 Scenario: Default area chosen among the team’s explicit areas', () => {
  it('offers «Платформа» and «Платформа / API» only, never «Платформа / API / Шлюз» it inherits', async () => {
    const tree = await exampleTree();

    expect(defaultAreaOptions(tree, ids.platformTeam).map((node) => node.path)).toEqual([
      'Платформа',
      'Платформа / API',
    ]);
  });
});

describe('mod-002 US-6 Scenario: Created with or without an owner (the picker)', () => {
  it('puts «Наследовать от родительской области» first, then the active teams in list order', () => {
    const teams = [
      { id: 't1', name: 'Пример проекта' },
      { id: 't2', name: 'Платформа' },
    ] as Team[];

    expect(ownerOptions(teams)).toEqual([
      { value: '', label: 'Наследовать от родительской области' },
      { value: 't1', label: 'Пример проекта' },
      { value: 't2', label: 'Платформа' },
    ]);
    expect(INHERIT_OWNER_LABEL).toBe('Наследовать от родительской области');
  });

  it('sends «Наследовать…» as null and a team as its id', () => {
    expect(ownerTeamIdOf('')).toBeNull();
    expect(ownerTeamIdOf('t2')).toBe('t2');
  });
});
