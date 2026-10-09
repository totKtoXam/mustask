import { ApiError } from '../../../shared/api/httpClient';
import { organizationErrorCodes, workItemErrorCodes } from '../../../shared/api/errorCodes';
import { apiErrorText } from '../../../shared/lib/apiErrorText';
import type { WorkItemFieldName } from '../api/workItemsApi';
import { WORK_ITEM_FIELD_NAMES } from './fields';
import { notFoundLevel, type WorkItemsNotFoundLevel } from './notFoundLevel';

/**
 * The fields a write's refusal can land on: the dialog has «Заголовок», the
 * form mod-003's six and the fields its type has (feat-022), by their JSON names.
 */
export type RefusalField =
  'title' | 'description' | 'state' | 'assignee' | 'area' | 'parent' | WorkItemFieldName;

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

/**
 * The answers about one value of the body, each with the field it lands on.
 * feat-001 spec § Screens and copy: every `workitems.link.*` refusal is
 * «Родитель»'s error text — the parent's not-found included, never a frame.
 */
const FIELD_OF_CODE: Readonly<Record<string, RefusalField>> = {
  [workItemErrorCodes.stateNotAllowed]: 'state',
  [workItemErrorCodes.assigneeUnavailable]: 'assignee',
  [organizationErrorCodes.areaNotFound]: 'area',
  [workItemErrorCodes.linkParentNotFound]: 'parent',
  [workItemErrorCodes.linkParentLevelNotAllowed]: 'parent',
  [workItemErrorCodes.linkSelfParent]: 'parent',
  [workItemErrorCodes.linkCycle]: 'parent',
  [workItemErrorCodes.linkParentChanged]: 'parent',
  [workItemErrorCodes.linkItemDeleted]: 'parent',
};

/**
 * feat-001 BR-H-4: the parent the user saw is no longer the stored one — the
 * form reads the item again before it shows the refusal.
 */
export function isParentChanged(error: unknown): boolean {
  return error instanceof ApiError && error.errorCode === workItemErrorCodes.linkParentChanged;
}

/**
 * `workitems.validation_failed`'s field names a field of the screen shows
 * (§ 0.5): mod-003's two, and feat-022's by their own names (its § 3.2).
 */
const VALIDATED_FIELDS: Readonly<Record<string, RefusalField>> = {
  title: 'title',
  description: 'description',
  ...Object.fromEntries(WORK_ITEM_FIELD_NAMES.map((name) => [name, name])),
};

/**
 * Places a write's refusal by its `errorCode` alone, never by its text
 * (frontend.md "Named values"). The text is always the server's: the answer's
 * `detail`, or a field reason's own `detail`. A field reason about `requestId`
 * or `page` — a panel defect — a code about a field the screen lacks, and
 * feat-022's `field_not_on_type` — the panel never sends such a field — go to
 * the `Alert`.
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
      const field =
        reasons[0]?.code === workItemErrorCodes.fieldNotOnType ? undefined : VALIDATED_FIELDS[name];
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
