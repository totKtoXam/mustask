import { createContext, useContext } from 'react';

/**
 * The level a permission code is evaluated at (BR-8; doc-006 constraint 1):
 * instance codes travel in the access token (ADR-0018); organization codes
 * are served by `GET /api/organizations/{id}/access` from the caller's
 * membership (mod-016, ADR-0027). `project` and `team` only say how much of
 * the work context an entry needs — all three answer from the organization's
 * served codes (mod-016 design § 2.6).
 */
export type PermissionScope = 'instance' | 'organization' | 'project' | 'team';

/**
 * Whether the signed-in user holds `code` at `scope`. A check is always a
 * question about one organization (mod-016 design § 2.6): `organizationId`
 * names it. An instance code ignores it; an organization code asked about no
 * organization, or about one whose codes are not read, is "no" (it fails
 * closed).
 */
export type AccessCheck = (
  code: string,
  scope: PermissionScope,
  organizationId?: string,
) => boolean;

const denyEverything: AccessCheck = () => false;

/**
 * The check bound to one organization: the organization a call does not
 * name is `organizationId`. The gate of an organization's pages binds the
 * address's organization, so a page asks `access(code, scope)` and never
 * passes it (design § 2.6).
 */
export function bindAccessCheck(check: AccessCheck, organizationId: string): AccessCheck {
  return (code, scope, organization = organizationId) => check(code, scope, organization);
}

/**
 * The one permission check navigation and pages share (BR-8): neither keeps
 * a copy of its answers, so when the source changes — a token renewal, a
 * context switch, a refresh of served codes — both re-render from the same
 * function. The shell provides it unbound; the gate of an organization's
 * pages provides it bound to the address's organization; outside the shell it
 * fails closed.
 */
export const AccessCheckContext = createContext<AccessCheck>(denyEverything);

export function useAccessCheck(): AccessCheck {
  return useContext(AccessCheckContext);
}
