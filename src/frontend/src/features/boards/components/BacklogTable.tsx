import {
  memo,
  useCallback,
  useEffect,
  useId,
  useLayoutEffect,
  useMemo,
  useRef,
  useState,
  type DragEvent,
  type KeyboardEvent,
  type MouseEvent,
} from 'react';
import { Link as RouterLink } from 'react-router-dom';
import Box from '@mui/material/Box';
import IconButton from '@mui/material/IconButton';
import Link from '@mui/material/Link';
import ListItemIcon from '@mui/material/ListItemIcon';
import ListItemText from '@mui/material/ListItemText';
import Menu from '@mui/material/Menu';
import MenuItem from '@mui/material/MenuItem';
import Paper from '@mui/material/Paper';
import Table from '@mui/material/Table';
import TableBody from '@mui/material/TableBody';
import TableCell from '@mui/material/TableCell';
import TableContainer from '@mui/material/TableContainer';
import TableHead from '@mui/material/TableHead';
import TableRow from '@mui/material/TableRow';
import Tooltip from '@mui/material/Tooltip';
import Typography from '@mui/material/Typography';
import type { Theme } from '@mui/material/styles';
import AccountTreeOutlinedIcon from '@mui/icons-material/AccountTreeOutlined';
import ChevronRightIcon from '@mui/icons-material/ChevronRight';
import DragIndicatorIcon from '@mui/icons-material/DragIndicator';
import ExpandMoreIcon from '@mui/icons-material/ExpandMore';
import LinkOffIcon from '@mui/icons-material/LinkOff';
import MoreHorizIcon from '@mui/icons-material/MoreHoriz';
import { useQueryClient } from '@tanstack/react-query';
import {
  AssigneeName,
  StateName,
  TypeName,
  useSetWorkItemParent,
  workItemPath,
  type ParentWriteResult,
  type WorkItemRef,
} from '../../workitems';
import type { Backlog, BacklogRow, BacklogTreeItem, ReorderBody } from '../api/boardsApi';
import { boardsKeys } from '../api/queryKeys';
import { useReorderBacklog } from '../hooks/mutations';
import { useReadTeamAgain } from '../hooks/queries';
import { useMoveView } from '../hooks/useMoveView';
import { LEVEL_LABELS, levelAbove, type BoardLevelCode } from '../lib/levels';
import { moveRefusalNotice, parentRefusal, type ParentRefusal } from '../lib/refusals';
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
import { hasChildren, toggled, treeLines } from '../lib/tree';
import { ACTIONS_HEADER, HANDLE_HEADER, ITEM_COLUMNS, ORDER_HEADER } from './backlogColumns';
import { ChangeParentDialog } from './ChangeParentDialog';

/** Where the drop line stands against a row: its top edge, or the last row's bottom. */
type LineEdge = 'top' | 'bottom';

/** The «Родитель» cell of a row with no parent, or one in «Удалена» (BR-H-13, BR-H-8). */
const NO_PARENT_TEXT = 'Нет';

/** The row menu's entries (feat-001 spec § Page «Бэклоги», «Действия»). */
const CHANGE_PARENT_LABEL = 'Изменить родителя…';
const CLEAR_PARENT_LABEL = 'Убрать родителя';

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
  /** The drop line's gap between top-level blocks: 0 above the first … n below the last; `null` off the rows. */
  gap: number | null;
}

/** A row's «Ещё действия» while its menu is open. */
interface ActionsMenuState {
  anchor: HTMLElement;
  /** The row as it was when the menu opened: its entries decide on that parent (BR-H-4). */
  row: BacklogRow;
}

/** The dialog «Изменить родителя» while it is open. */
interface ParentDialogState {
  /**
   * The row as the dialog shows it: as it was when the dialog opened, and
   * replaced only by the read after `parent_changed`, which also preselects
   * its parent. Its parent is the one the write names as seen and the one
   * «Сохранить» waits to differ from (BR-H-4) — never a re-read the dialog
   * did not show. Kept should the row leave the backlog.
   */
  row: BacklogRow;
  chosen: WorkItemRef | null;
  busy: boolean;
  fieldError?: string;
  alert: string | null;
}

