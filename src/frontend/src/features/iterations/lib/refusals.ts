import { ApiError } from '../../../shared/api/httpClient';
import { iterationErrorCodes } from '../../../shared/api/errorCodes';
import { apiErrorText } from '../../../shared/lib/apiErrorText';

/** The form fields a refusal can stand under (design § 5.1). */
export type SprintField = 'name' | 'dates' | 'destination';

export interface SprintRefusal {
  /** The server's own text. */
  text: string;
  /** The field it stands under, or `null`: a notice at the top of the dialog. */
  field: SprintField | null;
  /** The code, for the branch a dialog takes (close, re-read, …). */
  code: string | undefined;
  /** The team is gone: the page answers `NotFound`. */
  teamGone: boolean;
  /** The sprint is gone (404). */
  sprintGone: boolean;
  /** The caller lost the code (403). */
  forbidden: boolean;
}

const FIELD_OF_CODE: Readonly<Record<string, SprintField>> = {
  [iterationErrorCodes.nameTaken]: 'name',
  [iterationErrorCodes.nameInvalid]: 'name',
  [iterationErrorCodes.datesUnpaired]: 'dates',
  [iterationErrorCodes.datesReversed]: 'dates',
  [iterationErrorCodes.activeDatesRequired]: 'dates',
  [iterationErrorCodes.closeDestinationInvalid]: 'destination',
};

const TEAM_NOT_FOUND = 'organizations.team.not_found';
const FORBIDDEN = 'organizations.access.forbidden';

/**
 * Places a sprint write's refusal by its `errorCode` alone, never by its text (frontend.md "Named
 * values"). A 400 `validation_failed` names its field in `errors`; the text is the server's.
 */
export function placeSprintRefusal(error: unknown): SprintRefusal {
  const base: SprintRefusal = {
    text: apiErrorText(error),
    field: null,
    code: undefined,
    teamGone: false,
    sprintGone: false,
    forbidden: false,
  };
  if (!(error instanceof ApiError)) return base;
  const code = error.errorCode;
  const next = { ...base, code };
  next.teamGone = code === TEAM_NOT_FOUND;
  next.forbidden = code === FORBIDDEN || error.status === 403;
  next.sprintGone = code === iterationErrorCodes.notFound;
  if (code === iterationErrorCodes.validationFailed) {
    const errors = error.body?.errors ?? {};
    const name = errors.name?.[0]?.detail;
    const end = errors.endDate?.[0]?.detail ?? errors.startDate?.[0]?.detail;
    if (name) return { ...next, field: 'name', text: name };
    if (end) return { ...next, field: 'dates', text: end };
    return next;
  }
  if (code && FIELD_OF_CODE[code]) {
    return { ...next, field: FIELD_OF_CODE[code] };
  }
  return next;
}
