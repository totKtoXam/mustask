import { ApiError } from '../../../shared/api/httpClient';
import { organizationErrorCodes } from '../../../shared/api/errorCodes';

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
