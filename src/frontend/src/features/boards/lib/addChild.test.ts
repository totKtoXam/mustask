import { describe, expect, it } from 'vitest';
import { addControlLabel, addEntriesOf, addEntryLabel } from './addChild';

// feat-027 BR-T-9: what a row can be given as a child, by the appearance of
// its type, resolved against the process's own names.

const TYPES = [
  { id: 'epic-id', name: 'Эпик', appearance: 'epic' },
  { id: 'feature-id', name: 'Фича', appearance: 'feature' },
  { id: 'story-id', name: 'Пользовательская история', appearance: 'story' },
  { id: 'bug-id', name: 'Баг', appearance: 'bug' },
  { id: 'task-id', name: 'Задача', appearance: 'task' },
];

describe('addEntriesOf (BR-T-9)', () => {
  it('offers a feature to an epic, a story and a bug to a feature, a task and a bug to a story — in that order', () => {
    expect(addEntriesOf('epic', TYPES).map((entry) => entry.name)).toEqual(['Фича']);
    expect(addEntriesOf('feature', TYPES).map((entry) => entry.name)).toEqual([
      'Пользовательская история',
      'Баг',
    ]);
    expect(addEntriesOf('story', TYPES).map((entry) => entry.name)).toEqual(['Задача', 'Баг']);
  });

  it('offers nothing to a bug or a task, nor to a token it does not know', () => {
    expect(addEntriesOf('bug', TYPES)).toEqual([]);
    expect(addEntriesOf('task', TYPES)).toEqual([]);
    expect(addEntriesOf('rocket', TYPES)).toEqual([]);
  });

  it('names the process’s own type of the appearance, never a fixed name', () => {
    const renamed = TYPES.map((type) =>
      type.appearance === 'feature' ? { ...type, name: 'Возможность' } : type,
    );

    expect(addEntriesOf('epic', renamed)).toEqual([
      { typeId: 'feature-id', name: 'Возможность', appearance: 'feature' },
    ]);
  });

  it('gives no entry for an appearance no type of the process has, and none before the process is read', () => {
    const withoutBug = TYPES.filter((type) => type.appearance !== 'bug');

    expect(addEntriesOf('feature', withoutBug).map((entry) => entry.name)).toEqual([
      'Пользовательская история',
    ]);
    expect(addEntriesOf('epic', [])).toEqual([]);
  });
});

describe('the «+»’s names', () => {
  it('names one entry after its type with the identifier, several as a menu', () => {
    const [feature] = addEntriesOf('epic', TYPES);
    const both = addEntriesOf('feature', TYPES);

    expect(addControlLabel(feature ? [feature] : [], 'MSK-EPIC-1')).toBe(
      'Добавить: Фича (MSK-EPIC-1)',
    );
    expect(addControlLabel(both, 'MSK-FEAT-6')).toBe('Добавить дочерний элемент (MSK-FEAT-6)');
    expect(feature && addEntryLabel(feature)).toBe('Добавить: Фича');
  });
});
