import { describe, expect, it } from 'vitest';
import { ApiError } from '../../../shared/api/httpClient';
import { problem, validationProblem } from '../testFixtures';
import { notFoundLevel } from './notFoundLevel';
import { placeRefusal, type RefusalField } from './refusals';

function refusal(code: string, params?: Record<string, string>): ApiError {
  const body = problem(code, params);
  return new ApiError(body.status, body);
}

const NAME: readonly RefusalField[] = ['name'];
const PROJECT: readonly RefusalField[] = ['name', 'key'];
const MOVE: readonly RefusalField[] = ['parent'];
const DEFAULT_AREA: readonly RefusalField[] = ['area'];
const CONFIRM: readonly RefusalField[] = [];

describe('mod-002 design § 4.4: every refusal lands where the table says, by its code', () => {
  it.each([
    [
      'organizations.organization.name_taken',
      NAME,
      'name',
      'Название «Альфа» уже занято',
      { name: 'Альфа' },
    ],
    [
      'organizations.project.key_taken',
      PROJECT,
      'key',
      'Ключ «PRM» уже используется в организации',
      { key: 'PRM' },
    ],
    [
      'organizations.project.name_taken',
      PROJECT,
      'name',
      'Проект с таким названием уже есть в организации',
      {},
    ],
    [
      'organizations.project.name_taken_by_deactivated',
      PROJECT,
      'name',
      'Проект с таким названием уже есть в организации: он деактивирован',
      {},
    ],
    [
      'organizations.team.name_taken',
      NAME,
      'name',
      'Команда с таким названием уже есть в проекте',
      {},
    ],
    [
      'organizations.area.name_taken',
      NAME,
      'name',
      'Область с таким названием уже есть на этом уровне',
      {},
    ],
    [
      'organizations.area.name_taken',
      MOVE,
      'parent',
      'Область с таким названием уже есть на этом уровне',
      {},
    ],
    [
      'organizations.area.move_into_own_subtree',
      MOVE,
      'parent',
      'Нельзя переместить область внутрь неё самой или её вложенной области',
      {},
    ],
    [
      'organizations.team.default_area_not_owned',
      DEFAULT_AREA,
      'area',
      'Область по умолчанию должна принадлежать самой команде',
      {},
    ],
  ])('%s → the field «%s» of its dialog', (code, fields, field, text, params) => {
    const placed = placeRefusal(refusal(code, params), fields);

    expect(placed.fields).toEqual({ [field]: text });
    expect(placed.alert).toBeNull();
    expect(placed.contextGone).toBe(false);
  });

  it.each([
    [
      'organizations.organization.create_failed',
      'Не удалось создать организацию. Попробуйте ещё раз',
      {},
    ],
    ['organizations.organization.create_forbidden', 'Недостаточно прав', {}],
    [
      'organizations.area.is_team_default',
      'Область используется как область по умолчанию команды «Платформа»',
      { team: 'Платформа' },
    ],
    [
      'organizations.area.not_empty',
      'Нельзя удалить область, в которой есть рабочие элементы (включая удалённые) или вложенные области',
      {},
    ],
    [
      'organizations.team.first_team_cannot_be_deleted',
      'Нельзя удалить команду, которой принадлежит корневая область проекта',
      {},
    ],
    ['organizations.area.root_is_fixed', 'Корневая область меняется только вместе с проектом', {}],
    ['organizations.area.not_found', 'Область не найдена.', {}],
    [
      'organizations.team.first_team_default_fixed',
      'У первой команды область по умолчанию не меняется',
      {},
    ],
  ])('%s → the dialog’s Alert with the server’s text', (code, text, params) => {
    const placed = placeRefusal(refusal(code, params), ['name', 'key', 'area', 'parent']);

    expect(placed.fields).toEqual({});
    expect(placed.alert).toBe(text);
    expect(placed.contextGone).toBe(false);
  });

  it.each([
    ['organizations.organization.not_found', 'Организация не найдена.'],
    ['organizations.project.not_found', 'Проект не найден.'],
    ['organizations.team.not_found', 'Команда не найдена.'],
  ])('%s → the Alert, and the page’s context is gone', (code, text) => {
    const placed = placeRefusal(refusal(code), NAME);

    expect(placed).toEqual({ fields: {}, alert: text, contextGone: true });
  });

  it('a code about a field the dialog lacks goes to the Alert', () => {
    expect(placeRefusal(refusal('organizations.team.name_taken'), CONFIRM).alert).toBe(
      'Команда с таким названием уже есть в проекте',
    );
  });

  it('`validation_failed` puts each field reason’s own text on its field', () => {
    const body = validationProblem({
      name: ['organizations.name_length'],
      key: ['organizations.project.key_invalid'],
    });

    const placed = placeRefusal(new ApiError(400, body), PROJECT);

    expect(placed).toEqual({
      fields: {
        name: 'Название должно содержать от 1 до 100 символов',
        key: 'Ключ проекта: от 2 до 10 латинских заглавных букв и цифр, начиная с буквы',
      },
      alert: null,
      contextGone: false,
    });
  });

  it('`validation_failed` about a field the dialog lacks shows its reason in the Alert', () => {
    const body = validationProblem({ key: ['organizations.project.key_required'] });

    expect(placeRefusal(new ApiError(400, body), NAME)).toEqual({
      fields: {},
      alert: 'Укажите ключ проекта',
      contextGone: false,
    });
  });

  it('anything else, a network failure included, goes to the Alert', () => {
    expect(placeRefusal(new ApiError(500, null), NAME).alert).toBe('Ошибка API (HTTP 500).');
    const network = new TypeError('fetch failed');
    expect(placeRefusal(network, NAME).alert).toBe(
      'Не удалось соединиться с API. Проверьте, что Mustask.Api запущен.',
    );
  });
});

describe('mod-002 design § 12: the not-found code picks the frame', () => {
  it('maps the three page levels and nothing else', () => {
    expect(notFoundLevel(refusal('organizations.organization.not_found'))).toBe('organization');
    expect(notFoundLevel(refusal('organizations.project.not_found'))).toBe('project');
    expect(notFoundLevel(refusal('organizations.team.not_found'))).toBe('team');
    expect(notFoundLevel(refusal('organizations.area.not_found'))).toBeNull();
    expect(notFoundLevel(refusal('organizations.team.name_taken'))).toBeNull();
    expect(notFoundLevel(new ApiError(404, null))).toBeNull();
    expect(notFoundLevel(new Error('x'))).toBeNull();
  });
});