/**
 * The backlog's table (US-1, US-2; spec § Page «Бэклоги»; feat-001 US-4,
 * US-5): the whole backlog, no paging. Each top-level row has its handle; a
 * row with children to show has an expand control, and while it is expanded
 * its children stand beneath it one tier deeper, with no «Порядок», no handle
 * and no «Ещё действия» (BR-H-11). What is expanded is kept here, by item, so
 * it survives the page's re-reads; the page keys the table by its level and
 * team, so another of either — or a reload — starts collapsed (BR-H-12).
 *
 * A move — the handle's menu or a pointer drag by the handle, the browser's
 * own drag and drop — takes a top-level row with its shown children: the
 * drop line stands only between top-level blocks, and «Выше» / «Ниже» step
 * over blocks. The block shows at its new place at once with «Порядок»
 * renumbered, keeps focus on its handle and is announced; then the backlog is
 * read again. A refusal puts the row back and shows its notice.
 *
 * On «Фичи» and «Истории» a top-level row's «Ещё действия» offers
 * «Изменить родителя…» — the dialog, searching only the level above — and
 * «Убрать родителя», which shows «Нет» at once (BR-H-14). Either is the parent
 * write, then the backlog is read again, before the notice. Either names as
 * seen the parent the row had when its menu opened — the dialog keeps it
 * while open — so a change read in meanwhile is refused with
 * `parent_changed`, not overwritten (BR-H-4).
 *
 * One write at a time: while a move or a parent write is saved every handle
 * and every «Ещё действия» is off (`aria-disabled`, not draggable), so focus
 * can stay where it is.
 */
