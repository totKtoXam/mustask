import { describe, expect, it } from 'vitest';
import type { WorkItem, WorkItemRef } from '../api/workItemsApi';
import { changesOf, draftOf, hasChanges, rebaseDraft } from './draft';

function ref(id: string, identifier: string, title: string): WorkItemRef {
  return {
    id,
    identifier,
    type: { id: 'type-feat', key: 'FEAT', name: 'Фича', appearance: 'feature' },
    title,
    state: { id: 'state-new', name: 'Новая', category: { code: 'Proposed', label: 'Новые' } },
  };
}

const FEAT_6 = ref('feat-6', 'MSK-FEAT-6', 'Вход по ссылке');
const FEAT_8 = ref('feat-8', 'MSK-FEAT-8', 'Вход через SSO');

const ITEM: WorkItem = {
  id: 'item-1',
  identifier: 'MSK-BUG-4',
  number: 4,
  projectId: 'project-1',
  type: { id: 'type-bug', key: 'BUG', name: 'Баг', appearance: 'bug' },
  title: 'Падение при входе',
  description: '',
  state: {
    id: 'state-active',
    name: 'Активна',
    category: { code: 'InProgress', label: 'В работе' },
  },
  assignee: { id: 'anna', displayName: 'Анна Смирнова', email: 'anna@example.com' },
  area: { id: 'area-api', path: 'Платформа / API' },
  createdBy: 'creator',
  createdAt: '2026-10-05T14:30:00+00:00',
  changedAt: '2026-10-05T14:30:00+00:00',
  parent: null,
  children: [],
};

// BR-M3-25, api-contract § 2.4: a save sends exactly the fields that changed.

describe('changesOf', () => {
  it('is empty for the draft of the loaded item: «Сохранить» stays disabled', () => {
    const body = changesOf(ITEM, draftOf(ITEM));
    expect(body).toEqual({});
    expect(hasChanges(body)).toBe(false);
  });

  it('carries only the title and the description when only they changed', () => {
    const draft = { ...draftOf(ITEM), title: 'Новый', description: 'Шаги' };
    expect(changesOf(ITEM, draft)).toEqual({ title: 'Новый', description: 'Шаги' });
  });

  it('carries the state alone when only the state changed', () => {
    expect(changesOf(ITEM, { ...draftOf(ITEM), stateId: 'state-closed' })).toEqual({
      stateId: 'state-closed',
    });
  });

  it('clears the assignee with `{ userId: null }`, never `assignee: null`', () => {
    expect(changesOf(ITEM, { ...draftOf(ITEM), assignee: null })).toEqual({
      assignee: { userId: null },
    });
  });

  it('assigns another user by id, and an unchanged assignee is not sent', () => {
    const other = { id: 'petr', displayName: 'Пётр', email: '' };
    expect(changesOf(ITEM, { ...draftOf(ITEM), assignee: other })).toEqual({
      assignee: { userId: 'petr' },
    });
    expect(
      changesOf(ITEM, { ...draftOf(ITEM), assignee: { ...ITEM.assignee!, displayName: 'x' } }),
    ).toEqual({});
  });

  it('carries the area by id; the same area under another path is no change', () => {
    expect(
      changesOf(ITEM, { ...draftOf(ITEM), area: { id: 'area-root', path: 'Мастаск' } }),
    ).toEqual({ areaId: 'area-root' });
    expect(
      changesOf(ITEM, { ...draftOf(ITEM), area: { id: 'area-api', path: 'Отчёты / Шлюз' } }),
    ).toEqual({});
  });

  it('a value changed and changed back is no change', () => {
    const changed = { ...draftOf(ITEM), title: 'Другой' };
    const back = { ...changed, title: ITEM.title };
    expect(hasChanges(changesOf(ITEM, changed))).toBe(true);
    expect(hasChanges(changesOf(ITEM, back))).toBe(false);
  });

  it('an empty description is `""`, sent as such', () => {
    const item = { ...ITEM, description: 'Было' };
    expect(changesOf(item, { ...draftOf(item), description: '' })).toEqual({ description: '' });
  });
});

// feat-001 api-contract § 2.3, § 3.3: the parent rides the same rule — sent
// only when another one, or none, is chosen, with the loaded one as seen.

describe('changesOf: «Родитель» (feat-001 BR-H-4, BR-H-6)', () => {
  it('sets a parent where none was seen', () => {
    expect(changesOf(ITEM, { ...draftOf(ITEM), parent: FEAT_6 })).toEqual({
      parent: { parentId: 'feat-6', seenParentId: null },
    });
  });

  it('replaces the stored parent, naming it as seen', () => {
    const item = { ...ITEM, parent: FEAT_6 };
    expect(changesOf(item, { ...draftOf(item), parent: FEAT_8 })).toEqual({
      parent: { parentId: 'feat-8', seenParentId: 'feat-6' },
    });
  });

  it('clears with `parentId: null`, never `parent: null`', () => {
    const item = { ...ITEM, parent: FEAT_6 };
    expect(changesOf(item, { ...draftOf(item), parent: null })).toEqual({
      parent: { parentId: null, seenParentId: 'feat-6' },
    });
  });

  it('is not sent when untouched, picked again, or changed and changed back', () => {
    const item = { ...ITEM, parent: FEAT_6 };
    expect(changesOf(item, { ...draftOf(item), title: 'Другой' })).toEqual({ title: 'Другой' });
    expect(changesOf(item, { ...draftOf(item), parent: { ...FEAT_6, title: 'x' } })).toEqual({});
    expect(hasChanges(changesOf(ITEM, { ...draftOf(ITEM), parent: FEAT_6 }))).toBe(true);
    expect(hasChanges(changesOf(ITEM, { ...draftOf(ITEM), parent: null }))).toBe(false);
  });

  it('travels with the other changed fields in one body', () => {
    expect(changesOf(ITEM, { ...draftOf(ITEM), title: 'Новый', parent: FEAT_6 })).toEqual({
      title: 'Новый',
      parent: { parentId: 'feat-6', seenParentId: null },
    });
  });
});

describe('rebaseDraft: the fresh read after `parent_changed` (feat-001 spec § Screens and copy)', () => {
  it('takes the stored parent, keeps the touched fields, and takes the rest from the fresh read', () => {
    const fresh: WorkItem = {
      ...ITEM,
      description: 'Описал другой участник',
      parent: FEAT_8,
      changedAt: '2026-10-05T15:00:00+00:00',
    };
    const draft = { ...draftOf(ITEM), title: 'Мой заголовок', parent: FEAT_6 };

    const rebased = rebaseDraft(ITEM, fresh, draft);

    expect(rebased).toEqual({
      ...draftOf(fresh),
      title: 'Мой заголовок',
    });
    // The next save carries only the user's own change.
    expect(changesOf(fresh, rebased)).toEqual({ title: 'Мой заголовок' });
  });

  it('keeps a touched assignee and area, compared by id', () => {
    const fresh: WorkItem = { ...ITEM, parent: FEAT_8 };
    const area = { id: 'area-root', path: 'Мастаск' };
    const draft = { ...draftOf(ITEM), assignee: null, area, parent: null };

    expect(rebaseDraft(ITEM, fresh, draft)).toEqual({
      ...draftOf(fresh),
      assignee: null,
      area,
    });
  });
});
