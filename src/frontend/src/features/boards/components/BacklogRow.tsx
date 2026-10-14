import { memo, useId, type DragEvent, type KeyboardEvent, type MouseEvent } from 'react';
import { Link as RouterLink } from 'react-router-dom';
import Box from '@mui/material/Box';
import IconButton from '@mui/material/IconButton';
import Link from '@mui/material/Link';
import TableRow from '@mui/material/TableRow';
import Tooltip from '@mui/material/Tooltip';
import { useTheme } from '@mui/material/styles';
import MoreVertIcon from '@mui/icons-material/MoreVert';
import { isPlainClick } from '../../workitems';
import { ThinAddIcon, ThinChevronDownIcon, ThinChevronRightIcon } from '../../../shared/ui';
import { CategoryMarker, TypeMark } from '../../../shared/ui/WorkTrackingMarks';
import type { BacklogTreeItem } from '../api/boardsApi';
import { addControlLabel, addEntryLabel, type AddEntry } from '../lib/addChild';
import { hasChildren } from '../lib/tree';
import { planningNumber, valueAreaLabel } from '../lib/values';
import {
  CONTROL,
  clipped,
  controlSx,
  dropLineSx,
  gripSx,
  rowSx,
  type DropTargetCue,
  type LineEdge,
} from './backlogRowStyle';
import { BacklogCell } from './BacklogCell';

/**
 * One row of the backlog (feat-027 US-1, US-2, US-4; design § 4): the
 * hierarchy cell with the grip and the add control, «Порядок» on a top-level
 * row, «Тип» as text, the title cell — indent, chevron, type icon, plain title
 * that opens the item, «⋮» — «Статус» as a dot and a name, and the three
 * planning values, left-aligned as the reference draws them (D-10). The whole
 * row is the source of a drag, top-level or nested (US-5; D-11) — draggable
 * while no write is in flight; the grip at its left edge is only the hint that
 * it can be taken. The row is also a drop target while a drag is over it,
 * framed as allowed or refused. Memoized, so a drop line or a frame moving over
 * a long backlog redraws two rows, not all of them.
 */
