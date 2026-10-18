import { useDebouncedValue } from './useDebouncedValue';
import {
  useMemberCandidates,
  useNewTeamMemberCandidates,
  useTeamMemberCandidates,
} from './queries';
import { isSearchable, searchTextOf, type UserSearch } from '../lib/userSearch';

/**
 * What a user search looks in: the organization's active users, a team's candidates, or — for
 * a team being created — the organization's active members (feat-028 api-contract § 2.3a).
 */
export type UserSearchScope =
  | { kind: 'organization'; organizationId: string }
  | { kind: 'team'; organizationId: string; teamId: string }
  | { kind: 'newTeam'; organizationId: string };

/**
 * The state of the candidate search for what is typed now (design § 3.5): the
 * add-member search of the organization (api-contract § 1.3), the
 * add-to-team search (§ 2.2) or the creation form's (§ 2.3a), the text trimmed,
 * settled after a pause and asked only from 2 characters on. The three
 * endpoints answer in one shape.
 */
export function useUserSearch(
  scope: UserSearchScope,
  typed: string,
  { enabled = true }: { enabled?: boolean } = {},
): { search: UserSearch; retry(): void } {
  const text = searchTextOf(typed);
  const settled = useDebouncedValue(text);
  const asked = enabled && isSearchable(settled);
  const inOrganization = useMemberCandidates(
    scope.organizationId,
    settled,
    asked && scope.kind === 'organization',
  );
  const inTeam = useTeamMemberCandidates(
    scope.organizationId,
    scope.kind === 'team' ? scope.teamId : '',
    settled,
    asked && scope.kind === 'team',
  );
  const inNewTeam = useNewTeamMemberCandidates(
    scope.organizationId,
    settled,
    asked && scope.kind === 'newTeam',
  );
  const query =
    scope.kind === 'team' ? inTeam : scope.kind === 'newTeam' ? inNewTeam : inOrganization;

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
