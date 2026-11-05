import type { ReactNode } from 'react';
import Box from '@mui/material/Box';
import type { RowDragHandlers, RowDragState } from '../hooks/useRowDrag';

/**
 * The first cell of a movable row (feat-048 design § 5): the cell's content is the drag source and
 * the drop target; while a drag is over it the 2 px accent line shows above or below. The cell
 * dims while its row is dragged. The keyboard path is the row's «Ещё действия».
 */
export function RowDragCell({
  index,
  count,
  drag,
  handlers,
  label,
  children,
}: {
  index: number;
  count: number;
  drag: RowDragState | null;
  handlers: RowDragHandlers;
  label: string;
  children: ReactNode;
}) {
  const line =
    drag?.gap === index ? 'top' : drag?.gap === index + 1 && index === count - 1 ? 'bottom' : null;
  return (
    <Box
      {...handlers}
      data-drag-row={label}
      data-drop-line={line ?? undefined}
      data-dragging={drag?.from === index ? 'true' : undefined}
      sx={(theme) => ({
        cursor: 'grab',
        opacity: drag?.from === index ? 0.5 : 1,
        borderTop: `${theme.spacing(0.25)} solid ${line === 'top' ? theme.palette.primary.main : 'transparent'}`,
        borderBottom: `${theme.spacing(0.25)} solid ${line === 'bottom' ? theme.palette.primary.main : 'transparent'}`,
      })}
    >
      {children}
    </Box>
  );
}
