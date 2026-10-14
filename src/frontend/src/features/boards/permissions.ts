// The slice's codes (api-contract § 0.1, BR-M4-27; mod-016 api-contract
// § 0.2). All are organization-scoped and in no token: the panel reads them
// from `GET …/access` and a page asks them through `useAccessCheck()`, bound
// to the organization in its address by the organization gate (ADR-0027). The
// two navigation entries declare the read; the reorder's code is held by an
// administrator and a member, not by a reader.
export const boardsPermissions = {
  /** Both reads and both entries: mod-003's read code. */
  workItemRead: 'workitems.workitem.read',
  /** The reorder (§ 3.2); a card move and a creation are mod-003's write. */
  backlogReorder: 'boards.backlog.reorder',
} as const;

export type BoardsPermission = (typeof boardsPermissions)[keyof typeof boardsPermissions];
