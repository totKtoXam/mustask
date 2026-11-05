import type { QueryClient } from '@tanstack/react-query';
import type { OrganizationRole } from '../lib/roles';

/**
 * The last role read per organization, per `QueryClient` (mod-016 design
 * § 2.4). A re-read that shows a different role raises the role-change
 * notice; the very first read of an organization never does. The user's own
 * change of role in this tab remembers the new role *before* the re-read, so
 * that action's own notice speaks and this one stays silent.
 */
const memory = new WeakMap<QueryClient, Map<string, OrganizationRole>>();

function rolesOf(client: QueryClient): Map<string, OrganizationRole> {
  let roles = memory.get(client);
  if (!roles) {
    roles = new Map();
    memory.set(client, roles);
  }
  return roles;
}

export function rememberRole(
  client: QueryClient,
  organizationId: string,
  role: OrganizationRole,
): void {
  rolesOf(client).set(organizationId, role);
}

export function lastRole(
  client: QueryClient,
  organizationId: string,
): OrganizationRole | undefined {
  return rolesOf(client).get(organizationId);
}

/** The organization's access entry is gone from the cache: so is what was last read. */
export function forgetRole(client: QueryClient, organizationId: string): void {
  rolesOf(client).delete(organizationId);
}
