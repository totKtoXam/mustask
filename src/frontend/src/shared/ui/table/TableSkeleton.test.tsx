import { describe, expect, it } from 'vitest';
import { render, screen } from '@testing-library/react';
import { px, theme } from '../../../test/kitTesting';
import { TableSkeleton } from './TableSkeleton';

// tech-015 design § 5.11, § 5.12 «TableSkeleton»: the real header labels and skeleton rows in
// the same card, one status «Загрузка данных», the table hidden from assistive technology.

const HEADERS = ['Код', 'Название', 'Статус'];

function rows(container: HTMLElement) {
  return Array.from(container.querySelectorAll('tbody tr')) as HTMLElement[];
}

describe('tech-015 Scenario: TableSkeleton (design § 5.11, § 5.12)', () => {
  it('is one status named «Загрузка данных»', () => {
    render(<TableSkeleton headers={HEADERS} />);
    expect(screen.getAllByRole('status')).toHaveLength(1);
    expect(screen.getByRole('status', { name: 'Загрузка данных' })).toBeInTheDocument();
  });

  it('shows the real header labels, so the table’s structure is there at once', () => {
    render(<TableSkeleton headers={HEADERS} />);
    const headers = screen.getAllByRole('columnheader', { hidden: true });
    expect(headers.map((header) => header.textContent)).toEqual(HEADERS);
  });

  it('draws five skeleton rows by default, and as many as asked', () => {
    const { container, unmount } = render(<TableSkeleton headers={HEADERS} />);
    expect(rows(container)).toHaveLength(5);
    unmount();
    const other = render(<TableSkeleton headers={HEADERS} rows={2} />);
    expect(rows(other.container)).toHaveLength(2);
  });

  it('draws a shape in every cell of every row', () => {
    const { container } = render(<TableSkeleton headers={HEADERS} rows={3} />);
    for (const row of rows(container)) {
      expect(row.children).toHaveLength(HEADERS.length);
      for (const cell of Array.from(row.children)) {
        expect(cell.querySelector('.MuiSkeleton-root')).not.toBeNull();
      }
    }
  });

  it('hides the rows — and the table — from assistive technology', () => {
    const { container } = render(<TableSkeleton headers={HEADERS} />);
    for (const row of rows(container)) {
      expect(row.closest('[aria-hidden="true"]')).not.toBeNull();
    }
    expect(screen.queryByRole('table')).toBeNull();
    expect(screen.getByRole('status')).not.toHaveAttribute('aria-hidden');
  });

  it('is in the same card as the loaded table: white, scrolling inside itself', () => {
    const { container } = render(<TableSkeleton headers={HEADERS} />);
    expect(container.querySelector('[data-table-card]')).not.toBeNull();
  });

  it('draws rows of the real row height, so nothing moves when the data arrives', () => {
    const { container } = render(<TableSkeleton headers={HEADERS} />);
    expect(px(getComputedStyle(rows(container)[0] as HTMLElement).height)).toBe(
      theme.density.registryRowHeight,
    );
  });
});
