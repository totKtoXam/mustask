import { createRef } from 'react';
import { describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen } from '@testing-library/react';
import { px, rgb, theme } from '../../../test/kitTesting';
import { BodyRow } from './BodyRow';
import { Cell, type CellProps } from './Cell';
import { TableCard } from './TableCard';

// tech-015 design § 5.11 «Cell»: a `td` in the table body's type (13 / 20), three paddings, an
// optional ellipsis; role and attributes pass through (a treegrid gives it `gridcell`).

function renderCell(props: CellProps, role: 'table' | 'treegrid' = 'table') {
  return render(
    <TableCard aria-label="Таблица" head={null} role={role}>
      <BodyRow>
        <Cell data-testid="cell" {...props} />
      </BodyRow>
    </TableCard>,
  );
}

function cell() {
  return screen.getByTestId('cell');
}

describe('tech-015 Scenario: Cell (design § 5.11)', () => {
  describe('Scenario: the body’s type', () => {
    it('is typography.tableCell 13 / 20 in the neutral text colour, with no line under it', () => {
      renderCell({ children: 'Русский' });
      const style = getComputedStyle(cell());
      expect(cell().tagName).toBe('TD');
      expect(px(style.fontSize)).toBe(13);
      expect(Math.round(Number(style.lineHeight) * px(style.fontSize))).toBe(20);
      expect(style.fontWeight).toBe('400');
      expect(style.color).toBe(rgb(theme.palette.text.primary));
      expect(style.borderBottomStyle).not.toBe('solid');
    });
  });

  describe('Scenario: the paddings', () => {
    it('is 8 px at each side by default', () => {
      renderCell({ children: 'x' });
      const style = getComputedStyle(cell());
      expect([px(style.paddingLeft), px(style.paddingRight)]).toEqual([8, 8]);
    });

    it('is 4 px at each side for the tree’s title cell, whose controls and icon carry the rest', () => {
      renderCell({ padding: 'tree', children: 'x' });
      const style = getComputedStyle(cell());
      expect([px(style.paddingLeft), px(style.paddingRight)]).toEqual([4, 4]);
    });

    it('is 12 px at the left and positioned for the hierarchy cell — the drag grip’s containing block', () => {
      renderCell({ padding: 'hierarchy', children: 'x' });
      const style = getComputedStyle(cell());
      expect(px(style.paddingLeft)).toBe(12);
      expect(px(style.paddingRight)).toBe(8);
      expect(style.position).toBe('relative');
    });
  });

  describe('Scenario: clip', () => {
    it('cuts a text to one line with an ellipsis and keeps the full text in the native title', () => {
      renderCell({ clip: true, children: 'Пользовательская история' });
      const text = cell().firstElementChild as HTMLElement;
      expect(text).toHaveAttribute('title', 'Пользовательская история');
      const style = getComputedStyle(text);
      expect([style.overflow, style.textOverflow, style.whiteSpace]).toEqual([
        'hidden',
        'ellipsis',
        'nowrap',
      ]);
      expect(cell()).toHaveTextContent('Пользовательская история');
    });

    it('sets no title for content that is not a text', () => {
      renderCell({ clip: true, children: <b>Жирный</b> });
      expect((cell().firstElementChild as HTMLElement).getAttribute('title')).toBeNull();
    });

    it('draws the content as it is without it', () => {
      renderCell({ children: 'Русский' });
      expect(cell().firstElementChild).toBeNull();
    });
  });

  describe('Scenario: alignment', () => {
    it('aligns its content', () => {
      renderCell({ align: 'right', children: '20' });
      expect(getComputedStyle(cell()).textAlign).toBe('right');
    });
  });

  describe('Scenario: roles and attributes', () => {
    it('is a plain cell in a plain table', () => {
      renderCell({ children: 'x' });
      expect(cell()).not.toHaveAttribute('role');
    });

    it('is a gridcell in a treegrid', () => {
      renderCell({ children: 'x' }, 'treegrid');
      expect(cell()).toHaveAttribute('role', 'gridcell');
    });

    it('lets the host’s own role win', () => {
      renderCell({ role: 'rowheader', children: 'x' }, 'treegrid');
      expect(cell()).toHaveAttribute('role', 'rowheader');
    });

    it('passes tabIndex, aria-*, data-* and the handlers through to the td', () => {
      const onKeyDown = vi.fn();
      renderCell({
        tabIndex: -1,
        'aria-colspan': 2,
        'data-column': 'name',
        onKeyDown,
        children: 'x',
      });
      expect(cell()).toHaveAttribute('tabindex', '-1');
      expect(cell()).toHaveAttribute('aria-colspan', '2');
      expect(cell()).toHaveAttribute('data-column', 'name');
      fireEvent.keyDown(cell(), { key: 'Enter' });
      expect(onKeyDown).toHaveBeenCalledOnce();
    });

    it('hands its ref to the td', () => {
      const ref = createRef<HTMLTableCellElement>();
      renderCell({ ref, children: 'x' });
      expect(ref.current).toBe(cell());
    });
  });
});
