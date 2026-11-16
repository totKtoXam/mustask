import { describe, expect, it } from 'vitest';
import type { WorkItem, WorkItemSprint } from '../api/workItemsApi';
import { defaultFields } from '../testFixtures';
import {
  NO_EDITS,
  cardFieldOrder,
  editedDraft,
  hasUnsavedEdits,
  normaliseEdits,
  sprintCleared,
  touchedChanges,
  withEdit,
} from './edits';
import { placeRefusal } from './refusals';
import { ApiError } from '../../../shared/api/httpClient';
import { problem } from '../../../test/api';

// mod-007 US-4, design § 7: the item's «Спринт» in the card's draft of touched fields.

const TEAM_A = { id: 'team-a', name: 'Платформа' };
const TEAM_B = { id: 'team-b', name: 'Аналитика' };
const S1: WorkItemSprint = { id: 'sprint-1', name: 'Спринт 1', state: 'Active' };
const S2: WorkItemSprint = { id: 'sprint-2', name: 'Спринт 2', state: 'Planned' };

const STORY: WorkItem = {
  id: 'item-2',
  identifier: 'MSK-US-2',
  number: 2,
  projectId: 'project-1',
  project: { id: 'project-1', name: 'Мастаск' },
  type: { id: 'type-us', key: 'US', name: 'Пользовательская история', appearance: 'story' },
  title: 'Письмо со ссылкой',
  description: '',
  descriptionFormat: 'plain',
  state: { id: 'state-new', name: 'Новая', category: { code: 'Proposed', label: 'Новые' } },
  assignee: null,
  area: { id: 'area-platform', path: 'Платформа' },
  team: TEAM_A,
  sprint: S1,
  createdBy: 'creator',
  createdAt: '2026-10-05T14:30:00+00:00',
  changedAt: '2026-10-05T14:30:00+00:00',
  parent: null,
  children: [],
  deletion: null,
  ...defaultFields('story'),
};

describe('mod-007: the sprint in the draft', () => {
  it('shows the stored sprint and sends nothing while it is untouched', () => {
    expect(editedDraft(STORY, NO_EDITS).sprint).toEqual(S1);
    expect(touchedChanges(STORY, NO_EDITS)).toEqual({});
  });

  it('sends the chosen sprint in its wrapper, and `{ sprintId: null }` for «Без спринта»', () => {
    const picked = withEdit(STORY, NO_EDITS, { field: 'sprint', value: S2 });
    expect(touchedChanges(STORY, picked)).toEqual({ sprint: { sprintId: 'sprint-2' } });
    const cleared = withEdit(STORY, NO_EDITS, { field: 'sprint', value: null });
    expect(touchedChanges(STORY, cleared)).toEqual({ sprint: { sprintId: null } });
  });

  it('is no edit once the stored sprint is chosen again', () => {
    const picked = withEdit(STORY, NO_EDITS, { field: 'sprint', value: S2 });
    expect(withEdit(STORY, picked, { field: 'sprint', value: S1 })).toEqual(NO_EDITS);
    expect(hasUnsavedEdits(STORY, NO_EDITS)).toBe(false);
  });

  it('shows «Без спринта» and says it will be cleared when the team is changed under a sprint', () => {
    const moved = withEdit(STORY, NO_EDITS, { field: 'team', value: TEAM_B });
    expect(sprintCleared(STORY, moved)).toBe(true);
    expect(editedDraft(STORY, moved).sprint).toBeNull();
    expect(touchedChanges(STORY, moved)).toMatchObject({
      team: { teamId: 'team-b' },
      sprint: { sprintId: null },
    });
  });

  it('follows the stored sprint again when the team is changed back', () => {
    const moved = withEdit(STORY, NO_EDITS, { field: 'team', value: TEAM_B });
    const back = withEdit(STORY, moved, { field: 'team', value: TEAM_A });
    expect(sprintCleared(STORY, back)).toBe(false);
    expect(editedDraft(STORY, back).sprint).toEqual(S1);
  });

  it('drops a sprint picked for the earlier team when the team changes', () => {
    const picked = withEdit(STORY, NO_EDITS, { field: 'sprint', value: S2 });
    const moved = withEdit(STORY, picked, { field: 'team', value: TEAM_B });
    expect(moved.sprint).toBeUndefined();
  });

  it('keeps a touched sprint through a re-read that still differs', () => {
    const picked = withEdit(STORY, NO_EDITS, { field: 'sprint', value: S2 });
    expect(normaliseEdits(STORY, picked)).toEqual(picked);
    expect(normaliseEdits({ ...STORY, sprint: S2 }, picked)).toEqual(NO_EDITS);
  });

  it('looks for the first error at «Спринт» right after «Команда»', () => {
    const order = cardFieldOrder('story');
    expect(order.indexOf('sprint')).toBe(order.indexOf('team') + 1);
  });
});

describe('mod-007 US-4: where the four refusals stand', () => {
  it.each([
    'iterations.sprint.other_team',
    'iterations.sprint.team_required',
    'iterations.sprint.closed_target',
    'iterations.sprint.not_found',
  ])('puts %s under «Спринт», with the server’s text', (code) => {
    const body = problem(code);
    const placed = placeRefusal(new ApiError(body.status, body), ['title', 'team', 'sprint']);
    expect(placed.fields).toEqual({ sprint: body.detail });
    expect(placed.alert).toBeNull();
    expect(placed.frame).toBeNull();
  });
});
