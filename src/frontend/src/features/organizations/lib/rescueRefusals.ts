import { ApiError } from '../../../shared/api/httpClient';
import { organizationErrorCodes as codes } from '../../../shared/api/errorCodes';
import { apiErrorText } from '../../../shared/lib/apiErrorText';
import { RESCUE_REFUSAL_TEXTS } from './rescueTexts';

/** Where a refusal of the appointment shows (design § 4.2, § 6). */
export interface PlacedAppointmentRefusal {
  /** 403: the page becomes «Доступ ограничен» and the dialog closes (DC-8). */
  forbidden: boolean;
  /** The organization is gone: the dialog's error notice, «Назначить» disabled, the list read again. */
  organizationGone: boolean;
  /** The three refusals of the user: the helper of «Пользователь». */
  userError: string | null;
  /** The server's «Причина» error: the helper of «Причина». */
  reasonError: string | null;
  /** Any other refusal (network, 5xx, an unknown code): the dialog's notice with the server's text. */
  alert: string | null;
}

const NOTHING: PlacedAppointmentRefusal = {
  forbidden: false,
  organizationGone: false,
  userError: null,
  reasonError: null,
  alert: null,
};

const USER_CODES: readonly string[] = [
  codes.rescueUserUnavailable,
  codes.rescueSelfAppointment,
  codes.rescueAlreadyAdmin,
];

const REASON_CODES: readonly string[] = [
  codes.rescueReasonRequired,
  codes.rescueReasonTooLong,
  codes.rescueReasonInvalid,
];

/** The text of a code from the Russian table; the server's own when the code is not in it. */
function textOf(code: string, error: unknown): string {
  return RESCUE_REFUSAL_TEXTS[code] ?? apiErrorText(error);
}

/** The first reason code of a 400's `errors.reason`, if the answer carries one. */
function reasonCodeOf(error: ApiError): string | undefined {
  return error.body?.errors?.reason
    ?.map((reason) => reason.code)
    .find((code) => REASON_CODES.includes(code));
}

/**
 * Places the refusal of «Назначить» by its `errorCode` alone, never by its text; the text shown is
 * the Russian one of the catalog (`RESCUE_REFUSAL_TEXTS`), the server's only as the fallback.
 */
export function placeAppointmentRefusal(error: unknown): PlacedAppointmentRefusal {
  if (!(error instanceof ApiError)) {
    return { ...NOTHING, alert: apiErrorText(error) };
  }
  const code = error.errorCode;
  if (error.status === 403 || code === codes.rescueForbidden) {
    return { ...NOTHING, forbidden: true };
  }
  if (code === codes.notFound) {
    return { ...NOTHING, organizationGone: true };
  }
  if (code !== undefined && USER_CODES.includes(code)) {
    return { ...NOTHING, userError: textOf(code, error) };
  }
  const reasonCode = reasonCodeOf(error);
  if (reasonCode !== undefined) {
    return { ...NOTHING, reasonError: textOf(reasonCode, error) };
  }
  return { ...NOTHING, alert: apiErrorText(error) };
}
