import { ApiError } from '../../../shared/api/httpClient';
import { organizationErrorCodes } from '../../../shared/api/errorCodes';

/** The levels with a not-found frame (mod-018 design § 4: an area's page has one). */
export type OrganizationsNotFoundLevel = 'organization' | 'project' | 'team' | 'area';

const LEVELS: Readonly<Record<string, OrganizationsNotFoundLevel>> = {
  [organizationErrorCodes.notFound]: 'organization',
  [organizationErrorCodes.projectNotFound]: 'project',
  [organizationErrorCodes.teamNotFound]: 'team',
  [organizationErrorCodes.areaNotFound]: 'area',
};

/**
 * The frame a page's first read answers with, from the error code alone
 * (frontend.md "Named values"); `null` for any other failure.
 * In a dialog the same codes stay the alert or the field (the dialog decides).
 */
export function notFoundLevel(error: unknown): OrganizationsNotFoundLevel | null {
  if (!(error instanceof ApiError)) {
    return null;
  }
  const code = error.errorCode;
  return code !== undefined ? (LEVELS[code] ?? null) : null;
}
