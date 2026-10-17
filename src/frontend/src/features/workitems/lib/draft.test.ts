import { describe, expect, it } from 'vitest';
import type { WorkItem, WorkItemRef } from '../api/workItemsApi';
import { defaultFields } from '../testFixtures';
import {
  changesOf,
  draftErrors,
  draftOf,
  hasChanges,
  isDirty,
  rebaseDraft,
  type WorkItemDraft,
} from './draft';
import { INCOMPLETE_DATE } from './fieldValues';

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
  team: { id: 'team-mars', name: 'MARS' },
  createdBy: 'creator',
  createdAt: '2026-10-05T14:30:00+00:00',
  changedAt: '2026-10-05T14:30:00+00:00',
  parent: null,
  children: [],
  ...defaultFields('bug'),
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

  it('carries the team in a wrapper: a team by id, «Без команды» as `{ teamId: null }`, the same team is no change (mod-017 § 0.2)', () => {
    const venus = { id: 'team-venus', name: 'VENUS' };
    expect(changesOf(ITEM, { ...draftOf(ITEM), team: venus })).toEqual({
      team: { teamId: 'team-venus' },
    });
    expect(changesOf(ITEM, { ...draftOf(ITEM), team: null })).toEqual({ team: { teamId: null } });
    expect(
      changesOf(ITEM, {
        ...draftOf(ITEM),
        team: { id: 'team-mars', name: 'MARS (переименована)' },
      }),
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

// feat-022 api-contract § 3.2, BR-M3-38: the fields per type ride the same
// rule — only the type's fields that changed, texts as strings, every other
// field in `{ value }`, `{ value: null }` to empty.

function itemOf(appearance: string, fields: Partial<WorkItem> = {}): WorkItem {
  return { ...ITEM, ...defaultFields(appearance), type: { ...ITEM.type, appearance }, ...fields };
}

function withFields(item: WorkItem, fields: Partial<WorkItemDraft['fields']>): WorkItemDraft {
  const draft = draftOf(item);
  return { ...draft, fields: { ...draft.fields, ...fields } };
}

describe('changesOf: the fields per type (feat-022 US-1 «A save sends only the fields the user changed»)', () => {
  it.each(['epic', 'feature', 'story', 'task', 'bug'])(
    'is empty for the loaded %s with its defaults: «Сохранить» stays disabled',
    (appearance) => {
      const item = itemOf(appearance);
      expect(changesOf(item, draftOf(item))).toEqual({});
      expect(isDirty(item, draftOf(item))).toBe(false);
    },
  );

  it('shows a stored number with «,» and no trailing zeros, and a stored list value by its key', () => {
    const item = itemOf('task', { remainingWork: 5.5, originalEstimate: 8, activity: 'Testing' });
    expect(draftOf(item).fields).toMatchObject({
      remainingWork: '5,5',
      originalEstimate: '8',
      completedWork: '',
      activity: 'Testing',
      priority: '2',
    });
  });

  it('sends US-1’s three values of a story in one body: points, risk in `{ value }`, and structured acceptance criteria', () => {
    const item = itemOf('story');
    const draft = withFields(item, {
      storyPoints: '5',
      risk: '2',
      acceptanceCriteria: [
        {
          id: 'crit-1',
          given: 'пользователь на странице',
          when: 'вводит пароль',
          then: 'входит',
        },
      ],
    });
    expect(changesOf(item, draft)).toEqual({
      storyPoints: { value: 5 },
      risk: { value: 2 },
      acceptanceCriteria: [
        {
          given: 'пользователь на странице',
          when: 'вводит пароль',
          then: 'входит',
        },
      ],
    });
  });

  it('reads «,» and «.» alike and ignores spaces at either end (US-3: «5,5», «2.5»)', () => {
    const item = itemOf('task');
    expect(
      changesOf(item, withFields(item, { remainingWork: ' 5,5 ', completedWork: '2.5' })),
    ).toEqual({ remainingWork: { value: 5.5 }, completedWork: { value: 2.5 } });
  });

  it('«2,50» against a stored 2,5 is no change (US-1 «Nothing to save»)', () => {
    const item = itemOf('story', { storyPoints: 2.5 });
    const draft = withFields(item, { storyPoints: '2,50' });
    expect(changesOf(item, draft)).toEqual({});
    expect(isDirty(item, draft)).toBe(false);
  });

  it('empties a number with `{ value: null }`, and 0 is a value of its own (US-2, US-3)', () => {
    const item = itemOf('epic', { effort: 5 });
    expect(changesOf(item, withFields(item, { effort: '' }))).toEqual({ effort: { value: null } });
    expect(changesOf(item, withFields(item, { effort: '0' }))).toEqual({ effort: { value: 0 } });
  });

  it('sends a list by its key — a number for Приоритет, Риск, Серьёзность, a string for the others', () => {
    const item = itemOf('bug');
    expect(
      changesOf(item, withFields(item, { priority: '1', severity: '1', activity: 'Development' })),
    ).toEqual({
      priority: { value: 1 },
      severity: { value: 1 },
      activity: { value: 'Development' },
    });
    const epic = itemOf('epic', { risk: 2 });
    expect(changesOf(epic, withFields(epic, { risk: '', valueArea: 'Architectural' }))).toEqual({
      risk: { value: null },
      valueArea: { value: 'Architectural' },
    });
  });

  it('sends a date as `YYYY-MM-DD`, empties it with `{ value: null }`', () => {
    const item = itemOf('feature', { targetDate: '2027-03-31' });
    expect(changesOf(item, withFields(item, { startDate: '2026-11-01', targetDate: '' }))).toEqual({
      startDate: { value: '2026-11-01' },
      targetDate: { value: null },
    });
  });

  it('empties a type text with `""`, never `null`', () => {
    const item = itemOf('bug', { reproSteps: 'Шаги' });
    expect(changesOf(item, withFields(item, { reproSteps: '' }))).toEqual({ reproSteps: '' });
  });

  it('never sends a field the type lacks (BR-M3-30), whatever the draft holds', () => {
    const item = itemOf('task');
    const draft = withFields(item, {
      storyPoints: '5',
      acceptanceCriteria: [{ id: 'crit-x', given: 'x', when: 'y', then: 'z' }],
      startDate: '2026-01-01',
    });
    expect(changesOf(item, draft)).toEqual({});
    expect(isDirty(item, draft)).toBe(false);
  });

  it('a number’s text that is not a number, or a date typed in part, is not sent but counts as a change', () => {
    const epic = itemOf('epic');
    const notANumber = withFields(epic, { effort: 'пять' });
    expect(changesOf(epic, notANumber)).toEqual({});
    expect(isDirty(epic, notANumber)).toBe(true);

    const partial = withFields(epic, { startDate: INCOMPLETE_DATE });
    expect(changesOf(epic, partial)).toEqual({});
    expect(isDirty(epic, partial)).toBe(true);
  });

  it('travels with mod-003’s fields and the parent in one body', () => {
    const item = itemOf('task');
    const draft = { ...withFields(item, { priority: '4' }), title: 'Новый', parent: FEAT_6 };
    expect(changesOf(item, draft)).toEqual({
      title: 'Новый',
      parent: { parentId: 'feat-6', seenParentId: null },
      priority: { value: 4 },
    });
  });
});

describe('draftErrors: the browser’s checks (feat-022 spec § Controls and copy)', () => {
  it.each([
    ['effort', '-1', 'Допустимо значение от 0 до 9 999.'],
    ['timeCriticality', '10000', 'Допустимо значение от 0 до 9 999.'],
    ['effort', '2,555', 'Не более двух знаков после запятой.'],
    ['businessValue', '2,5', 'Введите целое число.'],
    ['effort', 'пять', 'Введите число.'],
  ] as const)('US-2: «%s» = «%s» is refused with «%s»', (name, text, message) => {
    const epic = itemOf('epic');
    expect(draftErrors(epic, withFields(epic, { [name]: text }))).toEqual({ [name]: message });
  });

  it('US-3: «1,333» hours are refused, never rounded', () => {
    const task = itemOf('task');
    expect(draftErrors(task, withFields(task, { originalEstimate: '1,333' }))).toEqual({
      originalEstimate: 'Не более двух знаков после запятой.',
    });
  });

  it('accepts 0, 9 999, two decimals, a whole Бизнес-ценность and an emptied field', () => {
    const epic = itemOf('epic', { effort: 3 });
    expect(
      draftErrors(
        epic,
        withFields(epic, { effort: '', timeCriticality: '9999', businessValue: '0' }),
      ),
    ).toEqual({});
    const task = itemOf('task');
    expect(draftErrors(task, withFields(task, { remainingWork: '0,25' }))).toEqual({});
  });

  it('a date typed in part, and a date outside 2000–2099', () => {
    const epic = itemOf('epic');
    expect(draftErrors(epic, withFields(epic, { startDate: INCOMPLETE_DATE }))).toEqual({
      startDate: 'Введите дату полностью.',
    });
    expect(draftErrors(epic, withFields(epic, { targetDate: '1999-12-31' }))).toEqual({
      targetDate: 'Укажите дату с 2000 по 2099 год.',
    });
  });

  it('US-2: a start after the stored target is refused under «Целевая дата», though only the start changed', () => {
    const epic = itemOf('epic', { targetDate: '2027-03-31' });
    expect(draftErrors(epic, withFields(epic, { startDate: '2027-04-01' }))).toEqual({
      targetDate: 'Целевая дата не может быть раньше даты начала.',
    });
    expect(
      draftErrors(epic, withFields(epic, { startDate: '2027-04-01', targetDate: '2027-06-30' })),
    ).toEqual({});
    expect(draftErrors(epic, withFields(epic, { targetDate: '' }))).toEqual({});
  });

  it('a type text of 100 001 characters: «Не более 100 000 символов.»', () => {
    const bug = itemOf('bug');
    expect(draftErrors(bug, withFields(bug, { systemInfo: 'о'.repeat(100_001) }))).toEqual({
      systemInfo: 'Не более 100 000 символов.',
    });
  });

  it('checks only what changed, and «Заголовок» and «Описание» as mod-003 does', () => {
    const story = itemOf('story');
    expect(draftErrors(story, draftOf(story))).toEqual({});
    expect(draftErrors(story, { ...withFields(story, { storyPoints: 'x' }), title: ' ' })).toEqual({
      title: 'Введите заголовок.',
      storyPoints: 'Введите число.',
    });
  });
});

describe('rebaseDraft: the fields per type', () => {
  it('keeps a touched field, takes every other from the fresh read', () => {
    const loaded = itemOf('task');
    const fresh: WorkItem = { ...loaded, remainingWork: 4, parent: FEAT_8 };
    const draft = withFields(loaded, { activity: 'Testing' });

    const rebased = rebaseDraft(loaded, fresh, draft);

    expect(rebased.fields).toMatchObject({ activity: 'Testing', remainingWork: '4' });
    expect(changesOf(fresh, rebased)).toEqual({ activity: { value: 'Testing' } });
  });
});
