import { describe, expect, it } from 'vitest';
import { useState } from 'react';
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Route, Routes, useLocation } from 'react-router-dom';
import { TreeRegistry, type TreeRegistryNode } from './TreeRegistry';

// mod-018 design D-7: `labelTo` turns each row's label into a router link; the row stays one tab
// stop, Enter follows the link, Space still selects.

const NODES: TreeRegistryNode[] = [
  { id: 'root', parentId: null, label: 'Корень' },
  { id: 'leaf', parentId: 'root', label: 'Лист' },
];

function Where() {
  return <div data-testid="where">{useLocation().pathname}</div>;
}

function Harness() {
  const [selected, setSelected] = useState<string | null>(null);
  return (
    <MemoryRouter initialEntries={['/tree']}>
      <Routes>
        <Route
          path="/tree"
          element={
            <TreeRegistry
              aria-label="Иерархия"
              firstColumn="Узел"
              columns={[]}
              nodes={NODES}
              labelTo={(node) => `/areas/${node.id}`}
              actions={() => []}
              showActions={false}
              selectedId={selected}
              onSelect={setSelected}
              expanded={new Set(['root'])}
              onToggle={() => {}}
            />
          }
        />
        <Route path="*" element={<Where />} />
      </Routes>
    </MemoryRouter>
  );
}

describe('mod-018 D-7: TreeRegistry `labelTo`', () => {
  it('draws each label as a link to its address, out of the tab order', () => {
    render(<Harness />);

    const link = screen.getByRole('link', { name: 'Лист' });
    expect(link).toHaveAttribute('href', '/areas/leaf');
    expect(link).toHaveAttribute('tabindex', '-1');
    expect(screen.getByRole('link', { name: 'Корень' })).toHaveAttribute('href', '/areas/root');
  });

  it('follows the link with Enter on the row, and selects with Space without leaving', async () => {
    render(<Harness />);
    const rowOf = (label: string) =>
      screen
        .getAllByRole('row')
        .find(
          (candidate) =>
            within(candidate).queryByText(label) && candidate.hasAttribute('aria-level'),
        )!;

    rowOf('Лист').focus();
    await userEvent.keyboard(' ');
    expect(rowOf('Лист')).toHaveAttribute('aria-selected', 'true');
    expect(screen.queryByTestId('where')).not.toBeInTheDocument();

    await userEvent.keyboard('{Enter}');
    expect(await screen.findByTestId('where')).toHaveTextContent('/areas/leaf');
  });

  it('follows the link by a click on the label', async () => {
    render(<Harness />);

    await userEvent.click(screen.getByRole('link', { name: 'Корень' }));

    expect(await screen.findByTestId('where')).toHaveTextContent('/areas/root');
  });
});
