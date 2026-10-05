import { ApiError } from '../../../shared/api/httpClient';
import { organizationErrorCodes, workItemErrorCodes } from '../../../shared/api/errorCodes';

/** The levels with a not-found frame on this slice's pages (spec § Not-found answers). */
export type WorkItemsNotFoundLevel = 'organization' | 'project' | 'workItem';

const LEVELS: Readonly<Record<string, WorkItemsNotFoundLevel>> = {
  [organizationErrorCodes.notFound]: 'organization',
  [organizationErrorCodes.projectNotFound]: 'project',
  [workItemErrorCodes.notFound]: 'workItem',
};

/**
 * The frame a path's not-found answers with, from the error code alone
 * (frontend.md "Named values"); `null` for any other failure. The not-found of
 * a body reference — `workitems.workitem.type_not_found`,
 * `organizations.area.not_found` — is `null` too: it never means the page is
 * gone (api-contract § 0.5).
 */
export function notFoundLevel(error: unknown): WorkItemsNotFoundLevel | null {
  if (!(error instanceof ApiError)) {
    return null;
  }
  const code = error.errorCode;
  return code !== undefined ? (LEVELS[code] ?? null) : null;
}
