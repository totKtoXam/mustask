import { describe, expect, it, vi } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import FilterListOffIcon from '@mui/icons-material/FilterListOff';
import { px } from '../../../test/kitTesting';
import { DataTable, type DataColumn, type DataTableProps } from './DataTable';

// tech-015 design § 5.11 «DataTable»: the plain registry built from the parts, owning its
// loading, empty and error states; a plain table has Tab stops, no arrow navigation and no
// `aria-selected`.

interface Language {
  code: string;
  name: string;
}

const ROWS: Language[] = [
  { code: 'ru', name: 'Русский' },
  { code: 'kk', name: 'Қазақша' },
  { code: 'en', name: 'English' },
];

const COLUMNS: DataColumn<Language>[] = [
  { key: 'code', header: 'Код', render: (row) => row.code },
  { key: 'name', header: 'Название', render: (row) => row.name },
];

function renderTable(props: Partial<DataTableProps<Language>> = {}) {
  return render(
    <DataTable
      aria-label="Реестр языков"
      columns={COLUMNS}
      rows={ROWS}
      rowKey={(row) => row.code}
      {...props}
    />,
  );
}

describe('tech-015 Scenario: DataTable (design § 5.11)', () => {
  describe('Scenario: the rows', () => {
    it('is a table named by its aria-label, a header per column and a row per record', () => {
      renderTable();
      const table = screen.getByRole('table', { name: 'Реестр языков' });
      expect(
        within(table)
          .getAllByRole('columnheader')
          .map((header) => header.textContent),
      ).toEqual(['Код', 'Название']);
      expect(within(table).getAllByRole('row')).toHaveLength(1 + ROWS.length);
      expect(within(table).getByRole('cell', { name: 'Қазақша' })).toBeInTheDocument();
    });

    it('renders every column’s cell from its own `render`', () => {
      renderTable({
        columns: [
          ...COLUMNS,
          { key: 'upper', header: 'Прописные', render: (row) => row.code.toUpperCase() },
        ],
      });
      expect(screen.getByRole('cell', { name: 'KK' })).toBeInTheDocument();
    });

    it('is a plain table: its rows are not stops of the Tab order and are never aria-selected', () => {
      renderTable({ selectedKey: 'kk', onSelect: () => {} });
      for (const row of screen.getAllByRole('row').slice(1)) {
        expect(row).not.toHaveAttribute('tabindex');
        expect(row).not.toHaveAttribute('aria-selected');
      }
    });

    it('aligns a column’s header and cells, and clips a column’s text', () => {
      renderTable({
        columns: [
          { key: 'code', header: 'Код', align: 'right', render: (row) => row.code },
          { key: 'name', header: 'Название', clip: true, render: (row) => row.name },
        ],
      });
      expect(getComputedStyle(screen.getByRole('columnheader', { name: 'Код' })).textAlign).toBe(
        'right',
      );
      expect(getComputedStyle(screen.getByRole('cell', { name: 'ru' })).textAlign).toBe('right');
      expect(screen.getByText('Русский')).toHaveAttribute('title', 'Русский');
    });

    it('fixes the layout when a column has a width, and not otherwise', () => {
      const plain = renderTable();
      expect(getComputedStyle(screen.getByRole('table')).tableLayout).not.toBe('fixed');
      plain.unmount();
      renderTable({
        columns: [{ ...COLUMNS[0]!, width: 120 }, COLUMNS[1]!],
      });
      const table = screen.getByRole('table');
      expect(getComputedStyle(table).tableLayout).toBe('fixed');
      expect((table.querySelector('col') as HTMLElement).style.width).toBe('120px');
    });

    it('offers a sortable header its sort', async () => {
      const user = userEvent.setup();
      const onSort = vi.fn();
      renderTable({
        columns: [{ ...COLUMNS[0]!, sort: { direction: 'asc', onSort } }, COLUMNS[1]!],
      });
      expect(screen.getByRole('columnheader', { name: 'Код' })).toHaveAttribute(
        'aria-sort',
        'ascending',
      );
      await user.click(screen.getByRole('button', { name: 'Код' }));
      expect(onSort).toHaveBeenCalledOnce();
    });
  });

  describe('Scenario: selection', () => {
    it('frames the selected row and calls onSelect with the key of the row clicked', async () => {
      const user = userEvent.setup();
      const onSelect = vi.fn();
      renderTable({ selectedKey: 'kk', onSelect });
      const rows = screen.getAllByRole('row');
      expect(rows[2]).toHaveAttribute('data-selected', 'true');
      expect(rows[1]).not.toHaveAttribute('data-selected');
      await user.click(screen.getByRole('cell', { name: 'English' }));
      expect(onSelect).toHaveBeenCalledExactlyOnceWith('en');
    });

    it('selects nothing without onSelect: a click is just a click', async () => {
      const user = userEvent.setup();
      renderTable();
      await user.click(screen.getByRole('cell', { name: 'English' }));
      expect(screen.getAllByRole('row').some((row) => row.hasAttribute('data-selected'))).toBe(
        false,
      );
    });
  });

  describe('Scenario: loading, no rows yet', () => {
    it('is a TableSkeleton with the real column labels, in the card', () => {
      const { container } = renderTable({ rows: [], loading: true });
      expect(screen.getByRole('status', { name: 'Загрузка данных' })).toBeInTheDocument();
      const headers = screen.getAllByRole('columnheader', { hidden: true });
      expect(headers.map((header) => header.textContent)).toEqual(['Код', 'Название']);
      expect(container.querySelector('[data-table-card]')).not.toBeNull();
      expect(screen.queryByRole('table')).toBeNull();
    });

    it('keeps the rows it has while it reloads: loading only matters with no rows', () => {
      renderTable({ loading: true });
      expect(screen.queryByRole('status', { name: 'Загрузка данных' })).toBeNull();
      expect(screen.getByRole('table')).toBeInTheDocument();
    });
  });

  describe('Scenario: empty', () => {
    it('replaces the card with the empty frame — an h2, the host’s words', () => {
      renderTable({
        rows: [],
        empty: {
          icon: FilterListOffIcon,
          heading: 'Языки не найдены',
          hint: 'Измените условия поиска.',
        },
      });
      expect(
        screen.getByRole('heading', { level: 2, name: 'Языки не найдены' }),
      ).toBeInTheDocument();
      expect(screen.getByText('Измените условия поиска.')).toBeInTheDocument();
      expect(screen.queryByRole('table')).toBeNull();
    });

    it('draws the empty table when the host gives no frame', () => {
      renderTable({ rows: [] });
      expect(screen.getByRole('table')).toBeInTheDocument();
      expect(screen.getAllByRole('row')).toHaveLength(1);
    });
  });

  describe('Scenario: a read error', () => {
    it('with no rows replaces the card with the error text and «Повторить»', async () => {
      const user = userEvent.setup();
      const onRetry = vi.fn();
      renderTable({ rows: [], error: 'Не удалось загрузить языки.', onRetry });
      const notice = screen.getByRole('alert');
      expect(notice).toHaveTextContent('Не удалось загрузить языки.');
      expect(screen.queryByRole('table')).toBeNull();
      await user.click(within(notice).getByRole('button', { name: 'Повторить' }));
      expect(onRetry).toHaveBeenCalledOnce();
    });

    it('has no retry without onRetry', () => {
      renderTable({ rows: [], error: 'Не удалось загрузить языки.' });
      expect(screen.queryByRole('button', { name: 'Повторить' })).toBeNull();
    });

    it('with rows shown keeps the table — rows, selection — and stands the error above the card', () => {
      const { container } = renderTable({
        selectedKey: 'kk',
        error: 'Не удалось обновить список.',
        onRetry: () => {},
      });
      const notice = screen.getByRole('alert');
      const card = container.querySelector('[data-table-card]') as HTMLElement;
      expect(notice).toHaveTextContent('Не удалось обновить список.');
      expect(screen.getAllByRole('row')).toHaveLength(1 + ROWS.length);
      expect(screen.getAllByRole('row')[2]).toHaveAttribute('data-selected', 'true');
      expect(notice.compareDocumentPosition(card) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
      expect(card).not.toContainElement(notice);
    });

    it('spaces the notice from the card by the stack, never by a margin of the notice’s own', () => {
      const { container } = renderTable({ error: 'Не удалось обновить список.' });
      const card = container.querySelector('[data-table-card]') as HTMLElement;
      expect(px(getComputedStyle(card).marginTop)).toBe(16);
      expect(parseFloat(getComputedStyle(screen.getByRole('alert')).marginTop || '0')).toBe(0);
      expect(parseFloat(getComputedStyle(screen.getByRole('alert')).marginBottom || '0')).toBe(0);
    });

    it('removes the error once the retry has worked: no error, the table alone', () => {
      const { rerender } = renderTable({ error: 'Не удалось обновить список.', onRetry: () => {} });
      expect(screen.getByRole('alert')).toBeInTheDocument();
      rerender(
        <DataTable
          aria-label="Реестр языков"
          columns={COLUMNS}
          rows={ROWS}
          rowKey={(row) => row.code}
        />,
      );
      expect(screen.queryByRole('alert')).toBeNull();
      expect(screen.getByRole('table')).toBeInTheDocument();
    });
  });

  describe('Scenario: the pager', () => {
    it('draws the pager in the card’s foot, and calls onPageChange with the page', async () => {
      const user = userEvent.setup();
      const onPageChange = vi.fn();
      const { container } = renderTable({
        pager: { page: 0, rowsPerPage: 10, count: 25, onPageChange },
      });
      const card = container.querySelector('[data-table-card]') as HTMLElement;
      expect(within(card).getByText('1–10 из 25')).toBeInTheDocument();
      await user.click(within(card).getByRole('button', { name: /следующ/i }));
      expect(onPageChange).toHaveBeenCalledExactlyOnceWith(1);
    });

    it('draws no pager without one', () => {
      renderTable();
      expect(screen.queryByText(/из \d+/)).toBeNull();
    });
  });

  describe('Scenario: scrolling inside the card', () => {
    it('passes the offset and the floor to the card', () => {
      const { container } = renderTable({ maxHeightOffset: 200, minHeight: 300 });
      const style = getComputedStyle(container.querySelector('[data-table-card]') as HTMLElement);
      expect(style.maxHeight).toBe('calc(100dvh - 200px)');
      expect(px(style.minHeight)).toBe(300);
    });
  });
});
