import {
  memo,
  useCallback,
  useEffect,
  useId,
  useRef,
  useState,
  type DragEvent,
  type MouseEvent,
} from 'react';
import { Link as RouterLink } from 'react-router-dom';
import Box from '@mui/material/Box';
import Button from '@mui/material/Button';
import Card from '@mui/material/Card';
import IconButton from '@mui/material/IconButton';
import Link from '@mui/material/Link';
import ListSubheader from '@mui/material/ListSubheader';
import Menu from '@mui/material/Menu';
import MenuItem from '@mui/material/MenuItem';
import Paper from '@mui/material/Paper';
import Typography from '@mui/material/Typography';
import type { Theme } from '@mui/material/styles';
import AddIcon from '@mui/icons-material/Add';
import MoreHorizIcon from '@mui/icons-material/MoreHoriz';
import { useQueryClient } from '@tanstack/react-query';
import {
  AssigneeName,
  StateName,
  isPlainClick,
  useSetWorkItemState,
  useWorkItemDialog,
  workItemPath,
  type WorkItemStateReference,
} from '../../workitems';
import { TypeMark } from '../../../shared/ui/WorkTrackingMarks';
import type { BacklogItem, Board, BoardColumn } from '../api/boardsApi';
import { boardsKeys } from '../api/queryKeys';
import { useReadTeamAgain } from '../hooks/queries';
import { useMoveView } from '../hooks/useMoveView';
import type { NewWorkItem } from '../hooks/useNewWorkItem';
import { applyCardMove, columnOf, columnTarget, moveTargets } from '../lib/board';
import type { BoardLevelCode } from '../lib/levels';
import { moveRefusalNotice } from '../lib/refusals';

/** spec § Page «Доски»: the button at the top of the first column. */
const NEW_CARD_LABEL = 'Новый элемент';

/** The card menu's subheader, which also names the menu. */
const MOVE_TO_COLUMN = 'Переместить в колонку';

/** feat-022 spec § Backlog and board: the card's priority, after «Исполнитель». */
function priorityText(priority: number): string {
  return `Приоритет: ${priority}`;
}

interface DragState {
  cardId: string;
  /** The column the pointer is over, when it is one the card may go to. */
  over: string | null;
}

/** A column a dragged card may go to: a dashed accent outline (spec § Page «Доски»). */
function dropAreaSx(over: boolean) {
  return (theme: Theme) => ({
    outline: `${theme.spacing(0.25)} dashed ${theme.palette.primary.main}`,
    outlineOffset: `-${theme.spacing(0.25)}`,
    backgroundColor: over ? theme.palette.action.selected : theme.palette.action.hover,
  });
}

/**
 * The board's columns (US-4, US-5, US-6; spec § Page «Доски»; mod-015 § 10.4
 * without settings and collapse): side by side at `layout.boardColumnWidth`,
 * scrolling sideways inside their own container. A move — a card's menu, or
 * the browser's own drag and drop of the card onto a column that maps a state
 * of its type — shows the card in the new column at its rank at once, moves
 * focus to its «Ещё действия» there and is announced; while it is saved every
 * card menu is off (`aria-disabled`) and no card is draggable; then the board
 * is read again. A refusal puts the card back and shows its notice.
 */
