import type { HTMLAttributes, MouseEvent, ReactNode, Ref } from 'react';
import TableRow from '@mui/material/TableRow';
import type { DataAttributes } from '../passThrough';
import { rowSx, type RowCue } from './rowStyles';

/** Every attribute of a `tr` the host may drive — and none that styles it. */
export type RowAttributes = Omit<
  HTMLAttributes<HTMLTableRowElement>,
  'className' | 'style' | 'children'
> &
  DataAttributes;

export interface BodyRowProps extends RowAttributes {
  /** The reference's framed selection: `rowSelected` and an accent frame. */
  selected?: boolean;
  /** Called by a click on the row — a pointer's way to select it. */
  onSelect?: () => void;
  /** A drag's target: framed as allowed or refused, or a line above or below the row. */
  cue?: RowCue;
  /** This is the row being dragged: its content is dimmed. */
  dragging?: boolean;
  children?: ReactNode;
  ref?: Ref<HTMLTableRowElement>;
}

/**
 * A table row (tech-015 design § 5.11): 36 px, white, no lines between rows, a light
 * hover, a framed selection, a drag source's dimming and a drop target's cues — the
 * backlog's rows made generic. A `RowControl` inside it is revealed by the row's hover,
 * by focus within it and by its selection.
 *
 * **It sets no role, no `tabIndex` and no `aria-*` of its own** — a plain table's row is
 * not a stop and is never `aria-selected`; a treegrid host passes its row attributes
 * (`role`, `tabIndex`, `aria-level`, `aria-expanded`, `aria-selected`), its handlers and
 * its `ref` and they reach the `tr` as given. `data-selected`, `data-dragging` and
 * `data-cue` are the part's own and are what its styles read.
 */
export function BodyRow({
  selected = false,
  onSelect,
  cue,
  dragging = false,
  onClick,
  children,
  ref,
  ...attributes
}: BodyRowProps) {
  return (
    <TableRow
      ref={ref}
      data-selected={selected ? 'true' : undefined}
      data-dragging={dragging ? 'true' : undefined}
      data-cue={cue}
      onClick={(event: MouseEvent<HTMLTableRowElement>) => {
        onClick?.(event);
        onSelect?.();
      }}
      sx={(theme) => rowSx(theme, selected)}
      {...attributes}
    >
      {children}
    </TableRow>
  );
}
