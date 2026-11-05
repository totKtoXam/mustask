import { useCallback, useMemo, useSyncExternalStore } from 'react';
import { useQueryClient, type QueryClient } from '@tanstack/react-query';
import type { AccessCheck } from '../../../shared/lib/access';
import { organizationsKeys } from '../api/queryKeys';
import type { AccessAnswer } from './accessAnswer';

/** What the tab holds for an organization's access, straight from the cache. */
export function readServed(client: QueryClient, organizationId: string): AccessAnswer | undefined {
  return client.getQueryData<AccessAnswer>(organizationsKeys.access(organizationId));
}

/** The organization an access entry is for; `null` for any other key. */
export function accessOrganizationOf(queryKey: readonly unknown[]): string | null {
  const [root, entity, organizationId] = queryKey;
  return root === organizationsKeys.accesses[0] &&
    entity === organizationsKeys.accesses[1] &&
    typeof organizationId === 'string'
    ? organizationId
    : null;
}

/**
 * Everything the tab holds of served access, as one string: it changes
 * exactly when a served answer changes — an organization's codes or role, a
 * 404 held, an entry gone — and not when a re-read answers the same thing
 * (plan-frontend P-5). `useSyncExternalStore` compares it by value, so the
 * shell's check changes identity, and its consumers re-render, only then.
 */
function servedSignature(client: QueryClient): string {
  return client
    .getQueryCache()
    .findAll({ queryKey: organizationsKeys.accesses })
    .flatMap((query) => {
      const organizationId = accessOrganizationOf(query.queryKey);
      const answer = query.state.data as AccessAnswer | undefined;
      if (organizationId === null || answer === undefined) {
        return [];
      }
      return [
        answer.kind === 'member'
          ? `${organizationId}=${answer.role}:${answer.permissions.join(',')}`
          : `${organizationId}=none`,
      ];
    })
    .sort()
    .join('|');
}

function subscribeTo(client: QueryClient) {
  return (onChange: () => void) =>
    client.getQueryCache().subscribe((event) => {
      if (
        (event.type === 'updated' || event.type === 'removed' || event.type === 'added') &&
        accessOrganizationOf(event.query.queryKey) !== null
      ) {
        onChange();
      }
    });
}

export interface ServedAccess {
  /**
   * The shell's one check, unbound (design § 2.6): an instance code from the
   * token, an organization code from the served codes of the organization the
   * caller names — "no" for none named, unread, failed or answered 404. It
   * reads the cache, not React state, and changes identity exactly when a
   * served answer changes.
   */
  access: AccessCheck;
  /**
   * Whether an answer is held for the organization — the codes, or the 404.
   * The switch resolver does not wait for an unread organization (design
   * § 2.5).
   */
  isCodesRead(organizationId: string | undefined): boolean;
}

/**
 * The check the shell provides and the resolver asks (mod-016 design § 2.6,
 * plan-frontend P-5). `can` is the token's: `useAuth().can`.
 */
export function useServedAccessCheck(can: (code: string) => boolean): ServedAccess {
  const client = useQueryClient();
  const signature = useSyncExternalStore(
    useMemo(() => subscribeTo(client), [client]),
    () => servedSignature(client),
    () => '',
  );

  const access = useMemo<AccessCheck>(
    () => (code, scope, organizationId) => {
      if (scope === 'instance') {
        return can(code);
      }
      if (organizationId === undefined || organizationId === '') {
        return false;
      }
      const answer = readServed(client, organizationId);
      return answer?.kind === 'member' && answer.permissions.includes(code);
    },
    // `signature` gives the check a new identity when a served answer
    // changes, so the pages and the navigation that hold it ask again.
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [can, client, signature],
  );
  const isCodesRead = useCallback(
    (organizationId: string | undefined) =>
      organizationId !== undefined && readServed(client, organizationId) !== undefined,
    [client],
  );
  return useMemo(() => ({ access, isCodesRead }), [access, isCodesRead]);
}
