import {
  memo,
  useCallback,
  useId,
  useLayoutEffect,
  useRef,
  useState,
  type DragEvent,
  type MouseEvent,
} from 'react';
import { Link as RouterLink } from 'react-router-dom';
import Box from '@mui/material/Box';
import IconButton from '@mui/material/IconButton';
import Link from '@mui/material/Link';
import Menu from '@mui/material/Menu';
import MenuItem from '@mui/material/MenuItem';
import Paper from '@mui/material/Paper';
import Table from '@mui/material/Table';
import TableBody from '@mui/material/TableBody';
import TableCell from '@mui/material/TableCell';
import TableContainer from '@mui/material/TableContainer';
import TableHead from '@mui/material/TableHead';
import TableRow from '@mui/material/TableRow';
import type { Theme } from '@mui/material/styles';
import DragIndicatorIcon from '@mui/icons-material/DragIndicator';
import { useQueryClient } from '@tanstack/react-query';
import { AssigneeName, StateName, TypeName, workItemPath } from '../../workitems';
import type { Backlog, BacklogItem, ReorderBody } from '../api/boardsApi';
import { boardsKeys } from '../api/queryKeys';
import { useReorderBacklog } from '../hooks/mutations';
import { useMoveView } from '../hooks/useMoveView';
import type { BoardLevelCode } from '../lib/levels';
import { moveRefusalNotice } from '../lib/refusals';
import {
  MOVE_ENTRIES,
  MOVE_ENTRY_LABELS,
  applyMove,
  dropMove,
  entryMove,
  isEffectiveGap,
  isEntryEnabled,
  type MoveEntry,
} from '../lib/reorder';
import { HANDLE_HEADER, ITEM_COLUMNS, ORDER_HEADER } from './backlogColumns';

/** Where the drop line stands against a row: its top edge, or the last row's bottom. */
type LineEdge = 'top' | 'bottom';

/** The 2 px accent line between rows (spec § Page «Бэклоги»), drawn on the row's cells. */
function dropLineSx(edge: LineEdge) {
  return (theme: Theme) => ({
    '& > td': {
      boxShadow: `inset 0 ${edge === 'top' ? '' : '-'}${theme.spacing(0.25)} 0 0 ${theme.palette.primary.main}`,
    },
  });
}

interface DragState {
  id: string;
  /** The drop line's gap: 0 above the first row … n below the last; `null` off the rows. */
  gap: number | null;
}

/**
 * The backlog's table (US-1, US-2; spec § Page «Бэклоги»): the whole backlog,
 * no paging, each row with its handle. A move — the handle's menu or a pointer
 * drag by the handle, the browser's own drag and drop — shows the row at its
 * new place at once with «Порядок» renumbered, keeps focus on its handle and
 * is announced; while it is saved every handle is off (`aria-disabled`, not
 * draggable), so focus can stay on the moved one; then the backlog is read
 * again. A refusal puts the row back and shows its notice.
 */
