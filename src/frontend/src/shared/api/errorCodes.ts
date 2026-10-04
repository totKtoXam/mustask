/**
 * Backend error codes the panel branches on (frontend.md "Named values"): a
 * string constant per code, one to one with the api-contract.md of the
 * feature that uses it. A page branches on these and never on a status alone
 * or on the localized `detail`.
 *
 * `organizations.*` — `contracts/errors/organizations.json`
 * (mod-001-process-seed api-contract § 1.3, D-7). The drift test beside this
 * file checks every constant against that catalog.
 */
export const organizationErrorCodes = {
  /**
   * An organization the caller cannot see: unknown, malformed, without a
   * process, and later deactivated or not the caller's — one answer for all.
   */
  notFound: 'organizations.organization.not_found',
} as const;

export type OrganizationErrorCode =
  (typeof organizationErrorCodes)[keyof typeof organizationErrorCodes];