export const BacklogRow = memo(function BacklogRow({
  item,
  order,
  tier,
  block,
  line,
  dropTarget,
  dragging,
  expanded,
  selected,
  off,
  draggable,
  showActions,
  addEntries,
  addMenuId,
  actionsId,
  formPath,
  onOpenItem,
  onSelect,
  onToggle,
  onAdd,
  onOpenActions,
  onDragStart,
  onDragEnd,
  registerActions,
  registerAdd,
}: {
  item: BacklogTreeItem;
  /** The row's «Порядок»; `null` on a nested row, which has none. */
  order: number | null;
  tier: number;
  block: number;
  line: LineEdge | null;
  /** A drag is over this row: framed as an allowed or a refused target (BR-T-12). */
  dropTarget: DropTargetCue | null;
  /** This is the row being dragged: its content is dimmed. */
  dragging: boolean;
  expanded: boolean;
  selected: boolean;
  /** A write is being saved: the grip, «+» and «⋮» are off. */
  off: boolean;
  /**
   * The user's codes allow some drop of this row (mod-016 design § 6.2): a drop
   * onto a row needs `workitems.link.write`, a move between top-level blocks
   * `boards.backlog.reorder` and a top-level row. Without either the row draws
   * no grip and is not a drag source.
   */
  draggable: boolean;
  /** One of the «⋮» menu's items is allowed: without it the row draws no «Ещё действия». */
  showActions: boolean;
  /**
   * What the row's «+» offers; none draws no «+» (BR-T-9) — also when the user
   * lacks `workitems.workitem.write` or `workitems.link.write` (the create
   * carries the parent).
   */
  addEntries: readonly AddEntry[];
  /** The open «+» menu's id when it is this row's. */
  addMenuId: string | null;
  /** The open «⋮» menu's id when it is this row's. */
  actionsId: string | null;
  /** The item's full page: the title's `href`, followed by a modified click (BR-C-1). */
  formPath: string;
  /** A plain click on the title opens the item's card as a dialog over this page (BR-C-1). */
  onOpenItem(identifier: string): void;
  onSelect(id: string): void;
  onToggle(id: string): void;
  onAdd(
    event: MouseEvent<HTMLElement> | KeyboardEvent<HTMLElement>,
    item: BacklogTreeItem,
    entries: readonly AddEntry[],
  ): void;
  onOpenActions(event: MouseEvent<HTMLElement> | KeyboardEvent<HTMLElement>, id: string): void;
  onDragStart(event: DragEvent<HTMLElement>, id: string, identifier: string): void;
  onDragEnd(): void;
  registerActions(id: string, element: HTMLElement | null): void;
  registerAdd(id: string, element: HTMLElement | null): void;
}) {
  const theme = useTheme();
  const { table, iconAction } = theme.layout;
  const describedBy = useId();
  const [onlyEntry] = addEntries;
  const addMany = addEntries.length > 1;

  return (
    <TableRow
      data-identifier={item.identifier}
      data-block={block}
      data-tier={tier}
      data-drop-line={line ?? undefined}
      data-drop-target={dropTarget ?? undefined}
      data-dragging={dragging ? 'true' : undefined}
      data-selected={selected ? 'true' : undefined}
      onClick={() => onSelect(item.id)}
      // The whole row is the drag source (D-11): a press anywhere on it drags it, the
      // buttons inside it included. The one text field of the table is in the add row,
      // which is not this component and is never draggable.
      draggable={draggable && !off}
      onDragStart={(event: DragEvent<HTMLElement>) => onDragStart(event, item.id, item.identifier)}
      onDragEnd={onDragEnd}
      sx={line ? [rowSx(selected), dropLineSx(line)] : rowSx(selected)}
    >
      <BacklogCell kind="hierarchy">
        {draggable && (
          <Box
            component="span"
            className={CONTROL}
            data-grip=""
            aria-hidden="true"
            tabIndex={-1}
            sx={(current) => gripSx(current, off)}
          />
        )}
        {onlyEntry && (
          <Tooltip title={addMany ? 'Добавить дочерний элемент' : addEntryLabel(onlyEntry)}>
            <IconButton
              size="small"
              className={CONTROL}
              ref={(element: HTMLElement | null) => registerAdd(item.id, element)}
              aria-label={addControlLabel(addEntries, item.identifier)}
              aria-haspopup={addMany ? 'menu' : undefined}
              aria-expanded={addMany ? (addMenuId !== null ? 'true' : 'false') : undefined}
              aria-controls={addMenuId ?? undefined}
              aria-disabled={off ? 'true' : undefined}
              onClick={(event: MouseEvent<HTMLElement>) => {
                // «+» is a gesture of its own, not a click on the row: left to bubble it would
                // select the row again after opening the add row cleared the selection, and
                // two rows would be drawn selected (F-8).
                event.stopPropagation();
                onAdd(event, item, addEntries);
              }}
              onKeyDown={(event: KeyboardEvent<HTMLElement>) => {
                // mod-015 § 12.4: ArrowDown opens a menu too.
                if (addMany && event.key === 'ArrowDown') {
                  event.preventDefault();
                  onAdd(event, item, addEntries);
                }
              }}
              sx={(current) => ({
                ...controlSx(current),
                position: 'relative',
                zIndex: 1,
                color: off ? current.palette.text.disabled : current.palette.shell.addAction,
              })}
            >
              <ThinAddIcon />
            </IconButton>
          </Tooltip>
        )}
      </BacklogCell>
      <BacklogCell>{order}</BacklogCell>
      <BacklogCell>
        <Box component="span" title={item.type.name} sx={clipped}>
          {item.type.name}
        </Box>
      </BacklogCell>
      <BacklogCell kind="title">
        <Box
          sx={{
            display: 'flex',
            alignItems: 'center',
            minWidth: 0,
            paddingLeft: `${tier * table.treeIndent}px`,
          }}
        >
          {hasChildren(item) ? (
            <IconButton
              size="small"
              aria-label={`${expanded ? 'Свернуть' : 'Развернуть'} ${item.identifier}`}
              aria-expanded={expanded ? 'true' : 'false'}
              onClick={() => onToggle(item.id)}
              sx={(current) => ({
                width: current.layout.iconAction.chevron.box,
                height: current.layout.iconAction.chevron.box,
                p: 0,
                flexShrink: 0,
                color: 'text.primary',
                '& .MuiSvgIcon-root': { fontSize: current.layout.iconAction.chevron.glyph },
              })}
            >
              {expanded ? <ThinChevronDownIcon /> : <ThinChevronRightIcon />}
            </IconButton>
          ) : (
            <Box
              aria-hidden="true"
              sx={{
                width: iconAction.chevron.box,
                height: iconAction.chevron.box,
                flexShrink: 0,
              }}
            />
          )}
          <Box
            component="span"
            sx={{ display: 'inline-flex', flexShrink: 0, marginLeft: 0.5, marginRight: 0.5 }}
          >
            <TypeMark
              appearance={item.type.appearance}
              compact
              colour={selected ? theme.palette.primary.dark : undefined}
            />
          </Box>
          <Link
            component={RouterLink}
            to={formPath}
            color="inherit"
            underline="hover"
            title={`${item.identifier}: ${item.title}`}
            onClick={(event: MouseEvent<HTMLElement>) => {
              if (isPlainClick(event)) {
                event.preventDefault();
                onOpenItem(item.identifier);
              }
            }}
            // A link drags by itself: without this the browser would drag its address,
            // not the row.
            draggable={false}
            aria-describedby={describedBy}
            sx={{ ...clipped, flex: '0 1 auto', minWidth: 0 }}
          >
            {item.title}
          </Link>
          <Box id={describedBy} component="span" sx={(current) => current.mixins.visuallyHidden}>
            {`${item.identifier}. Уровень вложенности: ${tier + 1}.`}
          </Box>
          <Box sx={{ flex: '1 1 0', minWidth: 0 }} />
          {showActions && (
            <IconButton
              size="small"
              className={CONTROL}
              ref={(element: HTMLElement | null) => registerActions(item.id, element)}
              aria-label={`Ещё действия: ${item.identifier}`}
              aria-haspopup="menu"
              aria-expanded={actionsId !== null ? 'true' : 'false'}
              aria-controls={actionsId ?? undefined}
              aria-disabled={off ? 'true' : undefined}
              onClick={(event: MouseEvent<HTMLElement>) => onOpenActions(event, item.id)}
              onKeyDown={(event: KeyboardEvent<HTMLElement>) => {
                // mod-015 § 12.4: ArrowDown opens a menu too.
                if (event.key === 'ArrowDown') {
                  event.preventDefault();
                  onOpenActions(event, item.id);
                }
              }}
              sx={(current) => ({
                ...controlSx(current),
                color: off ? current.palette.text.disabled : current.palette.text.primary,
              })}
            >
              <MoreVertIcon />
            </IconButton>
          )}
        </Box>
      </BacklogCell>
      <BacklogCell>
        <Box
          component="span"
          title={item.state.name}
          sx={{ display: 'flex', alignItems: 'center', gap: 0.75, minWidth: 0 }}
        >
          <CategoryMarker code={item.state.category.code} />
          <Box component="span" sx={clipped}>
            {item.state.name}
          </Box>
        </Box>
      </BacklogCell>
      <BacklogCell>{planningNumber(item.effort)}</BacklogCell>
      <BacklogCell>{planningNumber(item.businessValue)}</BacklogCell>
      <BacklogCell>
        <Box component="span" sx={clipped}>
          {valueAreaLabel(item.valueArea)}
        </Box>
      </BacklogCell>
    </TableRow>
  );
});
