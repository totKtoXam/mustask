import { describe, expect, it, vi } from 'vitest';
import { createRef, useState } from 'react';
import { act, render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import AddIcon from '@mui/icons-material/Add';
import DeleteOutlineOutlinedIcon from '@mui/icons-material/DeleteOutlineOutlined';
import { TreeRegistry, type TreeRegistryHandle, type TreeRegistryNode } from './TreeRegistry';

interface Node extends TreeRegistryNode {
  extra: string;
}

/** A small hierarchy: «Корень» › («Ветка» › «Лист 1», «Лист 2»), «Побег». */
const NODES: Node[] = [
  { id: 'root', parentId: null, label: 'Корень', extra: 'к' },
  { id: 'branch', parentId: 'root', label: 'Ветка', extra: 'в' },
  { id: 'leaf-1', parentId: 'branch', label: 'Лист 1', extra: 'л1' },
  { id: 'leaf-2', parentId: 'branch', label: 'Лист 2', extra: 'л2' },
  { id: 'shoot', parentId: 'root', label: 'Побег', extra: 'п' },
];

function Harness({
  onAction = () => {},
  handle,
  initiallyExpanded = ['root'],
}: {
  onAction?: (label: string, id: string) => void;
  handle?: React.Ref<TreeRegistryHandle>;
  initiallyExpanded?: string[];
}) {
  const [selected, setSelected] = useState<string | null>(null);
  const [expanded, setExpanded] = useState<ReadonlySet<string>>(new Set(initiallyExpanded));
  return (
    <>
      <button type="button">До</button>
      <TreeRegistry
        ref={handle}
        aria-label="Иерархия"
        firstColumn="Узел"
        columns={[{ header: 'Свойство', render: (node: Node) => node.extra }]}
        nodes={NODES}
        adornment={(node) => (node.id === 'branch' ? <span>метка</span> : null)}
        actions={(node) => [
          { label: 'Добавить', icon: <AddIcon />, onClick: () => onAction('Добавить', node.id) },
          ...(node.parentId
            ? [
                {
                  label: 'Убрать',
                  icon: <DeleteOutlineOutlinedIcon />,
                  onClick: () => onAction('Убрать', node.id),
                  destructive: true,
                },
              ]
            : []),
        ]}
        selectedId={selected}
        onSelect={setSelected}
        expanded={expanded}
        onToggle={(id) =>
          setExpanded((current) => {
            const next = new Set(current);
            if (next.has(id)) next.delete(id);
            else next.add(id);
            return next;
          })
        }
      />
      <button type="button">После</button>
    </>
  );
}

function row(label: string): HTMLElement {
  const found = screen
    .getAllByRole('row')
    .find(
      (candidate) => candidate.getAttribute('aria-level') && within(candidate).queryByText(label),
    );
  if (!found) {
    throw new Error(`No row «${label}»`);
  }
  return found;
}

function visibleLabels(): string[] {
  return screen
    .getAllByRole('row')
    .filter((candidate) => candidate.hasAttribute('aria-level'))
    .map((candidate) => within(candidate).getAllByRole('gridcell')[0]!.textContent ?? '');
}

describe('mod-002 US-5 Scenario: Shown as the tree registry — the markup (design § 11.2)', () => {
  it('is a treegrid named by its aria-label, with the real headers and one row per visible node', () => {
    render(<Harness />);

    const grid = screen.getByRole('treegrid', { name: 'Иерархия' });
    expect(
      within(grid)
        .getAllByRole('columnheader')
        .map((cell) => cell.textContent),
    ).toEqual(['Узел', 'Свойство', '']);
    expect(visibleLabels()).toEqual(['Корень', 'Веткаметка', 'Побег']);
  });

  it('gives each row its level, set size, position, selection and, with children, its expansion', () => {
    render(<Harness />);

    expect(row('Корень')).toHaveAttribute('aria-level', '1');
    expect(row('Корень')).toHaveAttribute('aria-expanded', 'true');
    expect(row('Ветка')).toHaveAttribute('aria-level', '2');
    expect(row('Ветка')).toHaveAttribute('aria-setsize', '2');
    expect(row('Ветка')).toHaveAttribute('aria-posinset', '1');
    expect(row('Ветка')).toHaveAttribute('aria-expanded', 'false');
    expect(row('Побег')).toHaveAttribute('aria-posinset', '2');
    expect(row('Побег')).not.toHaveAttribute('aria-expanded');
    expect(row('Побег')).toHaveAttribute('aria-selected', 'false');
  });

  it('names the expand control by the node and keeps it out of the tab order', async () => {
    const user = userEvent.setup();
    render(<Harness />);

    const expand = screen.getByRole('button', { name: 'Развернуть «Ветка»' });
    expect(expand).toHaveAttribute('tabindex', '-1');
    expect(screen.getByRole('button', { name: 'Свернуть «Корень»' })).toBeInTheDocument();

    await user.click(expand);

    expect(visibleLabels()).toEqual(['Корень', 'Веткаметка', 'Лист 1', 'Лист 2', 'Побег']);
    expect(row('Лист 1')).toHaveAttribute('aria-level', '3');
    expect(screen.getByRole('button', { name: 'Свернуть «Ветка»' })).toBeInTheDocument();
  });

  it('shows the adornment after the label and each column’s cell', () => {
    render(<Harness />);

    expect(within(row('Ветка')).getByText('метка')).toBeInTheDocument();
    expect(within(row('Ветка')).getByText('в')).toBeInTheDocument();
  });
});

describe('mod-002 US-5 Scenario: Shown as the tree registry — the keyboard model (design § 11.6)', () => {
  it('Tab enters on the first row, then reaches its «Действия», then leaves the tree', async () => {
    const user = userEvent.setup();
    render(<Harness />);

    screen.getByRole('button', { name: 'До' }).focus();
    await user.tab();
    expect(row('Корень')).toHaveFocus();
    await user.tab();
    expect(screen.getByRole('button', { name: 'Действия: Корень' })).toHaveFocus();
    await user.tab();
    expect(screen.getByRole('button', { name: 'После' })).toHaveFocus();
  });

  it('keeps one row and one «Действия» in the tab order (roving tabIndex)', async () => {
    const user = userEvent.setup();
    render(<Harness />);

    row('Корень').focus();
    await user.keyboard('{ArrowDown}');

    const rows = screen
      .getAllByRole('row')
      .filter((candidate) => candidate.hasAttribute('aria-level'));
    expect(rows.filter((candidate) => candidate.getAttribute('tabindex') === '0')).toEqual([
      row('Ветка'),
    ]);
    const actions = screen.getAllByRole('button', { name: /^Действия: / });
    expect(actions.filter((button) => button.getAttribute('tabindex') === '0')).toEqual([
      screen.getByRole('button', { name: 'Действия: Ветка' }),
    ]);
  });

  it('Tab enters on the selected row when there is one', async () => {
    const user = userEvent.setup();
    render(<Harness />);
    await user.click(row('Побег'));
    screen.getByRole('button', { name: 'До' }).focus();

    await user.tab();

    expect(row('Побег')).toHaveFocus();
  });

  it('ArrowDown / ArrowUp move between visible rows; Home / End go to the first and last', async () => {
    const user = userEvent.setup();
    render(<Harness />);
    row('Корень').focus();

    await user.keyboard('{ArrowDown}');
    expect(row('Ветка')).toHaveFocus();
    await user.keyboard('{ArrowDown}');
    expect(row('Побег')).toHaveFocus();
    await user.keyboard('{ArrowDown}');
    expect(row('Побег')).toHaveFocus();
    await user.keyboard('{ArrowUp}');
    expect(row('Ветка')).toHaveFocus();
    await user.keyboard('{End}');
    expect(row('Побег')).toHaveFocus();
    await user.keyboard('{Home}');
    expect(row('Корень')).toHaveFocus();
  });

  it('ArrowRight expands a collapsed node, then goes to its first child; on a leaf it does nothing', async () => {
    const user = userEvent.setup();
    render(<Harness />);
    row('Ветка').focus();

    await user.keyboard('{ArrowRight}');
    expect(row('Ветка')).toHaveAttribute('aria-expanded', 'true');
    expect(row('Ветка')).toHaveFocus();
    await user.keyboard('{ArrowRight}');
    expect(row('Лист 1')).toHaveFocus();
    await user.keyboard('{ArrowRight}');
    expect(row('Лист 1')).toHaveFocus();
  });

  it('ArrowLeft collapses an expanded node, otherwise goes to the parent', async () => {
    const user = userEvent.setup();
    render(<Harness initiallyExpanded={['root', 'branch']} />);
    row('Лист 2').focus();

    await user.keyboard('{ArrowLeft}');
    expect(row('Ветка')).toHaveFocus();
    await user.keyboard('{ArrowLeft}');
    expect(row('Ветка')).toHaveAttribute('aria-expanded', 'false');
    expect(visibleLabels()).not.toContain('Лист 2');
    await user.keyboard('{ArrowLeft}');
    expect(row('Корень')).toHaveFocus();
  });

  it('Enter and Space select the focused row, one at a time', async () => {
    const user = userEvent.setup();
    render(<Harness />);
    row('Ветка').focus();

    await user.keyboard('{Enter}');
    expect(row('Ветка')).toHaveAttribute('aria-selected', 'true');
    await user.keyboard('{ArrowDown}{ }');
    expect(row('Побег')).toHaveAttribute('aria-selected', 'true');
    expect(row('Ветка')).toHaveAttribute('aria-selected', 'false');
  });

  it('a click selects and focuses the row', async () => {
    const user = userEvent.setup();
    render(<Harness />);

    await user.click(within(row('Побег')).getByText('Побег'));

    expect(row('Побег')).toHaveAttribute('aria-selected', 'true');
    expect(row('Побег')).toHaveFocus();
  });

  it.each([['{Shift>}{F10}{/Shift}'], ['{ContextMenu}']])(
    '%s opens the focused row’s menu; Escape closes it and returns to «Действия»',
    async (keys) => {
      const user = userEvent.setup();
      render(<Harness />);
      row('Ветка').focus();

      await user.keyboard(keys);

      const menu = await screen.findByRole('menu');
      expect(
        within(menu)
          .getAllByRole('menuitem')
          .map((item) => item.textContent),
      ).toEqual(['Добавить', 'Убрать']);
      await user.keyboard('{Escape}');
      expect(screen.queryByRole('menu')).not.toBeInTheDocument();
      expect(screen.getByRole('button', { name: 'Действия: Ветка' })).toHaveFocus();
    },
  );

  it('«Действия» opens the row’s menu; an entry closes it and runs its action', async () => {
    const user = userEvent.setup();
    const onAction = vi.fn();
    render(<Harness onAction={onAction} />);

    const button = screen.getByRole('button', { name: 'Действия: Корень' });
    expect(button).toHaveAttribute('aria-haspopup', 'menu');
    await user.click(button);
    const menu = await screen.findByRole('menu');
    expect(
      within(menu)
        .getAllByRole('menuitem')
        .map((item) => item.textContent),
    ).toEqual(['Добавить']);
    await user.click(within(menu).getByRole('menuitem', { name: 'Добавить' }));

    expect(onAction).toHaveBeenCalledWith('Добавить', 'root');
    expect(screen.queryByRole('menu')).not.toBeInTheDocument();
  });
});

describe('mod-002 design § 11.7: the caller moves focus after a change', () => {
  it('focusRow focuses a rendered row now, and a row not yet rendered once it appears', async () => {
    const handle = createRef<TreeRegistryHandle>();
    const { rerender } = render(<Harness handle={handle} />);

    act(() => handle.current!.focusRow('shoot'));
    expect(row('Побег')).toHaveFocus();

    act(() => handle.current!.focusRow('leaf-2'));
    expect(screen.queryByText('Лист 2')).not.toBeInTheDocument();
    rerender(<Harness handle={handle} initiallyExpanded={['root']} />);
    await userEvent.click(screen.getByRole('button', { name: 'Развернуть «Ветка»' }));

    expect(row('Лист 2')).toHaveFocus();
  });
});

describe('mod-002 design § 11.4: the tree registry’s loading and error states', () => {
  it('loads as the registry skeleton with the real headers', () => {
    render(
      <TreeRegistry
        aria-label="Иерархия"
        firstColumn="Узел"
        columns={[{ header: 'Свойство', render: () => null }]}
        nodes={[]}
        actions={() => []}
        selectedId={null}
        onSelect={() => {}}
        expanded={new Set()}
        onToggle={() => {}}
        loading
      />,
    );

    const status = screen.getByRole('status', { name: 'Загрузка данных' });
    expect(within(status).getByText('Узел')).toBeInTheDocument();
    expect(within(status).getByText('Свойство')).toBeInTheDocument();
    expect(screen.queryByRole('treegrid')).not.toBeInTheDocument();
  });

  it('fails as the error banner with «Повторить»', async () => {
    const onRetry = vi.fn();
    render(
      <TreeRegistry
        aria-label="Иерархия"
        firstColumn="Узел"
        columns={[]}
        nodes={[]}
        actions={() => []}
        selectedId={null}
        onSelect={() => {}}
        expanded={new Set()}
        onToggle={() => {}}
        error={new Error('x')}
        onRetry={onRetry}
      />,
    );

    await userEvent.click(screen.getByRole('button', { name: 'Повторить' }));

    expect(onRetry).toHaveBeenCalledOnce();
    expect(screen.queryByRole('treegrid')).not.toBeInTheDocument();
  });
});
