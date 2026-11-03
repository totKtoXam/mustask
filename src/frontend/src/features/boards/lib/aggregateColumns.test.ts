import { describe, expect, it } from 'vitest';
import type { ProcessDefinitionResponse } from '../../process';
import { aggregateColumns, levelTypes } from './aggregateColumns';

let n = 0;
const state = (name: string, category: string, order: number) => ({
  id: `s${(n += 1)}`,
  name,
  order,
  isInitial: order === 1,
  category: { code: category, label: category },
});
const type = (id: string, levelId: string, states: ReturnType<typeof state>[]) => ({
  id,
  key: id,
  name: id,
  appearance: 'story',
  backlogLevel: { id: levelId, name: levelId },
  states,
});

const process = {
  id: 'p',
  backlogLevels: [
    { id: 'L-epic', name: 'Epics', rank: 3, kind: 'portfolio' },
    { id: 'L-feat', name: 'Features', rank: 2, kind: 'portfolio' },
    { id: 'L-story', name: 'Stories', rank: 1, kind: 'requirement' },
    { id: 'L-task', name: 'Tasks', rank: 0, kind: 'task' },
  ],
  workItemTypes: [
    type('EPIC', 'L-epic', [state('Новая', 'Proposed', 1)]),
    type('US', 'L-story', [
      state('Новая', 'Proposed', 1),
      state('Активна', 'InProgress', 2),
      state('Разрешена', 'Resolved', 3),
      state('Закрыта', 'Completed', 4),
    ]),
    type('BUG', 'L-story', [
      state('Новый', 'Proposed', 1),
      state('Активен', 'InProgress', 2),
      state('В тесте', 'InProgress', 3),
      state('Разрешена', 'Resolved', 4),
      state('Удалена', 'Removed', 5),
    ]),
  ],
} as unknown as ProcessDefinitionResponse;

describe('aggregateColumns', () => {
  it('resolves the levels as the server does', () => {
    expect(levelTypes(process, 'stories').map((t) => t.id)).toEqual(['US', 'BUG']);
    expect(levelTypes(process, 'epics').map((t) => t.id)).toEqual(['EPIC']);
  });

  it('derives keys, headers and per-type targets in category order', () => {
    const columns = aggregateColumns(process, 'stories');
    expect(columns.map((c) => c.key)).toEqual([
      'Proposed-1',
      'InProgress-1',
      'InProgress-2',
      'Resolved-1',
      'Completed-1',
    ]);
    // The first type in display order that maps a state heads the column.
    expect(columns.map((c) => c.header)).toEqual([
      'Новая',
      'Активна',
      'В тесте',
      'Разрешена',
      'Закрыта',
    ]);
    const second = columns[2];
    expect(second?.stateOfType.has('US')).toBe(false);
    expect(second?.stateOfType.has('BUG')).toBe(true);
  });
});
