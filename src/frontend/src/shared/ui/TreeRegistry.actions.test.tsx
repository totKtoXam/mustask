import { describe, expect, it } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import AddIcon from '@mui/icons-material/Add';
import {
  TreeRegistry,
  type TreeRegistryAction,
  type TreeRegistryNode,
  type TreeRegistryProps,
} from './TreeRegistry';

// mod-016 DC-2 (design § 6.2, § 12): the actions column — its header cell,
// its cells and the «Действия» buttons — is drawn only when some node has an
// action, so a user with no write code gets no column of dead buttons.

const NODES: TreeRegistryNode[] = [
  { id: 'root', parentId: null, label: 'Корень' },
  { id: 'leaf', parentId: 'root', label: 'Лист' },
];

const ADD: TreeRegistryAction = { label: 'Добавить', icon: <AddIcon />, onClick: () => {} };

function renderTree(props: Partial<TreeRegistryProps<TreeRegistryNode>> = {}) {
  return render(
    <TreeRegistry
      aria-label="Иерархия"
      firstColumn="Узел"
      columns={[{ header: 'Свойство', render: () => 'значение' }]}
      nodes={NODES}
      actions={() => []}
      selectedId={null}
      onSelect={() => {}}
      expanded={new Set(['root'])}
      onToggle={() => {}}
      {...props}
    />,
  );
}

describe('mod-016 DC-2 Scenario: the tree draws no actions column when no node has an action', () => {
  it('has no «Действия» button, no blank header cell and no extra cell per row', () => {
    renderTree();

    expect(screen.queryByRole('button', { name: /^Действия: / })).not.toBeInTheDocument();
    expect(screen.getAllByRole('columnheader').map((cell) => cell.textContent)).toEqual([
      'Узел',
      'Свойство',
    ]);
    for (const row of screen.getAllByRole('row').slice(1)) {
      expect(within(row).getAllByRole('gridcell')).toHaveLength(2);
    }
    expect(screen.getByRole('treegrid')).toHaveAccessibleName('Иерархия');
  });

  it('stays a treegrid whose rows expand, collapse and select with the keyboard', async () => {
    renderTree({ expanded: new Set() });

    const row = screen.getByRole('row', { name: /Корень/ });
    row.focus();
    await userEvent.keyboard('{ContextMenu}');
    await userEvent.keyboard('{Shift>}{F10}{/Shift}');

    expect(screen.queryByRole('menu')).not.toBeInTheDocument();
    expect(row).toHaveFocus();
  });

  it('draws the column once any node has an action, with a button on the rows that have one only', () => {
    renderTree({ actions: (node) => (node.id === 'root' ? [ADD] : []) });

    expect(screen.getAllByRole('columnheader')).toHaveLength(3);
    expect(screen.getAllByRole('button', { name: /^Действия: / })).toHaveLength(1);
  });

  it('draws it when showActions says so, with no action to show yet', () => {
    renderTree({ showActions: true });

    expect(screen.getAllByRole('columnheader')).toHaveLength(3);
  });

  it('leaves it out when showActions is false, whatever the nodes carry', () => {
    renderTree({ showActions: false, actions: () => [ADD] });

    expect(screen.queryByRole('button', { name: /^Действия: / })).not.toBeInTheDocument();
    expect(screen.getAllByRole('columnheader')).toHaveLength(2);
  });
});

describe('mod-016 DC-2 Scenario: the loading skeleton keeps the blank column unless told otherwise', () => {
  it('keeps the blank column by default, as an administrator’s skeleton has it', () => {
    renderTree({ loading: true, nodes: [] });

    const status = screen.getByRole('status', { name: 'Загрузка данных' });
    expect(within(status).getAllByRole('columnheader')).toHaveLength(3);
  });

  it('drops it with showActions false, as a reader’s skeleton has none', () => {
    renderTree({ loading: true, nodes: [], showActions: false });

    const status = screen.getByRole('status', { name: 'Загрузка данных' });
    expect(within(status).getAllByRole('columnheader')).toHaveLength(2);
  });
});
