// The slice's codes (api-contract § 0.1, BR-M4-27). All are
// organization-scoped and in no token yet: the two navigation entries declare
// the read, the shell's interim clause satisfies it for any signed-in user
// (mod-015 BR-8), and no page checks a code until mod-016 — the reorder's code
// is registered by the backend and only named here.
export const boardsPermissions = {
  /** Both reads and both entries: mod-003's read code. */
  workItemRead: 'workitems.workitem.read',
  /** The reorder (§ 3.2); a card move and a creation are mod-003's write. */
  backlogReorder: 'boards.backlog.reorder',
} as const;

export type BoardsPermission = (typeof boardsPermissions)[keyof typeof boardsPermissions];
