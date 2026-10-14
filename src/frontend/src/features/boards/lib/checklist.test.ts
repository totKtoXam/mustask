import { describe, expect, it } from 'vitest';
import type { Board, BoardCardItem, ChecklistChild } from '../api/boardsApi';
import {
  applyChildState,
  checkboxName,
  collapseName,
  counterName,
  counterText,
  countersOf,
  isDone,
  listName,
  stateAnnouncement,
  tickTarget,
} from './checklist';

// feat-032 BR-C-1..-5, -7 and design § 3.2, § 6: the counters of a card, their words and
// the state a tick writes.

const TASK_TYPE = { id: 'type-task', key: 'TASK', name: 'Задача', appearance: 'task' };
const BUG_TYPE = { id: 'type-bug', key: 'BUG', name: 'Баг', appearance: 'bug' };
const CASE_TYPE = { id: 'type-tc', key: 'TC', name: 'Тест-кейс', appearance: 'testcase' };

function state(name: string, code: string) {
  return { id: `state-${name}`, name, category: { code, label: code } };
}

function child(
  identifier: string,
  type: ChecklistChild['type'],
  stateName: string,
  code: string,
): ChecklistChild {
  return {
    id: `id-${identifier}`,
    identifier,
    number: Number(identifier.split('-').pop()),
    type,
    title: identifier,
    assignee: null,
    state: state(stateName, code),
  };
}

function card(appearance: string, checklist: ChecklistChild[]): BoardCardItem {
  return {
    order: 1,
    id: 'id-card',
    identifier: 'MSK-US-2',
    number: 2,
    type: { id: 'type-card', key: 'US', name: 'Пользовательская история', appearance },
    title: 'Повторная отправка',
    assignee: null,
    state: state('Активна', 'InProgress'),
    priority: 2,
    checklist,
  };
}

const NAMES = { task: 'Задача', bug: 'Баг' };

const STORY_CHILDREN = [
  child('MSK-TASK-3', TASK_TYPE, 'Закрыта', 'Completed'),
  child('MSK-TASK-4', TASK_TYPE, 'Новая', 'Proposed'),
  child('MSK-TASK-6', TASK_TYPE, 'Активна', 'InProgress'),
  child('MSK-BUG-5', BUG_TYPE, 'Закрыта', 'Completed'),
  child('MSK-BUG-8', BUG_TYPE, 'Разрешена', 'Resolved'),
  child('MSK-TC-10', CASE_TYPE, 'Готов', 'InProgress'),
  child('MSK-TC-12', CASE_TYPE, 'Закрыта', 'Completed'),
];

describe('US-1 Scenario: A card shows its counters (BR-C-2, BR-C-3)', () => {
  it('counts a task and a bug as done in Completed only — a «Разрешена» bug is in the total, not in the done count', () => {
    const counters = countersOf(card('story', STORY_CHILDREN), NAMES);

    expect(counters.map((counter) => [counter.kind, counter.done, counter.total])).toEqual([
      ['task', 1, 3],
      ['bug', 1, 2],
      ['testcase', 0, 2],
    ]);
  });

  it('puts the counters in the order task, bug, test case, whatever the order of the children', () => {
    const reversed = [...STORY_CHILDREN].reverse();

    expect(countersOf(card('story', reversed), NAMES).map((counter) => counter.kind)).toEqual([
      'task',
      'bug',
      'testcase',
    ]);
  });

  it('keeps the children of a kind in the answer’s order', () => {
    const [task] = countersOf(card('story', STORY_CHILDREN), NAMES);

    expect(task?.children.map((entry) => entry.identifier)).toEqual([
      'MSK-TASK-3',
      'MSK-TASK-4',
      'MSK-TASK-6',
    ]);
  });

  it('reads the type name from a child, and the process’s name only for a «0/0»', () => {
    const [task] = countersOf(
      card('story', [child('MSK-TASK-3', TASK_TYPE, 'Новая', 'Proposed')]),
      {
        task: 'Работа',
      },
    );
    const [empty] = countersOf(card('story', []), { task: 'Работа' });

    expect(task?.typeName).toBe('Задача');
    expect(empty).toMatchObject({ kind: 'task', typeName: 'Работа', done: 0, total: 0 });
  });

  it('gives a story the task counter even with no children, and no other', () => {
    expect(countersOf(card('story', []), NAMES).map((counter) => counter.kind)).toEqual(['task']);
  });

  it('gives a card that offers no task no counter when it has no children', () => {
    for (const appearance of ['epic', 'feature', 'bug', 'task']) {
      expect(countersOf(card(appearance, []), NAMES)).toEqual([]);
    }
  });

  it('draws no «0/0» before the process has named the task type', () => {
    expect(countersOf(card('story', []), {})).toEqual([]);
  });

  it('gives a feature a bug counter «0/1» for a bug child, and no task counter', () => {
    const counters = countersOf(
      card('feature', [child('MSK-BUG-1', BUG_TYPE, 'Активна', 'InProgress')]),
      NAMES,
    );

    expect(counters.map((counter) => counterText(counter))).toEqual(['0/1']);
  });
});

