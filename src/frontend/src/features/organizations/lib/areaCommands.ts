import type { AreaNode } from '../api/organizationsApi';
import { areaCommands } from './areaTexts';

/** The commands a node can have, on its row and on its page (mod-018 design § 3). */
export type AreaCommandKind = 'createChild' | 'rename' | 'describe' | 'move' | 'delete';

/**
 * Which commands a node offers (design § 3, "per type"; nothing else is drawn):
 * a department renames, describes and deletes (no move, no sub-area: OQ-3 A, OQ-7 A); a project
 * node creates a sub-area, describes and moves (it is renamed and deleted with its project: OQ-10 A,
 * OQ-5 A); a sub-area does all five. A node of a deactivated project has none (OQ-6 A), and nobody
 * without `area.write` has any. A project at the top level moves only for the holder of
 * `area.create`, since a move to or from the top level needs it (api-contract § 1).
 */
export function commandKinds(
  node: Pick<AreaNode, 'type' | 'projectActive' | 'parentId'>,
  access: { canWrite: boolean; canCreate: boolean },
): AreaCommandKind[] {
  if (!access.canWrite || node.projectActive === false) {
    return [];
  }
  switch (node.type) {
    case 'department':
      return ['rename', 'describe', 'delete'];
    case 'project':
      return node.parentId === null && !access.canCreate
        ? ['createChild', 'describe']
        : ['createChild', 'describe', 'move'];
    case 'area':
      return ['createChild', 'rename', 'describe', 'move', 'delete'];
  }
}

/** The command's label; the delete command is named for the type. */
export function commandLabel(kind: AreaCommandKind, type: AreaNode['type']): string {
  if (kind === 'delete') {
    return type === 'department' ? areaCommands.deleteDepartment : areaCommands.deleteArea;
  }
  return areaCommands[kind];
}
