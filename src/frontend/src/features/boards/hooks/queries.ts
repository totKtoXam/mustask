import { useCallback } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { getBacklog, getBoard } from '../api/boardsApi';
import { boardsKeys } from '../api/queryKeys';
import type { BoardLevelCode } from '../lib/levels';

// One query hook per read of api-contract § 3, and the read-again of a team's
// surfaces after a write.

/**
 * No live push (BR-M4-28): a page shows its last read, read again when it
 * opens, after each write of its user, and when the tab regains focus — the
 * last one against the client's default, as the switcher's lists do.
 */
const SURFACE_QUERY_OPTIONS = { refetchOnWindowFocus: true } as const;

/** § 3.1: a team's backlog of one level; its first answer validates the whole address. */
export function useBacklog(
  organizationId: string,
  projectId: string,
  teamId: string,
  level: BoardLevelCode,
) {
  return useQuery({
    queryKey: boardsKeys.backlog(organizationId, projectId, teamId, level),
    queryFn: () => getBacklog(organizationId, projectId, teamId, level),
    ...SURFACE_QUERY_OPTIONS,
  });
}

/** § 3.3: a team's board of one level; its first answer validates the whole address. */
export function useBoard(
  organizationId: string,
  projectId: string,
  teamId: string,
  level: BoardLevelCode,
) {
  return useQuery({
    queryKey: boardsKeys.board(organizationId, projectId, teamId, level),
    queryFn: () => getBoard(organizationId, projectId, teamId, level),
    ...SURFACE_QUERY_OPTIONS,
  });
}

/**
 * Reads every backlog and board of the team again — the page on screen at
 * once, the others when next shown — and resolves once the page's own read
 * has answered. Never rejects: a failed read is the page's error state.
 */
export function useReadTeamAgain(organizationId: string, projectId: string, teamId: string) {
  const client = useQueryClient();
  return useCallback(async () => {
    await Promise.all([
      client.invalidateQueries({
        queryKey: boardsKeys.backlogs(organizationId, projectId, teamId),
      }),
      client.invalidateQueries({ queryKey: boardsKeys.boards(organizationId, projectId, teamId) }),
    ]);
  }, [client, organizationId, projectId, teamId]);
}
