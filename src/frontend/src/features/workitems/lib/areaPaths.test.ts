import { describe, expect, it } from 'vitest';
import { projectRelativePath } from './areaPaths';

const nodes = [
  { type: 'project', name: 'Alpha', path: 'Логистика / Alpha' },
  { type: 'area', name: 'Платформа', path: 'Логистика / Alpha / Платформа' },
  { type: 'area', name: 'API', path: 'Логистика / Alpha / Платформа / API' },
];

describe('projectRelativePath', () => {
  it('cuts the department off: the node itself is «Alpha»', () => {
    expect(nodes.map((node) => projectRelativePath(nodes, node))).toEqual([
      'Alpha',
      'Alpha / Платформа',
      'Alpha / Платформа / API',
    ]);
  });

  it('keeps the path of a project at the top of the organization', () => {
    const top = [
      { type: 'project', name: 'Alpha', path: 'Alpha' },
      { type: 'area', name: 'API', path: 'Alpha / API' },
    ];
    expect(projectRelativePath(top, top[1]!)).toBe('Alpha / API');
  });

  it('keeps a path as it is when the tree has no project node', () => {
    expect(projectRelativePath([], nodes[1]!)).toBe(nodes[1]!.path);
  });
});
