import { useMutation } from '@tanstack/react-query';
import { reorderBacklog, type ReorderBody } from '../api/boardsApi';
import type { BoardLevelCode } from '../lib/levels';
import { useReadTeamAgain } from './queries';

/**
 * § 3.2: the reorder. Whatever the answer, the team's surfaces are read again
 * before the mutation settles (BR-M4-28) — so on `boards.backlog.reorder_stale`
 * the backlog is read again first, and the notice follows (spec § Notices).
 */
export function useReorderBacklog(
  organizationId: string,
  projectId: string,
  teamId: string,
  level: BoardLevelCode,
) {
  const readAgain = useReadTeamAgain(organizationId, projectId, teamId);
  return useMutation({
    mutationFn: (body: ReorderBody) =>
      reorderBacklog(organizationId, projectId, teamId, level, body),
    onSettled: () => readAgain(),
  });
}
