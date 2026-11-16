import { useObjectAccess } from '../../../shared/lib/objectAccess';
import { useTeam } from '../../organizations';
import { iterationsPermissions } from '../permissions';

/**
 * The team of a sprint page and what the caller may do on it. The sprint-management code is decided
 * at the team, so it is read from the team's own served `codes` (mod-019 BR-SA-18), never from the
 * organization's answer, and the client holds no role list (design § 8). While the team is unread
 * the code is not granted: unknown is not allowed.
 */
export function useSprintAccess(organizationId: string, teamId: string) {
  const team = useTeam(organizationId, teamId);
  const access = useObjectAccess(team.data?.codes);
  return { team, canManage: access(iterationsPermissions.sprintManage, 'team') };
}
