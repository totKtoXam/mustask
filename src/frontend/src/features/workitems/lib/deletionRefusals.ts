import { ApiError } from '../../../shared/api/httpClient';
import { workItemErrorCodes } from '../../../shared/api/errorCodes';
import { apiErrorText } from '../../../shared/lib/apiErrorText';
import { notFoundLevel, type WorkItemsNotFoundLevel } from './notFoundLevel';

/**
 * What a refused or failed Delete, Restore or Purge means for the dialog that sent it
 * (mod-005 design § 8), by the `errorCode` alone (frontend.md "Named values"):
 *
 * - `gone`: the path's own level is not there — the item (a purge got there first, or it never
 *   was), its project or its organization. The dialog closes and the card gives way to that
 *   level's frame.
 * - `stale`: the item is not in the state the dialog assumed — `…deleted` for a Delete that lost,
 *   `…not_deleted` for a Restore or a Purge that lost. The dialog closes, an error notice says
 *   the server's text and the card reads the item again.
 * - `stays`: anything else — no answer, a server error, a refused code (403). The dialog stays
 *   open with the text above its body, and its primary tries again.
 */
export type DeletionRefusal =
  | { kind: 'gone'; level: WorkItemsNotFoundLevel }
  | { kind: 'stale'; text: string }
  | { kind: 'stays'; text: string };

/** Places a failed write of the recycle bin. */
export function deletionRefusal(error: unknown): DeletionRefusal {
  const level = notFoundLevel(error);
  if (level !== null) {
    return { kind: 'gone', level };
  }
  if (
    error instanceof ApiError &&
    (error.errorCode === workItemErrorCodes.deleted ||
      error.errorCode === workItemErrorCodes.notDeleted)
  ) {
    return { kind: 'stale', text: apiErrorText(error) };
  }
  return { kind: 'stays', text: apiErrorText(error) };
}

/** `workitems.workitem.deleted`: a save (or a card move) of an item that was deleted meanwhile. */
export function isDeletedRefusal(error: unknown): boolean {
  return error instanceof ApiError && error.errorCode === workItemErrorCodes.deleted;
}

/** `workitems.workitem.state_not_allowed`: a Restore's chosen state is none of the type's. */
export function isStateNotAllowed(error: unknown): boolean {
  return error instanceof ApiError && error.errorCode === workItemErrorCodes.stateNotAllowed;
}
