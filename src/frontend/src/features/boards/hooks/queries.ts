import { useCallback } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import {
  getBacklog,
  getBoard,
  getProjectBacklog,
  getProjectBoard,
  type Backlog,
  type Board,
  type ProjectBacklog,
  type ProjectBoard,
} from '../api/boardsApi';
import { backlogKey, boardKey, boardsKeys } from '../api/queryKeys';
import type { BoardLevelCode } from '../lib/levels';
import { scopeProjectId, scopeTeamId, type BoardScope } from '../lib/scope';

// One query hook per read of api-contract § 3, and the read-again of a scope's
// surfaces after a write. A scope is a team's (mod-006; since feat-040 over every project
// it works on, at the team's address) or the whole project's (feat-039): the same page
// over another route.

/**
 * No live push (BR-M4-28): a page shows its last read, read again when it
 * opens, after each write of its user, and when the tab regains focus — the
 * last one against the client's default, as the switcher's lists do.
 */
const SURFACE_QUERY_OPTIONS = { refetchOnWindowFocus: true } as const;

/**
 * Both scopes' views keep the rows they show while another filter of the same level is read
 * (mod-017 design § 5.2, § 6.3; feat-040 design § 4.2): the key of a read is
 * `[boards, entity, organization, owner, level, filter]`. A level change shows no stand-in: it is
 * a read of another page.
 */
function keepWhileFiltering<T>(level: BoardLevelCode) {
  return (
    previous: T | undefined,
    previousQuery: { queryKey: readonly unknown[] } | undefined,
  ): T | undefined => (previousQuery?.queryKey[4] === level ? previous : undefined);
}

/**
 * § 3.1: the backlog of one level, a team's (over its projects, narrowed by the project filter
 * `filterIds`; feat-040 § 3.1) or the project's (feat-039 § 3.1, narrowed by the team filter,
 * mod-017 § 3); its first answer validates the whole address. Both keep the previous rows on
 * screen while another filter is read.
 */
export function useBacklog(
  organizationId: string,
  scope: BoardScope,
  level: BoardLevelCode,
  filterIds: readonly string[] = [],
) {
  return useQuery<Backlog | ProjectBacklog>({
    queryKey: backlogKey(organizationId, scope, level, filterIds),
    queryFn: () =>
      scope.kind === 'team'
        ? getBacklog(organizationId, scope.teamId, level, filterIds)
        : getProjectBacklog(organizationId, scope.projectId, level, filterIds),
    placeholderData: keepWhileFiltering<Backlog | ProjectBacklog>(level),
    ...SURFACE_QUERY_OPTIONS,
  });
}

/**
 * § 3.3: the board of one level, a team's or the project's (feat-039 § 3.3); its
 * first answer validates the whole address. Narrowed by the scope's filter.
 */
export function useBoard(
  organizationId: string,
  scope: BoardScope,
  level: BoardLevelCode,
  filterIds: readonly string[] = [],
) {
  return useQuery<Board | ProjectBoard>({
    queryKey: boardKey(organizationId, scope, level, filterIds),
    queryFn: () =>
      scope.kind === 'team'
        ? getBoard(organizationId, scope.teamId, level, filterIds)
        : getProjectBoard(organizationId, scope.projectId, level, filterIds),
    placeholderData: keepWhileFiltering<Board | ProjectBoard>(level),
    ...SURFACE_QUERY_OPTIONS,
  });
}

/**
 * Reads every backlog and board of the scope again — the page on screen at
 * once, the others when next shown — and resolves once the page's own read
 * has answered. Never rejects: a failed read is the page's error state. A
 * project page reads the project's two views; a write there changes no read a
 * team has on screen, and a team's views are read when they open (BR-M4-28).
 */
export function useReadAgain(organizationId: string, scope: BoardScope) {
  const client = useQueryClient();
  const teamId = scopeTeamId(scope);
  const projectId = scopeProjectId(scope);
  return useCallback(async () => {
    const [backlogs, boards] =
      teamId !== null
        ? [boardsKeys.backlogs(organizationId, teamId), boardsKeys.boards(organizationId, teamId)]
        : [
            boardsKeys.projectBacklogs(organizationId, projectId ?? ''),
            boardsKeys.projectBoards(organizationId, projectId ?? ''),
          ];
    await Promise.all([
      client.invalidateQueries({ queryKey: backlogs }),
      client.invalidateQueries({ queryKey: boards }),
    ]);
  }, [client, organizationId, teamId, projectId]);
}
