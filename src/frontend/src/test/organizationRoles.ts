import type { AccessCheck } from '../shared/lib/access';
import { roleBundles } from '../shared/lib/roleBundles';

/**
 * The three organization roles and the codes each bundle holds. Since mod-019 they are read from
 * `contracts/access/role-bundles.json` (api-contract § 9, ADR-0038), which the backend generates from the
 * domain — no longer a hand copy of the contracts' tables (tech-026 OP-7). Every bundle lists every code
 * it holds, so a missing code is a code left out of the list and never one implied (mod-016 design § 12).
 * The names (`ADMIN_CODES`, `checkFor`, `servedFor`…) are the ones the tests already use.
 */
export type TestRole = 'admin' | 'member' | 'viewer';

/** Every code the administrator holds. */
export const ADMIN_CODES: readonly string[] = roleBundles.organizationRoles.admin;

/** The member: the reads, the work-item writes and the delete, none of the administrator's. */
export const MEMBER_CODES: readonly string[] = roleBundles.organizationRoles.member;

/** The reader: the reads. */
export const VIEWER_CODES: readonly string[] = roleBundles.organizationRoles.viewer;

/** The reads all three roles hold. */
export const READ_CODES: readonly string[] = VIEWER_CODES;

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
