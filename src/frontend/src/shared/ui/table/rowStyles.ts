import type { Theme } from '@mui/material/styles';

/**
 * The attribute a row's own controls carry (`RowControl`): `BodyRow` reveals what carries
 * it, `opacity` and not `visibility` or `display`, so a hidden control keeps its place in
 * the Tab order.
 */
export const ROW_CONTROL = 'data-row-control';

/** How a drag draws its target (`BodyRow.cue`). */
export type RowCue = 'dropAllowed' | 'dropRefused' | 'dropLineTop' | 'dropLineBottom';

/**
 * A row as the reference draws it (tech-015 design § 5.11; lifted from the backlog,
 * feat-027 design § 4.2, § 4.6): `density.registryRowHeight` high, white, no lines between
 * rows, a light hover, and the selected row light blue with an accent frame drawn as the
 * cells' inset shadows — `frame.thick` at the first cell's left edge, `frame.thin` at the
 * top and the bottom of every cell and at the last cell's right edge.
 *
 * Its controls (`[data-row-control]`) are transparent at rest and shown by this row's hover
 * (where the pointer can hover), by focus within it, by its selection and while their own
 * menu is open. With no hover, a tap selects the row, which shows them.
 */
export function rowSx(theme: Theme, selected: boolean) {
  const { thick, thin } = theme.layout.frame;
  const accent = theme.palette.primary.main;
  const edges = `inset 0 ${thin}px 0 ${accent}, inset 0 -${thin}px 0 ${accent}`;
  return {
    height: theme.density.registryRowHeight,
    backgroundColor: selected ? theme.palette.shell.rowSelected : theme.palette.background.paper,
    '&:hover': {
      backgroundColor: selected
        ? theme.palette.shell.rowSelected
        : theme.palette.background.default,
    },
    ...(selected
      ? {
          '& > td': { boxShadow: edges },
          '& > td:first-of-type': { boxShadow: `inset ${thick}px 0 0 ${accent}, ${edges}` },
          '& > td:last-of-type': { boxShadow: `inset -${thin}px 0 0 ${accent}, ${edges}` },
        }
      : {}),
    [`& [${ROW_CONTROL}]`]: { opacity: 0 },
    '@media (hover: hover)': { [`&:hover [${ROW_CONTROL}]`]: { opacity: 1 } },
    [`&:focus-within [${ROW_CONTROL}]`]: { opacity: 1 },
    [`&[data-selected="true"] [${ROW_CONTROL}]`]: { opacity: 1 },
    [`& [${ROW_CONTROL}][aria-expanded="true"]`]: { opacity: 1 },
    // The row being dragged: its content dimmed.
    '&[data-dragging="true"] > td': { opacity: theme.layout.table.draggedOpacity },
    // A drop target under the pointer: a frame round the row, solid on an allowed one,
    // dashed on a refused one. An outline inset by its own width takes no room.
    '&[data-cue="dropAllowed"]': {
      backgroundColor: theme.palette.action.selected,
      outline: `${thick}px solid ${accent}`,
      outlineOffset: `-${thick}px`,
    },
    '&[data-cue="dropRefused"]': {
      outline: `${thick}px dashed ${theme.palette.error.main}`,
      outlineOffset: `-${thick}px`,
    },
    // The line between rows, drawn on the cells.
    '&[data-cue="dropLineTop"] > td': { boxShadow: `inset 0 ${thick}px 0 0 ${accent}` },
    '&[data-cue="dropLineBottom"] > td': { boxShadow: `inset 0 -${thick}px 0 0 ${accent}` },
  };
}

/** A text that does not fit ends in an ellipsis; the full text is the `title` attribute. */
export const clipped = {
  display: 'block',
  overflow: 'hidden',
  textOverflow: 'ellipsis',
  whiteSpace: 'nowrap',
} as const;
