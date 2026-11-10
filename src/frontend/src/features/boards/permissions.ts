// The slice's codes (api-contract § 0.1, BR-M4-27; mod-016 api-contract
// § 0.2). None is in a token. Since mod-019 (ADR-0038, BR-SA-18) none is organization-scoped either: the
// work-item read and the reorders are decided at a project or a team, so the panel reads them from the
// object's served `codes` (the project rows, the team's read), never from `GET …/access`, which carries the
// organization-scope codes only. The navigation entries ask the organization read every member holds. The
// reorder's code is held by an administrator and a member, not by a reader.
export const boardsPermissions = {
  /** Both reads and both entries: mod-003's read code. */
  workItemRead: 'workitems.workitem.read',
  /** The reorder (§ 3.2); a card move and a creation are mod-003's write. */
  backlogReorder: 'boards.backlog.reorder',
  /** The project's own backlog (mod-019, ADR-0038): decided on the project, read from the project's `codes`. */
  projectBacklogReorder: 'boards.projectbacklog.reorder',
} as const;

export type BoardsPermission = (typeof boardsPermissions)[keyof typeof boardsPermissions];
