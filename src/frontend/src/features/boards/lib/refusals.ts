import { ApiError } from '../../../shared/api/httpClient';
import { organizationErrorCodes, workItemErrorCodes } from '../../../shared/api/errorCodes';

/** The levels of the address with a not-found frame on both pages (spec § Not-found answers). */
export type BoardsNotFoundLevel = 'organization' | 'project' | 'team';

const LEVELS: Readonly<Record<string, BoardsNotFoundLevel>> = {
  [organizationErrorCodes.notFound]: 'organization',
  [organizationErrorCodes.projectNotFound]: 'project',
  [organizationErrorCodes.teamNotFound]: 'team',
};

/**
 * The frame a read's not-found answers with, from the error code alone
 * (frontend.md "Named values"); `null` for any other failure.
 * `boards.level.not_found` is `null` too: an unknown level never leaves the
 * router, so it answers a panel defect and shows as the page's `ErrorBanner`
 * (api-contract § 4).
 */
export function notFoundLevel(error: unknown): BoardsNotFoundLevel | null {
  if (!(error instanceof ApiError)) {
    return null;
  }
  const code = error.errorCode;
  return code !== undefined ? (LEVELS[code] ?? null) : null;
}

/**
 * The scoped check refused the write (`organizations.access.forbidden`, 403;
 * mod-016 design § 9): the user's role no longer holds the code. By the
 * `errorCode` alone. The page shows the refusal's notice at once, before the
 * read that follows its write: the shell reads the served codes again, and a
 * role that changed replaces that notice with its own (design § 2.4) — a
 * refusal shown after the role-change notice would be the newer and close it.
 */
export function isAccessRefusal(error: unknown): boolean {
  return error instanceof ApiError && error.errorCode === organizationErrorCodes.accessForbidden;
}

/** spec § Notices: no answer, or an answer without a code. */
export const MOVE_FAILED_TEXT = 'Не удалось сохранить перемещение. Попробуйте ещё раз.';

/**
 * The notice a refused reorder or card move shows (spec § Notices), or `null`
 * for none: a not-found of the address shows no notice — the page, read
 * again, is replaced by its frame. By the `errorCode` alone, never by the
 * text: `boards.backlog.reorder_stale` and any other code show the server's
 * `detail` (the stale code's is the catalog's text the spec quotes); no
 * answer, or one without a code, `MOVE_FAILED_TEXT`. Every write is followed
 * by a read (`useReadTeamAgain`), which has answered before this is shown —
 * so on the stale refusal the backlog is read again first.
 */
export function moveRefusalNotice(error: unknown): string | null {
  if (notFoundLevel(error) !== null) {
    return null;
  }
  if (!(error instanceof ApiError) || error.errorCode === undefined) {
    return MOVE_FAILED_TEXT;
  }
  return error.body?.detail || MOVE_FAILED_TEXT;
}

/** feat-032 spec § Screens and copy: no answer, or an answer without a code. */
export const STATE_FAILED_TEXT = 'Не удалось сохранить статус. Попробуйте ещё раз.';

/**
 * The notice a refused tick or untick of a checklist row shows (feat-032 spec
 * § Screens and copy), or `null` for none: a not-found of the address shows no
 * notice — the page, read again, is replaced by its frame. The server's `detail`
 * for an answer with a code, by the `errorCode` alone; `STATE_FAILED_TEXT` for
 * no answer or one without a code.
 */
export function stateRefusalNotice(error: unknown): string | null {
  if (notFoundLevel(error) !== null) {
    return null;
  }
  if (!(error instanceof ApiError) || error.errorCode === undefined) {
    return STATE_FAILED_TEXT;
  }
  return error.body?.detail || STATE_FAILED_TEXT;
}

/** feat-001 spec § Notices on «Бэклоги»: no answer, or an answer without a code. */
export const PARENT_SAVE_FAILED_TEXT = 'Не удалось сохранить родителя. Попробуйте ещё раз.';

/**
 * feat-001 api-contract § 5: the refusals about the link itself, and feat-027
 * D-9's — a chain of parents holds at most 64 items — which a drop, the dialog
 * and the inline add row can all meet.
 */
const LINK_CODES: ReadonlySet<string> = new Set([
  workItemErrorCodes.linkParentNotFound,
  workItemErrorCodes.linkParentLevelNotAllowed,
  workItemErrorCodes.linkSelfParent,
  workItemErrorCodes.linkCycle,
  workItemErrorCodes.linkParentChanged,
  workItemErrorCodes.linkItemDeleted,
  workItemErrorCodes.linkDepthExceeded,
]);

/** Where a refused parent write shows (feat-001 spec § Page «Бэклоги», § Notices on «Бэклоги»). */
export interface ParentRefusal {
  /** A `workitems.link.*` refusal: the dialog's «Родитель» error, its text the server's. */
  field: string | null;
  /** The dialog's `Alert` for any other refusal, the notice of «Убрать родителя» for any; `null` for none. */
  notice: string | null;
  /** `workitems.link.parent_changed`: the backlog is read again and the dialog shows its parent. */
  parentChanged: boolean;
}

/**
 * Places a refused parent write by its `errorCode` alone (frontend.md "Named
 * values"): a `workitems.link.*` code is the field's error in the dialog and
 * the notice of «Убрать родителя», with the server's `detail` (the cycle's
 * rendered with its path); a not-found of the address shows nothing — the
 * page, read again, is replaced by its frame; any other code its `detail`;
 * no answer, or one without a code, `PARENT_SAVE_FAILED_TEXT`.
 */
export function parentRefusal(error: unknown): ParentRefusal {
  const parentChanged =
    error instanceof ApiError && error.errorCode === workItemErrorCodes.linkParentChanged;
  if (notFoundLevel(error) !== null) {
    return { field: null, notice: null, parentChanged };
  }
  if (!(error instanceof ApiError) || error.errorCode === undefined) {
    return { field: null, notice: PARENT_SAVE_FAILED_TEXT, parentChanged };
  }
  const text = error.body?.detail || PARENT_SAVE_FAILED_TEXT;
  return LINK_CODES.has(error.errorCode)
    ? { field: text, notice: text, parentChanged }
    : { field: null, notice: text, parentChanged };
}