export function BoardColumns({
  organizationId,
  projectId,
  teamId,
  level,
  board,
  newItem,
  stateOf,
  announce,
  showError,
}: {
  organizationId: string;
  projectId: string;
  teamId: string;
  level: BoardLevelCode;
  board: Board;
  newItem: Pick<NewWorkItem, 'open' | 'opensMenu' | 'menuOpen'>;
  /** A state's name and category from the process, once it is read. */
  stateOf(typeId: string, stateId: string): WorkItemStateReference | undefined;
  announce(text: string): void;
  showError(text: string): void;
}) {
  const client = useQueryClient();
  const setState = useSetWorkItemState(organizationId, projectId);
  const readAgain = useReadTeamAgain(organizationId, projectId, teamId);
  // A title's plain click opens the card as a dialog over the board (feat-029 BR-C-1);
  // `open` keeps its identity, so the memoised cards are not redrawn by the navigation.
  const { open: openItem } = useWorkItemDialog();
  const view = useMoveView(board);
  const [menu, setMenu] = useState<{ anchor: HTMLElement; cardId: string } | null>(null);
  const [drag, setDrag] = useState<DragState | null>(null);
  const menuButtons = useRef(new Map<string, HTMLElement>());
  /** The moved card until its move is saved; `placed`: focus was put on it once. */
  const pendingFocus = useRef<{ cardId: string; placed: boolean } | null>(null);
  const inFlight = useRef(false);
  const menuId = useId();
  const subheaderId = useId();

  const { saving, shown } = view;

  // The moved card is drawn anew in its new column: focus goes to its
  // «Ещё действия» there, and back to it if a later redraw drops it. A passive
  // effect, so the closing menu's focus trap has let go first: a layout
  // effect runs while the trap still holds focus inside the menu.
  useEffect(() => {
    const pending = pendingFocus.current;
    if (pending === null) {
      return;
    }
    const button = menuButtons.current.get(pending.cardId);
    const active = document.activeElement;
    if (button && (!pending.placed || active === null || active === document.body)) {
      button.focus();
      pending.placed = true;
    }
    if (!saving) {
      pendingFocus.current = null;
    }
  }, [shown, saving]);

  async function move(card: BacklogItem, column: BoardColumn) {
    const stateId = columnTarget(column, card.type.id);
    // On what is shown: right after a move it may be the newer read.
    if (inFlight.current || stateId === null || columnOf(shown, card.id)?.key === column.key) {
      return;
    }
    inFlight.current = true;
    pendingFocus.current = { cardId: card.id, placed: false };
    view.begin(applyCardMove(shown, card.id, column.key, stateOf(card.type.id, stateId)));
    announce(`${card.identifier} перемещён в колонку «${column.header}».`);
    let notice: string | null = null;
    try {
      await setState.mutateAsync({ identifier: card.identifier, stateId });
    } catch (error) {
      notice = moveRefusalNotice(error);
    }
    // The board is read again whatever the answer, before a refusal's notice.
    await readAgain();
    inFlight.current = false;
    view.end(
      client.getQueryData<Board>(boardsKeys.board(organizationId, projectId, teamId, level)),
      board,
    );
    if (notice !== null) {
      showError(notice);
    }
  }

  const registerMenuButton = useCallback((cardId: string, element: HTMLElement | null) => {
    if (element) {
      menuButtons.current.set(cardId, element);
    } else {
      menuButtons.current.delete(cardId);
    }
  }, []);

  const openMenu = useCallback(
    (event: MouseEvent<HTMLElement>, cardId: string) => {
      if (!saving) {
        setMenu({ anchor: event.currentTarget, cardId });
      }
    },
    [saving],
  );

  const startDrag = useCallback(
    (event: DragEvent<HTMLElement>, card: BacklogItem) => {
      if (saving) {
        event.preventDefault();
        return;
      }
      if (event.dataTransfer) {
        event.dataTransfer.effectAllowed = 'move';
        event.dataTransfer.setData('text/plain', card.identifier);
      }
      setDrag({ cardId: card.id, over: null });
    },
    [saving],
  );

  const endDrag = useCallback(() => setDrag(null), []);

  const draggedCard = drag
    ? columnOf(shown, drag.cardId)?.items.find((card) => card.id === drag.cardId)
    : undefined;
  const dropKeys = new Set(
    draggedCard ? moveTargets(shown, draggedCard).map((column) => column.key) : [],
  );

  function dragOver(event: DragEvent<HTMLElement>, column: BoardColumn) {
    if (drag === null || saving || !dropKeys.has(column.key)) {
      return;
    }
    event.preventDefault();
    if (event.dataTransfer) {
      event.dataTransfer.dropEffect = 'move';
    }
    if (drag.over !== column.key) {
      setDrag({ cardId: drag.cardId, over: column.key });
    }
  }

  function dragLeave(event: DragEvent<HTMLElement>, column: BoardColumn) {
    const next = event.relatedTarget as Node | null;
    if (drag?.over === column.key && (next === null || !event.currentTarget.contains(next))) {
      setDrag({ cardId: drag.cardId, over: null });
    }
  }

  function drop(event: DragEvent<HTMLElement>, column: BoardColumn) {
    if (drag === null || !draggedCard || !dropKeys.has(column.key)) {
      return;
    }
    event.preventDefault();
    setDrag(null);
    void move(draggedCard, column);
  }

  function closeMenu() {
    if (menu) {
      menuButtons.current.get(menu.cardId)?.focus();
    }
    setMenu(null);
  }

  const menuCard = menu
    ? columnOf(shown, menu.cardId)?.items.find((card) => card.id === menu.cardId)
    : undefined;

  return (
    <Box
      component="section"
      // feat-001 BR-H-16: the board region is named as the page, «Канбан».
      aria-label="Канбан"
      sx={{ display: 'flex', alignItems: 'flex-start', gap: 2, overflowX: 'auto', pb: 2 }}
    >
      {shown.columns.map((column, columnIndex) => {
        const isDropArea = dropKeys.has(column.key);
        return (
          <Paper
            key={column.key}
            variant="outlined"
            data-column={column.key}
            data-drop-target={isDropArea ? 'true' : undefined}
            onDragEnter={(event: DragEvent<HTMLElement>) => dragOver(event, column)}
            onDragOver={(event: DragEvent<HTMLElement>) => dragOver(event, column)}
            onDragLeave={(event: DragEvent<HTMLElement>) => dragLeave(event, column)}
            onDrop={(event: DragEvent<HTMLElement>) => drop(event, column)}
            sx={[
              (theme: Theme) => ({
                width: theme.layout.boardColumnWidth,
                flexShrink: 0,
                p: 1,
                display: 'flex',
                flexDirection: 'column',
                gap: 1,
              }),
              ...(isDropArea ? [dropAreaSx(drag?.over === column.key)] : []),
            ]}
          >
            <Box sx={{ display: 'flex', alignItems: 'baseline', gap: 1, px: 0.5 }}>
              <Typography component="h2" variant="subtitle2">
                {column.header}
              </Typography>
              <Typography variant="caption" color="text.secondary">
                {column.items.length}
              </Typography>
            </Box>
            {columnIndex === 0 && (
              <Button
                variant="text"
                startIcon={<AddIcon />}
                aria-haspopup={newItem.opensMenu ? 'menu' : undefined}
                aria-expanded={
                  newItem.opensMenu ? (newItem.menuOpen ? 'true' : 'false') : undefined
                }
                onClick={(event: MouseEvent<HTMLElement>) => newItem.open(event)}
                sx={{ alignSelf: 'flex-start' }}
              >
                {NEW_CARD_LABEL}
              </Button>
            )}
            <Box
              role="list"
              aria-label={`${column.header}, рабочих элементов: ${column.items.length}`}
              sx={{
                display: 'flex',
                flexDirection: 'column',
                gap: 1,
                minHeight: (theme) => theme.spacing(4),
              }}
            >
              {column.items.map((card) => (
                <BoardCard
                  key={card.id}
                  card={card}
                  saving={saving}
                  menuOpen={menu?.cardId === card.id}
                  menuId={menuId}
                  formPath={workItemPath(organizationId, projectId, card.identifier)}
                  onOpenItem={openItem}
                  onOpenMenu={openMenu}
                  onDragStart={startDrag}
                  onDragEnd={endDrag}
                  registerMenuButton={registerMenuButton}
                />
              ))}
            </Box>
          </Paper>
        );
      })}
      <Menu
        id={menuId}
        anchorEl={menu?.anchor ?? null}
        open={menu !== null}
        onClose={closeMenu}
        disableRestoreFocus
        anchorOrigin={{ vertical: 'bottom', horizontal: 'right' }}
        transformOrigin={{ vertical: 'top', horizontal: 'right' }}
        slotProps={{ list: { 'aria-labelledby': subheaderId } }}
      >
        <ListSubheader id={subheaderId} role="presentation">
          {MOVE_TO_COLUMN}
        </ListSubheader>
        {(menuCard ? moveTargets(shown, menuCard) : []).map((column) => (
          <MenuItem
            key={column.key}
            onClick={() => {
              setMenu(null);
              if (menuCard) {
                void move(menuCard, column);
              }
            }}
          >
            {column.header}
          </MenuItem>
        ))}
      </Menu>
    </Box>
  );
}

