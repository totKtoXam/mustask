import { describe, expect, it } from 'vitest';
import { ApiError, type ProblemDetails } from '../../../shared/api/httpClient';
import { notFoundLevel } from './notFoundLevel';
import { isParentChanged, placeRefusal, type RefusalField } from './refusals';

const FORM: readonly RefusalField[] = [
  'title',
  'description',
  'state',
  'assignee',
  'parent',
];
const DIALOG: readonly RefusalField[] = ['title'];

function refusal(
  status: number,
  errorCode: string,
  detail: string,
  extra: Partial<ProblemDetails> = {},
) {
  return new ApiError(status, { status, errorCode, detail, ...extra });
}

function validation(errors: Record<string, string>) {
  return refusal(400, 'workitems.validation_failed', 'Проверьте правильность заполнения полей.', {
    errors: Object.fromEntries(
      Object.entries(errors).map(([field, detail]) => [field, [{ code: `${field}.code`, detail }]]),
    ),
  });
}

describe('notFoundLevel (spec § Not-found answers)', () => {
  it.each([
    ['organizations.organization.not_found', 'organization'],
    ['organizations.project.not_found', 'project'],
    ['workitems.workitem.not_found', 'workItem'],
  ])('%s is the %s frame', (code, level) => {
    expect(notFoundLevel(refusal(404, code, 'x'))).toBe(level);
  });

  it.each([['workitems.workitem.type_not_found'], ['organizations.area.not_found']])(
    'a body reference’s %s is no frame (api-contract § 0.5)',
    (code) => {
      expect(notFoundLevel(refusal(404, code, 'x'))).toBeNull();
    },
  );

  it('a network failure is no frame', () => {
    expect(notFoundLevel(new TypeError('Failed to fetch'))).toBeNull();
  });
});

describe('placeRefusal (spec § Error codes; api-contract § 0.5)', () => {
  it('puts the title and description reasons on their fields, each with its own text', () => {
    const placed = placeRefusal(
      validation({ title: 'Введите заголовок.', description: 'Не более 100 000 символов.' }),
      FORM,
    );

    expect(placed).toEqual({
      fields: { title: 'Введите заголовок.', description: 'Не более 100 000 символов.' },
      alert: null,
      frame: null,
    });
  });

  it('a requestId reason — a panel defect — goes to the Alert, beside a placed title', () => {
    const placed = placeRefusal(
      validation({
        requestId: 'Не удалось отправить форму. Обновите страницу и попробуйте ещё раз.',
        title: 'Введите заголовок.',
      }),
      DIALOG,
    );

    expect(placed.fields).toEqual({ title: 'Введите заголовок.' });
    expect(placed.alert).toBe(
      'Не удалось отправить форму. Обновите страницу и попробуйте ещё раз.',
    );
  });

  it('a description reason in the dialog, which has no such field, goes to the Alert', () => {
    expect(placeRefusal(validation({ description: 'Не более 100 000 символов.' }), DIALOG)).toEqual(
      { fields: {}, alert: 'Не более 100 000 символов.', frame: null },
    );
  });

  it('a validation answer without reasons shows its own text in the Alert', () => {
    expect(placeRefusal(validation({}), FORM).alert).toBe(
      'Проверьте правильность заполнения полей.',
    );
  });

  it.each([
    [
      'workitems.workitem.state_not_allowed',
      409,
      'state',
      'Этот статус недоступен для рабочего элемента этого типа.',
    ],
    [
      'workitems.workitem.assignee_unavailable',
      409,
      'assignee',
      'Этого пользователя нельзя назначить исполнителем.',
    ],
  ])('%s lands under its field (%s → %s)', (code, status, field, text) => {
    expect(placeRefusal(refusal(status, code, text), FORM)).toEqual({
      fields: { [field]: text },
      alert: null,
      frame: null,
    });
  });

  it('the type’s not-found is the dialog’s Alert, never a frame', () => {
    expect(
      placeRefusal(
        refusal(404, 'workitems.workitem.type_not_found', 'Тип рабочего элемента не найден.'),
        DIALOG,
      ),
    ).toEqual({ fields: {}, alert: 'Тип рабочего элемента не найден.', frame: null });
  });

  it.each([
    ['organizations.organization.not_found', 'Организация не найдена.', 'organization'],
    ['organizations.project.not_found', 'Проект не найден.', 'project'],
    ['workitems.workitem.not_found', 'Рабочий элемент не найден.', 'workItem'],
  ])('a path’s %s names its frame and keeps the text for an open dialog', (code, text, frame) => {
    expect(placeRefusal(refusal(404, code, text), DIALOG)).toEqual({
      fields: {},
      alert: text,
      frame,
    });
  });

  it('branches on the code, never on the text: a 409 with a field’s text but another code is the Alert', () => {
    const placed = placeRefusal(
      refusal(
        409,
        'workitems.something_else',
        'Этот статус недоступен для рабочего элемента этого типа.',
      ),
      FORM,
    );
    expect(placed.fields).toEqual({});
    expect(placed.alert).toBe('Этот статус недоступен для рабочего элемента этого типа.');
  });

  it('a network failure is the Alert with the panel’s own text', () => {
    const placed = placeRefusal(Object.assign(new Error('x'), { name: 'TypeError' }), FORM);
    expect(placed).toEqual({
      fields: {},
      alert: 'Не удалось соединиться с API. Проверьте, что Mustask.Api запущен.',
      frame: null,
    });
  });
});

