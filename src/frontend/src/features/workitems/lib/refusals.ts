import { ApiError } from '../../../shared/api/httpClient';
import { organizationErrorCodes, workItemErrorCodes } from '../../../shared/api/errorCodes';
import { apiErrorText } from '../../../shared/lib/apiErrorText';
import { notFoundLevel, type WorkItemsNotFoundLevel } from './notFoundLevel';

/** The fields a write's refusal can land on: the dialog has «Заголовок», the form all five. */
export type RefusalField = 'title' | 'description' | 'state' | 'assignee' | 'area';

/** Where a write's refusal shows (spec § Error codes; api-contract § 0.5). */
export interface PlacedRefusal {
  /** A refusal about one field's value: that field's error text. */
  fields: Partial<Record<RefusalField, string>>;
  /** Any other refusal: the dialog's `Alert`, or the form's above the band. */
  alert: string | null;
  /**
   * The path's own level is gone: the form is replaced by this frame; an open
   * dialog shows the text in its `Alert` and the frame once it closes.
   */
  frame: WorkItemsNotFoundLevel | null;
}

/** The answers about one value of the body, each with the field it lands on. */
const FIELD_OF_CODE: Readonly<Record<string, RefusalField>> = {
  [workItemErrorCodes.stateNotAllowed]: 'state',
  [workItemErrorCodes.assigneeUnavailable]: 'assignee',
  [organizationErrorCodes.areaNotFound]: 'area',
};

/** `workitems.validation_failed`'s field names a field of the screen shows (§ 0.5). */
const VALIDATED_FIELDS: Readonly<Record<string, RefusalField>> = {
  title: 'title',
  description: 'description',
};

/**
 * Places a write's refusal by its `errorCode` alone, never by its text
 * (frontend.md "Named values"). The text is always the server's: the answer's
 * `detail`, or a field reason's own `detail`. A field reason about `requestId`
 * or `page` — a panel defect — and a code about a field the screen lacks go
 * to the `Alert`.
 */
export function placeRefusal(error: unknown, fields: readonly RefusalField[]): PlacedRefusal {
  const frame = notFoundLevel(error);
  if (frame !== null) {
    return { fields: {}, alert: apiErrorText(error), frame };
  }
  if (!(error instanceof ApiError) || error.errorCode === undefined) {
    return { fields: {}, alert: apiErrorText(error), frame: null };
  }

  if (error.errorCode === workItemErrorCodes.validationFailed) {
    const placed: PlacedRefusal = { fields: {}, alert: null, frame: null };
    const unplaced: string[] = [];
    for (const [name, reasons] of Object.entries(error.body?.errors ?? {})) {
      const detail = reasons[0]?.detail;
      if (!detail) {
        continue;
      }
      const field = VALIDATED_FIELDS[name];
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

  const field = FIELD_OF_CODE[error.errorCode];
  if (field && fields.includes(field)) {
    return { fields: { [field]: apiErrorText(error) }, alert: null, frame: null };
  }
  return { fields: {}, alert: apiErrorText(error), frame: null };
}
