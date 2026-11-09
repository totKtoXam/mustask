import { organizationErrorCodes as codes } from '../../../shared/api/errorCodes';
import { ApiError } from '../../../shared/api/httpClient';
import { apiErrorText } from '../../../shared/lib/apiErrorText';
import { giveDialog } from './grantTexts';
import { notFoundLevel } from './notFoundLevel';

/** Where a refused grant write shows, and what is read again (design § 3, § 7). */
export interface PlacedGrantRefusal {
  /** A refusal about the dialog's user field. */
  field: string | null;
  /** Any other refusal of a dialog: its error notice. */
  alert: string | null;
  /** The list (and the object's codes) are read again. */
  reread: boolean;
  /** The grant is already changed or gone: the dialog closes and the list says so. */
  changed: boolean;
  /** The object itself is gone: the page reads itself again and shows its frame. */
  contextGone: boolean;
}

export function placeGrantRefusal(error: unknown): PlacedGrantRefusal {
  const code = error instanceof ApiError ? error.errorCode : undefined;
  const base: PlacedGrantRefusal = {
    field: null,
    alert: null,
    reread: false,
    changed: false,
    contextGone: false,
  };
  if (code === codes.grantSelfGrant) {
    return { ...base, field: giveDialog.selfGrant };
  }
  if (code === codes.membershipNotFound) {
    return { ...base, field: giveDialog.memberNotFound, reread: true };
  }
  if (code === codes.grantRoleInvalid || code === codes.grantScopeNotAllowed) {
    return { ...base, alert: giveDialog.wrongScope };
  }
  if (code === codes.grantChanged) {
    return { ...base, changed: true, reread: true };
  }
  if (notFoundLevel(error) !== null) {
    return { ...base, alert: apiErrorText(error), contextGone: true, reread: true };
  }
  if (error instanceof ApiError && error.status === 403) {
    return { ...base, alert: giveDialog.forbidden, reread: true };
  }
  return { ...base, alert: apiErrorText(error) };
}
