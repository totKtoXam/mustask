import { describe, expect, it } from 'vitest';
import { ApiError } from '../../../shared/api/httpClient';
import { serveWorkItems } from '../testApi';
import { deleteRecord, hierarchyDataset, ids, recordOf, stateId } from '../testFixtures';
import {
  deleteWorkItem,
  getParentCandidates,
  getWorkItem,
  getWorkItems,
  purgeWorkItem,
  restoreWorkItem,
  saveWorkItem,
  setWorkItemParent,
} from './workItemsApi';

// feat-001 api-contract § 2.2, § 3.3, § 3.4 through the slice's own functions,
// against the in-memory API the component tests use; mod-005 api-contract § 2 below.

const ORG = ids.example;
const MSK = ids.msk;

describe('getParentCandidates (feat-001 api-contract § 3.4)', () => {
  it('asks with the text and no `levelAbove`, and gets the allowed items, most recently changed first', async () => {
    const api = serveWorkItems(hierarchyDataset());

    const answer = await getParentCandidates(ORG, MSK, 'MSK-US-2', '');

    expect(api.reads('parent-candidates').map((request) => request.path)).toEqual([
      `/api/organizations/${ORG}/projects/${MSK}/workitems/MSK-US-2/parent-candidates?text=`,
    ]);
    expect(answer.items.map((item) => item.identifier)).toEqual([
      'MSK-FEAT-8',
      'MSK-FEAT-6',
      'MSK-BUG-4',
      'MSK-EPIC-1',
    ]);
    expect(answer.moreMatched).toBe(false);
  });

  it('sends `levelAbove=true` only when asked, which narrows a feature’s candidates to epics', async () => {
    const api = serveWorkItems(hierarchyDataset());

    const answer = await getParentCandidates(ORG, MSK, 'MSK-FEAT-6', 'вход', { levelAbove: true });

    expect(api.reads('parent-candidates').at(-1)?.path).toBe(
      `/api/organizations/${ORG}/projects/${MSK}/workitems/MSK-FEAT-6/parent-candidates?text=${encodeURIComponent('вход')}&levelAbove=true`,
    );
    expect(answer.items.map((item) => item.identifier)).toEqual(['MSK-EPIC-1']);
  });
});

describe('setWorkItemParent (feat-001 api-contract § 3.2)', () => {
  it('PUTs `{ parentId, seenParentId }` to `…/parent` and answers the stored parent', async () => {
    const data = hierarchyDataset();
    const api = serveWorkItems(data);
    const feat6 = recordOf(data, 6);

    const answer = await setWorkItemParent(ORG, MSK, 'MSK-US-2', {
      parentId: feat6.id,
      seenParentId: null,
    });

    expect(api.writes()).toEqual([
      {
        method: 'PUT',
        path: `/api/organizations/${ORG}/projects/${MSK}/workitems/MSK-US-2/parent`,
        body: { parentId: feat6.id, seenParentId: null },
      },
    ]);
    expect(answer.parent?.identifier).toBe('MSK-FEAT-6');
    expect(recordOf(data, 2).parentId).toBe(feat6.id);
  });

  it('clears with `parentId: null`, answering `parent: null`', async () => {
    const data = hierarchyDataset();
    serveWorkItems(data);
    recordOf(data, 2).parentId = recordOf(data, 6).id;

    const answer = await setWorkItemParent(ORG, MSK, 'MSK-US-2', {
      parentId: null,
      seenParentId: recordOf(data, 6).id,
    });

    expect(answer).toEqual({ parent: null });
    expect(recordOf(data, 2).parentId).toBeNull();
  });

  it('a stale `seenParentId` is refused with `workitems.link.parent_changed`', async () => {
    const data = hierarchyDataset();
    serveWorkItems(data);
    recordOf(data, 2).parentId = recordOf(data, 8).id;

    const refusal = await setWorkItemParent(ORG, MSK, 'MSK-US-2', {
      parentId: recordOf(data, 6).id,
      seenParentId: null,
    }).catch((error: unknown) => error);

    expect((refusal as ApiError).errorCode).toBe('workitems.link.parent_changed');
    expect(recordOf(data, 2).parentId).toBe(recordOf(data, 8).id);
  });
});

