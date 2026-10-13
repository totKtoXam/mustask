import { describe, expect, it, vi } from 'vitest';
import { MemoryRouter } from 'react-router-dom';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { px, theme } from '../../../test/kitTesting';
import { TypeLabel } from '../marks/TypeLabel';
import { TruncatedText } from '../text/TruncatedText';
import { BodyRow } from './BodyRow';
import { RowControl } from './RowControl';
import { TableCard } from './TableCard';
import { TreeCell, type TreeCellProps } from './TreeCell';

// tech-015 design § 5.11 «TreeCell»: the title cell of a tree — the depth's indent, a 24 px
// chevron slot, the type's mark, the title cut to one line, a 24 px slot at the right edge.

function renderTree(props: Partial<TreeCellProps> & { role?: 'table' | 'treegrid' } = {}) {
  const { role = 'table', ...cell } = props;
  return render(
    <MemoryRouter>
      <TableCard aria-label="Бэклог" head={null} role={role}>
        <BodyRow>
          <TreeCell data-testid="cell" {...({ depth: 0, ...cell } as TreeCellProps)}>
            {cell.children ?? 'Письмо со ссылкой'}
          </TreeCell>
        </BodyRow>
      </TableCard>
    </MemoryRouter>,
  );
}

function cell() {
  return screen.getByTestId('cell');
}

/** The cell's one flex row: the indent, the chevron slot, the icon, the title, the spacer, the trailing slot. */
function strip() {
  return cell().firstElementChild as HTMLElement;
}

const EXPANDABLE = {
  expandable: true,
  expanded: false,
  onToggle: () => {},
  toggleLabel: 'Развернуть MSK-EPIC-1',
} as const;

