import {
  useCallback,
  useEffect,
  useMemo,
  useRef,
  useState,
  type MutableRefObject,
  type RefObject,
} from 'react';
import { useQueryClient } from '@tanstack/react-query';
import type { WorkItemType } from '../../process';
import {
  newRequestId,
  placeRefusal,
  useCreateWorkItemIn,
  useSetWorkItemStateIn,
  workItemPath,
  type WorkItem,
} from '../../workitems';
import type { BoardCardItem, BoardSurface, ChecklistChild } from '../api/boardsApi';
import { boardKey } from '../api/queryKeys';
import { addEntriesOf, type AddEntry } from '../lib/addChild';
import { columnOf } from '../lib/board';
import {
  CHECKLIST_KINDS,
  applyChildState,
  isChecklistKind,
  projectOfChild,
  stateAnnouncement,
  tickTarget,
  type ChecklistKind,
  type KindNames,
} from '../lib/checklist';
import type { BoardLevelCode } from '../lib/levels';
import { stateRefusalNotice } from '../lib/refusals';
import type { BoardScope } from '../lib/scope';
import { useReadAgain } from './queries';
import type { useMoveView } from './useMoveView';

/** How an add field closed without a create: for the focus rules (design § 5). */
export type AddCancel = 'escape' | 'empty' | 'blur';

/**
 * The board's one open add field (feat-032 design § 5): held with the board, not
 * with the card, so a list folded over it keeps its title and `requestId` (feat-027
 * F-7) and only one field is open at a time.
 */
export interface AddField {
  cardId: string;
  /** The card's identifier, which the field's name carries. */
  cardIdentifier: string;
  /** The card's project: the task is created there, with its next number (feat-040 BR-TS-5). */
  projectId: string;
  /** The task type the field creates: its id for the create, its name for the field's name. */
  typeId: string;
  typeName: string;
  /** What is typed, kept while the list is folded. */
  title: string;
  /** The create is saved: the field is read-only with a progress indicator. */
  busy: boolean;
  /** The refusal at the field, `null` for none. */
  error: string | null;
  /** A refusal was shown: closing the field reads the board again. */
  refused: boolean;
  /** The field takes focus when it is drawn (opened, or brought back to the user). */
  focus: boolean;
}

/** What the cards call. Stable: it is one object for the board's lifetime of the process read. */
export interface ChecklistActions {
  toggleList(cardId: string, kind: ChecklistKind): void;
  /** A checkbox changed: tick when `done`, untick otherwise. */
  tick(child: ChecklistChild, done: boolean): void;
  openAdd(card: BoardCardItem): void;
  editAdd(title: string): void;
  submitAdd(title: string): void;
  cancelAdd(how: AddCancel): void;
  /** The field has taken the focus it was asked for. */
  addFocused(): void;
  registerAddButton(cardId: string, element: HTMLElement | null): void;
  /** A child's form (BR-C-5: the title opens the item as the card's own title does), in the card's project. */
  pathOf(identifier: string, projectId: string): string;
}

/** What every card shares: stable while the process is. */
export interface ChecklistEnv {
  actions: ChecklistActions;
  /** The process's name of each kind, for a «0/0» with no child to read it from. */
  names: KindNames;
  /** The task the «Добавить задачу» creates; `undefined` until the process is read. */
  taskEntry: AddEntry | undefined;
  /** The types whose children can be ticked: those with a Completed state. */
  tickable: ReadonlySet<string>;
  /** The add field's input, which whichever card draws the field points at. */
  addField: RefObject<HTMLInputElement | null>;
}

const NONE: readonly ChecklistKind[] = [];

type MoveView = ReturnType<typeof useMoveView<BoardSurface>>;

/**
 * The checklists of a board's cards (feat-032): which lists are open (BR-C-4:
 * held by card id and kind, so a re-read and a card move keep them, and the visit
 * ends them), the tick and untick of a row (BR-C-7), and the add field of a task
 * (BR-C-9). The two writes run under the board's single in-flight guard and read
 * the board again, as a card move does (BR-C-8): `inFlightRef` is the board's own, and
 * `setCreating` switches the controls off for a create, which `view` cannot see.
 */
/** No filter: the board is read unfiltered. */
const NO_FILTER: readonly string[] = [];

