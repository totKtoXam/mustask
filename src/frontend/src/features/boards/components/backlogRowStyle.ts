import type { Theme } from '@mui/material/styles';

// The backlog table's rows as the reference draws them (feat-027 design § 4.2,
// § 4.6, § 4.9), shared by a row and the inline add row.

/** Where the drop line stands against a row: its top edge, or the last row's bottom. */
export type LineEdge = 'top' | 'bottom';

/**
 * The class of a row's own controls — the grip, «+», «⋮»: transparent at rest,
 * shown on hover, on focus within the row, on selection and while their menu is
 * open (feat-027 design § 4.6). `opacity`, not `visibility` or `display`, so
 * «+» and «⋮» stay in the Tab order. The grip is not a stop: it is `aria-hidden`
 * and only a hint, and the keyboard path of what a drag does is «Ещё действия»
 * (F-5).
 */
export const CONTROL = 'BacklogRow-control';

/** The opacity of the row being dragged (design § 4.6, "Dragging (the source)"). */
const DRAGGED_OPACITY = 0.6;

/** A drop target's cue while a drag is over its row: allowed, or refused (BR-T-12). */
export type DropTargetCue = 'allowed' | 'refused';

/** A text that does not fit ends in an ellipsis; the full text is the `title` attribute. */
export const clipped = {
  display: 'block',
  overflow: 'hidden',
  textOverflow: 'ellipsis',
  whiteSpace: 'nowrap',
} as const;

/** The 2 px accent line between rows (mod-006), drawn on the row's cells. */
export function dropLineSx(edge: LineEdge) {
  return (theme: Theme) => ({
    '& > td': {
      boxShadow: `inset 0 ${edge === 'top' ? '' : '-'}${theme.spacing(0.25)} 0 0 ${theme.palette.primary.main}`,
    },
  });
}

/**
 * A row's look (feat-027 design § 4.2, § 4.6): compact, white, no lines
 * between rows, a light hover, the selected row light blue with a blue frame
 * drawn as the cells' inset shadows — 2 px at the left edge, 1 px elsewhere.
 * Also the add row's, which is drawn selected.
 */
export function rowSx(selected: boolean) {
  return (theme: Theme) => {
    const frame = theme.palette.primary.main;
    const edges = `inset 0 1px 0 ${frame}, inset 0 -1px 0 ${frame}`;
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
            '& > td:first-of-type': { boxShadow: `inset 2px 0 0 ${frame}, ${edges}` },
            '& > td:last-of-type': { boxShadow: `inset -1px 0 0 ${frame}, ${edges}` },
          }
        : {}),
      [`& .${CONTROL}`]: { opacity: 0 },
      [`&:hover .${CONTROL}`]: { opacity: 1 },
      [`&:focus-within .${CONTROL}`]: { opacity: 1 },
      [`&[data-selected="true"] .${CONTROL}`]: { opacity: 1 },
      [`& .${CONTROL}[aria-expanded="true"]`]: { opacity: 1 },
      // The row being dragged: its content dimmed (design § 4.6).
      '&[data-dragging="true"] > td': { opacity: DRAGGED_OPACITY },
      // A drop target under the pointer (design § 4.6, § 4.9): a 2 px frame round
      // the row, solid on an allowed one, dashed red on a refused one. An outline
      // inset by its own width, so it takes no room and moves no cell.
      '&[data-drop-target="allowed"]': {
        backgroundColor: theme.palette.action.selected,
        outline: `${theme.spacing(0.25)} solid ${frame}`,
        outlineOffset: theme.spacing(-0.25),
      },
      '&[data-drop-target="refused"]': {
        outline: `${theme.spacing(0.25)} dashed ${theme.palette.error.main}`,
        outlineOffset: theme.spacing(-0.25),
      },
    };
  };
}

/** A row's own small control: the 24 px box of «+» and «⋮», its glyph the token's. */
export function controlSx(theme: Theme) {
  return {
    width: theme.layout.iconAction.row.box,
    height: theme.layout.iconAction.row.box,
    p: 0,
    flexShrink: 0,
    '& .MuiSvgIcon-root': { fontSize: theme.layout.iconAction.row.glyph },
  };
}

/**
 * The dotted grip at the row's left edge (feat-027 design § 4.9): a 16 px
 * strip, the full height of the cell, so the hit area is generous, and on it
 * the reference's mark — a 1 px column of four 2 px dots 6 px apart, 20 px
 * tall and centred on the row (in a 36 px row the dots stand at 8, 14, 20 and
 * 26 px), at the 8th pixel column. The mark is drawn once, not repeated: the
 * earlier five dots 8 px apart ran the row's full height and, on a hovered or
 * selected row, touched the frame and read as a longer, sparser dotted line
 * (V-4). Only the hint that the row can be taken: the whole row is the drag
 * source (D-11), so the strip carries no handlers and is not `draggable` itself.
 * Not a column and not a button: it is `aria-hidden` and not a tab stop, and the
 * keyboard path is «Ещё действия».
 */
export function gripSx(theme: Theme, off: boolean) {
  const { table } = theme.layout;
  const dot = theme.palette.action.disabled;
  return {
    position: 'absolute',
    left: 0,
    top: 0,
    bottom: 0,
    width: table.gripHitWidth,
    cursor: off ? 'default' : 'grab',
    backgroundImage: `repeating-linear-gradient(to bottom, ${dot} 0, ${dot} 2px, transparent 2px, transparent 6px)`,
    backgroundSize: `1px ${table.gripMarkHeight}px`,
    backgroundPosition: `${table.gripDotOffset}px center`,
    backgroundRepeat: 'no-repeat',
  } as const;
}
