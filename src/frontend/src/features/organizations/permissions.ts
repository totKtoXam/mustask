// The codes of the slice (mod-016 api-contract § 0.2, and feat-028 api-contract § 0.1 for the
// four codes of the teams' delivery). The sixteen organization
// codes are the contracts' matrix, named once here so that no page and no
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
  /** A team's name, description and members (feat-028 narrows it: it no longer creates or deletes). */
  teamWrite: 'organizations.team.write',
  /** feat-028 (ADR-0029): create a team. */
  teamCreate: 'organizations.team.create',
  /** feat-028: delete a team, and read the deletion's per-project counts. */
  teamDelete: 'organizations.team.delete',
  /** feat-028: the team's end of a link or an unlink; with `projectLink`, both being needed. */
  teamLink: 'organizations.team.link',
  /** feat-028: the project's end of a link or an unlink. */
  projectLink: 'organizations.project.link',
  areaRead: 'organizations.area.read',
  areaWrite: 'organizations.area.write',
  /** Instance code, in the token. */
  organizationCreate: 'organizations.organization.create',
} as const;

export type OrganizationsPermission =
  (typeof organizationsPermissions)[keyof typeof organizationsPermissions];
