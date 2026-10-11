import TableCell, { type TableCellProps } from '@mui/material/TableCell';
import type { Theme } from '@mui/material/styles';

/** What a body cell of the backlog is: its own padding and a name by what it holds. */
type CellKind = 'plain' | 'hierarchy' | 'title';

/**
 * A body cell of the backlog table (feat-027 design § 4.2): `typography.tableCell`
 * (13 / 20), the neutral text colour, no line under it and 8 px of padding at
 * each side; the hierarchy cell is the positioning context of the grip and
 * starts 12 px in, and the title cell holds its own 4 px — the controls and the
 * icon carry the rest. Styled on the cell itself, so the row's descendant
 * selectors have nothing to out-rank.
 */
function cellSx(kind: CellKind) {
  return (theme: Theme) => ({
    ...theme.typography.tableCell,
    color: theme.palette.text.primary,
    borderBottom: 'none',
    padding: theme.spacing(0, 1),
    ...(kind === 'hierarchy' ? { position: 'relative', paddingLeft: theme.spacing(1.5) } : {}),
    ...(kind === 'title' ? { padding: theme.spacing(0, 0.5) } : {}),
  });
}

export function BacklogCell({
  kind = 'plain',
  ...other
}: Omit<TableCellProps, 'sx'> & { kind?: CellKind }) {
  return <TableCell {...other} sx={cellSx(kind)} />;
}
