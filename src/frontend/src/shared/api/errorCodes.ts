/**
 * Backend error codes the panel branches on (frontend.md "Named values"): a
 * string constant per code, one to one with the api-contract.md of the
 * feature that uses it. A page branches on these and never on a status alone
 * or on the localized `detail`.
 *
 * `organizations.*` — `contracts/errors/organizations.json`, which mod-002
 * owns (its api-contract § 6, D-k); mod-001 registered `not_found` first
 * (mod-001-process-seed api-contract § 1.3, D-7). The drift tests beside
 * this file check the constants against that catalog in both directions.
 */
export const organizationErrorCodes = {
  /**
   * An organization the caller cannot see: unknown, malformed, deactivated,
   * and later not the caller's — one answer for all. The name is mod-001's.
   */
  notFound: 'organizations.organization.not_found',
  projectNotFound: 'organizations.project.not_found',
  teamNotFound: 'organizations.team.not_found',
  /** No page frame: a dialog's `Alert` (api-contract § 0.3). */
  areaNotFound: 'organizations.area.not_found',
  /** 400 with `errors.name` / `errors.key`, every field reason at once. */
  validationFailed: 'organizations.validation_failed',
  /** A field reason of `validationFailed`, never an answer's own code. */
  nameLength: 'organizations.name_length',
  createForbidden: 'organizations.organization.create_forbidden',
  createFailed: 'organizations.organization.create_failed',
  organizationNameTaken: 'organizations.organization.name_taken',
  projectNameTaken: 'organizations.project.name_taken',
  projectNameTakenByDeactivated: 'organizations.project.name_taken_by_deactivated',
  /** A field reason of `validationFailed`. */
  projectKeyRequired: 'organizations.project.key_required',
  /** A field reason of `validationFailed`. */
  projectKeyInvalid: 'organizations.project.key_invalid',
  projectKeyTaken: 'organizations.project.key_taken',
  teamNameTaken: 'organizations.team.name_taken',
  teamDefaultAreaNotOwned: 'organizations.team.default_area_not_owned',
  teamFirstTeamDefaultFixed: 'organizations.team.first_team_default_fixed',
  teamFirstTeamCannotBeDeleted: 'organizations.team.first_team_cannot_be_deleted',
  areaNameTaken: 'organizations.area.name_taken',
  areaMoveIntoOwnSubtree: 'organizations.area.move_into_own_subtree',
  areaIsTeamDefault: 'organizations.area.is_team_default',
  areaNotEmpty: 'organizations.area.not_empty',
  areaRootIsFixed: 'organizations.area.root_is_fixed',
} as const;

export type OrganizationErrorCode =
  (typeof organizationErrorCodes)[keyof typeof organizationErrorCodes];