describe('the item’s hierarchy and the save’s `parent` (feat-001 api-contract § 2.2, § 3.3)', () => {
  it('a save with `parent` answers the stored parent, and the parent’s read lists the child', async () => {
    const data = hierarchyDataset();
    serveWorkItems(data);
    const feat6 = recordOf(data, 6);

    const saved = await saveWorkItem(ORG, MSK, 'MSK-US-2', {
      parent: { parentId: feat6.id, seenParentId: null },
    });

    expect(saved.parent?.identifier).toBe('MSK-FEAT-6');
    expect(saved.children).toEqual([]);
    const parent = await getWorkItem(ORG, MSK, 'MSK-FEAT-6');
    expect(parent.children.map((child) => child.identifier)).toEqual(['MSK-US-2']);
  });

  it('a stale `seenParentId` is refused with `workitems.link.parent_changed` and nothing changes', async () => {
    const data = hierarchyDataset();
    serveWorkItems(data);
    recordOf(data, 2).parentId = recordOf(data, 8).id;

    const refusal = await saveWorkItem(ORG, MSK, 'MSK-US-2', {
      title: 'Другое',
      parent: { parentId: recordOf(data, 6).id, seenParentId: null },
    }).catch((error: unknown) => error);

    expect(refusal).toBeInstanceOf(ApiError);
    expect((refusal as ApiError).errorCode).toBe('workitems.link.parent_changed');
    expect(recordOf(data, 2).parentId).toBe(recordOf(data, 8).id);
    expect(recordOf(data, 2).title).toBe('Письмо со ссылкой');
  });
});

describe('the fields per type (feat-022 api-contract § 2, § 3.2)', () => {
  it('the read carries all 17 fields: the type’s defaults, `null` where the type lacks one', async () => {
    serveWorkItems(hierarchyDataset());

    const bug = await getWorkItem(ORG, MSK, 'MSK-BUG-4');

    expect(bug).toMatchObject({
      priority: 2,
      severity: 3,
      reproSteps: '',
      systemInfo: '',
      storyPoints: null,
      activity: null,
      valueArea: null,
      acceptanceCriteria: null,
      startDate: null,
    });
  });

  it('PATCHes texts as strings and every other field in `{ value }`, and answers them as stored', async () => {
    const data = hierarchyDataset();
    const api = serveWorkItems(data);

    const criteria = [
      {
        given: 'пользователь на странице входа',
        when: 'вводит пароль',
        then: 'происходит перенаправление в личный кабинет',
      },
    ];

    const saved = await saveWorkItem(ORG, MSK, 'MSK-US-2', {
      storyPoints: { value: 5 },
      risk: { value: 2 },
      acceptanceCriteria: criteria,
    });

    expect(api.writes()).toEqual([
      {
        method: 'PATCH',
        path: `/api/organizations/${ORG}/projects/${MSK}/workitems/MSK-US-2`,
        body: {
          storyPoints: { value: 5 },
          risk: { value: 2 },
          acceptanceCriteria: criteria,
        },
      },
    ]);
    expect(saved).toMatchObject({ storyPoints: 5, risk: 2, acceptanceCriteria: criteria });
    expect(recordOf(data, 2).fields.storyPoints).toBe(5);
  });

  it('a refused value stores nothing; its reason is keyed by the JSON name', async () => {
    const data = hierarchyDataset();
    serveWorkItems(data);

    const refusal = await saveWorkItem(ORG, MSK, 'MSK-FEAT-6', {
      risk: { value: 1 },
      businessValue: { value: 10_000 },
    }).catch((error: unknown) => error);

    expect((refusal as ApiError).errorCode).toBe('workitems.validation_failed');
    expect((refusal as ApiError).body?.errors?.businessValue?.[0]?.code).toBe(
      'workitems.workitem.number_out_of_range',
    );
    expect(recordOf(data, 6).fields.risk).toBeNull();
  });
});

