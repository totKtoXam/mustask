import type { AreaNode } from '../api/organizationsApi';
import { ORGANIZATION_PLACE, PICKER_OPTION_LIMIT, TREE_EXPAND_ALL_LIMIT } from './areaTexts';

/**
 * The organization's typed area tree as the pickers and the tree registry read it. The read gives it
 * in pre-order, each node before its children and siblings in display order (mod-018 api-contract
 * § 3); nothing here sorts it again.
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

/** How many nodes are beneath the node (design § 5.4: «… вместе со всеми вложенными областями: 2»). */
export function descendantCount(areas: readonly AreaNode[], areaId: string): number {
  return subtreeIds(areas, areaId).size - 1;
}

/** Whether the tree knows a child of the node (design § 5.5). */
export function hasChildren(areas: readonly AreaNode[], areaId: string): boolean {
  return areas.some((area) => area.parentId === areaId);
}

/** Whether a node of the type is read-only: any node of a deactivated project (design § 3, OQ-6 A). */
export function isReadOnly(node: Pick<AreaNode, 'projectActive'>): boolean {
  return node.projectActive === false;
}

/** The departments, in tree order. */
export function departmentsOf(areas: readonly AreaNode[]): AreaNode[] {
  return areas.filter((area) => area.type === 'department');
}

/**
 * One choice of a place picker: a node by its path, or the top level (`id: null`, design D-8). The
 * top level reads «Организация» and stands first; it is a place, not a node.
 */
export interface PlaceOption {
  id: string | null;
  label: string;
}

export const ORGANIZATION_OPTION: PlaceOption = { id: null, label: ORGANIZATION_PLACE };

/** «Расположение» of a new project: «Организация» first, then every department by name (design § 7). */
export function placementOptions(areas: readonly AreaNode[]): PlaceOption[] {
  return [
    ORGANIZATION_OPTION,
    ...departmentsOf(areas).map((area) => ({ id: area.id, label: area.name })),
  ];
}

/**
 * «Расположение» of a new project by the caller's codes (api-contract § 5, POST /projects): a
 * department parent needs `area.write`, the top level `area.create`.
 */
export function placementOptionsFor(
  areas: readonly AreaNode[],
  atTop: boolean,
  inDepartment: boolean,
): PlaceOption[] {
  const options = placementOptions(inDepartment ? areas : []);
  return atTop ? options : options.filter((option) => option.id !== null);
}

/**
 * A project's move offer (design § 5.4, D-5): «Организация» when it is not the current place, then
 * every department except the current one.
 */
export function projectMoveOptions(
  areas: readonly AreaNode[],
  currentParentId: string | null,
): PlaceOption[] {
  const places = departmentsOf(areas)
    .filter((area) => area.id !== currentParentId)
    .map((area) => ({ id: area.id, label: area.name }));
  return currentParentId === null ? places : [ORGANIZATION_OPTION, ...places];
}

/**
 * A sub-area's move offer, from its project's subtree (design § 5.4, D-5): the project's node and its
 * sub-areas by path, except the area, its descendants and its current parent. Never a department or
 * another project's node.
 */
export function areaMoveOptions(
  projectTree: readonly AreaNode[],
  areaId: string,
  currentParentId: string | null,
): PlaceOption[] {
  const excluded = subtreeIds(projectTree, areaId);
  if (currentParentId !== null) {
    excluded.add(currentParentId);
  }
  return projectTree
    .filter((area) => !excluded.has(area.id))
    .map((area) => ({ id: area.id, label: area.path }));
}

/** The parents a new sub-area can take: its project's node and sub-areas by path (design § 5.2). */
export function subAreaParentOptions(projectTree: readonly AreaNode[]): PlaceOption[] {
  return projectTree.map((area) => ({ id: area.id, label: area.path }));
}

/**
 * Where a node stands, as a project's «Расположение» reads it: its department's name, or
 * «Организация» at the top (design § 4). The department's id is the node's parent.
 */
export function placeOfProjectNode(node: Pick<AreaNode, 'path' | 'name' | 'parentId'>): {
  id: string | null;
  label: string;
} {
  if (node.parentId === null) {
    return { id: null, label: ORGANIZATION_PLACE };
  }
  const own = ` / ${node.name}`;
  const label = node.path.endsWith(own) ? node.path.slice(0, -own.length) : node.path;
  return { id: node.parentId, label };
}

/**
 * The tree's initial expansion (design D-3): every node with children while the tree has at most
 * `TREE_EXPAND_ALL_LIMIT` nodes, none (the top level alone) above that.
 */
export function initialExpansion(areas: readonly AreaNode[]): Set<string> {
  if (areas.length > TREE_EXPAND_ALL_LIMIT) {
    return new Set();
  }
  return new Set(areas.filter((area) => hasChildren(areas, area.id)).map((area) => area.id));
}

/** Every node that has children: «Развернуть всё». */
export function expandableIds(areas: readonly AreaNode[]): Set<string> {
  return new Set(areas.filter((area) => hasChildren(areas, area.id)).map((area) => area.id));
}

/** A local filter of a picker (design D-1): typed text narrows by the label and the key, ignoring case. */
export function narrow<O>(
  options: readonly O[],
  text: string,
  texts: (option: O) => readonly string[],
): { shown: O[]; total: number } {
  const needle = text.trim().toLowerCase();
  const matches =
    needle === ''
      ? [...options]
      : options.filter((option) =>
          texts(option).some((candidate) => candidate.toLowerCase().includes(needle)),
        );
  return { shown: matches.slice(0, PICKER_OPTION_LIMIT), total: matches.length };
}