describe('placeRefusal: the fields per type (feat-022 spec § Error codes; api-contract § 3.2)', () => {
  const EPIC_FORM: readonly RefusalField[] = [
    ...FORM,
    'risk',
    'effort',
    'businessValue',
    'targetDate',
  ];

  function fieldReasons(reasons: Record<string, [string, string]>) {
    return refusal(400, 'workitems.validation_failed', 'Проверьте правильность заполнения полей.', {
      errors: Object.fromEntries(
        Object.entries(reasons).map(([field, [code, detail]]) => [field, [{ code, detail }]]),
      ),
    });
  }

  it('puts each reason under its field, keyed by the JSON name, with the server’s text', () => {
    const placed = placeRefusal(
      fieldReasons({
        businessValue: [
          'workitems.workitem.number_out_of_range',
          'Допустимо значение от 0 до 9 999.',
        ],
        targetDate: [
          'workitems.workitem.target_before_start',
          'Целевая дата не может быть раньше даты начала.',
        ],
        risk: ['workitems.workitem.value_not_in_list', 'Выберите значение из списка.'],
      }),
      EPIC_FORM,
    );

    expect(placed).toEqual({
      fields: {
        businessValue: 'Допустимо значение от 0 до 9 999.',
        targetDate: 'Целевая дата не может быть раньше даты начала.',
        risk: 'Выберите значение из списка.',
      },
      alert: null,
      frame: null,
    });
  });

  it('`field_not_on_type` goes to the Alert, even keyed by a field the form shows', () => {
    const text = 'Это поле недоступно для рабочего элемента этого типа.';
    const placed = placeRefusal(
      fieldReasons({ effort: ['workitems.workitem.field_not_on_type', text] }),
      EPIC_FORM,
    );

    expect(placed).toEqual({ fields: {}, alert: text, frame: null });
  });

  it('a reason about a field the type’s form lacks goes to the Alert, beside a placed one', () => {
    const placed = placeRefusal(
      fieldReasons({
        storyPoints: [
          'workitems.workitem.number_too_precise',
          'Не более двух знаков после запятой.',
        ],
        effort: ['workitems.workitem.number_out_of_range', 'Допустимо значение от 0 до 9 999.'],
      }),
      EPIC_FORM,
    );

    expect(placed.fields).toEqual({ effort: 'Допустимо значение от 0 до 9 999.' });
    expect(placed.alert).toBe('Не более двух знаков после запятой.');
  });
});

