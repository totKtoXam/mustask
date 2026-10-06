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

/**
 * `workitems.*` — `contracts/errors/workitems.json`, which mod-003 owns (its
 * api-contract § 4). The drift test beside this file checks the constants
 * against that catalog in both directions. Where the panel shows each one is
 * the contract's § 0.5: a path not-found is a frame, a body reference's
 * not-found or a 409 is a field's error, a field reason is that field's.
 */
export const workItemErrorCodes = {
  /** The identifier in the address names nothing in the addressed project: the frame. */
  notFound: 'workitems.workitem.not_found',
  /** 400 with `errors.page` / `requestId` / `title` / `description`, every reason at once. */
  validationFailed: 'workitems.validation_failed',
  /** A field reason of `validationFailed`. */
  titleRequired: 'workitems.workitem.title_required',
  /** A field reason of `validationFailed`. */
  titleTooLong: 'workitems.workitem.title_too_long',
  /** A field reason of `validationFailed`. */
  titleMultiline: 'workitems.workitem.title_multiline',
  /** A field reason of `validationFailed`. */
  descriptionTooLong: 'workitems.workitem.description_too_long',
  /** A field reason of `validationFailed`: a panel defect, shown as any other refusal. */
  requestIdInvalid: 'workitems.workitem.request_id_invalid',
  /** A field reason of `validationFailed`: a panel defect, shown as any other refusal. */
  invalidPage: 'workitems.invalid_page',
  /** 404 to the create dialog's `typeId`: its `Alert`, never a frame. */
  typeNotFound: 'workitems.workitem.type_not_found',
  /** 409: the error under «Статус». */
  stateNotAllowed: 'workitems.workitem.state_not_allowed',
  /** 409: the error under «Исполнитель». */
  assigneeUnavailable: 'workitems.workitem.assignee_unavailable',
  // feat-001 api-contract § 5: the Parent/Child link. Where each shows is its spec § Screens and copy — the error under
  // «Родитель» on the form and in the dialog, the notice on «Убрать родителя».
  /** 404 to the body's `parentId`: never a frame. */
  linkParentNotFound: 'workitems.link.parent_not_found',
  /** 409. */
  linkParentLevelNotAllowed: 'workitems.link.parent_level_not_allowed',
  /** 409. */
  linkSelfParent: 'workitems.link.self_parent',
  /** 409 with `params.parent`, `params.item`, `params.path`. */
  linkCycle: 'workitems.link.cycle',
  /** 409: the view is read again first. */
  linkParentChanged: 'workitems.link.parent_changed',
  /** 409. */
  linkItemDeleted: 'workitems.link.item_deleted',
} as const;

export type WorkItemErrorCode = (typeof workItemErrorCodes)[keyof typeof workItemErrorCodes];

/**
 * `boards.*` — `contracts/errors/boards.json`, which mod-006 owns (its
 * api-contract § 4). The drift test beside this file checks the constants
 * against that catalog in both directions. Only `reorderStale` is a user's
 * case (spec § Notices); the other three answer a panel defect and are shown
 * as any other refusal: the level's not-found as the page's `ErrorBanner`,
 * the two 400s as the reorder's notice with the server's text.
 */
export const boardErrorCodes = {
  /** 409: the moved or the named item has left the backlog; the backlog is read again first. */
  reorderStale: 'boards.backlog.reorder_stale',
  /** 404: `{level}` is not one of the three, or the process has no such level. */
  levelNotFound: 'boards.level.not_found',
  /** 400 with `errors.position`: a panel defect. */
  validationFailed: 'boards.validation_failed',
  /** A field reason of `validationFailed`. */
  positionInvalid: 'boards.backlog.position_invalid',
} as const;

export type BoardErrorCode = (typeof boardErrorCodes)[keyof typeof boardErrorCodes];
