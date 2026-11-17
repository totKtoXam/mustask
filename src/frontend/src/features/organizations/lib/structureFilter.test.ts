import { describe, expect, it } from 'vitest';
import type { AreaNode } from '../api/organizationsApi';
import { filterStructure } from './structureFilter';

function node(
  id: string,
  type: AreaNode['type'],
  name: string,
  parentId: string | null,
  projectId: string | null = null,
): AreaNode {
  return {
    id,
    parentId,
    type,
    projectId,
    projectActive: projectId === null ? null : true,
    name,
    description: null,
    path: name,
    depth: parentId === null ? 1 : 2,
  };
}

const tree: AreaNode[] = [
  node('d1', 'department', 'Логистика', null),
  node('pr1', 'project', 'Склад', 'd1', 'p1'),
  node('a1', 'area', 'Приёмка', 'pr1', 'p1'),
  node('d2', 'department', 'Финансы', null),
  node('pr2', 'project', 'Бюджет', 'd2', 'p2'),
];
const projects = [
  { id: 'p1', key: 'WH' },
  { id: 'p2', key: 'BUD' },
];
const teams = [{ name: 'Mars', departmentId: 'd1' }];
const idsOf = (rows: AreaNode[]) => rows.map((row) => row.id);

describe('doc-016 BR-TD-21 (feat-061): the «Структура» filter', () => {
  it('keeps everything for an empty text', () => {
    expect(filterStructure(tree, ' ', { projects, teams })).toHaveLength(5);
  });

  it('matches a project by name or key, case ignored, and keeps its department', () => {
    expect(idsOf(filterStructure(tree, 'склад', { projects }))).toEqual(['d1', 'pr1']);
    expect(idsOf(filterStructure(tree, 'bu', { projects }))).toEqual(['d2', 'pr2']);
    expect(idsOf(filterStructure(tree, 'wh', { projects }))).toEqual(['d1', 'pr1']);
  });

  it('does not match a key it was not given (no hidden project is invented)', () => {
    expect(filterStructure(tree, 'wh', {})).toEqual([]);
  });

  it('a matching department keeps all its descendants', () => {
    expect(idsOf(filterStructure(tree, 'логи', { projects }))).toEqual(['d1', 'pr1', 'a1']);
  });

  it('a matching team keeps its department, and nothing under it', () => {
    expect(idsOf(filterStructure(tree, 'MARS', { projects, teams }))).toEqual(['d1']);
  });

  it('finds nothing in a text no name carries', () => {
    expect(filterStructure(tree, 'zzz', { projects, teams })).toEqual([]);
  });
});
