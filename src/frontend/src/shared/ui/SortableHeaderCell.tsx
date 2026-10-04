import TableCell from '@mui/material/TableCell';
import TableSortLabel from '@mui/material/TableSortLabel';

/** A column's sort state: ascending, descending, or not the sorted column. */
export type SortDirection = 'asc' | 'desc' | false;

export interface SortableHeaderCellProps {
  label: string;
  direction: SortDirection;
  onSort: () => void;
}

/**
 * A sortable column header (US-8, design § 8.2). MUI's `TableCell` turns
 * `sortDirection` into `aria-sort`, so the direction is announced; no
 * visually hidden "sorted" text is added, and the header's accessible name
 * stays its label (plan § 9, D-14).
 */
export function SortableHeaderCell({ label, direction, onSort }: SortableHeaderCellProps) {
  return (
    <TableCell sortDirection={direction}>
      <TableSortLabel active={direction !== false} direction={direction || 'asc'} onClick={onSort}>
        {label}
      </TableSortLabel>
    </TableCell>
  );
}
