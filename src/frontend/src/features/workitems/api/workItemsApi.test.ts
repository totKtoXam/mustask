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

describe('the description and its format (feat-018 api-contract § 1..§ 4)', () => {
  const html = '<p>Шаги:</p><ol><li><p>открыть форму</p></li></ol>';

  /** The first reason code of `errors.description` of a refused save. */
  function descriptionReason(refusal: unknown): string | undefined {
    return (refusal as ApiError).body?.errors?.description?.[0]?.code;
  }

  it('every read carries `descriptionFormat`: `plain` for a legacy or empty description', async () => {
    const data = hierarchyDataset();
    recordOf(data, 2).description = 'старый\nтекст';
    serveWorkItems(data);

    const legacy = await getWorkItem(ORG, MSK, 'MSK-US-2');
    const empty = await getWorkItem(ORG, MSK, 'MSK-FEAT-6');

    expect(legacy).toMatchObject({ description: 'старый\nтекст', descriptionFormat: 'plain' });
    expect(empty).toMatchObject({ description: '', descriptionFormat: 'plain' });
  });

  it('no declaration stores the text exactly as sent, as plain (§ 1.1)', async () => {
    serveWorkItems(hierarchyDataset());

    const saved = await saveWorkItem(ORG, MSK, 'MSK-US-2', { description: '<b>x</b>\n' });

    expect(saved).toMatchObject({ description: '<b>x</b>\n', descriptionFormat: 'plain' });
  });

  it('`html` is reduced and stored as html; the answer is the reduced value (§ 1.1)', async () => {
    const data = hierarchyDataset();
    serveWorkItems(data);

    const saved = await saveWorkItem(ORG, MSK, 'MSK-US-2', {
      description: '<b>a</b><script>x()</script>',
      descriptionFormat: 'html',
    });

    expect(saved).toMatchObject({
      description: '<p><strong>a</strong></p>',
      descriptionFormat: 'html',
    });
    expect(recordOf(data, 2).descriptionFormat).toBe('html');
  });

  it('a blank html value is stored as the empty plain description (BR-RT-6)', async () => {
    const data = hierarchyDataset();
    recordOf(data, 2).description = html;
    recordOf(data, 2).descriptionFormat = 'html';
    serveWorkItems(data);

    const saved = await saveWorkItem(ORG, MSK, 'MSK-US-2', {
      description: '<p></p><p> </p>',
      descriptionFormat: 'html',
    });

    expect(saved).toMatchObject({ description: '', descriptionFormat: 'plain' });
  });

  it('the (value, format) pair decides a change: the same html moves nothing, the same text as plain does (§ 1.2)', async () => {
    const data = hierarchyDataset();
    recordOf(data, 2).description = '<p>a</p>';
    recordOf(data, 2).descriptionFormat = 'html';
    serveWorkItems(data);
    const before = recordOf(data, 2).changedAt;

    const same = await saveWorkItem(ORG, MSK, 'MSK-US-2', {
      description: '<p>a</p>',
      descriptionFormat: 'html',
    });
    expect(same.changedAt).toBe(before);

    const asPlain = await saveWorkItem(ORG, MSK, 'MSK-US-2', { description: '<p>a</p>' });
    expect(asPlain).toMatchObject({ description: '<p>a</p>', descriptionFormat: 'plain' });
    expect(asPlain.changedAt).not.toBe(before);
  });

  it('an html re-send of "" over ("", plain) changes nothing (§ 1.1)', async () => {
    const data = hierarchyDataset();
    serveWorkItems(data);
    const before = recordOf(data, 2).changedAt;

    const saved = await saveWorkItem(ORG, MSK, 'MSK-US-2', {
      description: '',
      descriptionFormat: 'html',
    });

    expect(saved.changedAt).toBe(before);
  });

  it('a declaration with no description is ignored (§ 1)', async () => {
    const data = hierarchyDataset();
    recordOf(data, 2).description = 'текст';
    serveWorkItems(data);

    const saved = await saveWorkItem(ORG, MSK, 'MSK-US-2', { descriptionFormat: 'html' });

    expect(saved).toMatchObject({ description: 'текст', descriptionFormat: 'plain' });
  });

  it.each([['markdown'], ['HTML'], [' html'], ['']])(
    'refuses the format %j alone, with or without a description (§ 1, § 4)',
    async (format) => {
      const data = hierarchyDataset();
      serveWorkItems(data);

      for (const body of [
        { descriptionFormat: format },
        { description: 'x', descriptionFormat: format },
      ]) {
        const refusal = await saveWorkItem(
          ORG,
          MSK,
          'MSK-US-2',
          body as Parameters<typeof saveWorkItem>[3],
        ).catch((error: unknown) => error);

        expect((refusal as ApiError).errorCode).toBe('workitems.validation_failed');
        expect(descriptionReason(refusal)).toBe('workitems.workitem.description_format_unknown');
      }
      expect(recordOf(data, 2).description).toBe('');
    },
  );

  it('visible text over 100 000 code points is `description_too_long`, exactly 100 000 is stored (§ 3)', async () => {
    serveWorkItems(hierarchyDataset());

    const stored = await saveWorkItem(ORG, MSK, 'MSK-US-2', {
      description: `<p>${'я'.repeat(100_000)}</p>`,
      descriptionFormat: 'html',
    });
    expect(stored.descriptionFormat).toBe('html');

    const refusal = await saveWorkItem(ORG, MSK, 'MSK-US-2', {
      description: `<p>${'я'.repeat(100_001)}</p>`,
      descriptionFormat: 'html',
    }).catch((error: unknown) => error);
    expect(descriptionReason(refusal)).toBe('workitems.workitem.description_too_long');
  });

  it('reduced markup over 1 000 000 code points is `description_markup_too_large` (§ 3)', async () => {
    serveWorkItems(hierarchyDataset());

    const refusal = await saveWorkItem(ORG, MSK, 'MSK-US-2', {
      // Each pair writes `<strong>я</strong>x` (18 code points): 1 080 000 of markup, 120 000 visible.
      description: `<p>${'<b>я</b>x'.repeat(60_000)}</p>`,
      descriptionFormat: 'html',
    }).catch((error: unknown) => error);

    expect(descriptionReason(refusal)).toBe('workitems.workitem.description_markup_too_large');
  });

  it('a plain value keeps BR-M3-19: 100 000 UTF-16 units, no reduction', async () => {
    serveWorkItems(hierarchyDataset());

    const refusal = await saveWorkItem(ORG, MSK, 'MSK-US-2', {
      description: 'я'.repeat(100_001),
      descriptionFormat: 'plain',
    }).catch((error: unknown) => error);

    expect(descriptionReason(refusal)).toBe('workitems.workitem.description_too_long');
  });
});
