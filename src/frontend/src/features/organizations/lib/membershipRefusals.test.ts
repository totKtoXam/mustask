import { describe, expect, it } from 'vitest';
import { ApiError } from '../../../shared/api/httpClient';
import { problem } from '../testFixtures';
import { placeMembershipRefusal, type MembershipSurface } from './membershipRefusals';

// mod-016 design § 9: where each code shows on each surface, by the code
// alone, with the server's text. A cell the table leaves empty is the
// surface's alert (or, with no dialog, its notice).

function refusal(code: string): ApiError {
  const body = problem(code);
  return new ApiError(body.status, body);
}

const FIELD = 'field';
const ALERT = 'alert';
const NOTICE = 'notice';

type Place = typeof FIELD | typeof ALERT | typeof NOTICE | 'nowhere';

function placeOf(code: string, surface: MembershipSurface): { place: Place; reread: boolean } {
  const error = refusal(code);
  const placed = placeMembershipRefusal(error, surface);
  const text = error.body?.detail;
  const place: Place =
    placed.field === text
      ? FIELD
      : placed.alert === text
        ? ALERT
        : placed.notice === text
          ? NOTICE
          : 'nowhere';
  return { place, reread: placed.reread };
}

describe('mod-016 design § 9: the placement of every code of a membership write', () => {
  it.each([
    // [code, surface, place, re-read]
    ['organizations.membership.already_member', 'add', FIELD, true],
    ['organizations.membership.user_unavailable', 'add', FIELD, false],
    ['organizations.membership.last_admin', 'role', FIELD, false],
    ['organizations.membership.last_admin', 'remove', ALERT, false],
    ['organizations.membership.not_found', 'role', ALERT, true],
    ['organizations.membership.not_found', 'remove', ALERT, true],
    ['organizations.team.member_exists', 'teamAdd', FIELD, true],
    ['organizations.team.member_not_in_organization', 'teamAdd', FIELD, false],
    ['organizations.team.member_not_found', 'teamRemove', NOTICE, true],
    ['organizations.validation_failed', 'add', ALERT, false],
    ['organizations.validation_failed', 'role', ALERT, false],
    ['organizations.validation_failed', 'teamAdd', ALERT, false],
    ['organizations.access.forbidden', 'add', ALERT, false],
    ['organizations.access.forbidden', 'role', ALERT, false],
    ['organizations.access.forbidden', 'remove', ALERT, false],
    ['organizations.access.forbidden', 'teamAdd', ALERT, false],
    ['organizations.access.forbidden', 'teamRemove', NOTICE, false],
  ] as const)('%s on %s is the %s, re-reading: %s', (code, surface, place, reread) => {
    expect(placeOf(code, surface)).toEqual({ place, reread });
  });

  it.each(['add', 'role', 'remove', 'teamAdd', 'teamRemove'] as const)(
    'places «Организация не найдена» nowhere on %s: the page is already its frame',
    (surface) => {
      const placed = placeMembershipRefusal(
        refusal('organizations.organization.not_found'),
        surface,
      );

      expect(placed).toEqual({
        field: null,
        alert: null,
        notice: null,
        reread: false,
        contextGone: false,
      });
    },
  );

  it.each(['organizations.project.not_found', 'organizations.team.not_found'])(
    'says the page’s own entity is gone for %s on the team surfaces, and lands it in the alert or the notice',
    (code) => {
      const add = placeMembershipRefusal(refusal(code), 'teamAdd');
      const remove = placeMembershipRefusal(refusal(code), 'teamRemove');

      expect(add.contextGone).toBe(true);
      expect(add.alert).not.toBeNull();
      expect(remove.contextGone).toBe(true);
      expect(remove.notice).not.toBeNull();
    },
  );

  it('keeps a refusal without a code in the alert, with the generic text', () => {
    const placed = placeMembershipRefusal(new ApiError(500, null), 'add');

    expect(placed.alert).toBe('Ошибка API (HTTP 500).');
    expect(placed.field).toBeNull();
  });

  it('keeps an error that is not an API error in the alert, and in the notice on a surface with no dialog', () => {
    expect(placeMembershipRefusal(new Error('boom'), 'role').alert).toBe(
      'Непредвиденная ошибка. Повторите попытку.',
    );
    expect(placeMembershipRefusal(new Error('boom'), 'teamRemove').notice).toBe(
      'Непредвиденная ошибка. Повторите попытку.',
    );
  });

  it('branches on the code, never on the text', () => {
    const error = new ApiError(409, {
      status: 409,
      errorCode: 'organizations.membership.already_member',
      detail: 'Совсем другой текст',
    });

    expect(placeMembershipRefusal(error, 'add')).toMatchObject({
      field: 'Совсем другой текст',
      reread: true,
    });
  });
});
