import type { ReactElement } from 'react';
import Box from '@mui/material/Box';
import IconButton from '@mui/material/IconButton';
import TableCell from '@mui/material/TableCell';
import TableHead from '@mui/material/TableHead';
import TableRow from '@mui/material/TableRow';
import Tooltip from '@mui/material/Tooltip';
import type { Theme } from '@mui/material/styles';
import AddIcon from '@mui/icons-material/Add';
import RemoveIcon from '@mui/icons-material/Remove';
import {
  BUSINESS_VALUE_HEADER,
  COLLAPSE_ONE_LEVEL_LABEL,
  EFFORT_HEADER,
  EXPAND_ONE_LEVEL_LABEL,
  HIERARCHY_HEADER,
  ORDER_HEADER,
  STATE_HEADER,
  TITLE_HEADER,
  TYPE_HEADER,
  VALUE_AREA_HEADER,
} from './backlogColumns';

/** The class a header cell wears when a static separator stands at its right edge. */
const SEPARATED = 'BacklogHead-separated';

/**
 * A header cell (feat-027 design § 4.2): small grey labels on white, one
 * divider line under the row, the row 40 px high; sticky through the table's
 * `stickyHeader`, which keeps the separators in view too.
 */
function headCellSx(theme: Theme) {
  const table = theme.layout.backlogTable;
  return {
    ...theme.typography.caption,
    color: theme.palette.text.secondary,
    backgroundColor: theme.palette.background.paper,
    borderBottom: `${theme.layout.dividerWidth}px solid ${theme.palette.divider}`,
    height: theme.density.tabHeight,
    padding: theme.spacing(0, 1),
    // The reference's static thin vertical lines: no handle, no drag, no
    // cursor change — column options are out of this cut (design § 4.3).
    [`&.${SEPARATED}::after`]: {
      content: '""',
      position: 'absolute' as const,
      right: 0,
      top: '50%',
      transform: 'translateY(-50%)',
      // A length, not the bare number: `sx` reads a `width` of 1 or less as a
      // fraction of the parent, so `1` drew a bar as wide as the cell (V-1).
      width: `${theme.layout.dividerWidth}px`,
      height: table.separatorHeight,
      backgroundColor: theme.palette.divider,
    },
  };
}

/**
 * Where «Заголовок» starts in its header cell: the depth-0 icon's left edge (the
 * title cell's 4 px of padding, the 24 px chevron slot and 4 px of gap) and a
 * few px more, as the reference's label stands over the icon (V-1).
 */
const TITLE_LABEL_INSET = '36px';

/** A label that does not fit ends in an ellipsis; the text node is whole, so the name stays complete. */
const clipped = {
  display: 'block',
  overflow: 'hidden',
  textOverflow: 'ellipsis',
  whiteSpace: 'nowrap',
} as const;

/** The two small square outlined buttons of the hierarchy header (design § 4.5). */
function levelButtonSx(theme: Theme, available: boolean) {
  const table = theme.layout.backlogTable;
  // An idle button — nothing to open or close — keeps a readable outline and
  // glyph: the reference draws the pair alike, and `action.disabled` (about
  // 1.9:1) made «+» look broken beside the black «−» (V-3).
  const colour = available ? theme.palette.text.primary : theme.palette.text.secondary;
  return {
    width: table.headerButton,
    height: table.headerButton,
    p: 0,
    border: `1px solid ${colour}`,
    borderRadius: `${Number(theme.shape.borderRadius) / 2}px`,
    backgroundColor: theme.palette.background.paper,
    color: colour,
    '& .MuiSvgIcon-root': { fontSize: table.headerGlyph },
    '&:hover': { backgroundColor: available ? theme.palette.action.hover : undefined },
  };
}

/**
 * The table's header row (feat-027 US-1, US-3; design § 4.3, § 4.5). The first
 * cell is named «Иерархия» by its own `aria-label` — a column header takes its
 * name from its content, which here is the two buttons — and holds «Развернуть
 * на один уровень» and «Свернуть на один уровень». A button with nothing to do
 * is `aria-disabled`, dimmed and still focusable; activating it changes
 * nothing and announces nothing.
 */
export function BacklogHeadRow({
  canExpand,
  canCollapse,
  onExpand,
  onCollapse,
}: {
  /** The button has something to do and no write is in flight. */
  canExpand: boolean;
  canCollapse: boolean;
  onExpand(): void;
  onCollapse(): void;
}) {
  return (
    <TableHead>
      <TableRow>
        <TableCell aria-label={HIERARCHY_HEADER} sx={[headCellSx, { pl: 2 }]}>
          <Box sx={{ display: 'flex', alignItems: 'center', gap: 0.5 }}>
            <LevelButton
              label={EXPAND_ONE_LEVEL_LABEL}
              available={canExpand}
              onActivate={onExpand}
              icon={<AddIcon />}
            />
            <LevelButton
              label={COLLAPSE_ONE_LEVEL_LABEL}
              available={canCollapse}
              onActivate={onCollapse}
              icon={<RemoveIcon />}
            />
          </Box>
        </TableCell>
        <TableCell sx={headCellSx}>
          <Box component="span" sx={clipped}>
            {ORDER_HEADER}
          </Box>
        </TableCell>
        <TableCell className={SEPARATED} sx={headCellSx}>
          <Box component="span" sx={clipped}>
            {TYPE_HEADER}
          </Box>
        </TableCell>
        <TableCell className={SEPARATED} sx={[headCellSx, { pl: TITLE_LABEL_INSET }]}>
          <Box component="span" sx={clipped}>
            {TITLE_HEADER}
          </Box>
        </TableCell>
        <TableCell className={SEPARATED} sx={headCellSx}>
          <Box component="span" sx={clipped}>
            {STATE_HEADER}
          </Box>
        </TableCell>
        <TableCell className={SEPARATED} title={EFFORT_HEADER} sx={headCellSx}>
          <Box component="span" sx={clipped}>
            {EFFORT_HEADER}
          </Box>
        </TableCell>
        <TableCell className={SEPARATED} title={BUSINESS_VALUE_HEADER} sx={headCellSx}>
          <Box component="span" sx={clipped}>
            {BUSINESS_VALUE_HEADER}
          </Box>
        </TableCell>
        <TableCell sx={headCellSx}>
          <Box component="span" sx={clipped}>
            {VALUE_AREA_HEADER}
          </Box>
        </TableCell>
      </TableRow>
    </TableHead>
  );
}

function LevelButton({
  label,
  available,
  onActivate,
  icon,
}: {
  label: string;
  available: boolean;
  onActivate(): void;
  icon: ReactElement;
}) {
  return (
    <Tooltip title={label}>
      <IconButton
        size="small"
        aria-label={label}
        aria-disabled={available ? undefined : 'true'}
        onClick={() => {
          if (available) {
            onActivate();
          }
        }}
        sx={(theme) => levelButtonSx(theme, available)}
      >
        {icon}
      </IconButton>
    </Tooltip>
  );
}
