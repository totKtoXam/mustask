import { describe, expect, it } from 'vitest';
import type { WorkItem } from '../api/workItemsApi';
import { changesOf, draftOf, hasChanges } from './draft';

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
