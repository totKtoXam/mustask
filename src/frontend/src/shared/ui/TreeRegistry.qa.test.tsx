// Added by the frontend QA pass of mod-002 (qa.md § Frontend, F-11): the
// tree registry at the volume the spec names — 500 nodes in 10 levels (NFR
// Volumes) — still renders every row and still reaches every row by keyboard.
import { describe, expect, it } from 'vitest';
import { act, render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import AddIcon from '@mui/icons-material/Add';
import { TreeRegistry, type TreeRegistryNode } from './TreeRegistry';

/**
 * 500 nodes, 10 levels: a root, then a spine of nine levels under it, and
 * the rest spread as leaves under the spine's nodes. Pre-order, as the
 * area tree's read gives it.
 */
function bigTree(): TreeRegistryNode[] {
  const nodes: TreeRegistryNode[] = [{ id: 'n0', parentId: null, label: 'Узел 0' }];
  const spine = ['n0'];
  for (let level = 1; level < 10; level += 1) {
    const id = `n${level}`;
    nodes.push({ id, parentId: spine[level - 1]!, label: `Узел ${level}` });
    spine.push(id);
  }
  for (let index = nodes.length; index < 500; index += 1) {
    // Under levels 1–9 of the spine, so no leaf goes below level 10.
    nodes.push({ id: `n${index}`, parentId: spine[index % 9]!, label: `Узел ${index}` });
  }
  return nodes;
}

function Tree({ nodes, expanded }: { nodes: TreeRegistryNode[]; expanded: ReadonlySet<string> }) {
  return (
    <TreeRegistry
      aria-label="Иерархия"
      firstColumn="Узел"
      columns={[]}
      nodes={nodes}
      actions={() => [{ label: 'Добавить', icon: <AddIcon />, onClick: () => {} }]}
      selectedId={null}
      onSelect={() => {}}
      expanded={expanded}
      onToggle={() => {}}
    />
  );
}

function treeRows(): HTMLElement[] {
  return within(screen.getByRole('treegrid', { name: 'Иерархия' }))
    .getAllByRole('row')
    .filter((row) => row.hasAttribute('aria-level'));
}

describe('(QA) mod-002 F-11: a 500-node, 10-level tree (NFR Volumes)', () => {
  it('renders all 500 rows expanded, and End / Home reach the last and the first by keyboard', async () => {
    const nodes = bigTree();
    const expanded = new Set(nodes.map((node) => node.id));
    render(<Tree nodes={nodes} expanded={expanded} />);

    const rows = treeRows();
    expect(rows).toHaveLength(500);
    expect(Math.max(...rows.map((row) => Number(row.getAttribute('aria-level'))))).toBe(10);

    act(() => rows[0]!.focus());
    await userEvent.keyboard('{End}');
    expect(rows[rows.length - 1]).toHaveFocus();
    await userEvent.keyboard('{Home}');
    expect(rows[0]).toHaveFocus();
  }, 30_000);

  it('keeps the expanded set across a read that hands it new node objects', () => {
    const nodes = bigTree();
    const expanded = new Set(['n0', 'n1', 'n2']);
    const { rerender } = render(<Tree nodes={nodes} expanded={expanded} />);
    const before = treeRows().map((row) => row.textContent);

    rerender(<Tree nodes={bigTree()} expanded={expanded} />);

    expect(treeRows().map((row) => row.textContent)).toEqual(before);
  }, 30_000);
});
