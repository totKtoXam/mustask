// A refused save (api-contract § 2.5): the 400 `errors` are keyed `name`, `settings.source`,
// `settings.mappings[<i>].<part>` and `settings.recoding[<i>]`, every reason at once, each with the
// catalogue's Russian `detail`. The panel puts each on the field it names and says the rest in a banner.
import { ApiError } from '../../../shared/api/httpClient';
import { importErrorCodes } from '../../../shared/api/errorCodes';
import { apiErrorText } from '../../../shared/lib/apiErrorText';
import { mappedRows, type Draft, type DraftErrors } from './draft';

export interface SaveFailure {
  errors: DraftErrors;
  /** A refusal that lands on no field: shown in the form's banner. */
  banner: string | null;
  /** The template limit (409): the list page's own alert says the same. */
  limitReached: boolean;
}

const INDEX = /\[(\d+)\]/;

export function saveFailure(error: unknown, draft: Draft): SaveFailure {
  const errors: DraftErrors = { mappings: {}, missingRequired: [], recoding: {} };
  if (!(error instanceof ApiError)) {
    return { errors, banner: apiErrorText(error), limitReached: false };
  }
  if (error.errorCode === importErrorCodes.templateNameTaken) {
    errors.name = error.body?.detail ?? '';
    return { errors, banner: null, limitReached: false };
  }
  if (error.errorCode === importErrorCodes.templateLimitReached) {
    return { errors, banner: error.body?.detail ?? apiErrorText(error), limitReached: true };
  }
  if (error.errorCode !== importErrorCodes.validationFailed || !error.body?.errors) {
    return { errors, banner: apiErrorText(error), limitReached: false };
  }
  const mapped = mappedRows(draft);
  let unplaced = false;
  for (const [key, reasons] of Object.entries(error.body.errors)) {
    const text = reasons.map((reason) => reason.detail).join(' ');
    const at = Number(INDEX.exec(key)?.[1]);
    if (key === 'name') errors.name = text;
    else if (key.startsWith('settings.source')) {
      if (key.toLowerCase().includes('first')) errors.firstDataRow = text;
      else errors.headerRow = text;
    } else if (key.startsWith('settings.mappings[') && mapped[at]) {
      errors.mappings[mapped[at].id] = text;
    } else if (key.startsWith('settings.recoding[')) {
      const mappingId = draft.recoding[at]?.mappingId;
      if (mappingId) errors.recoding[mappingId] = text;
      else unplaced = true;
    } else {
      unplaced = true;
    }
  }
  return { errors, banner: unplaced ? (error.body.detail ?? null) : null, limitReached: false };
}
