import type { HTMLAttributes, ReactNode, Ref } from 'react';
import Box from '@mui/material/Box';
import TableCell from '@mui/material/TableCell';
import TableSortLabel from '@mui/material/TableSortLabel';
import type { Theme } from '@mui/material/styles';
import type { DataAttributes } from '../passThrough';
import { clipped } from './rowStyles';
import { useTableRole } from './tableContext';

export type HeadCellKind = 'cell' | 'hierarchy' | 'title';

/** A column's sort state: ascending, descending, or not the sorted column. */
export type HeadCellSortDirection = 'asc' | 'desc' | false;

export interface HeadCellSort {
  direction: HeadCellSortDirection;
  /** The first activation sorts ascending; the host sets `direction` and the next reverses. */
  onSort: () => void;
}

/** Every attribute of a `th` the host may drive — and none that styles it. */
export type HeadCellAttributes = Omit<
  HTMLAttributes<HTMLTableCellElement>,
  'className' | 'style' | 'children'
> &
  DataAttributes;

export interface HeadCellProps extends HeadCellAttributes {
  /** The column's name, drawn. */
  label?: string;
  /** Custom content instead of `label` — the hierarchy header's two buttons. */
  children?: ReactNode;
  /**
   * `cell` (default). `hierarchy` pads the cell 16 px at the left, for the two square
   * buttons. `title` insets the label by `layout.table.titleLabelInset`, so it stands
   * over the tree's icons.
   */
  kind?: HeadCellKind;
  width?: number | string;
  minWidth?: number | string;
  align?: 'left' | 'center' | 'right';
  /** A 1 px × `layout.table.separatorHeight` line at the cell's right edge, centred. */
  separator?: boolean;
  /** One line with an ellipsis; the full label is the native `title`. */
  clip?: boolean;
  /** Makes the column sortable: `aria-sort` follows `direction`. */
  sort?: HeadCellSort;
  ref?: Ref<HTMLTableCellElement>;
}

/**
 * A header cell (tech-015 design § 5.11): small grey labels (`caption`, `text.secondary`)
 * on white, one `divider` rule under the row, `layout.table.headerHeight` high, sticky
 * through the table's `stickyHeader`. A `th scope="col"`; in a `TableCard role="treegrid"`
 * a `columnheader`.
 *
 * `aria-label` on the cell replaces the name taken from its content — «Иерархия», whose
 * content is the two buttons that would otherwise be part of it.
 */
export function HeadCell({
  label,
  children,
  kind = 'cell',
  width,
  minWidth,
  align,
  separator = false,
  clip = false,
  sort,
  role,
  ref,
  ...attributes
}: HeadCellProps) {
  const tableRole = useTableRole();
  const content = children ?? label;
  const text = clip ? (
    <Box component="span" title={label} sx={clipped}>
      {content}
    </Box>
  ) : (
    content
  );
  return (
    <TableCell
      ref={ref}
      role={role ?? (tableRole === 'treegrid' ? 'columnheader' : undefined)}
      align={align}
      sortDirection={sort ? sort.direction : undefined}
      data-separated={separator ? 'true' : undefined}
      sx={(theme) => headCellSx(theme, kind, separator, width, minWidth)}
      {...attributes}
    >
      {sort ? (
        <TableSortLabel
          active={sort.direction !== false}
          direction={sort.direction || 'asc'}
          onClick={sort.onSort}
        >
          {text}
        </TableSortLabel>
      ) : (
        text
      )}
    </TableCell>
  );
}

function headCellSx(
  theme: Theme,
  kind: HeadCellKind,
  separator: boolean,
  width: number | string | undefined,
  minWidth: number | string | undefined,
) {
  const { table, dividerWidth } = theme.layout;
  return {
    ...theme.typography.caption,
    color: theme.palette.text.secondary,
    backgroundColor: theme.palette.background.paper,
    borderBottom: `${dividerWidth}px solid ${theme.palette.divider}`,
    height: table.headerHeight,
    padding: theme.spacing(0, 1),
    ...(kind === 'hierarchy' ? { paddingLeft: theme.spacing(2) } : {}),
    ...(kind === 'title' ? { paddingLeft: `${table.titleLabelInset}px` } : {}),
    ...(width !== undefined ? { width } : {}),
    ...(minWidth !== undefined ? { minWidth } : {}),
    ...(separator
      ? {
          // The reference's static thin vertical line: no handle, no drag, no cursor change.
          '&::after': {
            content: '""',
            position: 'absolute' as const,
            right: 0,
            top: '50%',
            transform: 'translateY(-50%)',
            // A length, not the bare number: `sx` reads a `width` of 1 or less as a
            // fraction of the parent, so `1` would draw a bar as wide as the cell.
            width: `${dividerWidth}px`,
            height: table.separatorHeight,
            backgroundColor: theme.palette.divider,
          },
        }
      : {}),
  };
}
