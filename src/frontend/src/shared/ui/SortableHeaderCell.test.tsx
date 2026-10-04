import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import Table from '@mui/material/Table';
import TableHead from '@mui/material/TableHead';
import TableRow from '@mui/material/TableRow';
import { SortableHeaderCell, type SortDirection } from './SortableHeaderCell';

function renderHeader(direction: SortDirection, onSort = vi.fn()) {
  render(
    <Table>
      <TableHead>
        <TableRow>
          <SortableHeaderCell label="Код" direction={direction} onSort={onSort} />
        </TableRow>
      </TableHead>
    </Table>,
  );
  return onSort;
}

describe('US-8 Scenario: Sorting is visible', () => {
  it('carries no aria-sort while the column is not sorted', () => {
    renderHeader(false);

    expect(screen.getByRole('columnheader', { name: 'Код' })).not.toHaveAttribute('aria-sort');
  });

  it('announces ascending, then descending, and keeps the label as the header’s name', () => {
    renderHeader('asc');
    expect(screen.getByRole('columnheader', { name: 'Код' })).toHaveAttribute(
      'aria-sort',
      'ascending',
    );
  });

  it('announces descending', () => {
    renderHeader('desc');
    expect(screen.getByRole('columnheader', { name: 'Код' })).toHaveAttribute(
      'aria-sort',
      'descending',
    );
  });

  it('asks for a sort when activated', async () => {
    const onSort = renderHeader(false);

    await userEvent.click(screen.getByRole('button', { name: 'Код' }));

    expect(onSort).toHaveBeenCalledTimes(1);
  });
});