describe('US-1 Scenario: The counter’s words (design § 6, D-5)', () => {
  const [task, bug, testcase] = countersOf(card('story', STORY_CHILDREN), NAMES);
  const flask = testcase && { ...testcase, typeName: 'Тест-кейс' };

  it('shows «1/3» for a task and a bug and the bare count for a test case', () => {
    expect([task, bug, flask].map((counter) => counter && counterText(counter))).toEqual([
      '1/3',
      '1/2',
      '2',
    ]);
  });

  it('starts the accessible name with the visible text', () => {
    if (!task || !bug || !flask) throw new Error('Counters missing.');

    expect(counterName(task, 'MSK-US-2')).toBe('1/3 — MSK-US-2, Задача: выполнено 1 из 3');
    expect(counterName(bug, 'MSK-US-2')).toBe('1/2 — MSK-US-2, Баг: выполнено 1 из 2');
    expect(counterName(flask, 'MSK-US-2')).toBe('2 — MSK-US-2, Тест-кейс: 2');
    for (const counter of [task, bug, flask]) {
      expect(counterName(counter, 'MSK-US-2').startsWith(counterText(counter))).toBe(true);
    }
  });

  it('names the list, its chevron, the checkbox and the announcement', () => {
    expect(listName('Задача', 'MSK-US-2')).toBe('Список: Задача (MSK-US-2)');
    expect(collapseName('Задача', 'MSK-US-2')).toBe('Свернуть список: Задача (MSK-US-2)');
    expect(checkboxName('MSK-TASK-4')).toBe('MSK-TASK-4: выполнено');
    expect(stateAnnouncement('MSK-TASK-4', 'Закрыта')).toBe('MSK-TASK-4: статус «Закрыта».');
  });

  it('calls a child done in the Completed category only', () => {
    expect(isDone(child('MSK-TASK-3', TASK_TYPE, 'Закрыта', 'Completed'))).toBe(true);
    expect(isDone(child('MSK-BUG-8', BUG_TYPE, 'Разрешена', 'Resolved'))).toBe(false);
  });
});

describe('US-2 Scenario: What a tick and an untick write (BR-C-7)', () => {
  const states = (list: [string, string, boolean][]) =>
    list.map(([name, code, isInitial]) => ({
      id: `s-${name}`,
      name,
      isInitial,
      category: { code, label: code },
    }));
  const types = [
    {
      id: 'type-bug',
      states: states([
        ['Новая', 'Proposed', true],
        ['Активна', 'InProgress', false],
        ['Разрешена', 'Resolved', false],
        ['Закрыта', 'Completed', false],
      ]),
    },
    { id: 'type-flat', states: states([['Новая', 'Proposed', true]]) },
  ];

  it('ticks into the first Completed state and unticks into the initial one', () => {
    expect(tickTarget(types, 'type-bug', true)?.name).toBe('Закрыта');
    expect(tickTarget(types, 'type-bug', false)?.name).toBe('Новая');
  });

  it('has no target for a type without a Completed state, or one the process does not know', () => {
    expect(tickTarget(types, 'type-flat', true)).toBeUndefined();
    expect(tickTarget(types, 'type-unknown', true)).toBeUndefined();
    expect(tickTarget([], 'type-bug', false)).toBeUndefined();
  });
});

describe('US-2 Scenario: A tick shows at once (design § 7.2)', () => {
  function board(): Board {
    const story = card('story', STORY_CHILDREN);
    const other = { ...card('story', []), id: 'id-other', identifier: 'MSK-US-9' };
    return {
      team: { id: 't', name: 'Платформа', defaultArea: { id: 'a', path: 'Платформа' } },
      level: { code: 'stories', name: 'Истории', types: [] },
      columns: [
        {
          key: 'Proposed-1',
          header: 'Новая',
          category: { code: 'Proposed', label: 'Новые' },
          targets: [],
          items: [story, other],
        },
      ],
    };
  }

  it('moves the child’s state in its card’s checklist, and nothing else', () => {
    const before = board();
    const closed = state('Закрыта', 'Completed');

    const after = applyChildState(before, 'id-MSK-TASK-4', closed);

    const [story, other] = after.columns[0]?.items ?? [];
    expect(story?.checklist.find((entry) => entry.identifier === 'MSK-TASK-4')?.state).toEqual(
      closed,
    );
    expect(story?.checklist.filter(isDone)).toHaveLength(4);
    expect(story?.state).toEqual(before.columns[0]?.items[0]?.state);
    expect(other).toBe(before.columns[0]?.items[1]);
  });
});
