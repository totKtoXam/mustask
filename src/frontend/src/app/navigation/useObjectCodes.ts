import { useMemo } from 'react';
import { useArea, useProject, useTeam } from '../../features/organizations';
import type { WorkContextSelection } from '../../shared/lib/workContext';
import type { NavigationContext } from './visibility';

/**
 * The served `codes` of the objects the selection names (mod-019 api-contract § 5, BR-SA-18): the shell's
 * «Параметры» entries are decided by what the caller holds on the chosen project, team, department or node,
 * not by the organization answer. The reads are the pages' own (one cache key each), so a page opened on the
 * object costs no second request; nothing is read for an object that is not chosen or without a real context.
 */
export function useObjectCodes(
  selection: WorkContextSelection,
  enabled: boolean,
): NonNullable<NavigationContext['objectCodes']> {
  const organizationId = selection.organizationId ?? '';
  const on = (id: string | undefined) => enabled && organizationId !== '' && id !== undefined;
  const project = useProject(organizationId, selection.projectId ?? '', {
    enabled: on(selection.projectId),
  });
  const team = useTeam(organizationId, selection.teamId ?? '', { enabled: on(selection.teamId) });
  const department = useArea(organizationId, selection.departmentId ?? '', {
    enabled: on(selection.departmentId),
  });
  const node = useArea(organizationId, selection.nodeId ?? '', { enabled: on(selection.nodeId) });
  const projectCodes = project.data?.codes;
  const teamCodes = team.data?.codes;
  const departmentCodes = department.data?.codes;
  const nodeCodes = node.data?.codes;
  return useMemo(
    () => ({
      project: projectCodes ?? undefined,
      team: teamCodes ?? undefined,
      department: departmentCodes ?? undefined,
      node: nodeCodes ?? undefined,
    }),
    [projectCodes, teamCodes, departmentCodes, nodeCodes],
  );
}
