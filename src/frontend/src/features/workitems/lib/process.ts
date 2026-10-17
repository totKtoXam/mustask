import type {
  CategoryCode,
  ProcessDefinitionResponse,
  StateDefinition,
  WorkItemType,
} from '../../process';

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
  /**
   * Its level has a backlog (mod-017 design § 4): the creation dialog says where the item
   * will stand. A «Задача» has none before feat-023, so neither line reads for it.
   */
  onBacklog: boolean;
}

/**
 * The types a parent of an item of `typeId` may have (feat-001 BR-H-2,
 * api-contract § 1): those whose backlog level ranks at the item's own or
 * higher, in the process's display order. None while the item's type or its
 * level is not in the read.
 */
export function allowedParentTypes(
  process: Pick<ProcessDefinitionResponse, 'backlogLevels' | 'workItemTypes'>,
  typeId: string,
): WorkItemType[] {
  const rankOf = (type: WorkItemType) =>
    process.backlogLevels.find((level) => level.id === type.backlogLevel.id)?.rank;
  const own = typeById(process.workItemTypes, typeId);
  const ownRank = own ? rankOf(own) : undefined;
  if (ownRank === undefined) {
    return [];
  }
  return process.workItemTypes.filter((type) => {
    const rank = rankOf(type);
    return rank !== undefined && rank >= ownRank;
  });
}

/** «Родитель»'s helper (feat-001 spec § Screens and copy); none without a type to name. */
export function parentHelperText(types: readonly Pick<WorkItemType, 'name'>[]): string | null {
  return types.length > 0
    ? `Родителем может быть: ${types.map((type) => type.name).join(', ')}.`
    : null;
}

/**
 * The process's types as the dialog offers them, in the order the read gives them. A type is on a
 * backlog unless its level is the task level (`kind: 'task'`); without the levels every type is.
 */
export function creatableTypes(
  types: readonly WorkItemType[],
  levels: ProcessDefinitionResponse['backlogLevels'] = [],
): CreatableType[] {
  return types.map((type) => ({
    id: type.id,
    name: type.name,
    appearance: type.appearance,
    initialStateName: initialState(type)?.name ?? '',
    onBacklog: levels.find((level) => level.id === type.backlogLevel.id)?.kind !== 'task',
  }));
}
