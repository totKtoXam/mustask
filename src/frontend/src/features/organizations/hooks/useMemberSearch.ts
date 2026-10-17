import { isSearchable, searchTextOf, type UserSearch } from '../lib/userSearch';
import { useMembers } from './queries';

/** At most this many users are offered at once, as the server's searches do (mod-016 § 1.3). */
const MAX_OFFERED = 20;

/**
 * The search of the creation form's «Участники» (feat-028 design § 5.1): among the **active
 * members of the organization**, read whole from `GET …/members` (every role holds its read code)
 * and cut here, because the contract's candidate searches are the organization's directory and a
 * team's own address, and a team being created has neither. Same states, texts and cut-off as
 * `useUserSearch`: two trimmed characters, at most 20, «Показаны первые 20. Уточните запрос.».
 */
export function useMemberSearch(
  organizationId: string,
  typed: string,
): { search: UserSearch; retry(): void } {
  const members = useMembers(organizationId);
  const text = searchTextOf(typed);

  let search: UserSearch;
  if (!isSearchable(text)) {
    search = { status: 'tooShort' };
  } else if (members.isPending || (members.isError && members.isFetching)) {
    search = { status: 'searching' };
  } else if (members.isError) {
    search = { status: 'failed' };
  } else {
    const needle = text.toLowerCase();
    const matched = members.data.filter(
      ({ user }) =>
        user.state === 'active' &&
        ((user.displayName ?? '').toLowerCase().includes(needle) ||
          (user.email ?? '').toLowerCase().includes(needle)),
    );
    search = {
      status: 'done',
      result: {
        users: matched.slice(0, MAX_OFFERED).map(({ user }) => ({
          id: user.id,
          displayName: user.displayName ?? '',
          email: user.email ?? '',
        })),
        moreMatched: matched.length > MAX_OFFERED,
      },
    };
  }
  return {
    search,
    retry: () => {
      if (members.isError) {
        void members.refetch();
      }
    },
  };
}
