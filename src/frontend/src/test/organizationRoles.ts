import type { AccessCheck } from '../shared/lib/access';
import { isOrganizationScope, roleBundles } from '../shared/lib/roleBundles';

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
  /**
   * Not part of the body (non-enumerable, never serialized): every code the test means the caller to hold,
   * the narrower ones included. The organization answer carries the organization-scope codes only, so these
   * are what the doubles serve as the caller's codes on the objects (`servedObjectCodes`).
   */
  readonly objectCodes?: readonly string[];
}

/** A served body that remembers every code asked for, beside the organization-scope ones it carries. */
function servedBody(role: TestRole, codes: readonly string[]): ServedAccess {
  const body: ServedAccess = { role, permissions: organizationScopeOnly(codes).sort() };
  Object.defineProperty(body, 'objectCodes', { value: [...codes].sort(), enumerable: false });
  return body;
}

/**
 * The caller's codes on the objects the doubles serve (mod-019 api-contract § 5): a project, a team or a
 * department read, and the project rows, carry them as `codes`. A render sets them from the test's role or
 * explicit bundle; `pinned` says a test fixed them itself and the render leaves them alone.
 */
export const servedObjectCodes: { current: readonly string[]; pinned: boolean } = {
  current: roleBundles.organizationRoles.admin,
  pinned: false,
};

/** The role the organization answer of the organizations double serves (the default handler of `GET …/access`). */
export const servedRole: { current: TestRole } = { current: 'admin' };

/** The codes a test's `access` option means the caller to hold on the objects. */
export function objectCodesOf(access: TestRole | ServedAccess): readonly string[] {
  return typeof access === 'string' ? BUNDLES[access] : (access.objectCodes ?? access.permissions);
}

/**
 * What the organization answer carries of a set of codes (api-contract § 5, BR-SA-11, BR-SA-18): the
 * organization-scope codes only. A code decided at a narrower scope (a project's work-item codes, a team's
 * reorder…) is read off the object's own `codes`, so the double never serves one here: a surface that still
 * asks the organization answer for it fails its suite, as it will against the server.
 */
export function organizationScopeOnly(codes: readonly string[]): string[] {
  return codes.filter((code) => isOrganizationScope(code));
}

export function servedFor(role: TestRole): ServedAccess {
  return servedBody(role, BUNDLES[role]);
}

/**
 * The served body of a role from which some codes are left out: a bundle the
 * catalog does not define today but the check must survive (design § 12's
 * backlog bundles, written code by code).
 */
export function withoutCodes(role: TestRole, ...omitted: readonly string[]): ServedAccess {
  return servedBody(
    role,
    BUNDLES[role].filter((code) => !omitted.includes(code)),
  );
}

/** The served body for exactly these codes, whatever role they are labelled with. */
export function servedWith(role: TestRole, codes: readonly string[]): ServedAccess {
  return servedBody(role, codes);
}

/**
 * An `AccessCheck` that answers an organization code from `codes` for any
 * organization, and an instance code from `instanceCodes` (the token's).
 */
export function checkForCodes(
  codes: readonly string[],
  instanceCodes: readonly string[] = [],
): AccessCheck {
  const served = organizationScopeOnly(codes);
  return (code, scope) =>
    scope === 'instance' ? instanceCodes.includes(code) : served.includes(code);
}

/** `checkForCodes` for a role's bundle. */
export function checkFor(role: TestRole, instanceCodes: readonly string[] = []): AccessCheck {
  return checkForCodes(BUNDLES[role], instanceCodes);
}
