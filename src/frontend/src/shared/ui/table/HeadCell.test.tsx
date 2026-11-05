import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { px, rgb, rulesFor, theme } from '../../../test/kitTesting';
import { IconAction } from '../actions/IconAction';
import { ThinAddIcon } from '../icons/ThinIcons';
import { HeadCell, type HeadCellProps } from './HeadCell';
import { TableCard } from './TableCard';

// tech-015 design § 5.11 «HeadCell»: a `th scope="col"`, white, small grey labels, 40 px, a rule
// under the row; kinds for the hierarchy and the title; a separator, an ellipsis, a sort.

function renderHead(props: HeadCellProps) {
  return render(
    <TableCard aria-label="Таблица" head={<HeadCell {...props} />}>
      {null}
    </TableCard>,
  );
}

function head() {
  return screen.getAllByRole('columnheader')[0] as HTMLElement;
}

describe('tech-015 Scenario: HeadCell (design § 5.11)', () => {
  describe('Scenario: the reference’s header look', () => {
    it('is a th with scope col, named by its label', () => {
      renderHead({ label: 'Заголовок' });
      const cell = screen.getByRole('columnheader', { name: 'Заголовок' });
      expect(cell.tagName).toBe('TH');
      expect(cell).toHaveAttribute('scope', 'col');
    });

    it('draws the label in caption 12 / 16 / 400, text.secondary, on white', () => {
      renderHead({ label: 'Заголовок' });
      const style = getComputedStyle(head());
      expect(px(style.fontSize)).toBe(12);
      expect(style.fontWeight).toBe('400');
      expect(style.color).toBe(rgb(theme.palette.text.secondary));
      expect(style.backgroundColor).toBe(rgb(theme.palette.background.paper));
    });

    it('is layout.table.headerHeight high with a 1 px divider rule under the row', () => {
      renderHead({ label: 'Заголовок' });
      const style = getComputedStyle(head());
      expect(px(style.height)).toBe(theme.layout.table.headerHeight);
      expect(px(style.borderBottomWidth)).toBe(theme.layout.dividerWidth);
      expect(style.borderBottomColor).toBe(rgb(theme.palette.divider));
    });

    it('is not the legacy grey semibold header', () => {
      renderHead({ label: 'Заголовок' });
      const style = getComputedStyle(head());
      expect(style.backgroundColor).not.toBe(rgb(theme.palette.shell.tableHeader));
      expect(style.fontWeight).not.toBe('600');
    });
  });

  describe('Scenario: kinds', () => {
    it('pads a cell 8 px at each side by default', () => {
      renderHead({ label: 'Статус' });
      expect(px(getComputedStyle(head()).paddingLeft)).toBe(8);
    });

    it('pads the hierarchy kind 16 px at the left, for the two square buttons', () => {
      renderHead({
        kind: 'hierarchy',
        'aria-label': 'Иерархия',
        children: <button type="button">+</button>,
      });
      expect(px(getComputedStyle(head()).paddingLeft)).toBe(16);
    });

    it('insets the title kind’s label by layout.table.titleLabelInset, over the tree’s icons', () => {
      renderHead({ kind: 'title', label: 'Заголовок' });
      expect(px(getComputedStyle(head()).paddingLeft)).toBe(theme.layout.table.titleLabelInset);
      expect(theme.layout.table.titleLabelInset).toBe(36);
    });
  });

  describe('Scenario: the name', () => {
    it('takes an aria-label that replaces the name its content would give — «Иерархия»', () => {
      renderHead({
        kind: 'hierarchy',
        'aria-label': 'Иерархия',
        children: (
          <>
            <IconAction label="Развернуть на один уровень" icon={<ThinAddIcon />} size="header" />
            <IconAction label="Свернуть на один уровень" icon={<ThinAddIcon />} size="header" />
          </>
        ),
      });
      expect(screen.getByRole('columnheader', { name: 'Иерархия' })).toBeInTheDocument();
      expect(
        screen.getByRole('button', { name: 'Развернуть на один уровень' }),
      ).toBeInTheDocument();
    });

    it('takes custom children instead of a label', () => {
      renderHead({ children: <span>Своё содержимое</span> });
      expect(head()).toHaveTextContent('Своё содержимое');
    });
  });

  describe('Scenario: the separator', () => {
    it('draws a 1 px × layout.table.separatorHeight divider line at the right edge, centred', () => {
      renderHead({ label: 'Тип', separator: true });
      expect(head()).toHaveAttribute('data-separated', 'true');
      const rule = rulesFor(head(), '::after').find((candidate) => candidate.declarations.content);
      expect(rule?.declarations.position).toBe('absolute');
      expect(rule?.declarations.right).toBe('0');
      expect(rule?.declarations.top).toBe('50%');
      expect(rule?.declarations.transform).toBe('translateY(-50%)');
      expect(rule?.declarations.height).toBe(`${theme.layout.table.separatorHeight}px`);
    });

    it('writes its width as a length, never a bare 1, which `sx` would read as 100 %', () => {
      renderHead({ label: 'Тип', separator: true });
      const rule = rulesFor(head(), '::after').find((candidate) => candidate.declarations.content);
      expect(rule?.declarations.width).toBe(`${theme.layout.dividerWidth}px`);
      expect(rule?.declarations.width).not.toBe('100%');
    });

    it('draws none by default', () => {
      renderHead({ label: 'Тип' });
      expect(head()).not.toHaveAttribute('data-separated');
      expect(rulesFor(head(), '::after')).toHaveLength(0);
    });
  });

  describe('Scenario: clip', () => {
    it('cuts a label that does not fit to one line, the full label in the native title', () => {
      renderHead({ label: 'Бизнес-ценность', clip: true });
      const text = head().firstElementChild as HTMLElement;
      expect(text).toHaveAttribute('title', 'Бизнес-ценность');
      const style = getComputedStyle(text);
      expect(style.overflow).toBe('hidden');
      expect(style.textOverflow).toBe('ellipsis');
      expect(style.whiteSpace).toBe('nowrap');
      expect(head()).toHaveTextContent('Бизнес-ценность');
    });
  });

  describe('Scenario: size and alignment', () => {
    it('takes a width and a least width', () => {
      renderHead({ label: 'Статус', width: 104, minWidth: 80 });
      const style = getComputedStyle(head());
      expect(px(style.width)).toBe(104);
      expect(px(style.minWidth)).toBe(80);
    });

    it('aligns its content', () => {
      renderHead({ label: 'Сумма', align: 'right' });
      expect(getComputedStyle(head()).textAlign).toBe('right');
    });
  });

  describe('Scenario: sort', () => {
    it('is not sortable without `sort`: no button, no aria-sort', () => {
      renderHead({ label: 'Код' });
      expect(screen.queryByRole('button')).toBeNull();
      expect(head()).not.toHaveAttribute('aria-sort');
    });

    it.each([
      ['asc', 'ascending'],
      ['desc', 'descending'],
    ] as const)('exposes aria-sort %s as %s', (direction, expected) => {
      renderHead({ label: 'Код', sort: { direction, onSort: () => {} } });
      expect(head()).toHaveAttribute('aria-sort', expected);
    });

    it('is a button named by the label, and the first activation is the host’s to make ascending', async () => {
      const user = userEvent.setup();
      const onSort = vi.fn();
      renderHead({ label: 'Код', sort: { direction: false, onSort } });
      expect(head()).not.toHaveAttribute('aria-sort');
      await user.click(screen.getByRole('button', { name: 'Код' }));
      expect(onSort).toHaveBeenCalledOnce();
      // The inactive arrow points up: the direction the first activation will give.
      expect(screen.getByRole('button', { name: 'Код' })).toHaveClass(
        'MuiTableSortLabel-directionAsc',
      );
    });

    it('activates with Enter and Space as any button', async () => {
      const user = userEvent.setup();
      const onSort = vi.fn();
      renderHead({ label: 'Код', sort: { direction: 'asc', onSort } });
      screen.getByRole('button', { name: 'Код' }).focus();
      await user.keyboard('{Enter}');
      await user.keyboard(' ');
      expect(onSort).toHaveBeenCalledTimes(2);
    });
  });

  describe('Scenario: the host’s attributes', () => {
    it('passes data-* and aria-* to the th', () => {
      renderHead({ label: 'Код', 'data-column': 'code', id: 'th-code' });
      expect(head()).toHaveAttribute('data-column', 'code');
      expect(head()).toHaveAttribute('id', 'th-code');
    });
  });
});
