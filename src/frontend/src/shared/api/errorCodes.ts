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
  // mod-017: the next four are deprecated in the catalog (the server no longer answers them; BR-OT-10, BR-OT-11).
  // A retired code stays taken in the catalog and so here, one to one with it (the drift test); no screen uses them.
  teamDefaultAreaNotOwned: 'organizations.team.default_area_not_owned',
  teamFirstTeamDefaultFixed: 'organizations.team.first_team_default_fixed',
  teamFirstTeamCannotBeDeleted: 'organizations.team.first_team_cannot_be_deleted',
  areaIsTeamDefault: 'organizations.area.is_team_default',
  /**
   * 409 (feat-028 api-contract § 0.3): an active team of the organization that is not linked to the project,
   * named as an owner, a default or an unlink destination.
   */
  teamNotInProject: 'organizations.team.not_in_project',
  /**
   * Deprecated in the catalog by feat-028 (BR-OL-10): a project may have no team, so the server no longer
   * answers it and no screen uses it; it stays taken, one to one with the catalog (the drift test).
   */
  teamLastTeamCannotBeDeleted: 'organizations.team.last_team_cannot_be_deleted',
  /** A field reason of `validationFailed` under `description` (feat-028 api-contract § 0.2): over 2 000 characters. */
  descriptionLength: 'organizations.description_length',
  /** A field reason of `validationFailed` under `destination` (feat-028 api-contract § 2.4): an unlink without one. */
  teamDestinationRequired: 'organizations.team.destination_required',
  areaNameTaken: 'organizations.area.name_taken',
  areaMoveIntoOwnSubtree: 'organizations.area.move_into_own_subtree',
  areaNotEmpty: 'organizations.area.not_empty',
  /** mod-018 api-contract § 6: the tree is at most 10 levels deep (409). */
  areaDepthExceeded: 'organizations.area.depth_exceeded',
  areaRootIsFixed: 'organizations.area.root_is_fixed',
  // mod-016 api-contract § 5: the scoped check, the memberships and the team members.
  // Where each shows is design § 9 (the screens of «Участники» and «Участники команды»,
  // the dialogs and the notices of the existing pages).
  /** 403: the one answer of the scoped check to a member whose role lacks the code, whatever the module of the route. */
  accessForbidden: 'organizations.access.forbidden',
  /** 409. */
  membershipAlreadyMember: 'organizations.membership.already_member',
  /** 409: the change would leave no known active administrator. */
  membershipLastAdmin: 'organizations.membership.last_admin',
  /** 409: the user is unknown to the projection or inactive. */
  membershipUserUnavailable: 'organizations.membership.user_unavailable',
  /** 404 to a member in the path: the list is read again. */
  membershipNotFound: 'organizations.membership.not_found',
  /** A field reason of `validationFailed`. */
  membershipRoleInvalid: 'organizations.membership.role_invalid',
  /** A field reason of `validationFailed`. */
  membershipUserInvalid: 'organizations.membership.user_invalid',
  /** 409. */
  teamMemberExists: 'organizations.team.member_exists',
  /** 409: only an active member of the organization joins a team. */
  teamMemberNotInOrganization: 'organizations.team.member_not_in_organization',
  /** 404 to a team member in the path. */
  teamMemberNotFound: 'organizations.team.member_not_found',
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
  // feat-027 api-contract § 5 (its D-9): the hierarchy's depth bound — a chain of parents holds at most 64 items.
  /**
   * 409 with `params.limit` (the bound, a number): the parent write, the form's save with a `parent` and the create with
   * a `parentId` all answer it after the cycle check, when the new link would make a chain longer. The text is the
   * server's `detail`.
   */
  linkDepthExceeded: 'workitems.link.depth_exceeded',
  // feat-022 api-contract § 6: field reasons of `validationFailed` for the fields per type, keyed by the field's
  // request name. Each is that field's error, except `fieldNotOnType`, the error `Alert` above the band.
  /** A number field below 0 or above 9 999. */
  numberOutOfRange: 'workitems.workitem.number_out_of_range',
  /** A decimal field with more than two digits after the point. */
  numberTooPrecise: 'workitems.workitem.number_too_precise',
  /** «Бизнес-ценность» with a fraction. */
  integerRequired: 'workitems.workitem.integer_required',
  /** A list field with a key the list does not hold. */
  valueNotInList: 'workitems.workitem.value_not_in_list',
  /** A date outside 2000–2099. */
  dateOutOfRange: 'workitems.workitem.date_out_of_range',
  /** On `targetDate`: earlier than the start date. */
  targetBeforeStart: 'workitems.workitem.target_before_start',
  /** A type's text field over 100 000 characters. */
  textTooLong: 'workitems.workitem.text_too_long',
  /** A field the item's type does not have: a panel defect, the error `Alert` above the band. */
  fieldNotOnType: 'workitems.workitem.field_not_on_type',
  // mod-005 api-contract § 4: the recycle bin. Where each shows is its design § 8 — the dialog closes, an
  // error notice says the server's text and the card is read again.
  /** 409: a write on an item in «Удалена» (a save, a second Delete), decided on the stored state. */
  deleted: 'workitems.workitem.deleted',
  /** 409: a Restore or a Purge of an item outside «Удалена». */
  notDeleted: 'workitems.workitem.not_deleted',
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