describe('placeRefusal: the link codes (feat-001 spec § Screens and copy, § Error codes; feat-027 D-9)', () => {
  it.each([
    ['workitems.link.parent_not_found', 404, 'Родитель не найден в этом проекте.'],
    [
      'workitems.link.parent_level_not_allowed',
      409,
      'Родителем может быть только рабочий элемент того же или более высокого уровня.',
    ],
    ['workitems.link.self_parent', 409, 'Рабочий элемент не может быть родителем самого себя.'],
    [
      'workitems.link.cycle',
      409,
      'Нельзя выбрать родителем MSK-TASK-12: он вложен в MSK-TASK-10 (MSK-TASK-10 → MSK-TASK-11 → MSK-TASK-12).',
    ],
    [
      'workitems.link.parent_changed',
      409,
      'Родитель уже изменён другим участником. Данные обновлены — проверьте и повторите.',
    ],
    ['workitems.link.item_deleted', 409, 'Удалённый рабочий элемент нельзя связывать.'],
    // `detail` is the finished text, the limit filled in by the server.
    [
      'workitems.link.depth_exceeded',
      409,
      'Слишком глубокая вложенность: в цепочке родителей может быть не больше 64 элементов.',
    ],
  ])('%s is «Родитель»’s error with the server’s text, never a frame', (code, status, text) => {
    expect(notFoundLevel(refusal(status, code, text))).toBeNull();
    expect(placeRefusal(refusal(status, code, text), FORM)).toEqual({
      fields: { parent: text },
      alert: null,
      frame: null,
    });
  });

  it('a screen without «Родитель» shows a link code in its Alert', () => {
    expect(
      placeRefusal(
        refusal(
          409,
          'workitems.link.self_parent',
          'Рабочий элемент не может быть родителем самого себя.',
        ),
        DIALOG,
      ),
    ).toEqual({
      fields: {},
      alert: 'Рабочий элемент не может быть родителем самого себя.',
      frame: null,
    });
  });

  it('isParentChanged tells `parent_changed` apart by its code alone', () => {
    expect(isParentChanged(refusal(409, 'workitems.link.parent_changed', 'x'))).toBe(true);
    expect(
      isParentChanged(
        refusal(
          409,
          'workitems.link.cycle',
          'Родитель уже изменён другим участником. Данные обновлены — проверьте и повторите.',
        ),
      ),
    ).toBe(false);
    expect(isParentChanged(new TypeError('Failed to fetch'))).toBe(false);
  });
});

describe('placeRefusal: the owning team (mod-017 design § 12, api-contract § 0.2)', () => {
  const WITH_TEAM: readonly RefusalField[] = ['title', 'team'];

  it.each([
    ['organizations.team.not_in_project', 409, 'Команда не участвует в проекте.'],
    ['organizations.team.not_found', 404, 'Команда не найдена.'],
  ])(
    '%s is the field «Команда», never a frame, where the form has the field',
    (code, status, text) => {
      const placed = placeRefusal(refusal(status, code, text), WITH_TEAM);

      expect(placed).toEqual({ fields: { team: text }, alert: null, frame: null });
    },
  );

  it('a screen without the field keeps the old reading: a team that is not found is the team frame, the other the Alert', () => {
    expect(
      placeRefusal(refusal(404, 'organizations.team.not_found', 'Команда не найдена.'), DIALOG),
    ).toEqual({ fields: {}, alert: 'Команда не найдена.', frame: 'team' });
    expect(
      placeRefusal(
        refusal(409, 'organizations.team.not_in_project', 'Команда не участвует в проекте.'),
        DIALOG,
      ),
    ).toEqual({ fields: {}, alert: 'Команда не участвует в проекте.', frame: null });
  });
});
