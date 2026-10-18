import { describe, expect, it } from 'vitest';
import type { WorkItem, WorkItemRef } from '../api/workItemsApi';
import { defaultFields } from '../testFixtures';
import {
  NO_EDITS,
  cardFieldOrder,
  editedDraft,
  firstErrorField,
  hasUnsavedEdits,
  isUntouched,
  normaliseEdits,
  rebaseEdits,
  touchedChanges,
  withEdit,
  type WorkItemEdits,
} from './edits';

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

const STORY: WorkItem = {
  id: 'item-2',
  identifier: 'MSK-US-2',
  number: 2,
  projectId: 'project-1',
  project: { id: 'project-1', name: 'Мастаск' },
  type: { id: 'type-us', key: 'US', name: 'Пользовательская история', appearance: 'story' },
  title: 'Письмо со ссылкой',
  description: 'Пользователь получает письмо.',
  state: { id: 'state-new', name: 'Новая', category: { code: 'Proposed', label: 'Новые' } },
  assignee: { id: 'anna', displayName: 'Анна Смирнова', email: 'anna@example.com' },
  area: { id: 'area-platform', path: 'Платформа' },
  team: null,
  createdBy: 'creator',
  createdAt: '2026-10-05T14:30:00+00:00',
  changedAt: '2026-10-05T14:30:00+00:00',
  parent: FEAT_6,
  children: [],
  deletion: null,
  ...defaultFields('story'),
  storyPoints: 5,
};

/** The same item after someone else saved it: another title, priority and parent. */
const STORY_LATER: WorkItem = {
  ...STORY,
  title: 'Письмо со ссылкой для входа',
  priority: 1,
  parent: FEAT_8,
  changedAt: '2026-10-05T15:00:00+00:00',
};

describe('the draft of touched fields (BR-C-16)', () => {
  it('holds nothing at first: every field shows the stored value and nothing is unsaved', () => {
    expect(isUntouched(NO_EDITS)).toBe(true);
    expect(editedDraft(STORY, NO_EDITS).title).toBe('Письмо со ссылкой');
    expect(touchedChanges(STORY, NO_EDITS)).toEqual({});
    expect(hasUnsavedEdits(STORY, NO_EDITS)).toBe(false);
  });

  it('lays a touched field over the stored item and leaves the others alone', () => {
    const edits = withEdit(STORY, NO_EDITS, { field: 'title', value: 'Новый' });
    const draft = editedDraft(STORY, edits);

    expect(draft.title).toBe('Новый');
    expect(draft.description).toBe('Пользователь получает письмо.');
    expect(draft.fields.priority).toBe('2');
    expect(edits).toEqual({ title: 'Новый' });
  });

  it('sends the touched fields alone, even when they were typed in another order', () => {
    let edits = withEdit(STORY, NO_EDITS, { field: 'priority', value: '1' });
    edits = withEdit(STORY, edits, { field: 'title', value: 'Письмо для входа' });

    expect(touchedChanges(STORY, edits)).toEqual({
      title: 'Письмо для входа',
      priority: { value: 1 },
    });
    expect(hasUnsavedEdits(STORY, edits)).toBe(true);
  });

  it('counts a value changed and changed back as no edit', () => {
    let edits = withEdit(STORY, NO_EDITS, { field: 'priority', value: '1' });
    edits = withEdit(STORY, edits, { field: 'priority', value: '2' });
    expect(edits).toEqual({});

    edits = withEdit(STORY, NO_EDITS, { field: 'title', value: 'Другой' });
    edits = withEdit(STORY, edits, { field: 'title', value: 'Письмо со ссылкой' });
    expect(edits).toEqual({});
    expect(hasUnsavedEdits(STORY, edits)).toBe(false);
  });

  it('keeps «Не назначен» as a touch, and sends `{ userId: null }` for it', () => {
    const edits = withEdit(STORY, NO_EDITS, { field: 'assignee', value: null });

    expect(editedDraft(STORY, edits).assignee).toBeNull();
    expect(touchedChanges(STORY, edits)).toEqual({ assignee: { userId: null } });
    expect(withEdit(STORY, edits, { field: 'assignee', value: STORY.assignee })).toEqual({});
  });

  it('compares state, area and assignee by id, not by their text', () => {
    expect(
      withEdit(STORY, NO_EDITS, { field: 'area', value: { id: 'area-platform', path: 'другой' } }),
    ).toEqual({});
    expect(withEdit(STORY, NO_EDITS, { field: 'state', value: 'state-new' })).toEqual({});
    expect(
      touchedChanges(STORY, withEdit(STORY, NO_EDITS, { field: 'state', value: 'state-active' })),
    ).toEqual({ stateId: 'state-active' });
  });

  it('keeps a half-typed number: «2,» is not the stored «2»', () => {
    const edits = withEdit(STORY, NO_EDITS, { field: 'storyPoints', value: '5,' });
    expect(edits.fields?.storyPoints).toBe('5,');
    expect(withEdit(STORY, edits, { field: 'storyPoints', value: '5' })).toEqual({});
  });
});

