import { describe, expect, it } from 'vitest';
import { ApiError } from '../../../shared/api/httpClient';
import { RESCUE_REFUSAL_TEXTS } from './rescueTexts';
import { placeAppointmentRefusal } from './rescueRefusals';
import { problem, validationProblem } from '../testFixtures';

// feat-033 design § 4.2, § 6: where each refusal of «Назначить» lands, by code, with the catalog's text.

function refusal(code: string) {
  const body = problem(code);
  return new ApiError(body.status, { ...body, detail: 'Чужой текст' });
}

describe('RESCUE_REFUSAL_TEXTS', () => {
  it.each(Object.entries(RESCUE_REFUSAL_TEXTS))(
    '%s reads as the catalog’s Russian text',
    (code, text) => {
      expect(problem(code).detail.replace(/\.$/, '')).toBe(text.replace(/\.$/, ''));
    },
  );

  it('holds every rescue code of the catalog', () => {
    const codes = Object.keys(RESCUE_REFUSAL_TEXTS).filter((code) => code.includes('.rescue.'));
    expect(codes).toHaveLength(8);
  });
});

describe('placeAppointmentRefusal', () => {
  it('a 403 is the page’s, not a field’s', () => {
    expect(placeAppointmentRefusal(refusal('organizations.rescue.forbidden')).forbidden).toBe(true);
    expect(placeAppointmentRefusal(new ApiError(403, null)).forbidden).toBe(true);
  });

  it('the missing organization is its own place', () => {
    expect(placeAppointmentRefusal(refusal('organizations.organization.not_found'))).toMatchObject({
      organizationGone: true,
      userError: null,
    });
  });

  it.each([
    [
      'organizations.rescue.user_unavailable',
      'Этого пользователя нельзя назначить администратором',
    ],
    ['organizations.rescue.self_appointment', 'Нельзя назначить администратором себя'],
    ['organizations.rescue.already_admin', 'Пользователь уже администратор этой организации'],
  ])(
    '%s is the helper of «Пользователь», in the catalog’s words, not the server’s',
    (code, text) => {
      expect(placeAppointmentRefusal(refusal(code))).toMatchObject({
        userError: text,
        alert: null,
      });
    },
  );

  it.each([
    ['organizations.rescue.reason_required', 'Укажите причину'],
    ['organizations.rescue.reason_too_long', 'Не более 500 символов'],
    ['organizations.rescue.reason_invalid', 'Причина содержит недопустимые символы'],
  ])('a 400 with %s under `reason` is the helper of «Причина»', (code, text) => {
    const body = validationProblem({ reason: [code] });
    expect(placeAppointmentRefusal(new ApiError(400, body))).toMatchObject({
      reasonError: text,
      alert: null,
    });
  });

  it('anything else is the dialog’s notice with the server’s text', () => {
    expect(
      placeAppointmentRefusal(new ApiError(500, { status: 500, detail: 'Сбой' })),
    ).toMatchObject({ alert: 'Сбой', userError: null, reasonError: null });
    expect(placeAppointmentRefusal(new TypeError('x')).alert).toBeTruthy();
  });
});
