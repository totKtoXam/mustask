import { memo, useEffect, useId, useRef, type FocusEvent } from 'react';
import { Link as RouterLink } from 'react-router-dom';
import Box from '@mui/material/Box';
import Checkbox from '@mui/material/Checkbox';
import Link from '@mui/material/Link';
import List from '@mui/material/List';
import ListItem from '@mui/material/ListItem';
import { TypeMark } from '../../../shared/ui/WorkTrackingMarks';
import { AssigneeAvatar } from '../../workitems';
import type { BoardCardItem, ChecklistChild } from '../api/boardsApi';
import { offersTask } from '../lib/addChild';
import {
  checkboxName,
  collapseName,
  counterName,
  counterText,
  countersOf,
  hasCheckbox,
  isDone,
  listName,
  type ChecklistKind,
  type Counter,
} from '../lib/checklist';
import type { AddField, ChecklistActions, ChecklistEnv } from '../hooks/useChecklists';
import { ChecklistAddField } from './ChecklistAddField';
import { ActionButton, IconAction, ThinAddIcon, ThinChevronUpIcon } from '../../../shared/ui';

/** The text of the add button (BR-C-9): fixed, not the process's type name. */
const ADD_TASK_LABEL = 'Добавить задачу';

/**
 * A card's checklist (feat-032; design § 3–§ 5): a row of counters, one per kind
 * the card has — a toggle each (`aria-expanded`), «1/3» for tasks and bugs, a bare
 * count for test cases — and under it the lists of the open counters, in the order
 * of the kinds. A list's rows are a checkbox (not on a test case), the type icon, the
 * assignee's avatar and the title as a link; a story's task list starts with
 * «Добавить задачу». Drawn only when the card has a counter (BR-C-2).
 *
 * The root carries `data-no-drag`: a press or a drag that starts inside it never starts
 * the card's drag (BR-C-12, design § 9). The set of open lists is the board's, not this
 * component's, which a card move or a re-read redraws (design § 4.3).
 */
export const CardChecklists = memo(function CardChecklists({
  card,
  env,
  openKinds,
  adding,
  off,
}: {
  card: BoardCardItem;
  env: ChecklistEnv;
  /** The kinds whose lists are open on this card. */
  openKinds: readonly ChecklistKind[];
  /** The board's open add field, when it is this card's. */
  adding: AddField | null;
  /** A write is saved: checkboxes and «Добавить задачу» are off (BR-C-8). */
  off: boolean;
}) {
  const { actions } = env;
  const base = useId();
  const counterButtons = useRef(new Map<ChecklistKind, HTMLElement>());
  /** The row that holds focus: if a re-read removes it, focus goes to its counter (design § 7.2). */
  const focusedRow = useRef<{ identifier: string; kind: ChecklistKind } | null>(null);
  const counters = countersOf(card, env.names);

  useEffect(() => {
    const lost = focusedRow.current;
    if (lost !== null && !card.checklist.some((child) => child.identifier === lost.identifier)) {
      focusedRow.current = null;
      (counterButtons.current.get(lost.kind) ?? [...counterButtons.current.values()][0])?.focus();
    }
  });

  if (counters.length === 0) {
    return null;
  }

  function rowFocused(event: FocusEvent<HTMLElement>) {
    const row = (event.target as HTMLElement).closest<HTMLElement>('[data-child]');
    const kind = row?.closest<HTMLElement>('[data-kind]')?.dataset.kind;
    focusedRow.current =
      row?.dataset.child && kind
        ? { identifier: row.dataset.child, kind: kind as ChecklistKind }
        : null;
  }

  function rowBlurred(event: FocusEvent<HTMLElement>) {
    // A removed node may blur without being a focus change: only a real move clears the row.
    if (event.relatedTarget !== null || (event.target as HTMLElement).isConnected) {
      focusedRow.current = null;
    }
  }

  const listId = (kind: ChecklistKind) => `${base}-${kind}`;
  return (
    <Box
      data-no-drag
      sx={{ display: 'flex', flexDirection: 'column', minWidth: 0 }}
      onFocus={rowFocused}
      onBlur={rowBlurred}
    >
      <Box
        role="group"
        aria-label={`Вложенные элементы: ${card.identifier}`}
        data-checklist={card.identifier}
        sx={{ display: 'flex', flexWrap: 'wrap', gap: 0.5, mt: 0.5 }}
      >
        {counters.map((counter) => (
          <CounterButton
            key={counter.kind}
            counter={counter}
            identifier={card.identifier}
            open={openKinds.includes(counter.kind)}
            listId={listId(counter.kind)}
            onToggle={() => actions.toggleList(card.id, counter.kind)}
            register={(element) => {
              if (element) {
                counterButtons.current.set(counter.kind, element);
              } else {
                counterButtons.current.delete(counter.kind);
              }
            }}
          />
        ))}
      </Box>
      {counters
        .filter((counter) => openKinds.includes(counter.kind))
        .map((counter) => (
          <Box
            key={counter.kind}
            id={listId(counter.kind)}
            data-kind={counter.kind}
            sx={{
              bgcolor: 'background.default',
              borderRadius: (theme) => `${theme.shape.borderRadius}px`,
              p: 0.5,
              mt: 0.5,
            }}
          >
            <List dense disablePadding aria-label={listName(counter.typeName, card.identifier)}>
              {counter.kind === 'task' && offersTask(card.type.appearance) && (
                <AddTaskLine card={card} env={env} adding={adding} off={off} />
              )}
              {counter.children.map((child) => (
                <ChecklistRow
                  key={child.id}
                  child={child}
                  kind={counter.kind}
                  tickable={env.tickable.has(child.type.id)}
                  off={off}
                  path={actions.pathOf(child.identifier, card.project.id)}
                  onTick={actions.tick}
                />
              ))}
            </List>
            <Box sx={{ display: 'flex', justifyContent: 'center' }}>
              <IconAction
                size="chevron"
                label={collapseName(counter.typeName, card.identifier)}
                icon={<ThinChevronUpIcon />}
                tooltip={false}
                onClick={() => {
                  actions.toggleList(card.id, counter.kind);
                  counterButtons.current.get(counter.kind)?.focus();
                }}
              />
            </Box>
          </Box>
        ))}
    </Box>
  );
});

