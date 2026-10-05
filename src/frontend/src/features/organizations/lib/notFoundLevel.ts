import { ApiError } from '../../../shared/api/httpClient';
import { organizationErrorCodes } from '../../../shared/api/errorCodes';

/** The levels with a not-found frame (design § 12); an area has none. */
export type OrganizationsNotFoundLevel = 'organization' | 'project' | 'team';

const LEVELS: Readonly<Record<string, OrganizationsNotFoundLevel>> = {
  [organizationErrorCodes.notFound]: 'organization',
  [organizationErrorCodes.projectNotFound]: 'project',
  [organizationErrorCodes.teamNotFound]: 'team',
};

/**
 * The frame a page's first read answers with, from the error code alone
 * (frontend.md "Named values"); `null` for any other failure.
 * `organizations.area.not_found` is `null` too: it lands in a dialog's
 * `Alert` (api-contract § 0.3).
 */
export function notFoundLevel(error: unknown): OrganizationsNotFoundLevel | null {
  if (!(error instanceof ApiError)) {
    return null;
  }
  const code = error.errorCode;
  return code !== undefined ? (LEVELS[code] ?? null) : null;
}