export function BacklogTable({
  organizationId,
  projectId,
  teamId,
  level,
  items,
  announce,
  showSuccess,
  showError,
}: {
  organizationId: string;
  projectId: string;
  teamId: string;
  level: BoardLevelCode;
  /** The backlog as last read, `order` 1..n. */
  items: readonly BacklogRow[];
  announce(text: string): void;
  /** A parent write's notice (feat-001 spec § Notices on «Бэклоги»). */
  showSuccess(text: string): void;
  showError(text: string): void;
}) {
  const client = useQueryClient();
  const reorder = useReorderBacklog(organizationId, projectId, teamId, level);
  const setParent = useSetWorkItemParent(organizationId, projectId);
  const readAgain = useReadTeamAgain(organizationId, projectId, teamId);
  const view = useMoveView(items);
  const [menu, setMenu] = useState<{ anchor: HTMLElement; id: string } | null>(null);
  const [actions, setActions] = useState<ActionsMenuState | null>(null);
  const [dialog, setDialog] = useState<ParentDialogState | null>(null);
  const [drag, setDrag] = useState<DragState | null>(null);
  const [expanded, setExpanded] = useState<ReadonlySet<string>>(() => new Set());
  /** A parent write from the dialog is being saved. */
  const [writing, setWriting] = useState(false);
  const handles = useRef(new Map<string, HTMLElement>());
  const actionButtons = useRef(new Map<string, HTMLElement>());
  /** The moved row's id until its move is saved: its handle keeps focus. */
  const pendingFocus = useRef<string | null>(null);
  /** The row whose «Ещё действия» takes focus back once its menu, dialog and write are done. */
  const pendingActionFocus = useRef<string | null>(null);
  const inFlight = useRef(false);
  const menuId = useId();
  const actionsMenuId = useId();

  const above = levelAbove(level);
  const withActions = above !== null;
  const { saving, shown: rows } = view;
  const off = saving || writing;
  const lines = useMemo(() => treeLines(rows, expanded), [rows, expanded]);

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

  // After the row menu, its dialog and its write, focus is on the row's
  // «Ещё действия» (spec § Page «Бэклоги»). A passive effect, so the closing
  // menu's or dialog's focus trap has let go first.
  useEffect(() => {
    const id = pendingActionFocus.current;
    if (id === null || dialog !== null) {
      return;
    }
    actionButtons.current.get(id)?.focus();
    if (!off) {
      pendingActionFocus.current = null;
    }
  }, [dialog, off, rows]);

  function backlogRead(): BacklogRow[] | undefined {
    return client.getQueryData<Backlog>(
      boardsKeys.backlog(organizationId, projectId, teamId, level),
    )?.items;
  }

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
    view.end(backlogRead(), items);
    if (notice !== null) {
      showError(notice);
    }
  }

  /** «Убрать родителя»: «Нет» at once, the write, the read, then the notice (US-5). */
  async function clearParent(row: BacklogRow) {
    const seen = row.parent;
    if (inFlight.current || seen === null) {
      return;
    }
    inFlight.current = true;
    pendingActionFocus.current = row.id;
    view.begin(
      rows.map((candidate) =>
        candidate.id === row.id ? { ...candidate, parent: null } : candidate,
      ),
    );
    let refusal: ParentRefusal | null = null;
    try {
      await setParent.mutateAsync({
        identifier: row.identifier,
        change: { parentId: null, seenParentId: seen.id },
      });
    } catch (error) {
      refusal = parentRefusal(error);
    }
    // The backlog is read again whatever the answer, before the notice.
    await readAgain();
    inFlight.current = false;
    view.end(backlogRead(), items);
    if (refusal === null) {
      showSuccess(`У ${row.identifier} больше нет родителя.`);
    } else if (refusal.notice !== null) {
      showError(refusal.notice);
    }
  }

  /** The dialog's «Сохранить»: the write, the read, then close and notice, or the refusal in place. */
  async function saveParent() {
    if (dialog === null || inFlight.current) {
      return;
    }
    // The row as the dialog shows it, not the latest read (BR-H-4).
    const row = dialog.row;
    const chosen = dialog.chosen;
    if (chosen === null || chosen.id === row.parent?.id) {
      return;
    }
    inFlight.current = true;
    setWriting(true);
    setDialog({ ...dialog, busy: true, fieldError: undefined, alert: null });
    let written: ParentWriteResult | null = null;
    let refusal: ParentRefusal | null = null;
    try {
      written = await setParent.mutateAsync({
        identifier: row.identifier,
        change: { parentId: chosen.id, seenParentId: row.parent?.id ?? null },
      });
    } catch (error) {
      refusal = parentRefusal(error);
    }
    await readAgain();
    inFlight.current = false;
    setWriting(false);
    if (written !== null) {
      setDialog(null);
      showSuccess(
        `У ${row.identifier} теперь родитель ${written.parent?.identifier ?? chosen.identifier}.`,
      );
      return;
    }
    // After `parent_changed` the read shows the stored parent: it is preselected,
    // and the dialog's row becomes that read. After any other refusal the
    // dialog still shows the parent it had, and keeps it.
    const fresh = refusal?.parentChanged
      ? backlogRead()?.find((candidate) => candidate.id === row.id)
      : undefined;
    setDialog((current) =>
      current === null
        ? null
        : {
            ...current,
            row: fresh ?? current.row,
            busy: false,
            fieldError: refusal?.field ?? undefined,
            alert: refusal?.field ? null : (refusal?.notice ?? null),
            chosen: refusal?.parentChanged ? (fresh?.parent ?? null) : current.chosen,
          },
    );
  }

  const registerHandle = useCallback((id: string, element: HTMLElement | null) => {
    if (element) {
      handles.current.set(id, element);
    } else {
      handles.current.delete(id);
    }
  }, []);

  const registerActions = useCallback((id: string, element: HTMLElement | null) => {
    if (element) {
      actionButtons.current.set(id, element);
    } else {
      actionButtons.current.delete(id);
    }
  }, []);

  const openMenu = useCallback(
    (event: MouseEvent<HTMLElement>, id: string) => {
      if (!off) {
        setMenu({ anchor: event.currentTarget, id });
      }
    },
    [off],
  );

  const openActions = useCallback(
    (event: MouseEvent<HTMLElement> | KeyboardEvent<HTMLElement>, id: string) => {
      const row = rows.find((candidate) => candidate.id === id);
      if (!off && row) {
        setActions({ anchor: event.currentTarget, row });
      }
    },
    [off, rows],
  );

  const toggle = useCallback((id: string) => setExpanded((current) => toggled(current, id)), []);

  const startDrag = useCallback(
    (event: DragEvent<HTMLElement>, id: string, identifier: string) => {
      if (off) {
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
    [off],
  );

  const endDrag = useCallback(() => setDrag(null), []);

  function dragOver(event: DragEvent<HTMLElement>) {
    if (drag === null || off) {
      return;
    }
    const row = (event.target as HTMLElement).closest<HTMLElement>('tr[data-block]');
    if (!row) {
      return;
    }
    event.preventDefault();
    if (event.dataTransfer) {
      event.dataTransfer.dropEffect = 'move';
    }
    // The pointer's half of the whole block — the top-level row and its shown
    // children — decides: a line never stands between nested rows.
    const block = Number(row.dataset.block);
    const blockRows = event.currentTarget.querySelectorAll<HTMLElement>(
      `tr[data-block="${block}"]`,
    );
    const top = (blockRows[0] ?? row).getBoundingClientRect().top;
    const bottom = (blockRows[blockRows.length - 1] ?? row).getBoundingClientRect().bottom;
    const gap = event.clientY < (top + bottom) / 2 ? block : block + 1;
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

  /** The open menu's row as it opened, while it is still in the backlog. */
  const actionsRow =
    actions && rows.some((row) => row.id === actions.row.id) ? actions.row : undefined;

  function chooseChange() {
    setActions(null);
    if (actionsRow) {
      pendingActionFocus.current = actionsRow.id;
      setDialog({ row: actionsRow, chosen: actionsRow.parent, busy: false, alert: null });
    }
  }

  function chooseClear() {
    setActions(null);
    if (actionsRow) {
      void clearParent(actionsRow);
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
            {withActions && (
              <TableCell padding="checkbox">
                <Box component="span" sx={(theme) => theme.mixins.visuallyHidden}>
                  {ACTIONS_HEADER}
                </Box>
              </TableCell>
            )}
          </TableRow>
        </TableHead>
        <TableBody
          onDragEnter={dragOver}
          onDragOver={dragOver}
          onDragLeave={dragLeave}
          onDrop={drop}
        >
          {lines.map(({ item, tier, block }, index) => {
            const top = tier === 0;
            return (
              <BacklogLine
                key={item.id}
                item={item}
                order={top ? (rows[block]?.order ?? null) : null}
                tier={tier}
                block={block}
                line={
                  top && lineGap === block
                    ? 'top'
                    : lineGap === rows.length && index === lines.length - 1
                      ? 'bottom'
                      : null
                }
                expanded={expanded.has(item.id)}
                off={off}
                withActions={withActions}
                menuId={menu?.id === item.id ? menuId : null}
                actionsId={actions?.row.id === item.id ? actionsMenuId : null}
                formPath={workItemPath(organizationId, projectId, item.identifier)}
                parentPath={
                  item.parent
                    ? workItemPath(organizationId, projectId, item.parent.identifier)
                    : null
                }
                onToggle={toggle}
                onOpenMenu={openMenu}
                onOpenActions={openActions}
                onDragStart={startDrag}
                onDragEnd={endDrag}
                registerHandle={registerHandle}
                registerActions={registerActions}
              />
            );
          })}
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
      {withActions && (
        <Menu
          id={actionsMenuId}
          anchorEl={actions?.anchor ?? null}
          open={actions !== null}
          onClose={() => setActions(null)}
          slotProps={{
            list: {
              'aria-label': actionsRow ? `Ещё действия: ${actionsRow.identifier}` : undefined,
            },
          }}
        >
          <MenuItem onClick={chooseChange}>
            <ListItemIcon sx={{ color: 'primary.main' }}>
              <AccountTreeOutlinedIcon fontSize="small" />
            </ListItemIcon>
            <ListItemText>{CHANGE_PARENT_LABEL}</ListItemText>
          </MenuItem>
          <MenuItem disabled={!actionsRow?.parent} onClick={chooseClear}>
            <ListItemIcon sx={{ color: 'primary.main' }}>
              <LinkOffIcon fontSize="small" />
            </ListItemIcon>
            <ListItemText>{CLEAR_PARENT_LABEL}</ListItemText>
          </MenuItem>
        </Menu>
      )}
      {dialog && above && (
        <ChangeParentDialog
          organizationId={organizationId}
          projectId={projectId}
          row={dialog.row}
          aboveLabel={LEVEL_LABELS[above]}
          value={dialog.chosen}
          busy={dialog.busy}
          fieldError={dialog.fieldError}
          alert={dialog.alert}
          onChange={(chosen) =>
            setDialog((current) =>
              current === null ? null : { ...current, chosen, fieldError: undefined },
            )
          }
          onSubmit={() => void saveParent()}
          onClose={() => {
            if (!dialog.busy) {
              setDialog(null);
            }
          }}
        />
      )}
    </TableContainer>
  );
}

/**
 * One line: «Порядок» and the handle on a top-level row (blank on a nested
 * one), ID, «Тип», «Заголовок» — indented 24 px per tier below the top, then
 * the expand control or a 28 px gap, then the link to the form — «Родитель»,
 * «Исполнитель», «Статус», and on «Фичи» and «Истории» «Ещё действия» on a
 * top-level row (BR-M4-14; feat-001 spec § Page «Бэклоги»). Memoized, so a
 * drop line moving over a long backlog redraws two rows, not all of them.
 *
 * The handle is an `IconButton` drawn as a `span` (MUI keeps it a `button`
 * by role, focusable, Enter and Space opening its menu): a native `button`
 * cannot be the source of the browser's own drag in every browser.
 */
const BacklogLine = memo(function BacklogLine({
  item,
  order,
  tier,
  block,
  line,
  expanded,
  off,
  withActions,
  menuId,
  actionsId,
  formPath,
  parentPath,
  onToggle,
  onOpenMenu,
  onOpenActions,
  onDragStart,
  onDragEnd,
  registerHandle,
  registerActions,
}: {
  item: BacklogTreeItem;
  /** The row's «Порядок»; `null` on a nested row, which has none. */
  order: number | null;
  tier: number;
  block: number;
  line: LineEdge | null;
  expanded: boolean;
  /** A write is being saved: the handle and «Ещё действия» are off. */
  off: boolean;
  /** The level has the «Действия» column. */
  withActions: boolean;
  /** The open move menu's id when it is this row's. */
  menuId: string | null;
  /** The open row menu's id when it is this row's. */
  actionsId: string | null;
  formPath: string;
  /** The parent's form, `null` with no parent to show. */
  parentPath: string | null;
  onToggle(id: string): void;
  onOpenMenu(event: MouseEvent<HTMLElement>, id: string): void;
  onOpenActions(event: MouseEvent<HTMLElement> | KeyboardEvent<HTMLElement>, id: string): void;
  onDragStart(event: DragEvent<HTMLElement>, id: string, identifier: string): void;
  onDragEnd(): void;
  registerHandle(id: string, element: HTMLElement | null): void;
  registerActions(id: string, element: HTMLElement | null): void;
}) {
  const top = order !== null;
  return (
    <TableRow
      hover
      data-block={block}
      data-tier={tier}
      data-drop-line={line ?? undefined}
      sx={line ? dropLineSx(line) : undefined}
    >
      <TableCell>{order}</TableCell>
      <TableCell padding="checkbox">
        {top && (
          <IconButton
            component="span"
            size="small"
            ref={(element: HTMLElement | null) => registerHandle(item.id, element)}
            aria-label={`Переместить ${item.identifier}`}
            aria-haspopup="menu"
            aria-expanded={menuId !== null ? 'true' : 'false'}
            aria-controls={menuId ?? undefined}
            aria-disabled={off ? 'true' : undefined}
            draggable={!off}
            onClick={(event: MouseEvent<HTMLElement>) => onOpenMenu(event, item.id)}
            onDragStart={(event: DragEvent<HTMLElement>) =>
              onDragStart(event, item.id, item.identifier)
            }
            onDragEnd={onDragEnd}
            sx={{ cursor: off ? 'default' : 'grab', color: off ? 'text.disabled' : undefined }}
          >
            <DragIndicatorIcon fontSize="small" />
          </IconButton>
        )}
      </TableCell>
      <TableCell sx={{ whiteSpace: 'nowrap' }}>{item.identifier}</TableCell>
      <TableCell>
        <TypeName type={item.type} />
      </TableCell>
      <TableCell sx={{ overflowWrap: 'anywhere' }}>
        <Box sx={{ display: 'flex', alignItems: 'center', gap: 0.5, pl: tier * 3 }}>
          {hasChildren(item) ? (
            <IconButton
              size="small"
              aria-label={`${expanded ? 'Свернуть' : 'Развернуть'} ${item.identifier}`}
              aria-expanded={expanded ? 'true' : 'false'}
              onClick={() => onToggle(item.id)}
              sx={{ flexShrink: 0 }}
            >
              {expanded ? (
                <ExpandMoreIcon fontSize="small" />
              ) : (
                <ChevronRightIcon fontSize="small" />
              )}
            </IconButton>
          ) : (
            <Box
              aria-hidden="true"
              sx={(theme) => ({ width: theme.spacing(3.5), flexShrink: 0 })}
            />
          )}
          <Link component={RouterLink} to={formPath}>
            {item.title}
          </Link>
        </Box>
      </TableCell>
      <TableCell sx={{ whiteSpace: 'nowrap' }}>
        {item.parent && parentPath ? (
          <Tooltip title={item.parent.title} describeChild>
            <Link component={RouterLink} to={parentPath}>
              {item.parent.identifier}
            </Link>
          </Tooltip>
        ) : (
          <Typography component="span" variant="body2" color="text.secondary">
            {NO_PARENT_TEXT}
          </Typography>
        )}
      </TableCell>
      <TableCell>
        <AssigneeName assignee={item.assignee} />
      </TableCell>
      <TableCell>
        <StateName state={item.state} />
      </TableCell>
      {withActions && (
        <TableCell padding="checkbox">
          {top && (
            <IconButton
              size="small"
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
              sx={{ color: off ? 'text.disabled' : undefined }}
            >
              <MoreHorizIcon fontSize="small" />
            </IconButton>
          )}
        </TableCell>
      )}
    </TableRow>
  );
});