/** One counter (design § 3.2): a text button that toggles its list, `aria-expanded`, never `aria-pressed`. */
function CounterButton({
  counter,
  identifier,
  open,
  listId,
  onToggle,
  register,
}: {
  counter: Counter;
  identifier: string;
  open: boolean;
  listId: string;
  onToggle(): void;
  register(element: HTMLElement | null): void;
}) {
  return (
    <Box
      sx={(theme) => ({
        borderRadius: `${theme.shape.borderRadius}px`,
        ...(open ? { backgroundColor: theme.palette.action.selected } : {}),
      })}
    >
      <ActionButton
        ref={register}
        tone="subtle"
        size="compact"
        label={counterText(counter)}
        icon={<TypeMark appearance={counter.kind} compact />}
        data-counter={counter.kind}
        aria-expanded={open ? 'true' : 'false'}
        aria-controls={open ? listId : undefined}
        aria-label={counterName(counter, identifier)}
        onClick={onToggle}
      />
    </Box>
  );
}

/** «Добавить задачу» and, once pressed, its field: the first line of a story's task list (BR-C-9). */
function AddTaskLine({
  card,
  env,
  adding,
  off,
}: {
  card: BoardCardItem;
  env: ChecklistEnv;
  adding: AddField | null;
  off: boolean;
}) {
  const { actions } = env;
  // With the process not read yet the task's type is not known: the button waits for it.
  if (env.taskEntry === undefined) {
    return null;
  }
  return (
    <ListItem
      disableGutters
      sx={{ display: 'flex', flexDirection: 'column', alignItems: 'stretch' }}
    >
      <Box sx={{ alignSelf: 'flex-start' }}>
        <ActionButton
          ref={(element) => actions.registerAddButton(card.id, element)}
          tone="subtle"
          size="compact"
          label={ADD_TASK_LABEL}
          icon={<ThinAddIcon />}
          aria-label={`${ADD_TASK_LABEL} (${card.identifier})`}
          aria-disabled={off ? 'true' : undefined}
          onClick={() => {
            if (!off) {
              actions.openAdd(card);
            }
          }}
        />
      </Box>
      {adding !== null && (
        <ChecklistAddField field={adding} off={off} inputRef={env.addField} actions={actions} />
      )}
    </ListItem>
  );
}

/**
 * One row (design § 4.1): a checkbox checked by the child's state, the type's icon, the
 * assignee's avatar, the title as a link to the child. While a write is saved the
 * checkbox is `aria-disabled`, not `disabled`, so a focused one keeps focus through the
 * read (design § 7.2); the change is ignored by the board. A type with no Completed
 * state has its checkbox `disabled`. Memoized, so a drag over a column redraws no row.
 */
const ChecklistRow = memo(function ChecklistRow({
  child,
  kind,
  tickable,
  off,
  path,
  onTick,
}: {
  child: ChecklistChild;
  kind: ChecklistKind;
  tickable: boolean;
  off: boolean;
  path: string;
  onTick: ChecklistActions['tick'];
}) {
  return (
    <ListItem
      disableGutters
      data-child={child.identifier}
      sx={(theme) => ({
        display: 'flex',
        alignItems: 'center',
        gap: 0.5,
        minHeight: theme.spacing(4),
        '&:hover': { backgroundColor: theme.palette.action.hover },
      })}
    >
      {hasCheckbox(kind) && (
        <Checkbox
          size="small"
          checked={isDone(child)}
          disabled={!tickable}
          onChange={(event) => onTick(child, event.target.checked)}
          slotProps={{
            input: {
              'aria-label': checkboxName(child.identifier),
              'aria-disabled': off ? 'true' : undefined,
            },
          }}
          sx={{ p: 0.5, ...(off ? { color: 'text.disabled' } : {}) }}
        />
      )}
      <TypeMark appearance={child.type.appearance} compact />
      <AssigneeAvatar assignee={child.assignee} />
      <Link
        component={RouterLink}
        to={path}
        variant="body2"
        title={`${child.identifier}: ${child.title}`}
        sx={{ flex: '1 1 auto', minWidth: 0, overflowWrap: 'anywhere' }}
      >
        {child.title}
      </Link>
    </ListItem>
  );
});
