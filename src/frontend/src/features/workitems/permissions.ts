// The slice's codes (api-contract § 0.1, BR-M3-27; mod-016 api-contract
// § 0.2). None is in a token. Since mod-019 (ADR-0038, BR-SA-18) they are decided at a project, so the panel
// reads them from the project's served `codes` (`ProjectAccess`, the project rows), never from `GET …/access`,
// which carries the organization-scope codes only; a page asks them through `useAccessCheck()` below a host
// that provides them. A non-member never reaches a page — the gate answers «Организация не найдена».
export const workItemsPermissions = {
  workItemRead: 'workitems.workitem.read',
  workItemWrite: 'workitems.workitem.write',
  /** Setting or clearing a parent (feat-027): a member holds it, a reader does not. */
  linkWrite: 'workitems.link.write',
  /** mod-005 api-contract § 0.1: Delete and Restore — the member and the administrator. */
  workItemDelete: 'workitems.workitem.delete',
  /** mod-005 api-contract § 0.1: Purge — the administrator alone. */
  workItemPurge: 'workitems.workitem.purge',
} as const;

export type WorkItemsPermission = (typeof workItemsPermissions)[keyof typeof workItemsPermissions];
