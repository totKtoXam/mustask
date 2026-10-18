import type { AccessCheck } from '../shared/lib/access';

/**
 * The three organization roles and the codes each bundle holds, hand-copied
 * from mod-016 api-contract § 0.2 — the one duplication of the contract in
 * the panel (plan-frontend § 2.8, OP-7); mod-005 added two work item codes to
 * them, from its own api-contract § 0.1. Every bundle lists every code it
 * holds, so a missing code is a code left out of the list and never one
 * implied (design § 12). E2E 27.9 reads the served bundles against this
 * file; it is the only check that they agree.
 */
export type TestRole = 'admin' | 'member' | 'viewer';

/** The reads all three roles hold (§ 0.2). */
export const READ_CODES = [
  'organizations.organization.read',
  'organizations.membership.read',
  'organizations.project.read',
  'organizations.team.read',
  'organizations.area.read',
  'process.definition.read',
  'workitems.workitem.read',
] as const;

/** Held by the administrator alone. */
const ADMIN_ONLY_CODES = [
  'organizations.organization.write',
  'organizations.organization.deactivate',
  'organizations.membership.write',
  'organizations.project.write',
  'organizations.project.deactivate',
  'organizations.team.write',
  'organizations.area.write',
] as const;

/** Held by the administrator and the member. */
const WORK_WRITE_CODES = [
  'workitems.workitem.write',
  'workitems.link.write',
  'boards.backlog.reorder',
] as const;

/** mod-005 api-contract § 0.1: Delete and Restore — the administrator and the member. */
const WORK_DELETE_CODES = ['workitems.workitem.delete'] as const;

/** mod-005 api-contract § 0.1: Purge — the administrator alone. */
const WORK_PURGE_CODES = ['workitems.workitem.purge'] as const;

/** Every code the administrator holds: the seventeen of § 0.2 and mod-005's two. */
export const ADMIN_CODES: readonly string[] = [
  ...READ_CODES,
  ...ADMIN_ONLY_CODES,
  ...WORK_WRITE_CODES,
  ...WORK_DELETE_CODES,
  ...WORK_PURGE_CODES,
];

/** The member: the reads, the work-item writes and the delete, none of the administrator's. */
export const MEMBER_CODES: readonly string[] = [
  ...READ_CODES,
  ...WORK_WRITE_CODES,
  ...WORK_DELETE_CODES,
];

/** The reader: the reads. */
export const VIEWER_CODES: readonly string[] = [...READ_CODES];

const BUNDLES: Readonly<Record<TestRole, readonly string[]>> = {
  admin: ADMIN_CODES,
  member: MEMBER_CODES,
  viewer: VIEWER_CODES,
};

/** The codes a role holds. */
export function codesOf(role: TestRole): readonly string[] {
  return BUNDLES[role];
}

/** The body of `GET /api/organizations/{id}/access` (api-contract § 1.1): codes in ordinal order. */
export interface ServedAccess {
  role: TestRole;
  permissions: string[];
}

export function servedFor(role: TestRole): ServedAccess {
  return { role, permissions: [...BUNDLES[role]].sort() };
}

/**
 * The served body of a role from which some codes are left out: a bundle the
 * catalog does not define today but the check must survive (design § 12's
 * backlog bundles, written code by code).
 */
export function withoutCodes(role: TestRole, ...omitted: readonly string[]): ServedAccess {
  return {
    role,
    permissions: [...BUNDLES[role]].filter((code) => !omitted.includes(code)).sort(),
  };
}

/** The served body for exactly these codes, whatever role they are labelled with. */
export function servedWith(role: TestRole, codes: readonly string[]): ServedAccess {
  return { role, permissions: [...codes].sort() };
}

/**
 * An `AccessCheck` that answers an organization code from `codes` for any
 * organization, and an instance code from `instanceCodes` (the token's).
 */
export function checkForCodes(
  codes: readonly string[],
  instanceCodes: readonly string[] = [],
): AccessCheck {
  return (code, scope) =>
    scope === 'instance' ? instanceCodes.includes(code) : codes.includes(code);
}

/** `checkForCodes` for a role's bundle. */
export function checkFor(role: TestRole, instanceCodes: readonly string[] = []): AccessCheck {
  return checkForCodes(BUNDLES[role], instanceCodes);
}
