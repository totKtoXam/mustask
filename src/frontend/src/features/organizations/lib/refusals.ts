import { ApiError } from '../../../shared/api/httpClient';
import { organizationErrorCodes as codes } from '../../../shared/api/errorCodes';
import { apiErrorText } from '../../../shared/lib/apiErrorText';
import { notFoundLevel } from './notFoundLevel';

/**
 * A dialog's fields a refusal can land on (design § 4.4): «Название»,
 * «Ключ», «Команда» (the default-team dialog, mod-017) and «Новая родительская
 * область» (the move dialog).
 */
export type RefusalField = 'name' | 'key' | 'team' | 'parent';

/** Where a write's refusal shows in its dialog. */
export interface PlacedRefusal {
  /** A refusal about one field's value: that field's helper text. */
  fields: Partial<Record<RefusalField, string>>;
  /** Any other refusal: the `Alert` at the top of the dialog. */
  alert: string | null;
  /**
   * The page's own context is gone (an organization, project or team
   * not-found code): closing the dialog refetches the page, which then shows
   * its frame (design § 4.4, last row).
   */
  contextGone: boolean;
}

/**
 * The fields each code is about, in order of preference: the first one the
 * dialog has takes it. «Область с таким названием уже есть на этом уровне»
 * is «Название» in a create or rename and «Новая родительская область» in a
 * move, which has no name field.
 */
const FIELDS_OF_CODE: Readonly<Record<string, readonly RefusalField[]>> = {
  [codes.organizationNameTaken]: ['name'],
  [codes.projectNameTaken]: ['name'],
  [codes.projectNameTakenByDeactivated]: ['name'],
  [codes.projectKeyTaken]: ['key'],
  [codes.teamNameTaken]: ['name'],
  [codes.areaNameTaken]: ['name', 'parent'],
  [codes.areaMoveIntoOwnSubtree]: ['parent'],
  // mod-017 api-contract § 0.2: a team named in the default-team write is refused under «Команда».
  [codes.teamNotFound]: ['team'],
  [codes.teamNotInProject]: ['team'],
};

/** `validation_failed`'s field names (api-contract § 0.5). */
const VALIDATED_FIELDS: readonly RefusalField[] = ['name', 'key'];

function alertOnly(error: unknown): PlacedRefusal {
  return { fields: {}, alert: apiErrorText(error), contextGone: notFoundLevel(error) !== null };
}

/**
 * Places a write's refusal by its `errorCode` alone, never by its text
 * (frontend.md "Named values"); the text is always the server's
 * (`apiErrorText`), or a field reason's own `detail` for
 * `organizations.validation_failed`. A code about a field the dialog does
 * not have goes to the `Alert`.
 */
export function placeRefusal(error: unknown, fields: readonly RefusalField[]): PlacedRefusal {
  if (!(error instanceof ApiError) || error.errorCode === undefined) {
    return alertOnly(error);
  }

  if (error.errorCode === codes.validationFailed) {
    const placed: PlacedRefusal = { fields: {}, alert: null, contextGone: false };
    const reasons = error.body?.errors ?? {};
    const unplaced: string[] = [];
    for (const [name, list] of Object.entries(reasons)) {
      const detail = list[0]?.detail;
      if (!detail) {
        continue;
      }
      const field = VALIDATED_FIELDS.find((candidate) => candidate === name);
      if (field && fields.includes(field)) {
        placed.fields[field] = detail;
      } else {
        unplaced.push(detail);
      }
    }
    if (unplaced.length > 0 || Object.keys(placed.fields).length === 0) {
      placed.alert = unplaced.length > 0 ? unplaced.join(' ') : apiErrorText(error);
    }
    return placed;
  }

  const field = (FIELDS_OF_CODE[error.errorCode] ?? []).find((candidate) =>
    fields.includes(candidate),
  );
  if (field) {
    return { fields: { [field]: apiErrorText(error) }, alert: null, contextGone: false };
  }
  return alertOnly(error);
}
