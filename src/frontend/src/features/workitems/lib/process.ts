import type { CategoryCode, StateDefinition, WorkItemType } from '../../process';

/** «Удалена»'s category: only mod-005's Delete reaches it, so no control offers it (BR-M3-21). */
const REMOVED: CategoryCode = 'Removed';

/** The type of the organization's process with this id, or none. */
export function typeById(types: readonly WorkItemType[], typeId: string): WorkItemType | undefined {
  return types.find((type) => type.id === typeId);
}

/**
 * The states «Статус» offers (spec § Pickers; api-contract § 3): the type's
 * own, in the seeded order the process read gives, without its `Removed` one.
 */
export function editableStates(type: WorkItemType): StateDefinition[] {
  return type.states.filter((state) => state.category.code !== REMOVED);
}

/**
 * A new item's state (BR-M3-20): the type's initial state — the one marked
 * initial, else the first in the seeded order; «Новая» on all five.
 */
export function initialState(type: WorkItemType): StateDefinition | undefined {
  return type.states.find((state) => state.isInitial) ?? type.states[0];
}

/**
 * A type the create dialog and the type picker offer: its look, and the name
 * of the state a new item of it starts in, which the dialog's text quotes.
 * The project's list builds them from the process read (`creatableTypes`); a
 * team's backlog or board from its level's types (mod-006 api-contract § 2.2).
 */
export interface CreatableType {
  id: string;
  name: string;
  /** The appearance token the theme draws. */
  appearance: string;
  initialStateName: string;
}

/** The process's types as the dialog offers them, in the order the read gives them. */
export function creatableTypes(types: readonly WorkItemType[]): CreatableType[] {
  return types.map((type) => ({
    id: type.id,
    name: type.name,
    appearance: type.appearance,
    initialStateName: initialState(type)?.name ?? '',
  }));
}
