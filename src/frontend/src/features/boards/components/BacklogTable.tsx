import {
  Fragment,
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
import Divider from '@mui/material/Divider';
import ListItemIcon from '@mui/material/ListItemIcon';
import ListItemText from '@mui/material/ListItemText';
import ListSubheader from '@mui/material/ListSubheader';
import Menu from '@mui/material/Menu';
import MenuItem from '@mui/material/MenuItem';
import Paper from '@mui/material/Paper';
import Table from '@mui/material/Table';
import TableBody from '@mui/material/TableBody';
import TableContainer from '@mui/material/TableContainer';
import { useTheme } from '@mui/material/styles';
import AccountTreeOutlinedIcon from '@mui/icons-material/AccountTreeOutlined';
import LinkOffIcon from '@mui/icons-material/LinkOff';
import { useQueryClient } from '@tanstack/react-query';
import { useProcessDefinition } from '../../process';
import {
  allowedParentTypes,
  newRequestId,
  parentHelperText,
  placeRefusal,
  useCreateWorkItem,
  useSetWorkItemParent,
  useWorkItemDialog,
  workItemPath,
  type ParentWriteResult,
  type WorkItem,
  type WorkItemRef,
} from '../../workitems';
import { TypeMark } from '../../../shared/ui/WorkTrackingMarks';
import type {
  Backlog,
  BacklogRow as BacklogRowData,
  BacklogTreeItem,
  ReorderBody,
} from '../api/boardsApi';
import { boardsKeys } from '../api/queryKeys';
import { useReorderBacklog } from '../hooks/mutations';
import { useReadTeamAgain } from '../hooks/queries';
import { useMoveView } from '../hooks/useMoveView';
import { addEntriesOf, addEntryLabel, type AddEntry } from '../lib/addChild';
import {
  descendantIds,
  dropVerdict,
  refusalNotice,
  typeRanks,
  type DropVerdict,
} from '../lib/drop';
import type { BoardLevelCode } from '../lib/levels';
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
import {
  collapseOneLevel,
  drawnCount,
  expandOneLevel,
  expandedWith,
  firstExpansion,
  pathTo,
  toggled,
  treeLines,
} from '../lib/tree';
import { AddChildRow, type AddCancel } from './AddChildRow';
import { BacklogHeadRow } from './BacklogHeadRow';
import { BacklogRow } from './BacklogRow';
import type { DropTargetCue } from './backlogRowStyle';
import { ChangeParentDialog } from './ChangeParentDialog';

/** The row menu's entries (feat-001 spec § Page «Бэклоги», «Действия»). */
const CHANGE_PARENT_LABEL = 'Изменить родителя…';
const CLEAR_PARENT_LABEL = 'Убрать родителя';
/** The subheader of the moves a top-level row's «⋮» offers (feat-027 design § 4.7). */
const MOVE_HEADING = 'Переместить';

/** A type with no child to offer: the row draws no «+». */
const NO_ENTRIES: readonly AddEntry[] = [];

/** The appearances whose entries the table resolves, once per process read. */
const APPEARANCES = ['epic', 'feature', 'story', 'bug', 'task'] as const;

/** A drag of a row, by any point of it (US-5, D-11): what it carries, and what is under the pointer. */
interface DragState {
  id: string;
  identifier: string;
  /** The parent the page showed when the drag began: the parent write's seen parent (BR-H-4). */
  seenParentId: string | null;
  /** Every descendant of the dragged row in the read, collapsed ones included (BR-T-12). */
  descendants: ReadonlySet<string>;
  /** The drop line's gap between top-level blocks: 0 above the first … n below the last; `null` off the rows. */
  gap: number | null;
  /** The row under the pointer that is a target, with how it is framed; `null` over none. */
  over: { id: string; cue: DropTargetCue } | null;
  /**
   * The dragged row is dimmed from the first pointer move over the table, not
   * from the start of the drag, so the drag image the browser takes at the start
   * is the row as it was.
   */
  dim: boolean;
}

/** What the pointer is over during a drag: the drop line's gap, or a row and what dropping there does. */
type Placement =
  { kind: 'gap'; gap: number } | { kind: 'row'; row: BacklogTreeItem; verdict: DropVerdict };

/** No type is ranked until the process is read: the server then decides what a drop may do. */
const NO_RANKS: ReadonlyMap<string, number> = new Map();

/** A row's «⋮» while its menu is open. */
interface ActionsMenuState {
  anchor: HTMLElement;
  /** The row as it was when the menu opened: its entries decide on that parent (BR-H-4). */
  row: BacklogTreeItem;
}

/** A row's «+» with several entries while its menu is open. */
interface AddMenuState {
  anchor: HTMLElement;
  row: BacklogTreeItem;
  entries: readonly AddEntry[];
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
  row: BacklogTreeItem;
  chosen: WorkItemRef | null;
  busy: boolean;
  fieldError?: string;
  alert: string | null;
}

/** The inline add row while it is open (BR-T-10); one at a time. */
interface AddingState {
  /** Which opening this is: every one is a fresh row, with a fresh `requestId` (BR-M3-26). */
  serial: number;
  parentId: string;
  parentIdentifier: string;
  entry: AddEntry;
  /** The create is being saved. */
  busy: boolean;
  /** A refusal shown at the field. */
  error: string | null;
  /** A create was refused: closing the row reads the backlog again, so a parent that is gone leaves it. */
  refused: boolean;
  /** The row takes focus the next time it is drawn: it has just been opened, or the user asked for it back (F-7). */
  focus: boolean;
  /** The title typed so far: a second add row does not open while it holds text, and the row drawn again starts from it. */
  title: string;
}

/**
 * The backlog's table (feat-027 US-1..US-4, feat-001 US-4, US-5; design § 4):
 * the whole backlog as a tree at full depth, no paging, drawn as the reference
 * screen draws it. The first state is expanded as deep as 2 000 drawn rows
 * allow (BR-T-7); what the user expands or collapses afterwards is kept here,
 * by item, so it survives the page's re-reads, and the page keys the table by
 * its level and team, so another of either — or a reload — starts again. The
 * hierarchy header's two buttons open or close one level of the whole table
 * (BR-T-8), a row's «+» adds a child inline (BR-T-9, BR-T-10), a click selects
 * a row.
 *
 * A move — «⋮»'s «Переместить» entries or a pointer drag of the row, the
 * browser's own drag and drop — takes a top-level row with its shown children:
 * the drop line stands only between top-level blocks (the upper quarter of a
 * block's first row, the lower of its last), and «Выше» / «Ниже» step over
 * blocks. The block shows at its new place at once with «Порядок» renumbered,
 * keeps focus on its «⋮» and is announced; then the backlog is read again. A
 * refusal puts the row back and shows its notice.
 *
 * A drag of any row, top-level or nested, dropped onto a row is the parent write
 * (US-5): the row becomes the target's child (BR-T-11). The target is framed as
 * allowed or, with a dashed red frame, refused — a type BR-H-2 does not allow
 * above the item's, or one of its own descendants — and a refused release sends
 * nothing and says why; the item itself and its current parent are no target
 * (BR-T-12). Accepted, the backlog is read again, the new parent is expanded
 * with the item beneath it, selected and focused on its «⋮», and the notice
 * follows.
 *
 * «⋮» also offers «Изменить родителя…» — the dialog, searching every type BR-H-2
 * allows above the row's own — and «Убрать родителя», on every row. Either is
 * the parent write, then the backlog is read again, before the notice. Either
 * names as seen the parent the row had when its menu opened — the dialog keeps
 * it while open — so a change read in meanwhile is refused with
 * `parent_changed`, not overwritten (BR-H-4).
 *
 * One write at a time: while a move, a parent write or a create is saved every
 * row is not draggable and every «+», «⋮» and both header buttons is off
 * (`aria-disabled`), so focus can stay where it is (BR-T-15).
 */
export function BacklogTable({
  organizationId,
  projectId,
  teamId,
  level,
  items,
  announce,
  showSuccess,
  showCreated,
  showError,
}: {
  organizationId: string;
  projectId: string;
  teamId: string;
  level: BoardLevelCode;
  /** The backlog as last read: the top-level rows, `order` 1..n, each with its whole tree. */
  items: readonly BacklogRowData[];
  announce(text: string): void;
  /** A parent write's notice (feat-001 spec § Notices on «Бэклоги»). */
  showSuccess(text: string): void;
  /** mod-003's plain notice of a created item. */
  showCreated(text: string): void;
  showError(text: string): void;
}) {
  const theme = useTheme();
  const table = theme.layout.backlogTable;
  const client = useQueryClient();
  const reorder = useReorderBacklog(organizationId, projectId, teamId, level);
  const setParent = useSetWorkItemParent(organizationId, projectId);
  const createItem = useCreateWorkItem(organizationId, projectId);
  const readAgain = useReadTeamAgain(organizationId, projectId, teamId);
  // A title's plain click opens the card as a dialog over the backlog (feat-029 BR-C-1);
  // `open` keeps its identity, so the memoised rows are not redrawn by the navigation.
  const { open: openItem } = useWorkItemDialog();
  const process = useProcessDefinition(organizationId);
  const view = useMoveView(items);
  const [actions, setActions] = useState<ActionsMenuState | null>(null);
  const [addMenu, setAddMenu] = useState<AddMenuState | null>(null);
  const [adding, setAdding] = useState<AddingState | null>(null);
  const [dialog, setDialog] = useState<ParentDialogState | null>(null);
  const [drag, setDrag] = useState<DragState | null>(null);
  // What the user has expanded and collapsed, or `null` while nothing is touched: the table
  // then shows the first state of the answer it has now, so a read that lands after the table
  // mounted on a cached one (a level or team visited before) opens the rows that gained
  // children meanwhile (F-2). A touch keeps its set, across the page's re-reads.
  const [chosen, setChosen] = useState<ReadonlySet<string> | null>(null);
  const first = useMemo(() => firstExpansion(items), [items]);
  const expanded = chosen ?? first;
  const firstRef = useRef(first);
  useLayoutEffect(() => {
    firstRef.current = first;
  }, [first]);
  /**
   * A change to the expanded set, made on what is shown now. One that changes nothing
   * (the row to open is open already) is no touch.
   */
  const changeExpanded = useCallback(
    (change: (open: ReadonlySet<string>) => ReadonlySet<string>) =>
      setChosen((current) => {
        const shown = current ?? firstRef.current;
        const next = change(shown);
        return current === null && next === shown ? null : next;
      }),
    [],
  );
  const [selectedId, setSelectedId] = useState<string | null>(null);
  /** A parent write from the dialog is being saved. */
  const [writing, setWriting] = useState(false);
  const tableRef = useRef<HTMLDivElement>(null);
  const actionButtons = useRef(new Map<string, HTMLElement>());
  const addButtons = useRef(new Map<string, HTMLElement>());
  const addField = useRef<HTMLInputElement | null>(null);
  /**
   * The `requestId` of the open add row's retries (BR-M3-26), made at its first Enter.
   * Here and not in the row, which is not drawn while an ancestor of its parent is
   * collapsed and must come back with it (F-7).
   */
  const addRequestId = useRef<string | null>(null);
  const addSerial = useRef(0);
  const addingRef = useRef<AddingState | null>(null);
  /** Whether the open add row is drawn now: its parent is among the lines. */
  const addDrawn = useRef(false);
  /** The moved row's id until its move is saved: its «⋮» keeps focus. */
  const pendingFocus = useRef<string | null>(null);
  /** The row whose «⋮» takes focus back once its menu, dialog and write are done. */
  const pendingActionFocus = useRef<string | null>(null);
  /** The row whose «+» takes focus back once its add row has closed. */
  const pendingAddFocus = useRef<string | null>(null);
  /** The identifier of a created row to bring into view once it is drawn. */
  const pendingScroll = useRef<string | null>(null);
  const inFlight = useRef(false);
  const actionsMenuId = useId();
  const addMenuId = useId();
  const moveHeadingId = useId();

  const { saving, shown: rows } = view;
  const rowsRef = useRef(rows);
  const lines = useMemo(() => treeLines(rows, expanded), [rows, expanded]);
  // The rows a drag can land on: the drawn ones, by identifier; and the last drawn row of each
  // top-level block, whose lower quarter is the drop line below the block.
  const drawn = useMemo(
    () => new Map(lines.map(({ item }) => [item.identifier, item] as const)),
    [lines],
  );
  const blockEnds = useMemo(
    () => new Map(lines.map(({ item, block }) => [block, item.identifier] as const)),
    [lines],
  );
  const ranks = useMemo(() => (process.data ? typeRanks(process.data) : NO_RANKS), [process.data]);
  // An add row lives while its parent is in the read — drawn, or folded away by a collapsed
  // ancestor, with its title and `requestId` kept (F-7) — or while its create is saved. One
  // whose parent has gone from a read is gone with it, so it cannot hold the one place there is.
  const addParentId = adding?.parentId ?? null;
  const addParentPresent = useMemo(
    () => addParentId !== null && findInTree(rows, addParentId) !== undefined,
    [rows, addParentId],
  );
  const addRow = adding !== null && (adding.busy || addParentPresent) ? adding : null;
  const addRowDrawn = addRow !== null && lines.some((line) => line.item.id === addRow.parentId);
  const off = saving || writing || addRow?.busy === true;

  // What a row's «+» offers, once per type appearance; none until the process is read.
  const entriesByAppearance = useMemo(() => {
    const types = process.data?.workItemTypes ?? [];
    return new Map<string, readonly AddEntry[]>(
      APPEARANCES.map((appearance) => [appearance, addEntriesOf(appearance, types)]),
    );
  }, [process.data]);

  const expandNext = useMemo(() => expandOneLevel(rows, expanded), [rows, expanded]);
  const collapseNext = useMemo(() => collapseOneLevel(rows, expanded), [rows, expanded]);

  // A moved row's node may be taken out and put back by the reorder, which
  // drops focus to the page: it goes back to the row's «Ещё действия».
  useLayoutEffect(() => {
    const id = pendingFocus.current;
    if (id === null) {
      return;
    }
    const button = actionButtons.current.get(id);
    const active = document.activeElement;
    if (button && (active === null || active === document.body)) {
      button.focus();
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

  // Once an add row has closed, focus is on the «+» that opened it (design § 4.8).
  useEffect(() => {
    if (addRow !== null) {
      return;
    }
    const opener = pendingAddFocus.current;
    if (opener !== null) {
      const button = addButtons.current.get(opener);
      if (button) {
        button.focus();
        pendingAddFocus.current = null;
      }
    }
  }, [addRow, rows, lines]);

  // A created or moved row is brought into view once it is drawn, whether or not an add
  // row is open. Whoever asks for the scroll also expands the row's ancestors in the same
  // update, and asks only for a row the read has, so the request is met by the render that
  // draws the row and does not wait for a later, unrelated expand.
  useEffect(() => {
    const target = pendingScroll.current;
    if (target === null) {
      return;
    }
    const row = tableRef.current?.querySelector<HTMLElement>(`[data-identifier="${target}"]`);
    if (row) {
      row.scrollIntoView?.({ block: 'nearest' });
      pendingScroll.current = null;
    }
  }, [rows, lines]);

  // The handlers read the add row that lives, drawn or folded away, not a state that
  // outlived its parent.
  useLayoutEffect(() => {
    addingRef.current = addRow;
    addDrawn.current = addRowDrawn;
  }, [addRow, addRowDrawn]);

  // The rows as drawn from now on, for handlers that are not made again with each render.
  useLayoutEffect(() => {
    rowsRef.current = rows;
  }, [rows]);

  function backlogRead(): BacklogRowData[] | undefined {
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

  /** «Убрать родителя»: the write, the read, then the notice (US-5, US-6). */
  async function clearParent(row: BacklogTreeItem) {
    const seen = row.parent;
    if (inFlight.current || seen === null) {
      return;
    }
    inFlight.current = true;
    pendingActionFocus.current = row.id;
    // The rows as shown: this puts the table into its saving state, controls off.
    view.begin(rows);
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
      // The row stands beneath its new parent on the read: every row on its way down from the
      // top — the new parent and the ancestors it may be folded away under — is expanded, so
      // the row is drawn, selected and in view, and «⋮» can take focus back (design § 4.7). A
      // row the read no longer has (its team is not this page's) is left to leave the page.
      const read = backlogRead();
      const way = pathTo(read, row.id);
      if (findInTree(read, chosen.id) && way.length > 0) {
        changeExpanded((open) => expandedWith(open, way.slice(0, -1)));
        setSelectedId(row.id);
        pendingScroll.current = row.identifier;
      }
      setDialog(null);
      showSuccess(
        `У ${row.identifier} теперь родитель ${written.parent?.identifier ?? chosen.identifier}.`,
      );
      return;
    }
    // After `parent_changed` the read shows the stored parent: it is preselected,
    // and the dialog's row becomes that read. After any other refusal the
    // dialog still shows the parent it had, and keeps it.
    const fresh = refusal?.parentChanged ? findInTree(backlogRead(), row.id) : undefined;
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

  // ---- the inline add row (BR-T-10)

  /**
   * Draws the open add row again when a collapsed ancestor of its parent has folded
   * it away: the rows on the way down to its parent are expanded and its field takes
   * focus once it is there (F-7).
   */
  const revealAdd = useCallback(
    (parentId: string) => {
      const way = pathTo(rowsRef.current, parentId);
      setAdding((open) => (open && !open.focus ? { ...open, focus: true } : open));
      changeExpanded((open) => expandedWith(open, way));
    },
    [changeExpanded],
  );

  /**
   * Whether the open add row is to stay: it holds text, or its create is being
   * saved. Then no other row opens — focus goes back to its field instead
   * (design § 4.8), the row drawn again first if a collapsed ancestor has folded
   * it away.
   */
  const addRowStays = useCallback(() => {
    const current = addingRef.current;
    if (current !== null && (current.busy || current.title.trim() !== '')) {
      if (addDrawn.current) {
        addField.current?.focus();
      } else {
        revealAdd(current.parentId);
      }
      return true;
    }
    return false;
  }, [revealAdd]);

  /** Opens the add row beneath `row`, its parent expanded; one at a time (design § 4.8). */
  const openAdd = useCallback(
    (row: BacklogTreeItem, entry: AddEntry) => {
      if (addRowStays()) {
        return;
      }
      // Every opening is a fresh row: no title, and a `requestId` of its own (BR-M3-26).
      addRequestId.current = null;
      pendingScroll.current = null;
      setSelectedId(null);
      changeExpanded((open) => expandedWith(open, [row.id]));
      addSerial.current += 1;
      setAdding({
        serial: addSerial.current,
        parentId: row.id,
        parentIdentifier: row.identifier,
        entry,
        busy: false,
        error: null,
        refused: false,
        focus: true,
        title: '',
      });
    },
    [addRowStays, changeExpanded],
  );

  function closeAdd(how: AddCancel) {
    const current = addingRef.current;
    if (current === null || current.busy) {
      return;
    }
    // A refused row read the backlog again when it closes, so a parent that is gone leaves the page.
    if (current.refused) {
      void readAgain();
    }
    if (how !== 'blur') {
      pendingAddFocus.current = current.parentId;
    }
    setAdding(null);
  }

  /** Enter in the add row: mod-003's create with the parent, one `requestId` per opening (BR-M3-26). */
  async function submitAdd(title: string) {
    const current = addingRef.current;
    if (current === null || inFlight.current) {
      return;
    }
    addRequestId.current ??= newRequestId();
    const requestId = addRequestId.current;
    inFlight.current = true;
    setAdding({ ...current, busy: true, error: null });
    let created: WorkItem | null = null;
    let error: string | null = null;
    let contextGone = false;
    try {
      created = await createItem.mutateAsync({
        requestId,
        typeId: current.entry.typeId,
        title,
        parentId: current.parentId,
      });
    } catch (caught) {
      const placed = placeRefusal(caught, ['title', 'parent']);
      error = placed.fields.title ?? placed.fields.parent ?? placed.alert;
      contextGone = placed.frame !== null;
    }
    if (created === null) {
      inFlight.current = false;
      setAdding((open) => open && { ...open, busy: false, error, refused: true });
      // The refusal shows at the field and the title is kept: a row folded away meanwhile
      // by a collapsed ancestor is drawn again with it, not left to hide it (F-7).
      if (addDrawn.current) {
        addField.current?.focus();
      } else {
        revealAdd(current.parentId);
      }
      if (contextGone) {
        // The team, project or organization is gone: the read shows its frame.
        void readAgain();
      }
      return;
    }
    await readAgain();
    inFlight.current = false;
    pendingAddFocus.current = current.parentId;
    pendingScroll.current = created.identifier;
    // The parent and every ancestor above it are open, so the new row is drawn.
    const way = pathTo(backlogRead(), current.parentId);
    changeExpanded((open) => expandedWith(open, way.length > 0 ? way : [current.parentId]));
    setSelectedId(created.id);
    setAdding(null);
    showCreated(`Рабочий элемент ${created.identifier} создан.`);
  }

  /** The table keeps the title typed, and typing again takes the refusal off the field. */
  const handleAddEdit = useCallback(
    (title: string) => setAdding((open) => open && { ...open, title, error: null }),
    [],
  );

  /** The field has taken the focus it was asked for: it is not asked again when the row is drawn again. */
  const handleAddFocused = useCallback(
    () => setAdding((open) => (open && open.focus ? { ...open, focus: false } : open)),
    [],
  );

  const registerActions = useCallback((id: string, element: HTMLElement | null) => {
    if (element) {
      actionButtons.current.set(id, element);
    } else {
      actionButtons.current.delete(id);
    }
  }, []);

  const registerAdd = useCallback((id: string, element: HTMLElement | null) => {
    if (element) {
      addButtons.current.set(id, element);
    } else {
      addButtons.current.delete(id);
    }
  }, []);

  const openActions = useCallback(
    (event: MouseEvent<HTMLElement> | KeyboardEvent<HTMLElement>, id: string) => {
      const row = findInTree(rows, id);
      if (!off && row) {
        setActions({ anchor: event.currentTarget, row });
      }
    },
    [off, rows],
  );

  const handleAdd = useCallback(
    (
      event: MouseEvent<HTMLElement> | KeyboardEvent<HTMLElement>,
      row: BacklogTreeItem,
      entries: readonly AddEntry[],
    ) => {
      const [only] = entries;
      if (off || !only || addRowStays()) {
        return;
      }
      if (entries.length === 1) {
        openAdd(row, only);
      } else {
        setAddMenu({ anchor: event.currentTarget, row, entries });
      }
    },
    [off, openAdd, addRowStays],
  );

  const select = useCallback((id: string) => setSelectedId(id), []);
  const toggle = useCallback(
    (id: string) => changeExpanded((current) => toggled(current, id)),
    [changeExpanded],
  );

  /** «Развернуть на один уровень» / «Свернуть на один уровень» (BR-T-8). */
  function showLevel(next: ReadonlySet<string> | null) {
    if (off || next === null) {
      return;
    }
    setChosen(next);
    announce(`Показано строк: ${drawnCount(rows, next)}.`);
  }

  const startDrag = useCallback(
    (event: DragEvent<HTMLElement>, id: string, identifier: string) => {
      const dragged = off ? undefined : findInTree(rows, id);
      if (!dragged) {
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
      // Starting a drag selects the dragged row (design § 4.6).
      setSelectedId(id);
      setDrag({
        id,
        identifier,
        seenParentId: dragged.parent?.id ?? null,
        descendants: descendantIds(dragged),
        gap: null,
        over: null,
        dim: false,
      });
    },
    [off, rows],
  );

  const endDrag = useCallback(() => setDrag(null), []);

  /**
   * What the pointer is over (design § 4.9): on a top-level row's drag, the upper
   * quarter of a block's first row and the lower quarter of its last are the drop
   * line between blocks; anything else on a row of the page is that row as a
   * target — the item itself and its current parent none, any other allowed or
   * refused (BR-T-12). A nested row's drag has row targets only (BR-T-13). `null`
   * over no row of an item.
   */
  function placementAt(event: DragEvent<HTMLElement>, dragState: DragState): Placement | null {
    const element = (event.target as HTMLElement).closest<HTMLElement>('tr[data-identifier]');
    const identifier = element?.dataset.identifier;
    const row = identifier === undefined ? undefined : drawn.get(identifier);
    const dragged = findInTree(rows, dragState.id);
    if (!element || !identifier || !row || !dragged) {
      return null;
    }
    if (rows.some((top) => top.id === dragState.id)) {
      const box = element.getBoundingClientRect();
      const block = Number(element.dataset.block);
      if (row.id === rows[block]?.id && event.clientY < box.top + box.height / 4) {
        return { kind: 'gap', gap: block };
      }
      if (blockEnds.get(block) === identifier && event.clientY > box.bottom - box.height / 4) {
        return { kind: 'gap', gap: block + 1 };
      }
    }
    return {
      kind: 'row',
      row,
      verdict: dropVerdict(dragged, row, { ranks, descendants: dragState.descendants }),
    };
  }

  function dragOver(event: DragEvent<HTMLElement>) {
    if (drag === null || off) {
      return;
    }
    const placement = placementAt(event, drag);
    let gap: number | null = null;
    let over: DragState['over'] = null;
    if (placement?.kind === 'gap') {
      gap = placement.gap;
    } else if (placement?.verdict.kind === 'allowed') {
      over = { id: placement.row.id, cue: 'allowed' };
    } else if (placement?.verdict.kind === 'refused') {
      over = { id: placement.row.id, cue: 'refused' };
    }
    // A place the row cannot be dropped on — the item itself, its parent, no row — takes no drop.
    if (gap !== null || over !== null) {
      event.preventDefault();
      if (event.dataTransfer) {
        event.dataTransfer.dropEffect = 'move';
      }
    }
    if (
      gap !== drag.gap ||
      over?.id !== drag.over?.id ||
      over?.cue !== drag.over?.cue ||
      !drag.dim
    ) {
      setDrag({ ...drag, gap, over, dim: true });
    }
  }

  function dragLeave(event: DragEvent<HTMLElement>) {
    const next = event.relatedTarget as Node | null;
    if (drag !== null && (next === null || !event.currentTarget.contains(next))) {
      setDrag({ ...drag, gap: null, over: null });
    }
  }

  function drop(event: DragEvent<HTMLElement>) {
    if (drag === null) {
      return;
    }
    event.preventDefault();
    const dragged = findInTree(rows, drag.id);
    const placement = off || !dragged ? null : placementAt(event, drag);
    setDrag(null);
    if (placement?.kind === 'gap') {
      const body = dropMove(
        rows,
        rows.findIndex((row) => row.id === drag.id),
        placement.gap,
      );
      if (body) {
        void move(body);
      }
    } else if (placement?.verdict.kind === 'refused' && dragged) {
      // Nothing is sent; the notice names the reason (BR-T-12).
      showError(refusalNotice(placement.verdict.reason, dragged, placement.row));
    } else if (placement?.verdict.kind === 'allowed') {
      void dropOnto(drag, placement.row);
    }
  }

  /** A drop onto a row: the parent write, the read, then the notice (US-5; BR-T-11). */
  async function dropOnto(dragged: DragState, target: BacklogTreeItem) {
    if (inFlight.current) {
      return;
    }
    inFlight.current = true;
    pendingActionFocus.current = dragged.id;
    // The rows as shown: this puts the table into its saving state, controls off.
    view.begin(rows);
    let written: ParentWriteResult | null = null;
    let refusal: ParentRefusal | null = null;
    try {
      written = await setParent.mutateAsync({
        identifier: dragged.identifier,
        change: { parentId: target.id, seenParentId: dragged.seenParentId },
      });
    } catch (error) {
      refusal = parentRefusal(error);
    }
    // The backlog is read again whatever the answer, before the notice.
    await readAgain();
    inFlight.current = false;
    if (written !== null) {
      // The item stands beneath its new parent, which shows it, selected and in view.
      changeExpanded((open) => expandedWith(open, [target.id]));
      setSelectedId(dragged.id);
      pendingScroll.current = dragged.identifier;
    }
    view.end(backlogRead(), items);
    if (written !== null) {
      showSuccess(
        `У ${dragged.identifier} теперь родитель ${written.parent?.identifier ?? target.identifier}.`,
      );
    } else if (refusal?.notice) {
      showError(refusal.notice);
    }
  }

  /** The open «⋮» menu's row as it opened, while it is still in the backlog. */
  const actionsRow = actions && findInTree(rows, actions.row.id) ? actions.row : undefined;
  const actionsIndex = actionsRow ? rows.findIndex((row) => row.id === actionsRow.id) : -1;

  function chooseMove(entry: MoveEntry) {
    const body = entryMove(rows, actionsIndex, entry);
    setActions(null);
    if (body) {
      void move(body);
    }
  }

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

  function chooseAdd(entry: AddEntry) {
    const row = addMenu?.row;
    setAddMenu(null);
    if (row) {
      openAdd(row, entry);
    }
  }

  const dragFrom = drag ? rows.findIndex((row) => row.id === drag.id) : -1;
  const lineGap =
    drag && drag.gap !== null && isEffectiveGap(rows.length, dragFrom, drag.gap) ? drag.gap : null;
  const fixedWidth =
    table.hierarchyColumn +
    table.orderColumn +
    table.typeColumn +
    table.stateColumn +
    table.effortColumn +
    table.businessValueColumn +
    table.valueAreaColumn;
  // «Родителем может быть: …»: every type BR-H-2 allows above the row's own, as the form names them.
  const dialogHelper =
    dialog && process.data
      ? parentHelperText(allowedParentTypes(process.data, dialog.row.type.id))
      : null;

  return (
    <TableContainer
      component={Paper}
      elevation={0}
      ref={tableRef}
      sx={{
        boxShadow: theme.cardShadow,
        overflow: 'auto',
        // The card is the containing block of the rows' visually hidden descriptions
        // (`position: absolute`): without it they sit at their place in the page, below
        // the card, and stretch the document by the table's whole height (F-3).
        position: 'relative',
        maxHeight: `calc(100dvh - ${table.viewportOffset}px)`,
        minHeight: 240,
        // The sticky header covers the top of the scrollport: a row focused or scrolled
        // to by the browser stays clear of it (F-10).
        scrollPaddingTop: theme.density.tabHeight,
      }}
    >
      <Table
        size="small"
        stickyHeader
        aria-label="Бэклог"
        sx={{ tableLayout: 'fixed', width: '100%', minWidth: fixedWidth + table.titleColumnMin }}
      >
        <colgroup>
          <col style={{ width: table.hierarchyColumn }} />
          <col style={{ width: table.orderColumn }} />
          <col style={{ width: table.typeColumn }} />
          <col />
          <col style={{ width: table.stateColumn }} />
          <col style={{ width: table.effortColumn }} />
          <col style={{ width: table.businessValueColumn }} />
          <col style={{ width: table.valueAreaColumn }} />
        </colgroup>
        <BacklogHeadRow
          canExpand={expandNext !== null && !off}
          canCollapse={collapseNext !== null && !off}
          onExpand={() => showLevel(expandNext)}
          onCollapse={() => showLevel(collapseNext)}
        />
        <TableBody
          onDragEnter={dragOver}
          onDragOver={dragOver}
          onDragLeave={dragLeave}
          onDrop={drop}
        >
          {lines.map(({ item, tier, block }, index) => {
            const top = tier === 0;
            return (
              <Fragment key={item.id}>
                <BacklogRow
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
                  dropTarget={drag?.over?.id === item.id ? drag.over.cue : null}
                  dragging={drag?.dim === true && drag.id === item.id}
                  expanded={expanded.has(item.id)}
                  selected={selectedId === item.id}
                  off={off}
                  addEntries={entriesByAppearance.get(item.type.appearance) ?? NO_ENTRIES}
                  addMenuId={addMenu?.row.id === item.id ? addMenuId : null}
                  actionsId={actions?.row.id === item.id ? actionsMenuId : null}
                  formPath={workItemPath(organizationId, projectId, item.identifier)}
                  onOpenItem={openItem}
                  onSelect={select}
                  onToggle={toggle}
                  onAdd={handleAdd}
                  onOpenActions={openActions}
                  onDragStart={startDrag}
                  onDragEnd={endDrag}
                  registerActions={registerActions}
                  registerAdd={registerAdd}
                />
                {addRow?.parentId === item.id && (
                  <AddChildRow
                    key={addRow.serial}
                    parentIdentifier={addRow.parentIdentifier}
                    entry={addRow.entry}
                    tier={tier + 1}
                    block={block}
                    busy={addRow.busy}
                    error={addRow.error}
                    focusOnMount={addRow.focus}
                    initialTitle={addRow.title}
                    inputRef={addField}
                    onFocused={handleAddFocused}
                    onEdit={handleAddEdit}
                    onSubmit={(title) => void submitAdd(title)}
                    onCancel={closeAdd}
                  />
                )}
              </Fragment>
            );
          })}
        </TableBody>
      </Table>
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
        {actionsIndex >= 0 && (
          <ListSubheader id={moveHeadingId} role="presentation">
            {MOVE_HEADING}
          </ListSubheader>
        )}
        {actionsIndex >= 0 &&
          MOVE_ENTRIES.map((entry) => (
            <MenuItem
              key={entry}
              // The visual heading is not spoken while the arrows walk the items, so the
              // items carry it as their description: «В начало» is heard as a move (F-12).
              aria-describedby={moveHeadingId}
              disabled={!isEntryEnabled(entry, actionsIndex, rows.length)}
              onClick={() => chooseMove(entry)}
            >
              {MOVE_ENTRY_LABELS[entry]}
            </MenuItem>
          ))}
        {actionsIndex >= 0 && <Divider />}
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
      <Menu
        id={addMenuId}
        anchorEl={addMenu?.anchor ?? null}
        open={addMenu !== null}
        onClose={() => setAddMenu(null)}
        slotProps={{
          list: {
            'aria-label': addMenu
              ? `Добавить дочерний элемент (${addMenu.row.identifier})`
              : undefined,
          },
        }}
      >
        {(addMenu?.entries ?? NO_ENTRIES).map((entry) => (
          <MenuItem key={entry.typeId} onClick={() => chooseAdd(entry)}>
            <ListItemIcon>
              <TypeMark appearance={entry.appearance} />
            </ListItemIcon>
            <ListItemText>{addEntryLabel(entry)}</ListItemText>
          </MenuItem>
        ))}
      </Menu>
      {dialog && (
        <ChangeParentDialog
          organizationId={organizationId}
          projectId={projectId}
          row={dialog.row}
          helperText={dialogHelper}
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

/** The row `id` anywhere in the tree, collapsed rows' children included — an iterative walk, so no depth can exhaust the stack. */
function findInTree(
  rows: readonly BacklogTreeItem[] | undefined,
  id: string,
): BacklogTreeItem | undefined {
  const pending = [...(rows ?? [])];
  for (let row = pending.pop(); row !== undefined; row = pending.pop()) {
    if (row.id === id) {
      return row;
    }
    for (const child of row.children) {
      pending.push(child);
    }
  }
  return undefined;
}
