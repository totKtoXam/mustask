// The slice's codes (api-contract § 0.1, BR-M3-27; mod-016 api-contract
// § 0.2). All are organization-scoped and in no token: the panel reads them
// from `GET …/access` and a page asks them through `useAccessCheck()`, bound
// to the organization in its address by the organization gate (ADR-0027). The
// navigation entry declares the read; the pages ask the write codes, and a
// non-member never reaches a page — the gate answers «Организация не найдена».
export const workItemsPermissions = {
  workItemRead: 'workitems.workitem.read',
  workItemWrite: 'workitems.workitem.write',
  /** Setting or clearing a parent (feat-027): a member holds it, a reader does not. */
  linkWrite: 'workitems.link.write',
} as const;

export type WorkItemsPermission = (typeof workItemsPermissions)[keyof typeof workItemsPermissions];
