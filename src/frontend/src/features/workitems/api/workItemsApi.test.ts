import { describe, expect, it } from 'vitest';
import { ApiError } from '../../../shared/api/httpClient';
import { serveWorkItems } from '../testApi';
import { hierarchyDataset, ids, recordOf } from '../testFixtures';
import { getParentCandidates, getWorkItem, saveWorkItem, setWorkItemParent } from './workItemsApi';

// feat-001 api-contract § 2.2, § 3.3, § 3.4 through the slice's own functions,
// against the in-memory API the component tests use.

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

    const saved = await saveWorkItem(ORG, MSK, 'MSK-US-2', {
      storyPoints: { value: 5 },
      risk: { value: 2 },
      acceptanceCriteria: 'Вход без пароля',
    });

    expect(api.writes()).toEqual([
      {
        method: 'PATCH',
        path: `/api/organizations/${ORG}/projects/${MSK}/workitems/MSK-US-2`,
        body: {
          storyPoints: { value: 5 },
          risk: { value: 2 },
          acceptanceCriteria: 'Вход без пароля',
        },
      },
    ]);
    expect(saved).toMatchObject({ storyPoints: 5, risk: 2, acceptanceCriteria: 'Вход без пароля' });
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
