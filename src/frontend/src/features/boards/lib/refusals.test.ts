import { describe, expect, it } from 'vitest';
import { ApiError } from '../../../shared/api/httpClient';
import { backlogSkeletonColumns } from '../components/backlogColumns';
import { catalogText, problem } from '../testFixtures';
import { PARENT_SAVE_FAILED_TEXT, isAccessRefusal, parentRefusal } from './refusals';

// feat-001 spec § Page «Бэклоги» and § Notices on «Бэклоги»: where a refused
// parent write shows.

function refusal(code: string): ApiError {
  const body = problem(code);
  return new ApiError(body.status, body);
}

describe('parentRefusal', () => {
  it.each([
    'workitems.link.parent_not_found',
    'workitems.link.parent_level_not_allowed',
    'workitems.link.self_parent',
    'workitems.link.cycle',
    'workitems.link.item_deleted',
  ])('%s is the field’s error and the notice, with the server’s text', (code) => {
    expect(parentRefusal(refusal(code))).toEqual({
      field: catalogText(code),
      notice: catalogText(code),
      parentChanged: false,
    });
  });

  it('`workitems.link.depth_exceeded` (feat-027 D-9) is the field’s error and the notice too — the dialog’s «Родитель», a drop’s notice — with the finished text the server sends', () => {
    const text =
      'Слишком глубокая вложенность: в цепочке родителей может быть не больше 64 элементов.';

    expect(
      parentRefusal(
        new ApiError(409, {
          status: 409,
          errorCode: 'workitems.link.depth_exceeded',
          detail: text,
          params: { limit: { value: 64, type: 'number' } },
        }),
      ),
    ).toEqual({ field: text, notice: text, parentChanged: false });
  });

  it('`workitems.link.parent_changed` is the field’s error too, and says the view is to be read again', () => {
    expect(parentRefusal(refusal('workitems.link.parent_changed'))).toEqual({
      field: catalogText('workitems.link.parent_changed'),
      notice: catalogText('workitems.link.parent_changed'),
      parentChanged: true,
    });
  });

  it('any other code is the dialog’s `Alert` with its text; a not-found of the address shows nothing', () => {
    expect(parentRefusal(refusal('workitems.workitem.not_found'))).toEqual({
      field: null,
      notice: catalogText('workitems.workitem.not_found'),
      parentChanged: false,
    });
    expect(parentRefusal(refusal('organizations.project.not_found'))).toEqual({
      field: null,
      notice: null,
      parentChanged: false,
    });
  });

  it('no answer, or one without a code, is «Не удалось сохранить родителя. Попробуйте ещё раз.»', () => {
    expect(PARENT_SAVE_FAILED_TEXT).toBe('Не удалось сохранить родителя. Попробуйте ещё раз.');
    expect(parentRefusal(new TypeError('Failed to fetch')).notice).toBe(PARENT_SAVE_FAILED_TEXT);
    expect(parentRefusal(new ApiError(500, { status: 500, title: 'Ошибка' })).notice).toBe(
      PARENT_SAVE_FAILED_TEXT,
    );
  });
});

describe('isAccessRefusal (mod-016 design § 2.4)', () => {
  it('is the scoped check’s 403 by its code alone', () => {
    expect(isAccessRefusal(refusal('organizations.access.forbidden'))).toBe(true);
  });

  it.each([
    ['another refusal', refusal('workitems.link.cycle')],
    ['an answer without a code', new ApiError(403, { status: 403, title: 'Недостаточно прав' })],
    ['a failure without an answer', new Error('Failed to fetch')],
    ['nothing', undefined],
  ])('is not %s', (_case, error) => {
    expect(isAccessRefusal(error)).toBe(false);
  });
});

describe('the skeleton’s headers (feat-027 BR-T-1)', () => {
  it('are the table’s own, the hierarchy header blank, with no «ID», «Родитель», «Исполнитель», «Приоритет» or «Действия»', () => {
    expect(backlogSkeletonColumns()).toEqual([
      '',
      'Порядок',
      'Тип',
      'Заголовок',
      'Статус',
      'Трудоёмкость',
      'Бизнес-ценность',
      'Вид ценности',
    ]);
  });
});
