import { describe, expect, it } from 'vitest';
import { commandKinds, commandLabel } from './areaCommands';

const admin = { canWrite: true, canCreate: true };

describe('mod-018 design § 3: the commands a node offers, per type', () => {
  it('a department renames, describes and deletes; it neither moves nor takes a sub-area', () => {
    expect(
      commandKinds({ type: 'department', projectActive: null, parentId: null }, admin),
    ).toEqual(['rename', 'describe', 'delete']);
  });

  it('a project node creates a sub-area, describes and moves; it is renamed and deleted with its project', () => {
    expect(commandKinds({ type: 'project', projectActive: true, parentId: 'd' }, admin)).toEqual([
      'createChild',
      'describe',
      'move',
    ]);
  });

  it('a sub-area does all five', () => {
    expect(commandKinds({ type: 'area', projectActive: true, parentId: 'p' }, admin)).toEqual([
      'createChild',
      'rename',
      'describe',
      'move',
      'delete',
    ]);
  });

  it('a project at the top level moves only for the holder of `area.create`', () => {
    const node = { type: 'project', projectActive: true, parentId: null } as const;

    expect(commandKinds(node, { canWrite: true, canCreate: false })).toEqual([
      'createChild',
      'describe',
    ]);
  });

  it('offers nothing on a node of a deactivated project, and nothing without `area.write`', () => {
    expect(commandKinds({ type: 'area', projectActive: false, parentId: 'p' }, admin)).toEqual([]);
    expect(
      commandKinds(
        { type: 'department', projectActive: null, parentId: null },
        { canWrite: false, canCreate: true },
      ),
    ).toEqual([]);
  });

  it('names the delete command for the type', () => {
    expect(commandLabel('delete', 'department')).toBe('Удалить департамент');
    expect(commandLabel('delete', 'area')).toBe('Удалить область');
    expect(commandLabel('move', 'project')).toBe('Переместить');
  });
});
