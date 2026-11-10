import { useMemo } from 'react';
import { useProjectCodes, useTeam, type ProjectCodes } from '../../organizations';
import { workItemsPermissions } from '../../workitems';
import { boardsPermissions } from '../permissions';
import type { BoardScope } from '../lib/scope';

/** Whether the project's served codes hold `code`; a project the list does not hold holds nothing. */
export function projectHolds(
  codes: ProjectCodes | undefined,
  projectId: string,
  code: string,
): boolean {
  return codes?.get(projectId)?.includes(code) ?? false;
}

/** What the signed-in user may do on one backlog or board, by the codes the objects serve (BR-SA-18). */
export interface BoardAccess {
  /** The codes of the projects, once per project (api-contract § 5): a card is judged by its own project. */
  projectCodes: ProjectCodes | undefined;
  /** `workitems.workitem.write` in at least one of the shown projects: the project's own, or one of a team's. */
  canWrite: boolean;
  /** `workitems.link.write`, as `canWrite`. */
  canLink: boolean;
  /**
   * The reorder: `boards.projectbacklog.reorder` on the project of a project's backlog, `boards.backlog.reorder`
   * on the team of a team's (the codes are decided at their own scope, ADR-0038).
   */
  canReorder: boolean;
}

/**
 * The checks of a backlog or board surface. Nothing here asks the organization answer for a project or team
 * code (mod-019 BR-SA-18, F-1, F-6): the work-item codes come from the project rows of `GET /projects`, the
 * team's reorder from the team's own read. `shownProjectIds` are the projects of the rows on screen (a team's
 * surface holds the rows of several); until the reads land every check fails closed.
 */
export function useBoardAccess(
  organizationId: string,
  scope: BoardScope,
  shownProjectIds: readonly string[],
  /** A surface that only creates has no use for the reorder, so it does not read the team for it. */
  options: { reorder?: boolean } = {},
): BoardAccess {
  const { codes: projectCodes } = useProjectCodes(organizationId);
  const teamId = scope.kind === 'team' ? scope.teamId : '';
  const team = useTeam(organizationId, teamId, {
    enabled: scope.kind === 'team' && (options.reorder ?? true),
  });
  const teamCodes = team.data?.codes;
  return useMemo(() => {
    const projectIds = scope.kind === 'project' ? [scope.projectId] : shownProjectIds;
    const some = (code: string) => projectIds.some((id) => projectHolds(projectCodes, id, code));
    return {
      projectCodes,
      canWrite: some(workItemsPermissions.workItemWrite),
      canLink: some(workItemsPermissions.linkWrite),
      canReorder:
        scope.kind === 'project'
          ? projectHolds(projectCodes, scope.projectId, boardsPermissions.projectBacklogReorder)
          : (teamCodes?.includes(boardsPermissions.backlogReorder) ?? false),
    };
  }, [scope, shownProjectIds, projectCodes, teamCodes]);
}
