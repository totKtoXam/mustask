import { createRef } from 'react';
import { describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen, within } from '@testing-library/react';
import { px, rgb, theme } from '../../../test/kitTesting';
import { BodyRow } from './BodyRow';
import { Cell } from './Cell';
import { HeadCell } from './HeadCell';
import { TableCard, type TableCardProps } from './TableCard';

// tech-015 design § 5.11 «TableCard»: the part that owns the `<table>` — a white card with the
// soft glow that scrolls inside itself, the one header row, the body, a foot below; a treegrid
// gives the parts their explicit roles.

function renderCard(props: Partial<TableCardProps> = {}) {
  return render(
    <TableCard
      aria-label="Бэклог"
      head={
        <>
          <HeadCell label="Код" />
          <HeadCell label="Название" />
        </>
      }
      {...props}
    >
      <BodyRow>
        <Cell>ru</Cell>
        <Cell>Русский</Cell>
      </BodyRow>
      <BodyRow>
        <Cell>kk</Cell>
        <Cell>Қазақша</Cell>
      </BodyRow>
    </TableCard>,
  );
}

function card(container: HTMLElement) {
  return container.querySelector('[data-table-card]') as HTMLElement;
}

describe('tech-015 Scenario: TableCard (design § 5.11)', () => {
  describe('Scenario: the structure it renders', () => {
    it('is a table named by its aria-label: table > thead > tr > the head, table > tbody > the rows', () => {
      const { container } = renderCard();
      const table = screen.getByRole('table', { name: 'Бэклог' });
      expect(table.tagName).toBe('TABLE');
      expect(table.querySelector('thead > tr')).toHaveTextContent('КодНазвание');
      expect(
        within(table.querySelector('thead') as HTMLElement).getAllByRole('columnheader'),
      ).toHaveLength(2);
      const body = table.querySelector('tbody') as HTMLElement;
      expect(body.children).toHaveLength(2);
      expect(card(container)).toContainElement(table);
      expect(screen.getAllByRole('row')).toHaveLength(3);
    });

    it('draws the foot below the table, inside the card', () => {
      const { container } = renderCard({ foot: <p>Подвал таблицы</p> });
      const table = screen.getByRole('table');
      const foot = screen.getByText('Подвал таблицы');
      expect(card(container)).toContainElement(foot);
      expect(table.compareDocumentPosition(foot) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
      expect(table).not.toContainElement(foot);
    });

    it('draws no colgroup unless it is given columns', () => {
      renderCard();
      expect(screen.getByRole('table').querySelector('colgroup')).toBeNull();
      expect(getComputedStyle(screen.getByRole('table')).tableLayout).not.toBe('fixed');
    });
  });

  describe('Scenario: the card is a white surface with the soft glow that scrolls inside itself', () => {
    it('is the paper colour with theme.cardShadow and the theme’s corner', () => {
      const { container } = renderCard();
      const style = getComputedStyle(card(container));
      expect(style.backgroundColor).toBe(rgb(theme.palette.background.paper));
      expect(style.boxShadow.replace(/\s+/g, '')).toBe(theme.cardShadow.replace(/\s+/g, ''));
      expect(style.borderRadius).toBe(`${theme.shape.borderRadius}px`);
    });

    it('is the scroll container and the containing block of the rows’ hidden descriptions', () => {
      const { container } = renderCard();
      const style = getComputedStyle(card(container));
      expect(style.overflow).toBe('auto');
      expect(style.position).toBe('relative');
    });

    it('hands its ref to the scroll container', () => {
      const ref = createRef<HTMLDivElement>();
      const { container } = renderCard({ ref });
      expect(ref.current).toBe(card(container));
    });

    it('keeps a focused row clear of the sticky header: scroll-padding-top is the header’s height', () => {
      const { container } = renderCard();
      expect(px(getComputedStyle(card(container)).scrollPaddingTop)).toBe(
        theme.layout.table.headerHeight,
      );
      expect(theme.layout.table.headerHeight).toBe(40);
    });

    it('makes the header cells sticky', () => {
      renderCard();
      for (const cell of screen.getAllByRole('columnheader')) {
        expect(cell).toHaveClass('MuiTableCell-stickyHeader');
      }
    });
  });

  describe('Scenario: the offset and the floor', () => {
    it('is as high as the window less the offset, and never under layout.table.minHeight', () => {
      const { container } = renderCard({ maxHeightOffset: 204 });
      const style = getComputedStyle(card(container));
      expect(style.maxHeight).toBe('calc(100dvh - 204px)');
      expect(px(style.minHeight)).toBe(theme.layout.table.minHeight);
      expect(theme.layout.table.minHeight).toBe(240);
    });

    it('takes a floor of the host’s with the offset', () => {
      const { container } = renderCard({ maxHeightOffset: 204, minHeight: 320 });
      expect(px(getComputedStyle(card(container)).minHeight)).toBe(320);
    });

    it('is as tall as its rows without an offset: no max height, no floor to stretch a short table', () => {
      const { container } = renderCard();
      const style = getComputedStyle(card(container));
      expect(style.maxHeight).toBe('');
      expect(style.minHeight).toBe('');
    });
  });

  describe('Scenario: fixed columns', () => {
    it('has a colgroup of the given widths, a fixed layout, the full width and the least width', () => {
      renderCard({ columns: [{ width: 72 }, {}], minWidth: 800 });
      const table = screen.getByRole('table');
      const cols = Array.from(table.querySelectorAll('colgroup > col')) as HTMLElement[];
      expect(cols).toHaveLength(2);
      expect(cols[0]?.style.width).toBe('72px');
      expect(cols[1]?.style.width).toBe('');
      const style = getComputedStyle(table);
      expect(style.tableLayout).toBe('fixed');
      expect(style.width).toBe('100%');
      expect(px(style.minWidth)).toBe(800);
    });
  });

  describe('Scenario: the body’s own attributes and handlers', () => {
    it('hands bodyProps to the tbody — the backlog’s drag events', () => {
      const onDrop = vi.fn();
      const onDragOver = vi.fn();
      renderCard({ bodyProps: { onDrop, onDragOver, 'data-drop-zone': 'rows' } });
      const body = screen.getByRole('table').querySelector('tbody') as HTMLElement;
      expect(body).toHaveAttribute('data-drop-zone', 'rows');
      fireEvent.dragOver(body);
      fireEvent.drop(body);
      expect(onDragOver).toHaveBeenCalledOnce();
      expect(onDrop).toHaveBeenCalledOnce();
    });
  });

  describe('Scenario: role="treegrid" gives the parts their WAI-ARIA roles', () => {
    it('makes the table a treegrid, the header a row of columnheaders and the cells gridcells', () => {
      renderCard({ role: 'treegrid' });
      const grid = screen.getByRole('treegrid', { name: 'Бэклог' });
      expect(screen.queryByRole('table')).toBeNull();
      const [headerRow] = within(grid).getAllByRole('row');
      expect(headerRow?.parentElement?.tagName).toBe('THEAD');
      expect(within(headerRow as HTMLElement).getAllByRole('columnheader')).toHaveLength(2);
      expect(within(grid).getAllByRole('gridcell')).toHaveLength(4);
    });

    it('leaves a plain table’s cells and headers to their implicit roles', () => {
      renderCard();
      expect(screen.queryByRole('treegrid')).toBeNull();
      expect(screen.queryAllByRole('gridcell')).toHaveLength(0);
      expect(screen.getAllByRole('cell')).toHaveLength(4);
      expect(screen.getByRole('table').querySelector('thead tr')).not.toHaveAttribute('role');
    });
  });
});