export function useChecklists({
  organizationId,
  scope,
  level,
  filterIds = NO_FILTER,
  board,
  view,
  inFlightRef,
  setCreating,
  types,
  announce,
  showError,
  showCreated,
}: {
  organizationId: string;
  scope: BoardScope;
  level: BoardLevelCode;
  /** The filter the board was read under (the team's project filter, the project's team filter): part of the cache key. */
  filterIds?: readonly string[];
  /** The last read. */
  board: BoardSurface;
  view: MoveView;
  inFlightRef: MutableRefObject<boolean>;
  setCreating(creating: boolean): void;
  /** The process's types, `[]` until it is read. */
  types: readonly WorkItemType[];
  announce(text: string): void;
  showError(text: string): void;
  showCreated(text: string): void;
}) {
  const client = useQueryClient();
  // Only `mutateAsync` is kept: query-core binds it once, while the `useMutation` result is a
  // new object each render, which would make `actions` and `env` new and redraw every card.
  const { mutateAsync: setStateAsync } = useSetWorkItemStateIn(organizationId);
  const { mutateAsync: createItemAsync } = useCreateWorkItemIn(organizationId);
  const readAgain = useReadAgain(organizationId, scope);
  const { shown } = view;

  const [open, setOpen] = useState<ReadonlyMap<string, readonly ChecklistKind[]>>(new Map());
  const [adding, setAdding] = useState<AddField | null>(null);
  const addingRef = useRef<AddField | null>(null);
  const addField = useRef<HTMLInputElement | null>(null);
  const addButtons = useRef(new Map<string, HTMLElement>());
  /** The `requestId` of the open field's retries (BR-M3-26), made at its first Enter. */
  const requestId = useRef<string | null>(null);
  /** The card whose «Добавить задачу» takes focus back once the field has closed. */
  const pendingAddFocus = useRef<string | null>(null);

  // Latest values for the callbacks below, which stay stable.
  const latest = useRef({ shown, board, types, view });
  useEffect(() => {
    latest.current = { shown, board, types, view };
  });
  useEffect(() => {
    addingRef.current = adding;
  }, [adding]);

  // A field's card that has left the read leaves with it, unless its create is saved: it
  // cannot hold the board's one place (design § 5, as feat-027 F-7).
  const addCardPresent = adding !== null && columnOf(shown, adding.cardId) !== undefined;
  const addOpen = adding !== null && (adding.busy || addCardPresent) ? adding : null;

  const names = useMemo<KindNames>(() => {
    const found: Partial<Record<ChecklistKind, string>> = {};
    for (const type of types) {
      if (isChecklistKind(type.appearance) && found[type.appearance] === undefined) {
        found[type.appearance] = type.name;
      }
    }
    return found;
  }, [types]);
  const taskEntry = useMemo(
    () => addEntriesOf('story', types).find((entry) => entry.appearance === 'task'),
    [types],
  );
  const tickable = useMemo(
    () =>
      new Set(
        types
          .filter((type) => type.states.some((state) => state.category.code === 'Completed'))
          .map((type) => type.id),
      ),
    [types],
  );

  // After a closed field, focus goes to its «Добавить задачу», once the card is drawn without it.
  useEffect(() => {
    const cardId = pendingAddFocus.current;
    if (cardId !== null && addOpen === null) {
      pendingAddFocus.current = null;
      addButtons.current.get(cardId)?.focus();
    }
  });

  const toggleList = useCallback((cardId: string, kind: ChecklistKind) => {
    setOpen((current) => {
      const kinds = current.get(cardId) ?? NONE;
      const next = kinds.includes(kind)
        ? kinds.filter((candidate) => candidate !== kind)
        : CHECKLIST_KINDS.filter((candidate) => candidate === kind || kinds.includes(candidate));
      const map = new Map(current);
      if (next.length === 0) {
        map.delete(cardId);
      } else {
        map.set(cardId, next);
      }
      return map;
    });
  }, []);

  /** Opens the card's task list so a folded field is drawn again (design § 5). */
  const revealAdd = useCallback((cardId: string) => {
    setOpen((current) => {
      const kinds = current.get(cardId) ?? NONE;
      if (kinds.includes('task')) {
        return current;
      }
      return new Map(current).set(
        cardId,
        CHECKLIST_KINDS.filter((candidate) => candidate === 'task' || kinds.includes(candidate)),
      );
    });
    setAdding((field) => field && { ...field, focus: true });
  }, []);

  /**
   * Whether the open field is to stay: it holds text, or its create is saved. Then no
   * other field opens — focus goes back to it, its list opened first if it is folded.
   */
  const addStays = useCallback(() => {
    const current = addingRef.current;
    // A field whose card has left the read is gone with it (unless its create is saved).
    const present =
      current !== null &&
      (current.busy || columnOf(latest.current.shown, current.cardId) !== undefined);
    if (current !== null && present && (current.busy || current.title.trim() !== '')) {
      if (addField.current !== null) {
        addField.current.focus();
      } else {
        revealAdd(current.cardId);
      }
      return true;
    }
    return false;
  }, [revealAdd]);

  const taskEntryRef = useRef(taskEntry);
  useEffect(() => {
    taskEntryRef.current = taskEntry;
  });

  const openAdd = useCallback(
    (card: BoardCardItem) => {
      const entry = taskEntryRef.current;
      if (entry === undefined || addStays()) {
        return;
      }
      // Every opening is a fresh field: no title, and a `requestId` of its own (BR-M3-26).
      requestId.current = null;
      addingRef.current = null;
      setAdding({
        cardId: card.id,
        cardIdentifier: card.identifier,
        projectId: card.project.id,
        typeId: entry.typeId,
        typeName: entry.name,
        title: '',
        busy: false,
        error: null,
        refused: false,
        focus: true,
      });
    },
    [addStays],
  );

  const cancelAdd = useCallback(
    (how: AddCancel) => {
      const current = addingRef.current;
      if (current === null || current.busy) {
        return;
      }
      // A refused field reads the board again as it closes, so a card that is gone leaves it.
      if (current.refused) {
        void readAgain();
      }
      if (how !== 'blur') {
        pendingAddFocus.current = current.cardId;
      }
      addingRef.current = null;
      setAdding(null);
    },
    [readAgain],
  );

  const editAdd = useCallback((title: string) => {
    setAdding((field) => field && { ...field, title, error: null });
  }, []);

  const addFocused = useCallback(() => {
    setAdding((field) => (field && field.focus ? { ...field, focus: false } : field));
  }, []);

  const submitAdd = useCallback(
    async (title: string) => {
      const current = addingRef.current;
      // BR-C-8: while another write is saved Enter does nothing and the text is kept.
      if (current === null || inFlightRef.current) {
        return;
      }
      requestId.current ??= newRequestId();
      inFlightRef.current = true;
      setCreating(true);
      setAdding({ ...current, title, busy: true, error: null });
      let created: WorkItem | null = null;
      let error: string | null = null;
      let contextGone = false;
      try {
        created = await createItemAsync({
          projectId: current.projectId,
          body: {
            requestId: requestId.current,
            typeId: current.typeId,
            title,
            parentId: current.cardId,
          },
        });
      } catch (caught) {
        const placed = placeRefusal(caught, ['title', 'parent']);
        error = placed.fields.title ?? placed.fields.parent ?? placed.alert;
        contextGone = placed.frame !== null;
      }
      if (created === null) {
        inFlightRef.current = false;
        setCreating(false);
        setAdding((field) => field && { ...field, busy: false, error, refused: true });
        // The refusal shows at the field and the title is kept (design § 5).
        if (addField.current !== null) {
          addField.current.focus();
        } else {
          revealAdd(current.cardId);
        }
        if (contextGone) {
          void readAgain();
        }
        return;
      }
      await readAgain();
      inFlightRef.current = false;
      setCreating(false);
      pendingAddFocus.current = current.cardId;
      addingRef.current = null;
      setAdding(null);
      showCreated(`Рабочий элемент ${created.identifier} создан.`);
    },
    [createItemAsync, inFlightRef, readAgain, revealAdd, setCreating, showCreated],
  );

  const tick = useCallback(
    async (child: ChecklistChild, done: boolean) => {
      const { shown: current, board: read, types: known, view: moveView } = latest.current;
      const target = tickTarget(known, child.type.id, done);
      // The child is written in its card's project (feat-040 BR-TS-5).
      const childProject = projectOfChild(read, child.id);
      // BR-C-8: the checkbox is off while a write is saved; its change is ignored.
      if (inFlightRef.current || target === undefined || childProject === undefined) {
        return;
      }
      inFlightRef.current = true;
      // The row and the counter show the new state at once, announced at once (design § 7.2).
      moveView.begin(applyChildState(current, child.id, target));
      announce(stateAnnouncement(child.identifier, target.name));
      let notice: string | null = null;
      try {
        await setStateAsync({
          projectId: childProject,
          identifier: child.identifier,
          stateId: target.id,
        });
      } catch (error) {
        notice = stateRefusalNotice(error);
      }
      // The board is read again whatever the answer, before a refusal's notice.
      await readAgain();
      inFlightRef.current = false;
      moveView.end(
        client.getQueryData<BoardSurface>(boardKey(organizationId, scope, level, filterIds)),
        read,
      );
      if (notice !== null) {
        showError(notice);
      }
    },
    [
      announce,
      client,
      inFlightRef,
      level,
      organizationId,
      readAgain,
      setStateAsync,
      showError,
      scope,
      filterIds,
    ],
  );

  const registerAddButton = useCallback((cardId: string, element: HTMLElement | null) => {
    if (element) {
      addButtons.current.set(cardId, element);
    } else {
      addButtons.current.delete(cardId);
    }
  }, []);

  const pathOf = useCallback(
    (identifier: string, projectId: string) => workItemPath(organizationId, projectId, identifier),
    [organizationId],
  );

  const actions = useMemo<ChecklistActions>(
    () => ({
      toggleList,
      tick: (child, done) => void tick(child, done),
      openAdd,
      editAdd,
      submitAdd: (title) => void submitAdd(title),
      cancelAdd,
      addFocused,
      registerAddButton,
      pathOf,
    }),
    [
      addFocused,
      cancelAdd,
      editAdd,
      openAdd,
      pathOf,
      registerAddButton,
      submitAdd,
      tick,
      toggleList,
    ],
  );

  const env = useMemo<ChecklistEnv>(
    () => ({ actions, names, taskEntry, tickable, addField }),
    [actions, names, taskEntry, tickable],
  );

  return {
    env,
    /** The kinds of a card whose lists are open. */
    openKinds: (cardId: string): readonly ChecklistKind[] => open.get(cardId) ?? NONE,
    /** The board's open add field when it is `cardId`'s, else `null`. */
    addFieldOf: (cardId: string): AddField | null => (addOpen?.cardId === cardId ? addOpen : null),
  };
}
