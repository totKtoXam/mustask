import type { Theme } from '@mui/material/styles';
import TablePagination from '@mui/material/TablePagination';

export interface TablePagerProps {
  /** 0-based, as MUI counts; a route's own page is usually 1-based. */
  page: number;
  rowsPerPage: number;
  /** The total number of rows, not the page's. */
  count: number;
  /** The value, not the event. */
  onPageChange: (page: number) => void;
  /**
   * Both arrows are inert, e.g. while the next page loads. They stay in the Tab order, marked
   * `aria-disabled`, so keyboard focus is not dropped to the page when a click starts the load.
   */
  disabled?: boolean;
}

/**
 * The foot of a table (tech-015 design § 5.11): «1–10 из 25» and the two arrows, from
 * MUI's `ruRU` locale, with a `divider` rule above. No rows-per-page choice — a page has
 * one size.
 */
export function TablePager({
  page,
  rowsPerPage,
  count,
  onPageChange,
  disabled = false,
}: TablePagerProps) {
  const arrow = disabled
    ? {
        'aria-disabled': true,
        sx: (theme: Theme) => ({
          color: theme.palette.action.disabled,
          cursor: 'default',
          '&:hover': { backgroundColor: 'transparent' },
        }),
      }
    : {};
  return (
    <TablePagination
      component="div"
      count={count}
      page={page}
      rowsPerPage={rowsPerPage}
      rowsPerPageOptions={[]}
      onPageChange={(_event, next) => {
        if (!disabled) {
          onPageChange(next);
        }
      }}
      slotProps={{ actions: { previousButton: arrow, nextButton: arrow } }}
      sx={(theme) => ({
        borderTop: `${theme.layout.dividerWidth}px solid ${theme.palette.divider}`,
      })}
    />
  );
}
