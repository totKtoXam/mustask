import type { AreaNode } from '../api/organizationsApi';
import { PICKER_OPTION_LIMIT, TREE_EXPAND_ALL_LIMIT, areaPage } from './areaTexts';

/**
 * The organization's area tree as the pickers and the tree registry read it. The read gives it in
 * pre-order, each node before its children and siblings in display order (mod-018 api-contract § 2);
 * nothing here sorts it again.
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

/** How many areas are beneath the node (design § 5.3: «… вместе со всеми вложенными областями: 2»). */
export function descendantCount(areas: readonly AreaNode[], areaId: string): number {
  return subtreeIds(areas, areaId).size - 1;
}

/** Whether the tree knows a child of the area (design § 5.4). */
export function hasChildren(areas: readonly AreaNode[], areaId: string): boolean {
  return areas.some((area) => area.parentId === areaId);
}

/**
 * One choice of a place picker: an area by its path, or the top level (`id: null`, design D-5). The
 * top level reads «Верхний уровень» and stands first.
 */
export interface PlaceOption {
  id: string | null;
  label: string;
}

export const TOP_LEVEL_OPTION: PlaceOption = { id: null, label: areaPage.topLevel };

/** The places a new area can take: «Верхний уровень» (only with `area.create`), then every area by path. */
export function parentOptions(areas: readonly AreaNode[], canTopLevel: boolean): PlaceOption[] {
  const places = areas.map((area) => ({ id: area.id, label: area.path }));
  return canTopLevel ? [TOP_LEVEL_OPTION, ...places] : places;
}

/**
 * The move picker's destinations, in tree order (design § 5.3, D-5): «Верхний уровень» first unless
 * the area is top-level already and only with `area.create`, then every area except the node, its
 * subtree and its current parent. Depth and name rules are refused on saving, not copied here.
 */
export function moveOptions(
  areas: readonly AreaNode[],
  areaId: string,
  canTopLevel: boolean,
): PlaceOption[] {
  const node = areas.find((area) => area.id === areaId);
  const excluded = subtreeIds(areas, areaId);
  if (node?.parentId) {
    excluded.add(node.parentId);
  }
  const places = areas
    .filter((area) => !excluded.has(area.id))
    .map((area) => ({ id: area.id, label: area.path }));
  return canTopLevel && node?.parentId ? [TOP_LEVEL_OPTION, ...places] : places;
}

/**
 * The tree's initial expansion (design D-3): every area with children while the tree has at most
 * `TREE_EXPAND_ALL_LIMIT` areas, none (the top level alone) above that.
 */
export function initialExpansion(areas: readonly AreaNode[]): Set<string> {
  if (areas.length > TREE_EXPAND_ALL_LIMIT) {
    return new Set();
  }
  return new Set(areas.filter((area) => hasChildren(areas, area.id)).map((area) => area.id));
}

/** Every area that has children: «Развернуть всё». */
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
