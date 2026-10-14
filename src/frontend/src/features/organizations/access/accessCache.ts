import type { QueryClient } from '@tanstack/react-query';
import { organizationsKeys } from '../api/queryKeys';
import type { AccessAnswer } from './accessAnswer';
import { forgetRole } from './roleMemory';

// The few writes anything makes to the served access of an organization
// (mod-016 plan-frontend § 2.2, "helpers in access/"). The watcher uses them,
// and so do the mutations of pass B (an own role change, a removal).

const NOT_MEMBER: AccessAnswer = { kind: 'not-member' };

/**
 * The tab now holds the 404 «Организация не найдена» for the organization
 * (design § 2.1 row 3): the codes held until then are dropped, so every check
 * answers "no" and the page is the full-page frame at once, before any re-read
 * answers (DC-12).
 */
export function holdNotMember(client: QueryClient, organizationId: string): void {
  client.setQueryData<AccessAnswer>(organizationsKeys.access(organizationId), NOT_MEMBER);
}

/**
 * Reads the organization's access again, in the background: what is held
 * stays until the answer (design § 2.2). With `inFlight: 'join'` a read that
 * is already running is the one that answers — several scoped calls that fail
 * together ask for one request — and by default the running read is replaced
 * by a fresh one, which is what a change the caller has just made needs.
 */
export function rereadAccess(
  client: QueryClient,
  organizationId: string,
  { inFlight = 'restart' }: { inFlight?: 'restart' | 'join' } = {},
): Promise<void> {
  return client.invalidateQueries(
    { queryKey: organizationsKeys.access(organizationId) },
    { cancelRefetch: inFlight === 'restart' },
  );
}

/**
 * The organization's access leaves the tab's memory — the caller has left it
 * (a removal of oneself, a deactivation): the entry and the role last read go,
 * and a later visit reads as a first one.
 */
export function forgetAccess(client: QueryClient, organizationId: string): void {
  client.removeQueries({ queryKey: organizationsKeys.access(organizationId) });
  forgetRole(client, organizationId);
}
