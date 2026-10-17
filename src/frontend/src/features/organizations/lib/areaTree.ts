import type { AreaNode } from '../api/organizationsApi';

/**
 * The area tree as the pickers and the tree registry read it. The read gives
 * it in pre-order, each node before its children and siblings in display
 * order (mod-017 api-contract § 2.10); nothing here sorts it again. An area
 * owns nothing (BR-OT-10): the owner pickers and the default-area picker of
 * mod-002 are gone with it.
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

/** The root, the one node without a parent. */
export function rootOf(areas: readonly AreaNode[]): AreaNode | undefined {
  return areas.find((area) => area.parentId === null);
}
