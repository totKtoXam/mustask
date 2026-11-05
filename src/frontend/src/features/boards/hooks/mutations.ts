import { useMutation } from '@tanstack/react-query';
import { reorderBacklog, reorderProjectBacklog, type ReorderBody } from '../api/boardsApi';
import type { BoardLevelCode } from '../lib/levels';
import type { BoardScope } from '../lib/scope';
import { useReadAgain } from './queries';

/**
 * § 3.2: the reorder, of the team's backlog or of the project's (feat-039
 * § 3.2). The filter is never sent: a reorder runs against the full list (feat-040 BR-TS-8).
 * Whatever the answer, the scope's surfaces are read again before the
 * mutation settles (BR-M4-28) — so on `boards.backlog.reorder_stale` the backlog
 * is read again first, and the notice follows (spec § Notices).
 */
export function useReorderBacklog(
  organizationId: string,
  scope: BoardScope,
  level: BoardLevelCode,
) {
  const readAgain = useReadAgain(organizationId, scope);
  return useMutation({
    mutationFn: (body: ReorderBody) =>
      scope.kind === 'team'
        ? reorderBacklog(organizationId, scope.teamId, level, body)
        : reorderProjectBacklog(organizationId, scope.projectId, level, body),
    onSettled: () => readAgain(),
  });
}
