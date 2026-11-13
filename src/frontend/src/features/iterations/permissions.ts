// The slice's code (api-contract § 0). It is decided at a team (ResourceScope.Team), so the panel
// reads it from the team's served `codes`, never from the organization's answer (mod-019 BR-SA-18).
export const iterationsPermissions = {
  /** Create, edit, rename, start, remove and close a team's sprints. */
  sprintManage: 'iterations.sprint.manage',
} as const;

export type IterationsPermission =
  (typeof iterationsPermissions)[keyof typeof iterationsPermissions];
