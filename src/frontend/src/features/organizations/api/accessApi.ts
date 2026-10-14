// The caller's access to an organization over HTTP, strictly per
// docs/features/mod-016-organization-access/api-contract.md § 1.1. The panel's
// one read of what the caller may do in an organization (BR-M1-31, ADR-0027):
// derived on the server from the caller's membership at the moment of the
// request, never from the token.
import { organizationErrorCodes } from '../../../shared/api/errorCodes';
import { ApiError, apiFetch } from '../../../shared/api/httpClient';
import type { AccessAnswer } from '../access/accessAnswer';
import type { OrganizationRole } from '../lib/roles';
import { organizationApiPath } from './organizationsApi';

/** § 1.1: the body of `GET …/access` — the role and exactly the role's bundle of codes. */
export interface OrganizationAccess {
  role: OrganizationRole;
  /** Organization codes only, in ordinal order; no instance code (those stay in the token). */
  permissions: string[];
}

/**
 * Reads the caller's access to `organizationId`. The 404 «Организация не
 * найдена» is an answer, not a failure — the same answer, byte for byte,
 * whether the organization is unknown, deactivated or the caller is not in it
 * (§ 0.1 row 2) — so it comes back as `not-member`; every other failure
 * throws and leaves what the tab holds as it was.
 */
export async function getAccess(organizationId: string): Promise<AccessAnswer> {
  try {
    const access = await apiFetch<OrganizationAccess>(
      `${organizationApiPath(organizationId)}/access`,
    );
    return { kind: 'member', role: access.role, permissions: access.permissions };
  } catch (error) {
    if (
      error instanceof ApiError &&
      error.status === 404 &&
      error.errorCode === organizationErrorCodes.notFound
    ) {
      return { kind: 'not-member' };
    }
    throw error;
  }
}