export function BacklogTable({
  organizationId,
  projectId,
  teamId,
  level,
  items,
  announce,
  showError,
}: {
  organizationId: string;
  projectId: string;
  teamId: string;
  level: BoardLevelCode;
  /** The backlog as last read, `order` 1..n. */
  items: readonly BacklogItem[];
  announce(text: string): void;
  showError(text: string): void;
}) {
  const client = useQueryClient();
  const reorder = useReorderBacklog(organizationId, projectId, teamId, level);
  const view = useMoveView(items);
  const [menu, setMenu] = useState<{ anchor: HTMLElement; id: string } | null>(null);
  const [drag, setDrag] = useState<DragState | null>(null);
  const handles = useRef(new Map<string, HTMLElement>());
  /** The moved row's id until its move is saved: its handle keeps focus. */
  const pendingFocus = useRef<string | null>(null);
  const inFlight = useRef(false);
  const menuId = useId();

  const { saving, shown: rows } = view;

  // A moved row's node may be taken out and put back by the reorder, which
  // drops focus to the page: it goes back to the row's handle.
  useLayoutEffect(() => {
    const id = pendingFocus.current;
    if (id === null) {
      return;
    }
    const handle = handles.current.get(id);
    const active = document.activeElement;
    if (handle && (active === null || active === document.body)) {
      handle.focus();
    }
    if (!saving) {
      pendingFocus.current = null;
    }
  }, [rows, saving]);

  async function move(body: ReorderBody) {
    // On what is shown: right after a move it may be the newer read.
    const item = rows.find((candidate) => candidate.id === body.itemId);
    if (inFlight.current || !item) {
      return;
    }
    const next = applyMove(rows, body);
    const position = next.findIndex((candidate) => candidate.id === body.itemId) + 1;
    inFlight.current = true;
    pendingFocus.current = body.itemId;
    view.begin(next);
    announce(`${item.identifier} перемещён: позиция ${position} из ${next.length}.`);
    let notice: string | null = null;
    try {
      // The mutation settles once the backlog has been read again.
      await reorder.mutateAsync(body);
    } catch (error) {
      notice = moveRefusalNotice(error);
    }
    inFlight.current = false;
    view.end(
      client.getQueryData<Backlog>(boardsKeys.backlog(organizationId, projectId, teamId, level))
        ?.items,
      items,
    );
    if (notice !== null) {
      showError(notice);
    }
  }

  const registerHandle = useCallback((id: string, element: HTMLElement | null) => {
    if (element) {
      handles.current.set(id, element);
    } else {
      handles.current.delete(id);
    }
  }, []);

  const openMenu = useCallback(
    (event: MouseEvent<HTMLElement>, id: string) => {
      if (!saving) {
        setMenu({ anchor: event.currentTarget, id });
      }
    },
    [saving],
  );

  const startDrag = useCallback(
    (event: DragEvent<HTMLElement>, id: string, identifier: string) => {
      if (saving) {
        event.preventDefault();
        return;
      }
      const transfer = event.dataTransfer;
      if (transfer) {
        transfer.effectAllowed = 'move';
        transfer.setData('text/plain', identifier);
        const row = event.currentTarget.closest('tr');
        if (row && typeof transfer.setDragImage === 'function') {
          transfer.setDragImage(row, 0, 0);
        }
      }
      setDrag({ id, gap: null });
    },
    [saving],
  );

  const endDrag = useCallback(() => setDrag(null), []);

  function dragOver(event: DragEvent<HTMLElement>) {
    if (drag === null || saving) {
      return;
    }
    const row = (event.target as HTMLElement).closest<HTMLElement>('tr[data-row]');
    if (!row) {
      return;
    }
    event.preventDefault();
    if (event.dataTransfer) {
      event.dataTransfer.dropEffect = 'move';
    }
    const index = Number(row.dataset.row);
    const box = row.getBoundingClientRect();
    const gap = event.clientY < box.top + box.height / 2 ? index : index + 1;
    if (gap !== drag.gap) {
      setDrag({ id: drag.id, gap });
    }
  }

  function dragLeave(event: DragEvent<HTMLElement>) {
    const next = event.relatedTarget as Node | null;
    if (drag !== null && (next === null || !event.currentTarget.contains(next))) {
      setDrag({ id: drag.id, gap: null });
    }
  }

  function drop(event: DragEvent<HTMLElement>) {
    if (drag === null) {
      return;
    }
    event.preventDefault();
    const from = rows.findIndex((row) => row.id === drag.id);
    const body = drag.gap === null ? null : dropMove(rows, from, drag.gap);
    setDrag(null);
    if (body) {
      void move(body);
    }
  }

  function choose(entry: MoveEntry) {
    if (!menu) {
      return;
    }
    const body = entryMove(
      rows,
      rows.findIndex((row) => row.id === menu.id),
      entry,
    );
    setMenu(null);
    if (body) {
      void move(body);
    }
  }

  const dragFrom = drag ? rows.findIndex((row) => row.id === drag.id) : -1;
  const lineGap =
    drag && drag.gap !== null && isEffectiveGap(rows.length, dragFrom, drag.gap) ? drag.gap : null;
  const menuIndex = menu ? rows.findIndex((row) => row.id === menu.id) : -1;
  const menuItem = rows[menuIndex];

  return (
    <TableContainer component={Paper} variant="outlined">
      <Table size="small" aria-label="Бэклог">
        <TableHead>
          <TableRow>
            <TableCell>{ORDER_HEADER}</TableCell>
            <TableCell padding="checkbox">
              <Box component="span" sx={(theme) => theme.mixins.visuallyHidden}>
                {HANDLE_HEADER}
              </Box>
            </TableCell>
            {ITEM_COLUMNS.map((column) => (
              <TableCell key={column}>{column}</TableCell>
            ))}
          </TableRow>
        </TableHead>
        <TableBody
          onDragEnter={dragOver}
          onDragOver={dragOver}
          onDragLeave={dragLeave}
          onDrop={drop}
        >
          {rows.map((item, index) => (
            <BacklogRow
              key={item.id}
              item={item}
              index={index}
              line={
                lineGap === index
                  ? 'top'
                  : lineGap === rows.length && index === rows.length - 1
                    ? 'bottom'
                    : null
              }
              saving={saving}
              menuId={menu?.id === item.id ? menuId : null}
              formPath={workItemPath(organizationId, projectId, item.identifier)}
              onOpenMenu={openMenu}
              onDragStart={startDrag}
              onDragEnd={endDrag}
              registerHandle={registerHandle}
            />
          ))}
        </TableBody>
      </Table>
      <Menu
        id={menuId}
        anchorEl={menu?.anchor ?? null}
        open={menu !== null}
        onClose={() => setMenu(null)}
        slotProps={{
          list: { 'aria-label': menuItem ? `Переместить ${menuItem.identifier}` : undefined },
        }}
      >
        {MOVE_ENTRIES.map((entry) => (
          <MenuItem
            key={entry}
            disabled={!isEntryEnabled(entry, menuIndex, rows.length)}
            onClick={() => choose(entry)}
          >
            {MOVE_ENTRY_LABELS[entry]}
          </MenuItem>
        ))}
      </Menu>
    </TableContainer>
  );
}

