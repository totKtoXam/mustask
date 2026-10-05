// The codes of the slice (design § 2.1, api-contract § 0.1, § 8). The four
// reads are organization- and project-scoped and in no token yet: the
// navigation entries declare them, the shell's interim clause satisfies
// them for any signed-in user (mod-015 BR-8, D-18), and mod-016 owns their
// final form. The create right is an instance code in the token (ADR-0018):
// the one check a page of this slice makes.
export const organizationsPermissions = {
  organizationRead: 'organizations.organization.read',
  projectRead: 'organizations.project.read',
  teamRead: 'organizations.team.read',
  areaRead: 'organizations.area.read',
  organizationCreate: 'organizations.organization.create',
} as const;

export type OrganizationsPermission =
  (typeof organizationsPermissions)[keyof typeof organizationsPermissions];
