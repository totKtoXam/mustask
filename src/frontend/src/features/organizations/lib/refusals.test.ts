import { describe, expect, it } from 'vitest';
import { ApiError } from '../../../shared/api/httpClient';
import { catalogText, problem, validationProblem } from '../testFixtures';
import { notFoundLevel } from './notFoundLevel';
import { placeRefusal, type RefusalField } from './refusals';

function refusal(code: string, params?: Record<string, string>): ApiError {
  const body = problem(code, params);
  return new ApiError(body.status, body);
}

const NAME: readonly RefusalField[] = ['name'];
const PROJECT: readonly RefusalField[] = ['name', 'key'];
const MOVE: readonly RefusalField[] = ['parent'];
const DEFAULT_TEAM: readonly RefusalField[] = ['team'];
const AREAS: readonly RefusalField[] = ['areas'];
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
      // feat-028 BR-OL-3: the name is unique in the organization (mod-002 BR-M1-1 replaced).
      'Команда с таким названием уже есть в организации',
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
      'organizations.area.depth_exceeded',
      MOVE,
      'parent',
      'Дерево областей не может быть глубже 10 уровней',
      {},
    ],
    // mod-018 api-contract § 7 (ADR-0031): the type rules land on the place field.
    [
      'organizations.area.type_not_allowed',
      MOVE,
      'parent',
      catalogText('organizations.area.type_not_allowed'),
      {},
    ],
    [
      'organizations.area.outside_project',
      MOVE,
      'parent',
      'Вложенную область нельзя переместить за пределы её проекта',
      {},
    ],
    [
      'organizations.area.link_type_not_allowed',
      AREAS,
      'areas',
      catalogText('organizations.area.link_type_not_allowed'),
      {},
    ],
    // mod-018 design §§ 5.1, 5.3: a vanished chosen parent or area lands on its field.
    ['organizations.area.not_found', MOVE, 'parent', 'Область не найдена.', {}],
    ['organizations.area.not_found', AREAS, 'areas', 'Область не найдена.', {}],
    ['organizations.team.not_found', DEFAULT_TEAM, 'team', 'Команда не найдена.', {}],
    [
      'organizations.team.not_in_project',
      DEFAULT_TEAM,
      'team',
      'Команда не участвует в проекте.',
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
      'organizations.area.not_empty',
      'Нельзя удалить область, в которой есть проекты, вложенные области или рабочие элементы',
      {},
    ],
    // The row of `organizations.team.last_team_cannot_be_deleted` is retired by name with the guard
    // (doc-012 P-1, feat-028 BR-OL-10): a project may have no team and the server no longer answers it.
    ['organizations.area.root_is_fixed', 'Корневая область меняется только вместе с проектом', {}],
  ])('%s → the dialog’s Alert with the server’s text', (code, text, params) => {
    const placed = placeRefusal(refusal(code, params), ['name', 'key']);

    expect(placed.fields).toEqual({});
    expect(placed.alert).toBe(text);
    expect(placed.contextGone).toBe(false);
  });

  it.each([
    ['organizations.organization.not_found', 'Организация не найдена.'],
    ['organizations.project.not_found', 'Проект не найден.'],
    ['organizations.team.not_found', 'Команда не найдена.'],
    // The page’s own area, or one the form names with no field to show it.
    ['organizations.area.not_found', 'Область не найдена.'],
  ])('%s → the Alert, and the page’s context is gone', (code, text) => {
    const placed = placeRefusal(refusal(code), NAME);

    expect(placed).toEqual({ fields: {}, alert: text, contextGone: true });
  });

  it('a code about a field the dialog lacks goes to the Alert', () => {
    expect(placeRefusal(refusal('organizations.team.name_taken'), CONFIRM).alert).toBe(
      'Команда с таким названием уже есть в организации',
    );
  });

  describe('feat-028 design §§ 5.1, 6.1, 6.2, 8: the pickers and the unlink', () => {
    const CREATE_TEAM: readonly RefusalField[] = ['name', 'description', 'members', 'projects'];
    const CREATE_PROJECT: readonly RefusalField[] = ['name', 'key', 'description', 'teams'];
    const LINK_TEAMS: readonly RefusalField[] = ['teams'];
    const LINK_PROJECTS: readonly RefusalField[] = ['projects'];
    const UNLINK: readonly RefusalField[] = ['destination'];

    it.each([
      [
        'organizations.team.member_not_in_organization',
        CREATE_TEAM,
        'members',
        'Добавить в команду можно только активного участника организации',
      ],
      ['organizations.project.not_found', CREATE_TEAM, 'projects', 'Проект не найден.'],
      ['organizations.team.not_found', CREATE_PROJECT, 'teams', 'Команда не найдена.'],
      ['organizations.team.not_found', LINK_TEAMS, 'teams', 'Команда не найдена.'],
      ['organizations.project.not_found', LINK_PROJECTS, 'projects', 'Проект не найден.'],
      [
        'organizations.team.not_in_project',
        UNLINK,
        'destination',
        'Команда не участвует в проекте.',
      ],
      ['organizations.team.not_found', UNLINK, 'destination', 'Команда не найдена.'],
    ] as const)('%s → «%s» of the dialog that has it', (code, fields, field, text) => {
      expect(placeRefusal(refusal(code), fields)).toEqual({
        fields: { [field]: text },
        alert: null,
        contextGone: false,
      });
    });

    it('the page’s own end is no field: a team that is not found in a project’s link dialog, a project in a team’s', () => {
      expect(placeRefusal(refusal('organizations.project.not_found'), LINK_TEAMS)).toEqual({
        fields: {},
        alert: 'Проект не найден.',
        contextGone: true,
      });
      expect(placeRefusal(refusal('organizations.team.not_found'), LINK_PROJECTS)).toEqual({
        fields: {},
        alert: 'Команда не найдена.',
        contextGone: true,
      });
    });

    it('puts the description’s and the destination’s field reasons on their fields', () => {
      const body = validationProblem({
        description: ['organizations.description_length'],
        destination: ['organizations.team.destination_required'],
      });

      expect(placeRefusal(new ApiError(400, body), [...CREATE_TEAM, 'destination'])).toEqual({
        fields: {
          description: 'Описание должно содержать не более 2000 символов',
          destination: 'Выберите команду или «Без команды»',
        },
        alert: null,
        contextGone: false,
      });
    });
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
  it('maps the four page levels and nothing else', () => {
    expect(notFoundLevel(refusal('organizations.organization.not_found'))).toBe('organization');
    expect(notFoundLevel(refusal('organizations.project.not_found'))).toBe('project');
    expect(notFoundLevel(refusal('organizations.team.not_found'))).toBe('team');
    expect(notFoundLevel(refusal('organizations.area.not_found'))).toBe('area');
    expect(notFoundLevel(refusal('organizations.team.name_taken'))).toBeNull();
    expect(notFoundLevel(new ApiError(404, null))).toBeNull();
    expect(notFoundLevel(new Error('x'))).toBeNull();
  });
});
