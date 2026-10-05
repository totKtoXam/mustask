import type { AreaNode, Team } from '../api/organizationsApi';

/**
 * The area tree as the pickers and the tree registry read it. The read gives
 * it in pre-order, each node before its children and siblings in display
 * order (api-contract § 4.1); nothing here sorts it again.
 */

/** The node and every node beneath it. */
export function subtreeIds(areas: readonly AreaNode[], areaId: string): Set<string> {
  const ids = new Set<string>([areaId]);
  // Pre-order: a node's descendants follow it until the next node that is
  // not beneath it, so one pass over the parents collects them.
  for (const area of areas) {
    if (area.parentId !== null && ids.has(area.parentId)) {
      ids.add(area.id);
    }
  }
  return ids;
}

/** One choice of the move picker (design § 11.5). */
export interface MoveOption {
  id: string;
  path: string;
  isRoot: boolean;
}

/**
 * The move picker's destinations, in tree order: every area except the node,
 * its subtree and its current parent (design § 11.5, DC-8), the root marked.
 * One project's tree only, so an area never leaves its project (US-5).
 */
export function moveOptions(areas: readonly AreaNode[], areaId: string): MoveOption[] {
  const node = areas.find((area) => area.id === areaId);
  const excluded = subtreeIds(areas, areaId);
  if (node?.parentId) {
    excluded.add(node.parentId);
  }
  return areas
    .filter((area) => !excluded.has(area.id))
    .map((area) => ({ id: area.id, path: area.path, isRoot: area.parentId === null }));
}

/**
 * The default-area picker's choices (US-4, BR-M1-14): the areas the team
 * owns explicitly, by path, in tree order. An area it owns by inheritance is
 * not offered.
 */
export function defaultAreaOptions(areas: readonly AreaNode[], teamId: string): AreaNode[] {
  return areas.filter((area) => area.ownerTeamId === teamId);
}

/** The owner pickers' «Наследовать от родительской области», the first choice (US-6). */
export const INHERIT_OWNER_LABEL = 'Наследовать от родительской области';

/** The value the owner pickers give «Наследовать…»; sent as `null`. */
export const INHERIT_OWNER_VALUE = '';

/** One choice of an owner picker. */
export interface OwnerOption {
  value: string;
  label: string;
}

/**
 * The owner pickers' choices: «Наследовать…» first, then the project's
 * active teams in the order the list gives (design § 4.6). A deleted team is
 * never offered: the list leaves it out (US-7).
 */
export function ownerOptions(teams: readonly Team[]): OwnerOption[] {
  return [
    { value: INHERIT_OWNER_VALUE, label: INHERIT_OWNER_LABEL },
    ...teams.map((team) => ({ value: team.id, label: team.name })),
  ];
}

/** The owner a picker value sends: `null` for «Наследовать…» (api-contract § 4.2, § 4.5). */
export function ownerTeamIdOf(value: string): string | null {
  return value === INHERIT_OWNER_VALUE ? null : value;
}

/** The root, the one node without a parent. */
export function rootOf(areas: readonly AreaNode[]): AreaNode | undefined {
  return areas.find((area) => area.parentId === null);
}
