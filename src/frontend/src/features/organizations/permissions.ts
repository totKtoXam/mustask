// The codes of the slice (mod-016 api-contract § 0.2). The twelve organization
// codes are the contract's matrix, named once here so that no page and no
// navigation entry types a code twice (frontend.md "Named values"): they are
// never in the token — the panel reads them from `GET …/access`, derived from
// the caller's membership on every request (ADR-0027) — and a page asks them
// through `useAccessCheck()`, bound to the organization in its address by the
// organization gate. The create right is the one instance code, in the token
// (ADR-0018): `useAccessCheck()` answers it from there at scope `instance`.
export const organizationsPermissions = {
  organizationRead: 'organizations.organization.read',
  organizationWrite: 'organizations.organization.write',
  organizationDeactivate: 'organizations.organization.deactivate',
  membershipRead: 'organizations.membership.read',
  membershipWrite: 'organizations.membership.write',
  projectRead: 'organizations.project.read',
  projectWrite: 'organizations.project.write',
  projectDeactivate: 'organizations.project.deactivate',
  teamRead: 'organizations.team.read',
  teamWrite: 'organizations.team.write',
  areaRead: 'organizations.area.read',
  areaWrite: 'organizations.area.write',
  /** Instance code, in the token. */
  organizationCreate: 'organizations.organization.create',
} as const;

export type OrganizationsPermission =
  (typeof organizationsPermissions)[keyof typeof organizationsPermissions];
