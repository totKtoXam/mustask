import { describe, expect, it } from 'vitest';
import { ApiError } from '../../../shared/api/httpClient';
import { projectKeyOf } from './identifier';
import { placeRefusal } from './refusals';
import {
  createdNotice,
  createdNoticeOnTeamView,
  holdingCaption,
  noProjectAreaLine,
  teamDroppedLine,
} from './teams';

// feat-040: what a team's view adds to the creation and to the card — the notice that says why a
// created item is not in the rows (D-6), the dialog's lines before a project is chosen (D-4) and
// after one drops the team shown (D-5), the project key an identifier carries (BR-TS-4), and the
// project's refusal that lands under «Проект» (BR-TS-9).

const MARS = { id: '0199-mars' };
const VENUS = { id: '0199-venus' };
const BETA = { id: '0199b0a2-beta' };
const ALPHA = { id: '0199b0a2-alpha' };

describe('feat-040 D-6 the notice after a creation on a team’s view', () => {
  it('is today’s plain notice for an item of the team that the filter shows', () => {
    expect(
      createdNoticeOnTeamView('BET-US-42', MARS, BETA, { teamId: MARS.id, projectIds: [] }),
    ).toBe('Рабочий элемент BET-US-42 создан.');
    expect(
      createdNoticeOnTeamView('BET-US-42', MARS, BETA, { teamId: MARS.id, projectIds: [BETA.id] }),
    ).toBe('Рабочий элемент BET-US-42 создан.');
  });

  it('says the item does not belong to this team when its team is another or none', () => {
    const view = { teamId: MARS.id, projectIds: [] };

    expect(createdNoticeOnTeamView('BET-US-42', VENUS, BETA, view)).toBe(
      'Рабочий элемент BET-US-42 создан. Он не принадлежит этой команде.',
    );
    expect(createdNoticeOnTeamView('BET-US-42', null, BETA, view)).toBe(
      'Рабочий элемент BET-US-42 создан. Он не принадлежит этой команде.',
    );
  });

  it('says the item does not fit the filter when the project filter hides its project', () => {
    expect(
      createdNoticeOnTeamView('BET-US-42', MARS, BETA, { teamId: MARS.id, projectIds: [ALPHA.id] }),
    ).toBe('Рабочий элемент BET-US-42 создан. Он не подходит под текущий фильтр.');
  });

  it('names the team first when both reasons hold', () => {
    expect(
      createdNoticeOnTeamView('BET-US-42', null, BETA, { teamId: MARS.id, projectIds: [ALPHA.id] }),
    ).toBe('Рабочий элемент BET-US-42 создан. Он не принадлежит этой команде.');
  });

  it('reads the ids of the address as the server does: either letter case', () => {
    expect(
      createdNoticeOnTeamView('BET-US-42', MARS, BETA, {
        teamId: MARS.id,
        projectIds: [BETA.id.toUpperCase()],
      }),
    ).toBe('Рабочий элемент BET-US-42 создан.');
  });

  it('leaves the project views’ notice as it was (mod-017 D-5)', () => {
    expect(createdNotice('BET-US-42', MARS, [])).toBe('Рабочий элемент BET-US-42 создан.');
    expect(createdNotice('BET-US-42', MARS, [VENUS.id])).toBe(
      'Рабочий элемент BET-US-42 создан. Он не подходит под текущий фильтр.',
    );
  });
});

describe('feat-040 the dialog’s lines (design § 6.2)', () => {
  it('holds the view’s team until a project is chosen, and says why (D-4)', () => {
    expect(holdingCaption('MARS')).toBe(
      'Встанет в конец бэклога команды «MARS». Другую команду можно выбрать после выбора проекта.',
    );
  });

  it('says a team that is not in the chosen project was set back to the view’s team (D-5)', () => {
    expect(teamDroppedLine('VENUS', 'Beta', 'MARS')).toBe(
      'Команда «VENUS» не участвует в проекте «Beta». Выбрана «MARS».',
    );
  });

  it('reads the area line without a project', () => {
    expect(noProjectAreaLine('Новая')).toBe(
      'Рабочий элемент будет создан в статусе «Новая». Остальные поля заполняются в его форме.',
    );
  });
});

describe('feat-040 BR-TS-4 the project an identifier names', () => {
  it('is the key before the first hyphen', () => {
    expect(projectKeyOf('BET-US-3')).toBe('BET');
    expect(projectKeyOf('MSK-FEAT-21')).toBe('MSK');
    expect(projectKeyOf('A1B2C3D4E5-TASK-12')).toBe('A1B2C3D4E5');
  });

  it.each([
    ['unknown shape', 'something'],
    ['a number of no item', 'BET-US-0'],
    ['no number', 'BET-US'],
    ['a key in lower case', 'bet-us-3'],
    ['a key of one letter', 'B-US-3'],
    ['a key starting with a digit', '1BET-US-3'],
    ['a key of eleven characters', 'ABCDEFGHIJK-US-3'],
    ['spaces', 'BET-US-3 '],
    ['empty', ''],
  ])('is none for %s', (_case, identifier) => {
    expect(projectKeyOf(identifier)).toBeNull();
  });
});

describe('feat-040 BR-TS-9 a refusal of the chosen project', () => {
  function refusal(errorCode: string, detail: string) {
    return new ApiError(404, { status: 404, errorCode, detail });
  }

  it('lands under «Проект» on a form that has the field, never as a lost page', () => {
    const placed = placeRefusal(refusal('organizations.project.not_found', 'Проект не найден.'), [
      'title',
      'team',
      'project',
    ]);

    expect(placed).toEqual({ fields: { project: 'Проект не найден.' }, alert: null, frame: null });
  });

  it('is the page’s frame on a form without the field, as before', () => {
    const placed = placeRefusal(refusal('organizations.project.not_found', 'Проект не найден.'), [
      'title',
      'team',
    ]);

    expect(placed.frame).toBe('project');
    expect(placed.fields).toEqual({});
  });

  it('leaves the team’s refusals under «Команда»', () => {
    const placed = placeRefusal(
      refusal('organizations.team.not_in_project', 'Команда не участвует в проекте.'),
      ['title', 'team', 'project'],
    );

    expect(placed.fields).toEqual({ team: 'Команда не участвует в проекте.' });
  });
});
