import TablePagination from '@mui/material/TablePagination';

export interface TablePagerProps {
  /** 0-based, as MUI counts; a route's own page is usually 1-based. */
  page: number;
  rowsPerPage: number;
  /** The total number of rows, not the page's. */
  count: number;
  /** The value, not the event. */
  onPageChange: (page: number) => void;
}

/**
 * The foot of a table (tech-015 design § 5.11): «1–10 из 25» and the two arrows, from
 * MUI's `ruRU` locale, with a `divider` rule above. No rows-per-page choice — a page has
 * one size.
 */
export function TablePager({ page, rowsPerPage, count, onPageChange }: TablePagerProps) {
  return (
    <TablePagination
      component="div"
      count={count}
      page={page}
      rowsPerPage={rowsPerPage}
      rowsPerPageOptions={[]}
      onPageChange={(_event, next) => onPageChange(next)}
      sx={(theme) => ({
        borderTop: `${theme.layout.dividerWidth}px solid ${theme.palette.divider}`,
      })}
    />
  );
}