describe('the parent edit (feat-001 BR-H-4)', () => {
  it('sends the parent the user saw as `seenParentId`, whatever is stored now', () => {
    const edits = withEdit(STORY, NO_EDITS, { field: 'parent', value: FEAT_8 });

    expect(touchedChanges(STORY, edits)).toEqual({
      parent: { parentId: 'feat-8', seenParentId: 'feat-6' },
    });
    // Read again after someone moved the item under another parent.
    expect(touchedChanges(STORY_LATER, edits)).toEqual({});
    const moved = withEdit(STORY_LATER, edits, { field: 'parent', value: null });
    expect(touchedChanges(STORY_LATER, moved)).toEqual({
      parent: { parentId: null, seenParentId: 'feat-6' },
    });
  });

  it('remembers the first parent seen, through a second pick', () => {
    let edits = withEdit(STORY, NO_EDITS, { field: 'parent', value: FEAT_8 });
    edits = withEdit(STORY, edits, { field: 'parent', value: null });

    expect(edits.parent).toEqual({ value: null, seenParentId: 'feat-6' });
  });

  it('is no edit when the stored parent is chosen again', () => {
    const edits = withEdit(STORY, NO_EDITS, { field: 'parent', value: FEAT_8 });
    expect(withEdit(STORY, edits, { field: 'parent', value: FEAT_6 })).toEqual({});
  });

  it('records «no parent seen» for an item that had none', () => {
    const orphan: WorkItem = { ...STORY, parent: null };
    const edits = withEdit(orphan, NO_EDITS, { field: 'parent', value: FEAT_8 });

    expect(touchedChanges(orphan, edits)).toEqual({
      parent: { parentId: 'feat-8', seenParentId: null },
    });
  });
});

describe('a fresh read of the item (BR-C-9)', () => {
  it('takes the stored value for a field the user did not touch, and keeps a touched one', () => {
    const edits = withEdit(STORY, NO_EDITS, { field: 'description', value: 'Мой текст' });
    const rebased = rebaseEdits(STORY_LATER, edits);
    const draft = editedDraft(STORY_LATER, rebased);

    expect(draft.description).toBe('Мой текст');
    expect(draft.title).toBe('Письмо со ссылкой для входа');
    expect(draft.fields.priority).toBe('1');
    expect(touchedChanges(STORY_LATER, rebased)).toEqual({ description: 'Мой текст' });
  });

  it('drops an edit the fresh read now agrees with', () => {
    const edits = withEdit(STORY, NO_EDITS, {
      field: 'title',
      value: 'Письмо со ссылкой для входа',
    });

    expect(rebaseEdits(STORY_LATER, edits)).toEqual({});
  });

  it('drops the parent edit on request, and keeps the rest', () => {
    let edits = withEdit(STORY, NO_EDITS, { field: 'parent', value: null });
    edits = withEdit(STORY, edits, { field: 'description', value: 'Мой текст' });

    expect(rebaseEdits(STORY_LATER, edits, { dropParent: true })).toEqual({
      description: 'Мой текст',
    });
    expect(rebaseEdits(STORY_LATER, edits).parent).toEqual({ value: null, seenParentId: 'feat-6' });
  });

  it('normalises edits that equal the store, and leaves the rest as they were', () => {
    const edits: WorkItemEdits = {
      title: STORY.title,
      stateId: 'state-active',
      fields: { priority: '2', risk: '3' },
    };

    expect(normaliseEdits(STORY, edits)).toEqual({
      stateId: 'state-active',
      fields: { risk: '3' },
    });
  });
});

describe('the team edit (mod-017 design § 3)', () => {
  const venus = { id: 'team-venus', name: 'VENUS' };

  it('an untouched team is never named by the save', () => {
    const edits = withEdit(STORY, NO_EDITS, { field: 'title', value: 'Другое' });
    expect(touchedChanges(STORY, edits)).toEqual({ title: 'Другое' });
  });

  it('a pick sends the team, «Без команды» sends `{ teamId: null }`, and picking the stored team again is no edit', () => {
    const picked = withEdit(STORY, NO_EDITS, { field: 'team', value: venus });
    expect(touchedChanges(STORY, picked)).toEqual({ team: { teamId: 'team-venus' } });
    expect(editedDraft(STORY, picked).team).toEqual(venus);

    const owned = { ...STORY, team: venus };
    const cleared = withEdit(owned, NO_EDITS, { field: 'team', value: null });
    expect(touchedChanges(owned, cleared)).toEqual({ team: { teamId: null } });
    expect(isUntouched(withEdit(STORY, picked, { field: 'team', value: null }))).toBe(true);
    expect(isUntouched(withEdit(owned, cleared, { field: 'team', value: venus }))).toBe(true);
  });

  it('a fresh read that already holds the picked team drops the edit; «Отменить» restores the stored team', () => {
    const picked = withEdit(STORY, NO_EDITS, { field: 'team', value: venus });
    expect(rebaseEdits({ ...STORY, team: venus }, picked)).toEqual({});
    expect(hasUnsavedEdits(STORY, picked)).toBe(true);
    expect(hasUnsavedEdits(STORY, NO_EDITS)).toBe(false);
  });
});

describe('where the first error is looked for (design § 4.6)', () => {
  it('runs title, assignee, state, area, team, then the texts, the sections and «Родитель»', () => {
    expect(cardFieldOrder('story')).toEqual([
      'title',
      'assignee',
      'state',
      'area',
      'team',
      'description',
      'acceptanceCriteria',
      'storyPoints',
      'priority',
      'risk',
      'valueArea',
      'parent',
    ]);
    expect(cardFieldOrder('task')).not.toContain('acceptanceCriteria');
  });

  it('names the earliest of several errors, and none for none', () => {
    expect(firstErrorField({ priority: 'x', title: 'y', parent: 'z' }, 'story')).toBe('title');
    expect(firstErrorField({ risk: 'x', storyPoints: 'y' }, 'story')).toBe('storyPoints');
    expect(firstErrorField({}, 'story')).toBeUndefined();
    // A field the type lacks is not looked for.
    expect(firstErrorField({ severity: 'x' }, 'story')).toBeUndefined();
  });
});
