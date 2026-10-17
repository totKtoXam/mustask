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
import { scopeTeamId, type BoardScope } from '../lib/scope';

// One query hook per read of api-contract § 3, and the read-again of a scope's
// surfaces after a write. A scope is a team's (mod-006) or the whole project's
// (feat-039): the same page over another route.

/**
 * No live push (BR-M4-28): a page shows its last read, read again when it
 * opens, after each write of its user, and when the tab regains focus — the
 * last one against the client's default, as the switcher's lists do.
 */
const SURFACE_QUERY_OPTIONS = { refetchOnWindowFocus: true } as const;

/**
 * § 3.1: the backlog of one level, a team's or the project's (feat-039 § 3.1); its
 * first answer validates the whole address.
 */
export function useBacklog(
  organizationId: string,
  projectId: string,
  scope: BoardScope,
  level: BoardLevelCode,
) {
  return useQuery<Backlog | ProjectBacklog>({
    queryKey: backlogKey(organizationId, projectId, scope, level),
    queryFn: () =>
      scope.kind === 'team'
        ? getBacklog(organizationId, projectId, scope.teamId, level)
        : getProjectBacklog(organizationId, projectId, level),
    ...SURFACE_QUERY_OPTIONS,
  });
}

/**
 * § 3.3: the board of one level, a team's or the project's (feat-039 § 3.3); its
 * first answer validates the whole address.
 */
export function useBoard(
  organizationId: string,
  projectId: string,
  scope: BoardScope,
  level: BoardLevelCode,
) {
  return useQuery<Board | ProjectBoard>({
    queryKey: boardKey(organizationId, projectId, scope, level),
    queryFn: () =>
      scope.kind === 'team'
        ? getBoard(organizationId, projectId, scope.teamId, level)
        : getProjectBoard(organizationId, projectId, level),
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
export function useReadAgain(organizationId: string, projectId: string, scope: BoardScope) {
  const client = useQueryClient();
  const teamId = scopeTeamId(scope);
  return useCallback(async () => {
    const [backlogs, boards] =
      teamId === null
        ? [
            boardsKeys.projectBacklogs(organizationId, projectId),
            boardsKeys.projectBoards(organizationId, projectId),
          ]
        : [
            boardsKeys.backlogs(organizationId, projectId, teamId),
            boardsKeys.boards(organizationId, projectId, teamId),
          ];
    await Promise.all([
      client.invalidateQueries({ queryKey: backlogs }),
      client.invalidateQueries({ queryKey: boards }),
    ]);
  }, [client, organizationId, projectId, teamId]);
}
