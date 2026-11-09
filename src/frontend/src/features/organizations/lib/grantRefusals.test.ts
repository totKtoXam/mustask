import { describe, expect, it } from 'vitest';
import { ApiError } from '../../../shared/api/httpClient';
import { placeGrantRefusal } from './grantRefusals';

function refusal(status: number, errorCode: string, detail = 'Текст сервера.') {
  return new ApiError(status, { status, errorCode, detail } as never, '/x');
}

describe('mod-019 where a refused grant write shows (design § 3, § 7)', () => {
  it('puts the self-grant on the user field', () => {
    expect(placeGrantRefusal(refusal(400, 'organizations.grant.self_grant')).field).toBe(
      'Нельзя выдать доступ самому себе.',
    );
  });

  it('puts a scope or role refusal into the dialog notice', () => {
    expect(placeGrantRefusal(refusal(409, 'organizations.grant.scope_not_allowed')).alert).toBe(
      'Эту роль нельзя выдать на этом объекте.',
    );
    expect(placeGrantRefusal(refusal(400, 'organizations.grant.role_invalid')).alert).toBe(
      'Эту роль нельзя выдать на этом объекте.',
    );
  });

  it('closes on a changed grant and reads the list again', () => {
    const placed = placeGrantRefusal(refusal(409, 'organizations.grant.changed'));
    expect(placed).toMatchObject({ changed: true, reread: true, alert: null });
  });

  it('says «Участник не найден.» on the field and reads again', () => {
    expect(placeGrantRefusal(refusal(404, 'organizations.membership.not_found'))).toMatchObject({
      field: 'Участник не найден.',
      reread: true,
    });
  });

  it('reports a gone object and answers a 403 as «Недостаточно прав» with a re-read', () => {
    expect(placeGrantRefusal(refusal(404, 'organizations.project.not_found')).contextGone).toBe(
      true,
    );
    expect(placeGrantRefusal(refusal(403, 'organizations.access.forbidden'))).toMatchObject({
      alert: 'Недостаточно прав',
      reread: true,
    });
  });
});