describe('tech-015 Scenario: TreeCell (design § 5.11)', () => {
  describe('Scenario: the indent', () => {
    it.each([0, 1, 2, 3])('indents depth %s by depth × layout.table.treeIndent', (depth) => {
      renderTree({ depth });
      expect(px(getComputedStyle(strip()).paddingLeft)).toBe(depth * theme.layout.table.treeIndent);
    });

    it('is 16 px a level — the reference’s, not the legacy 24', () => {
      expect(theme.layout.table.treeIndent).toBe(16);
    });

    it('is the tree cell’s 4 px padding, the controls and the icon carrying the rest', () => {
      renderTree({ depth: 0 });
      expect(px(getComputedStyle(cell()).paddingLeft)).toBe(4);
    });
  });

  describe('Scenario: the chevron', () => {
    it('is a 24 px thin chevron named by the host, aria-expanded, in the Tab order by default', () => {
      renderTree({ ...EXPANDABLE });
      const chevron = screen.getByRole('button', { name: 'Развернуть MSK-EPIC-1' });
      expect(chevron).toHaveAttribute('aria-expanded', 'false');
      expect(chevron).not.toHaveAttribute('tabindex', '-1');
      expect(px(getComputedStyle(chevron).width)).toBe(theme.layout.iconAction.chevron.box);
      expect(chevron.querySelector('svg')).toHaveAttribute('data-testid', 'ThinChevronRightIcon');
    });

    it('turns down and reads «Свернуть» when expanded, by the host’s label', () => {
      renderTree({ ...EXPANDABLE, expanded: true, toggleLabel: 'Свернуть MSK-EPIC-1' });
      const chevron = screen.getByRole('button', { name: 'Свернуть MSK-EPIC-1' });
      expect(chevron).toHaveAttribute('aria-expanded', 'true');
      expect(chevron.querySelector('svg')).toHaveAttribute('data-testid', 'ThinChevronDownIcon');
    });

    it('calls onToggle on a click', async () => {
      const user = userEvent.setup();
      const onToggle = vi.fn();
      renderTree({ ...EXPANDABLE, onToggle });
      await user.click(screen.getByRole('button', { name: 'Развернуть MSK-EPIC-1' }));
      expect(onToggle).toHaveBeenCalledOnce();
    });

    it('is out of the Tab order for a treegrid, whose row is the stop: toggleTabIndex -1', () => {
      renderTree({ ...EXPANDABLE, toggleTabIndex: -1, role: 'treegrid' });
      expect(screen.getByRole('button', { name: 'Развернуть MSK-EPIC-1' })).toHaveAttribute(
        'tabindex',
        '-1',
      );
    });

    it('has no tooltip: its row names it', async () => {
      const user = userEvent.setup();
      renderTree({ ...EXPANDABLE });
      await user.hover(screen.getByRole('button', { name: 'Развернуть MSK-EPIC-1' }));
      expect(screen.queryByRole('tooltip')).toBeNull();
    });

    it('keeps an empty aria-hidden 24 px box for a leaf, so the icons of one depth line up', () => {
      renderTree({ depth: 1 });
      expect(screen.queryByRole('button')).toBeNull();
      const leaf = cell().querySelector('[data-tree-leaf]') as HTMLElement;
      expect(leaf).toHaveAttribute('aria-hidden', 'true');
      expect(leaf).toBeEmptyDOMElement();
      const style = getComputedStyle(leaf);
      expect([px(style.width), px(style.height)]).toEqual([
        theme.layout.iconAction.chevron.box,
        theme.layout.iconAction.chevron.box,
      ]);
      expect(style.flexShrink).toBe('0');
    });
  });

  describe('Scenario: the icon, the title and the trailing slot', () => {
    it('puts 4 px either side of the icon', () => {
      renderTree({ icon: <TypeLabel appearance="epic" name="Эпик" size="compact" /> });
      const icon = cell().querySelector('[data-tree-icon]') as HTMLElement;
      expect(icon).toHaveTextContent('Эпик');
      const style = getComputedStyle(icon);
      expect([px(style.marginLeft), px(style.marginRight)]).toEqual([4, 4]);
    });

    it('draws no icon slot without an icon', () => {
      renderTree({});
      expect(cell().querySelector('[data-tree-icon]')).toBeNull();
    });

    it('cuts a title text to one line, the full text in the native title', () => {
      renderTree({ children: 'Очень длинный заголовок рабочего элемента' });
      const title = screen.getByText('Очень длинный заголовок рабочего элемента');
      expect(title).toHaveAttribute('title', 'Очень длинный заголовок рабочего элемента');
      const style = getComputedStyle(title);
      expect([style.overflow, style.textOverflow, style.whiteSpace]).toEqual([
        'hidden',
        'ellipsis',
        'nowrap',
      ]);
    });

    it('lets an element title stand as given — a link, a TruncatedText with `to`', () => {
      renderTree({
        children: <TruncatedText to="/items/MSK-US-2">Письмо со ссылкой</TruncatedText>,
      });
      expect(screen.getByRole('link', { name: 'Письмо со ссылкой' })).toHaveAttribute(
        'href',
        '/items/MSK-US-2',
      );
    });

    it('reserves a 24 px slot at the right edge for the trailing control, even when empty', () => {
      const { unmount } = renderTree({});
      const empty = cell().querySelector('[data-tree-trailing]') as HTMLElement;
      const style = getComputedStyle(empty);
      expect([px(style.width), px(style.height)]).toEqual([
        theme.layout.iconAction.row.box,
        theme.layout.iconAction.row.box,
      ]);
      expect(empty).toBeEmptyDOMElement();
      unmount();

      renderTree({
        trailing: <RowControl label="Ещё действия: MSK-US-2" icon={<span />} />,
      });
      const slot = cell().querySelector('[data-tree-trailing]') as HTMLElement;
      expect(slot).toContainElement(screen.getByRole('button', { name: 'Ещё действия: MSK-US-2' }));
    });

    it('puts the title, then a spacer, then the trailing slot, in that order', () => {
      renderTree({ icon: <span data-testid="mark" /> });
      const kinds = Array.from(strip().children).map((child) => {
        const element = child as HTMLElement;
        if (element.hasAttribute('data-tree-leaf')) return 'chevron';
        if (element.hasAttribute('data-tree-icon')) return 'icon';
        if (element.hasAttribute('data-tree-trailing')) return 'trailing';
        return element.textContent === '' ? 'spacer' : 'title';
      });
      expect(kinds).toEqual(['chevron', 'icon', 'title', 'spacer', 'trailing']);
    });
  });

  describe('Scenario: row and cell attributes pass through', () => {
    it('is a gridcell in a treegrid, and takes tabIndex and data-* as any cell', () => {
      renderTree({ role: 'treegrid', tabIndex: -1, 'data-column': 'title' } as never);
      expect(cell()).toHaveAttribute('role', 'gridcell');
      expect(cell()).toHaveAttribute('tabindex', '-1');
      expect(cell()).toHaveAttribute('data-column', 'title');
    });
  });
});