describe('the recycle bin (mod-005 api-contract § 2)', () => {
  const ITEM_URL = `/api/organizations/${ORG}/projects/${MSK}/workitems/MSK-US-2`;

  it('POSTs `…/delete` with no body and answers the deleted item with the marks of the deletion', async () => {
    const data = hierarchyDataset();
    const api = serveWorkItems(data);
    recordOf(data, 2).stateId = stateId('US', 'Активна');

    const answer = await deleteWorkItem(ORG, MSK, 'MSK-US-2');

    expect(api.writes()).toEqual([{ method: 'POST', path: `${ITEM_URL}/delete`, body: undefined }]);
    expect(answer.state).toMatchObject({ name: 'Удалена', category: { code: 'Removed' } });
    expect(answer.deletion).toMatchObject({
      fromState: { name: 'Активна' },
      by: { id: ids.anna, displayName: 'Анна Смирнова', isActive: true, isMember: true },
    });
    expect(answer.deletion?.at).toBe(answer.changedAt);
  });

  it('a second delete is refused with `workitems.workitem.deleted`, the first one’s marks kept', async () => {
    const data = hierarchyDataset();
    serveWorkItems(data);
    recordOf(data, 2).stateId = stateId('US', 'Активна');
    await deleteWorkItem(ORG, MSK, 'MSK-US-2');

    const refusal = await deleteWorkItem(ORG, MSK, 'MSK-US-2').catch((error: unknown) => error);

    expect((refusal as ApiError).errorCode).toBe('workitems.workitem.deleted');
    expect((await getWorkItem(ORG, MSK, 'MSK-US-2')).deletion?.fromState.name).toBe('Активна');
  });

  it('POSTs `{ stateId }` to `…/restore` and answers the live item with `deletion: null`', async () => {
    const data = hierarchyDataset();
    const api = serveWorkItems(data);
    deleteRecord(recordOf(data, 2));
    const chosen = stateId('US', 'Активна');

    const answer = await restoreWorkItem(ORG, MSK, 'MSK-US-2', chosen);

    expect(api.writes()).toEqual([
      { method: 'POST', path: `${ITEM_URL}/restore`, body: { stateId: chosen } },
    ]);
    expect(answer.state.id).toBe(chosen);
    expect(answer.deletion).toBeNull();
  });

  it('restores to the state held when none is sent, and to the first state for an item with no marks', async () => {
    const data = hierarchyDataset();
    serveWorkItems(data);
    recordOf(data, 2).stateId = stateId('US', 'Разрешена');
    deleteRecord(recordOf(data, 2));
    deleteRecord(recordOf(data, 3), { marks: false });

    const held = await restoreWorkItem(ORG, MSK, 'MSK-US-2');
    const first = await restoreWorkItem(ORG, MSK, 'MSK-TASK-3');

    expect(held.state.name).toBe('Разрешена');
    expect(first.state.name).toBe('Новая');
  });

  it('refuses `not_deleted` before `state_not_allowed`, and a state of another type or «Удалена»', async () => {
    const data = hierarchyDataset();
    serveWorkItems(data);
    const taskState = stateId('TASK', 'Активна');

    const live = await restoreWorkItem(ORG, MSK, 'MSK-US-2', taskState).catch(
      (error: unknown) => error,
    );
    deleteRecord(recordOf(data, 2));
    const foreign = await restoreWorkItem(ORG, MSK, 'MSK-US-2', taskState).catch(
      (error: unknown) => error,
    );
    const removed = await restoreWorkItem(ORG, MSK, 'MSK-US-2', stateId('US', 'Удалена')).catch(
      (error: unknown) => error,
    );

    expect((live as ApiError).errorCode).toBe('workitems.workitem.not_deleted');
    expect((foreign as ApiError).errorCode).toBe('workitems.workitem.state_not_allowed');
    expect((removed as ApiError).errorCode).toBe('workitems.workitem.state_not_allowed');
    expect((await getWorkItem(ORG, MSK, 'MSK-US-2')).state.name).toBe('Удалена');
  });

  it('POSTs `…/purge` and answers 204; the address then answers not found and the children lose the link', async () => {
    const data = hierarchyDataset();
    const api = serveWorkItems(data);
    recordOf(data, 2).parentId = recordOf(data, 6).id;
    recordOf(data, 3).parentId = recordOf(data, 2).id;
    deleteRecord(recordOf(data, 2));

    const answer = await purgeWorkItem(ORG, MSK, 'MSK-US-2');

    expect(answer).toBeUndefined();
    expect(api.writes()).toEqual([{ method: 'POST', path: `${ITEM_URL}/purge`, body: undefined }]);
    const gone = await getWorkItem(ORG, MSK, 'MSK-US-2').catch((error: unknown) => error);
    expect((gone as ApiError).errorCode).toBe('workitems.workitem.not_found');
    expect((await getWorkItem(ORG, MSK, 'MSK-TASK-3')).parent).toBeNull();
  });

  it('a purge of a live item is refused with `not_deleted`', async () => {
    serveWorkItems(hierarchyDataset());

    const refusal = await purgeWorkItem(ORG, MSK, 'MSK-US-2').catch((error: unknown) => error);

    expect((refusal as ApiError).errorCode).toBe('workitems.workitem.not_deleted');
  });

  it('the list stops returning a deleted item and does not count it (§ 3.2); a save of it is refused first (§ 3.4)', async () => {
    const data = hierarchyDataset();
    serveWorkItems(data);
    const before = await getWorkItems(ORG, MSK, 1);
    deleteRecord(recordOf(data, 2));

    const after = await getWorkItems(ORG, MSK, 1);
    // The title is 300 characters: the 409 comes before the 400 (§ 3.1).
    const refused = await saveWorkItem(ORG, MSK, 'MSK-US-2', { title: 'я'.repeat(300) }).catch(
      (error: unknown) => error,
    );

    expect(after.count).toBe(before.count - 1);
    expect(after.data.map((item) => item.identifier)).not.toContain('MSK-US-2');
    expect((refused as ApiError).errorCode).toBe('workitems.workitem.deleted');
  });
});
