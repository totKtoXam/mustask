import type { HTMLAttributes, ReactNode, Ref } from 'react';
import Paper from '@mui/material/Paper';
import Table from '@mui/material/Table';
import TableBody from '@mui/material/TableBody';
import TableHead from '@mui/material/TableHead';
import TableRow from '@mui/material/TableRow';
import { surfaceSx } from '../layout/surfaceStyles';
import type { DataAttributes } from '../passThrough';
import { TableContext } from './tableContext';

export interface TableColumn {
  /** A px number or a CSS size; absent: the column takes the rest. */
  width?: number | string;
}

/** The `tbody`'s own attributes and handlers — the backlog's drag events — and none that styles it. */
export type TableBodyAttributes = Omit<
  HTMLAttributes<HTMLTableSectionElement>,
  'className' | 'style' | 'children'
> &
  DataAttributes;

export interface TableCardProps {
  /** The table's name — «Бэклог», «Реестр языков». */
  'aria-label': string;
  /** `treegrid` gives the table, its header row, the head cells and the cells explicit WAI-ARIA roles. Default `table`. */
  role?: 'table' | 'treegrid';
  /** The `HeadCell`s of the one header row. */
  head: ReactNode;
  /** The `BodyRow`s. */
  children: ReactNode;
  /** Given, the table has a `colgroup` of these widths and a fixed layout. */
  columns?: readonly TableColumn[];
  /** With `columns`: the table's least width, so a narrow window scrolls it sideways. */
  minWidth?: number;
  bodyProps?: TableBodyAttributes;
  /** Below the table, in the card: a `TablePager`. */
  foot?: ReactNode;
  /**
   * Makes the card scroll inside itself: its height is the window's less this offset (the
   * page's chrome above and below it) and the header is sticky. Never under `minHeight`.
   */
  maxHeightOffset?: number;
  /** With `maxHeightOffset`: the least height. Default `layout.table.minHeight`. */
  minHeight?: number;
  /** The scroll container — the card itself. */
  ref?: Ref<HTMLDivElement>;
}

/**
 * The part that owns the `<table>` (tech-015 design § 5.11): a white card with the soft
 * glow that scrolls inside itself, holding the table, its `colgroup`, the one header row
 * and the body, and a foot below. No feature writes `Table`, `TableHead` or `TableBody`.
 *
 * The header is white with small grey labels and sticky; a sticky header needs
 * `maxHeightOffset`, since the card is what scrolls. `scroll-padding-top` is the header's
 * height, so a row the browser scrolls to stays clear of it.
 */
export function TableCard({
  'aria-label': ariaLabel,
  role = 'table',
  head,
  children,
  columns,
  minWidth,
  bodyProps,
  foot,
  maxHeightOffset,
  minHeight,
  ref,
}: TableCardProps) {
  return (
    <TableContext.Provider value={{ role }}>
      <Paper
        ref={ref}
        elevation={0}
        data-table-card=""
        sx={(theme) => ({
          ...surfaceSx(theme, 'card'),
          overflow: 'auto',
          // The card is the containing block of the rows' visually hidden descriptions
          // (`position: absolute`): without it they sit at their place in the page and
          // stretch the document by the table's whole height.
          position: 'relative',
          ...(maxHeightOffset === undefined
            ? minHeight === undefined
              ? {}
              : { minHeight }
            : {
                maxHeight: `calc(100dvh - ${maxHeightOffset}px)`,
                minHeight: minHeight ?? theme.layout.table.minHeight,
              }),
          // The sticky header covers the top of the scrollport.
          scrollPaddingTop: theme.layout.table.headerHeight,
        })}
      >
        <Table
          size="small"
          stickyHeader
          role={role}
          aria-label={ariaLabel}
          sx={{
            width: '100%',
            ...(columns ? { tableLayout: 'fixed', minWidth } : {}),
          }}
        >
          {columns && (
            <colgroup>
              {columns.map((column, index) => (
                <col
                  key={index}
                  style={column.width === undefined ? undefined : { width: column.width }}
                />
              ))}
            </colgroup>
          )}
          <TableHead>
            <TableRow role={role === 'treegrid' ? 'row' : undefined}>{head}</TableRow>
          </TableHead>
          <TableBody {...bodyProps}>{children}</TableBody>
        </Table>
        {foot}
      </Paper>
    </TableContext.Provider>
  );
}
