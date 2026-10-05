import { describe, expect, it } from 'vitest';
import { sampleProcessDefinition } from '../../process/testFixtures';
import { WORK_ITEMS_ROUTE, WORK_ITEM_ROUTE, workItemPath, workItemsPath } from '../paths';
import { editableStates, initialState, typeById } from './process';
import { newRequestId } from './requestId';

const UUID_V4 = /^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/;

describe('the process lookups (api-contract § 3; BR-M3-20, BR-M3-21)', () => {
  const types = sampleProcessDefinition.workItemTypes;
  const byKey = (key: string) => types.find((type) => type.key === key)!;

  it('offers a type’s states in the seeded order without its «Удалена»', () => {
    expect(editableStates(byKey('TASK')).map((state) => state.name)).toEqual([
      'Новая',
      'Активна',
      'Закрыта',
    ]);
    expect(editableStates(byKey('BUG')).map((state) => state.name)).toEqual([
      'Новая',
      'Активна',
      'Разрешена',
      'Закрыта',
    ]);
  });

  it('a new item starts in the type’s initial state, «Новая»', () => {
    for (const type of types) {
      expect(initialState(type)?.name).toBe('Новая');
    }
    const unmarked = {
      ...byKey('TASK'),
      states: byKey('TASK').states.map((state) => ({ ...state, isInitial: false })),
    };
    expect(initialState(unmarked)?.name).toBe('Новая');
  });

  it('finds a type by id, or none', () => {
    expect(typeById(types, byKey('EPIC').id)?.name).toBe('Эпик');
    expect(typeById(types, 'nothing')).toBeUndefined();
  });
});

describe('newRequestId (api-contract § 2.2)', () => {
  it('is a fresh v4 uuid each time', () => {
    const first = newRequestId();
    expect(first).toMatch(UUID_V4);
    expect(newRequestId()).not.toBe(first);
  });

  it('is built from getRandomValues where randomUUID is missing (an insecure context)', () => {
    const source = {
      getRandomValues: <T extends ArrayBufferView | null>(array: T): T => {
        if (array instanceof Uint8Array) {
          array.fill(0xff);
        }
        return array;
      },
    };
    expect(newRequestId(source)).toBe('ffffffff-ffff-4fff-bfff-ffffffffffff');
  });
});

describe('the slice’s addresses (D-5)', () => {
  it('builds the list and the form under the project prefix, ids and identifier re-encoded', () => {
    expect(workItemsPath('o-1', 'p 1')).toBe('/organizations/o-1/projects/p%201/workitems');
    expect(workItemPath('o-1', 'p-1', 'MSK-BUG-42')).toBe(
      '/organizations/o-1/projects/p-1/workitems/MSK-BUG-42',
    );
    expect(WORK_ITEMS_ROUTE).toBe('/organizations/:organizationId/projects/:projectId/workitems');
    expect(WORK_ITEM_ROUTE).toBe(
      '/organizations/:organizationId/projects/:projectId/workitems/:identifier',
    );
  });
});
