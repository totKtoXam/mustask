import { useQuery } from '@tanstack/react-query';
import { getAccess } from '../api/accessApi';
import { organizationsKeys } from '../api/queryKeys';
import type { AccessAnswer } from './accessAnswer';

export interface OrganizationAccessOptions {
  /** `false`: nothing is read (a placeholder context, no organization). */
  enabled?: boolean;
}

/**
 * The caller's served access to one organization (mod-016 design § 2.1,
 * plan-frontend P-2). The query *answers* instead of throwing on a 404: its
 * data is an `AccessAnswer` — the codes, or `not-member` — and only a read
 * that failed for another reason is an `error`. `data` is the last answer; a
 * failed re-read leaves it as it was (design § 2.2).
 *
 * It re-reads on every mount of an observer (the entry is stale at once, so a
 * switch to an organization, or a move to a page of it, reads in the
 * background over what is held) and on window focus — the client's default is
 * no refocus read, the switcher's lists and this opt in alike. The shell reads
 * the switcher's selection through it and the organization gate the
 * address's; when both name one organization they share the entry.
 *
 * The entry lives as long as the tab (`gcTime: Infinity`): the codes read
 * earlier show from the first frame of a switch back, and the role read last
 * is still there to compare with the re-read's (design § 6.1, § 2.4). Left
 * to the cache's default, an organization nothing observes for five minutes
 * would be dropped with its remembered role — the watcher forgets a role when
 * its entry is removed. The entry leaves the tab's memory only through
 * `forgetAccess`, when the caller has left the organization.
 */
export function useOrganizationAccess(
  organizationId: string | undefined,
  { enabled = true }: OrganizationAccessOptions = {},
) {
  return useQuery<AccessAnswer>({
    queryKey: organizationsKeys.access(organizationId ?? ''),
    queryFn: () => getAccess(organizationId ?? ''),
    enabled: enabled && organizationId !== undefined && organizationId !== '',
    refetchOnWindowFocus: true,
    gcTime: Infinity,
  });
}