/**
 * One row: «Порядок», the handle, ID, «Тип», «Заголовок» as a link to the
 * form, «Исполнитель», «Статус» (BR-M4-14). Memoized, so a drop line moving
 * over a long backlog redraws two rows, not all of them.
 *
 * The handle is an `IconButton` drawn as a `span` (MUI keeps it a `button`
 * by role, focusable, Enter and Space opening its menu): a native `button`
 * cannot be the source of the browser's own drag in every browser.
 */
const BacklogRow = memo(function BacklogRow({
  item,
  index,
  line,
  saving,
  menuId,
  formPath,
  onOpenMenu,
  onDragStart,
  onDragEnd,
  registerHandle,
}: {
  item: BacklogItem;
  index: number;
  line: LineEdge | null;
  saving: boolean;
  /** The open menu's id when it is this row's. */
  menuId: string | null;
  formPath: string;
  onOpenMenu(event: MouseEvent<HTMLElement>, id: string): void;
  onDragStart(event: DragEvent<HTMLElement>, id: string, identifier: string): void;
  onDragEnd(): void;
  registerHandle(id: string, element: HTMLElement | null): void;
}) {
  return (
    <TableRow
      hover
      data-row={index}
      data-drop-line={line ?? undefined}
      sx={line ? dropLineSx(line) : undefined}
    >
      <TableCell>{item.order}</TableCell>
      <TableCell padding="checkbox">
        <IconButton
          component="span"
          size="small"
          ref={(element: HTMLElement | null) => registerHandle(item.id, element)}
          aria-label={`Переместить ${item.identifier}`}
          aria-haspopup="menu"
          aria-expanded={menuId !== null ? 'true' : 'false'}
          aria-controls={menuId ?? undefined}
          aria-disabled={saving ? 'true' : undefined}
          draggable={!saving}
          onClick={(event: MouseEvent<HTMLElement>) => onOpenMenu(event, item.id)}
          onDragStart={(event: DragEvent<HTMLElement>) =>
            onDragStart(event, item.id, item.identifier)
          }
          onDragEnd={onDragEnd}
          sx={{ cursor: saving ? 'default' : 'grab', color: saving ? 'text.disabled' : undefined }}
        >
          <DragIndicatorIcon fontSize="small" />
        </IconButton>
      </TableCell>
      <TableCell sx={{ whiteSpace: 'nowrap' }}>{item.identifier}</TableCell>
      <TableCell>
        <TypeName type={item.type} />
      </TableCell>
      <TableCell sx={{ overflowWrap: 'anywhere' }}>
        <Link component={RouterLink} to={formPath}>
          {item.title}
        </Link>
      </TableCell>
      <TableCell>
        <AssigneeName assignee={item.assignee} />
      </TableCell>
      <TableCell>
        <StateName state={item.state} />
      </TableCell>
    </TableRow>
  );
});
