import type { GrantScope } from '../api/grantsApi';
import { useDebouncedValue } from './useDebouncedValue';
import {
  useAppointeeCandidates,
  useGrantCandidates,
  useMemberCandidates,
  useNewTeamMemberCandidates,
  useTeamMemberCandidates,
} from './queries';
import { isSearchable, searchTextOf, type UserSearch } from '../lib/userSearch';

/**
 * What a user search looks in: the organization's active users, a team's candidates, for a
 * team being created — the organization's active members (feat-028 api-contract § 2.3a) — or,
 * for the instance administrator's appointment, the active users of the projection with no
 * organization at all (feat-033 api-contract § 1.2, decision D-4).
 */
export type UserSearchScope =
  | { kind: 'organization'; organizationId: string }
  | { kind: 'team'; organizationId: string; teamId: string }
  | { kind: 'newTeam'; organizationId: string }
  | { kind: 'grant'; organizationId: string; grantScope: GrantScope; objectId: string }
  | { kind: 'appointee' };

/**
 * The state of the candidate search for what is typed now (design § 3.5): the
 * add-member search of the organization (api-contract § 1.3), the
 * add-to-team search (§ 2.2), the creation form's (§ 2.3a) or the appointee
 * search (feat-033 § 1.2), the text trimmed, settled after a pause and asked
 * only from 2 characters on. The four endpoints answer in one shape.
 */
export function useUserSearch(
  scope: UserSearchScope,
  typed: string,
  { enabled = true }: { enabled?: boolean } = {},
): { search: UserSearch; retry(): void } {
  const text = searchTextOf(typed);
  const settled = useDebouncedValue(text);
  const asked = enabled && isSearchable(settled);
  const organizationId = scope.kind === 'appointee' ? '' : scope.organizationId;
  const inOrganization = useMemberCandidates(
    organizationId,
    settled,
    asked && scope.kind === 'organization',
  );
  const inTeam = useTeamMemberCandidates(
    organizationId,
    scope.kind === 'team' ? scope.teamId : '',
    settled,
    asked && scope.kind === 'team',
  );
  const inNewTeam = useNewTeamMemberCandidates(
    organizationId,
    settled,
    asked && scope.kind === 'newTeam',
  );
  const inGrant = useGrantCandidates(
    organizationId,
    scope.kind === 'grant' ? scope.grantScope : 'project',
    scope.kind === 'grant' ? scope.objectId : '',
    settled,
    asked && scope.kind === 'grant',
  );
  const inInstance = useAppointeeCandidates(settled, asked && scope.kind === 'appointee');
  const query =
    scope.kind === 'grant'
      ? inGrant
      : scope.kind === 'team'
        ? inTeam
        : scope.kind === 'newTeam'
          ? inNewTeam
          : scope.kind === 'appointee'
            ? inInstance
            : inOrganization;

  let search: UserSearch;
  if (!isSearchable(text)) {
    search = { status: 'tooShort' };
  } else if (settled !== text || query.isPending || (query.isError && query.isFetching)) {
    search = { status: 'searching' };
  } else if (query.isError) {
    search = { status: 'failed' };
  } else {
    search = { status: 'done', result: query.data };
  }
  return {
    search,
    retry: () => {
      if (query.isError) {
        void query.refetch();
      }
    },
  };
}
