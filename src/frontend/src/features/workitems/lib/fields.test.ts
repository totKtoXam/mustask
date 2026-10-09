import { describe, expect, it } from 'vitest';
import { LIST_FIELDS, NOT_SET_LABEL, fieldsOf, layoutOf } from './fields';

// feat-022 spec § The field table and § The form's sections; api-contract § 1:
// the type is recognised by its appearance token, never by its name.

describe('layoutOf (spec § The form’s sections)', () => {
  it.each([
    [
      'epic',
      [],
      [
        [
          'Планирование',
          [
            'priority',
            'risk',
            'effort',
            'businessValue',
            'timeCriticality',
            'startDate',
            'targetDate',
          ],
        ],
        ['Классификация', ['valueArea']],
      ],
    ],
    [
      'story',
      ['acceptanceCriteria'],
      [
        ['Планирование', ['storyPoints', 'priority', 'risk']],
        ['Классификация', ['valueArea']],
      ],
    ],
    [
      'task',
      [],
      [
        ['Планирование', ['priority', 'activity']],
        ['Трудозатраты (часы)', ['originalEstimate', 'remainingWork', 'completedWork']],
      ],
    ],
    [
      'bug',
      ['reproSteps', 'systemInfo'],
      [
        ['Планирование', ['storyPoints', 'priority', 'severity', 'activity']],
        ['Трудозатраты (часы)', ['originalEstimate', 'remainingWork', 'completedWork']],
      ],
    ],
  ])('%s: texts %j, then its sections top to bottom', (appearance, texts, sections) => {
    const layout = layoutOf(appearance);
    expect(layout.texts).toEqual(texts);
    expect(layout.sections.map((section) => [section.title, section.fields])).toEqual(sections);
  });

  it('a feature is laid out as an epic', () => {
    expect(layoutOf('feature')).toEqual(layoutOf('epic'));
  });

  it('a kind the process does not hold shows no section and has no field', () => {
    expect(layoutOf('unknown')).toEqual({ texts: [], sections: [] });
    expect(fieldsOf('unknown')).toEqual([]);
  });
});

describe('fieldsOf (BR-M3-30)', () => {
  it.each([
    ['epic', 8],
    ['feature', 8],
    ['story', 5],
    ['task', 5],
    ['bug', 9],
  ])('%s has %i fields, «Приоритет» among them', (appearance, count) => {
    expect(fieldsOf(appearance)).toHaveLength(count);
    expect(fieldsOf(appearance)).toContain('priority');
  });
});

describe('the fixed lists (spec § The field table, BR-M3-31)', () => {
  it('only «Риск» and «Вид работ» may be empty, offered as «Не указан»', () => {
    expect(NOT_SET_LABEL).toBe('Не указан');
    expect(
      Object.entries(LIST_FIELDS)
        .filter(([, spec]) => spec.optional)
        .map(([name]) => name),
    ).toEqual(['risk', 'activity']);
  });

  it('offer their labels in the spec’s order', () => {
    expect(LIST_FIELDS.priority.options.map((option) => option.label)).toEqual([
      '1',
      '2',
      '3',
      '4',
    ]);
    expect(LIST_FIELDS.severity.options.map((option) => option.label)).toEqual([
      '1 — Критическая',
      '2 — Высокая',
      '3 — Средняя',
      '4 — Низкая',
    ]);
    expect(LIST_FIELDS.activity.options.map((option) => option.label)).toEqual([
      'Развёртывание',
      'Проектирование',
      'Разработка',
      'Документирование',
      'Требования',
      'Тестирование',
    ]);
    expect(LIST_FIELDS.valueArea.options.map((option) => option.key)).toEqual([
      'Business',
      'Architectural',
    ]);
  });
});
