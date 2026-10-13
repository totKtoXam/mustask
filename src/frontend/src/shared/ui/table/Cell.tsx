import type { HTMLAttributes, ReactNode, Ref } from 'react';
import Box from '@mui/material/Box';
import TableCell from '@mui/material/TableCell';
import type { Theme } from '@mui/material/styles';
import type { DataAttributes } from '../passThrough';
import { clipped } from './rowStyles';
import { useTableRole } from './tableContext';

export type CellPadding = 'cell' | 'tree' | 'hierarchy';

/** Every attribute of a `td` the host may drive — and none that styles it. */
export type CellAttributes = Omit<
  HTMLAttributes<HTMLTableCellElement>,
  'className' | 'style' | 'children'
> &
  DataAttributes;

export interface CellProps extends CellAttributes {
  /**
   * `cell` (default): 8 px at each side. `tree`: 4 — the title cell, whose controls and
   * icon carry the rest. `hierarchy`: 12 at the left and the positioning context of the
   * drag grip.
   */
  padding?: CellPadding;
  /** One line with an ellipsis; a text child is also the native `title`. */
  clip?: boolean;
  align?: 'left' | 'center' | 'right';
  children?: ReactNode;
  ref?: Ref<HTMLTableCellElement>;
}

/**
 * A body cell (tech-015 design § 5.11): `typography.tableCell` (13 / 20), the neutral
 * text colour, no line under it, centred vertically. In a `TableCard role="treegrid"` it
 * is a `gridcell`; `role`, `tabIndex`, `aria-*`, `data-*` and the handlers pass through.
 */
export function Cell({
  padding = 'cell',
  clip = false,
  align,
  role,
  children,
  ref,
  ...attributes
}: CellProps) {
  const tableRole = useTableRole();
  return (
    <TableCell
      ref={ref}
      role={role ?? (tableRole === 'treegrid' ? 'gridcell' : undefined)}
      align={align}
      sx={(theme) => cellSx(theme, padding)}
      {...attributes}
    >
      {clip ? (
        <Box
          component="span"
          title={typeof children === 'string' ? children : undefined}
          sx={clipped}
        >
          {children}
        </Box>
      ) : (
        children
      )}
    </TableCell>
  );
}

function cellSx(theme: Theme, padding: CellPadding) {
  return {
    ...theme.typography.tableCell,
    color: theme.palette.text.primary,
    borderBottom: 'none',
    padding: padding === 'tree' ? theme.spacing(0, 0.5) : theme.spacing(0, 1),
    ...(padding === 'hierarchy' ? { position: 'relative', paddingLeft: theme.spacing(1.5) } : {}),
  } as const;
}
