import type { WorkItemStateReference } from '../../workitems';
import type { Board, BoardCardItem, ChecklistChild } from '../api/boardsApi';
import { offersTask } from './addChild';

// What a board card's checklist is made of (feat-032 BR-C-1..-5, -7; design
// § 3.2, § 6): the three kinds, their counters, their words, and the state a
// tick or an untick writes.

/** BR-C-1: the kinds a checklist holds, in the order of the counters (BR-C-2). */
export const CHECKLIST_KINDS = ['task', 'bug', 'testcase'] as const;
export type ChecklistKind = (typeof CHECKLIST_KINDS)[number];

/** The process's own name of each kind (`type.name`), where the process has a type of it. */
export type KindNames = Readonly<Partial<Record<ChecklistKind, string>>>;

/** BR-C-3: a child is done in the Completed category; Resolved is not done. */
const COMPLETED = 'Completed';
const PROPOSED = 'Proposed';

export function isChecklistKind(appearance: string): appearance is ChecklistKind {
  return (CHECKLIST_KINDS as readonly string[]).includes(appearance);
}

/** BR-C-3. */
export function isDone(child: Pick<ChecklistChild, 'state'>): boolean {
  return child.state.category.code === COMPLETED;
}

/** BR-C-5: a test case is a count only, with no checkbox. */
export function hasCheckbox(kind: ChecklistKind): boolean {
  return kind !== 'testcase';
}

/** One counter of a card: a kind with its children, in the order of their numbers (BR-C-5). */
export interface Counter {
  kind: ChecklistKind;
  /** The process's name of the kind: read from a child, else from the process (design § 3.2). */
  typeName: string;
  children: readonly ChecklistChild[];
  done: number;
  total: number;
}

/**
 * The counters of a card, in the order task, bug, test case: a kind with
 * children, and the task counter always on a card that offers a task (BR-C-2,
 * BR-C-9) — «0/0» included. A «0/0» whose type name the process has not given yet
 * is not drawn rather than drawn with a guessed word (design § 3.2).
 */
export function countersOf(card: BoardCardItem, names: KindNames): Counter[] {
  const counters: Counter[] = [];
  for (const kind of CHECKLIST_KINDS) {
    const children = card.checklist.filter((child) => child.type.appearance === kind);
    const first = children[0];
    const typeName = first?.type.name ?? names[kind];
    const wanted = children.length > 0 || (kind === 'task' && offersTask(card.type.appearance));
    if (!wanted || typeName === undefined) {
      continue;
    }
    counters.push({
      kind,
      typeName,
      children,
      done: hasCheckbox(kind) ? children.filter(isDone).length : 0,
      total: children.length,
    });
  }
  return counters;
}

/** The counter's visible text (design § 3.2): «1/3» for task and bug, «2» for test case. */
export function counterText(counter: Pick<Counter, 'kind' | 'done' | 'total'>): string {
  return hasCheckbox(counter.kind) ? `${counter.done}/${counter.total}` : `${counter.total}`;
}

/**
 * The counter's accessible name, which starts with its visible text (D-5,
 * WCAG 2.5.3): «1/3 — MSK-US-2, Задача: выполнено 1 из 3», «2 — MSK-US-2,
 * Тест-кейс: 2».
 */
export function counterName(
  counter: Pick<Counter, 'kind' | 'done' | 'total' | 'typeName'>,
  identifier: string,
): string {
  const text = counterText(counter);
  return hasCheckbox(counter.kind)
    ? `${text} — ${identifier}, ${counter.typeName}: выполнено ${counter.done} из ${counter.total}`
    : `${text} — ${identifier}, ${counter.typeName}: ${counter.total}`;
}

/** The list's name (design § 6). */
export function listName(typeName: string, identifier: string): string {
  return `Список: ${typeName} (${identifier})`;
}

/** The chevron's name (design § 6). */
export function collapseName(typeName: string, identifier: string): string {
  return `Свернуть список: ${typeName} (${identifier})`;
}

/** The row checkbox's name: the child's own identifier (design § 6). */
export function checkboxName(identifier: string): string {
  return `${identifier}: выполнено`;
}

/** The announcement at a tick (design § 6): «MSK-TASK-4: статус «Закрыта».». */
export function stateAnnouncement(identifier: string, stateName: string): string {
  return `${identifier}: статус «${stateName}».`;
}

interface ProcessState {
  id: string;
  name: string;
  isInitial: boolean;
  category: { code: string; label: string };
}

interface ProcessTypeStates {
  id: string;
  states: readonly ProcessState[];
}

function reference(state: ProcessState): WorkItemStateReference {
  return { id: state.id, name: state.name, category: state.category };
}

/**
 * BR-C-7: the state a tick writes — the first state of the Completed category
 * in the process's order — or, for an untick, the initial state (the first of
 * Proposed). `undefined` when the type has none, or the process is not read:
 * the checkbox is then off.
 */
export function tickTarget(
  types: readonly ProcessTypeStates[],
  typeId: string,
  done: boolean,
): WorkItemStateReference | undefined {
  const states = types.find((type) => type.id === typeId)?.states ?? [];
  const found = done
    ? states.find((state) => state.category.code === COMPLETED)
    : (states.find((state) => state.isInitial) ??
      states.find((state) => state.category.code === PROPOSED));
  return found && reference(found);
}

/** The board with the child shown in `state` in every checklist it is in, at once (design § 7.2). */
export function applyChildState<T extends Pick<Board, 'columns'>>(
  board: T,
  childId: string,
  state: WorkItemStateReference,
): T {
  return {
    ...board,
    columns: board.columns.map((column) => ({
      ...column,
      items: column.items.map((card) =>
        card.checklist.some((child) => child.id === childId)
          ? {
              ...card,
              checklist: card.checklist.map((child) =>
                child.id === childId ? { ...child, state } : child,
              ),
            }
          : card,
      ),
    })),
  };
}
