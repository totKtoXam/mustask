// The slice's codes (api-contract § 0.1, BR-M3-27). Both are
// organization-scoped and in no token yet: the navigation entry declares the
// read, the shell's interim clause satisfies it for any signed-in user
// (mod-015 BR-8), and no page checks either until mod-016 — a non-member is
// then answered by the API's not-found code, not by a refusal here.
export const workItemsPermissions = {
  workItemRead: 'workitems.workitem.read',
  workItemWrite: 'workitems.workitem.write',
} as const;

export type WorkItemsPermission = (typeof workItemsPermissions)[keyof typeof workItemsPermissions];
