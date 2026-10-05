import { describe, expect, it } from 'vitest';
import { ApiError, type ProblemDetails } from '../../../shared/api/httpClient';
import { notFoundLevel } from './notFoundLevel';
import { placeRefusal, type RefusalField } from './refusals';

const FORM: readonly RefusalField[] = ['title', 'description', 'state', 'assignee', 'area'];
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
    ['organizations.area.not_found', 404, 'area', 'Область не найдена.'],
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