/**
 * One card (spec § Page «Доски», BR-M4-14): a 4 px stripe in the type's
 * colour; the type icon, ID and «Заголовок» as a link to the form; the
 * category dot and the state; «Исполнитель», then «Приоритет: <n>» as a
 * caption (feat-022 BR-M3-39); and «Ещё действия: <ID>». The
 * card itself is the drag source. Memoized, so a drag over one column redraws
 * no card.
 */
const BoardCard = memo(function BoardCard({
  card,
  saving,
  menuOpen,
  menuId,
  formPath,
  onOpenItem,
  onOpenMenu,
  onDragStart,
  onDragEnd,
  registerMenuButton,
}: {
  card: BacklogItem;
  saving: boolean;
  menuOpen: boolean;
  menuId: string;
  /** The item's full page: the title's `href`, followed by a modified click (BR-C-1). */
  formPath: string;
  /** A plain click on the title opens the item's card as a dialog over the board (BR-C-1). */
  onOpenItem(identifier: string): void;
  onOpenMenu(event: MouseEvent<HTMLElement>, cardId: string): void;
  onDragStart(event: DragEvent<HTMLElement>, card: BacklogItem): void;
  onDragEnd(): void;
  registerMenuButton(cardId: string, element: HTMLElement | null): void;
}) {
  return (
    <Card
      role="listitem"
      elevation={1}
      draggable={!saving}
      data-card={card.identifier}
      // The card's dialog gives focus back to this card's title by the identifier (BR-C-4).
      data-identifier={card.identifier}
      onDragStart={(event: DragEvent<HTMLElement>) => onDragStart(event, card)}
      onDragEnd={onDragEnd}
      sx={(theme) => ({
        p: 1,
        display: 'flex',
        flexDirection: 'column',
        gap: 0.5,
        cursor: saving ? 'default' : 'grab',
        borderLeft: `${theme.spacing(0.5)} solid ${theme.workTracking.typeAppearance(card.type.appearance).colour}`,
      })}
    >
      <Box sx={{ display: 'flex', alignItems: 'flex-start', gap: 1 }}>
        <TypeMark appearance={card.type.appearance} />
        <Typography variant="body2" sx={{ whiteSpace: 'nowrap' }}>
          {card.identifier}
        </Typography>
        <Link
          component={RouterLink}
          to={formPath}
          variant="body2"
          onClick={(event: MouseEvent<HTMLElement>) => {
            if (isPlainClick(event)) {
              event.preventDefault();
              onOpenItem(card.identifier);
            }
          }}
          sx={{ flex: '1 1 auto', minWidth: 0, overflowWrap: 'anywhere' }}
        >
          {card.title}
        </Link>
        <IconButton
          size="small"
          ref={(element: HTMLElement | null) => registerMenuButton(card.id, element)}
          aria-label={`Ещё действия: ${card.identifier}`}
          aria-haspopup="menu"
          aria-expanded={menuOpen ? 'true' : 'false'}
          aria-controls={menuOpen ? menuId : undefined}
          aria-disabled={saving ? 'true' : undefined}
          onClick={(event: MouseEvent<HTMLElement>) => onOpenMenu(event, card.id)}
          sx={{ mt: -0.5, color: saving ? 'text.disabled' : undefined }}
        >
          <MoreHorizIcon fontSize="small" />
        </IconButton>
      </Box>
      <Typography component="div" variant="body2">
        <StateName state={card.state} />
      </Typography>
      <Box
        sx={{ display: 'flex', alignItems: 'center', columnGap: 1, flexWrap: 'wrap', minWidth: 0 }}
      >
        <Typography component="div" variant="body2" sx={{ minWidth: 0 }}>
          <AssigneeName assignee={card.assignee} />
        </Typography>
        <Typography component="div" variant="caption" color="text.secondary">
          {priorityText(card.priority)}
        </Typography>
      </Box>
    </Card>
  );
});
