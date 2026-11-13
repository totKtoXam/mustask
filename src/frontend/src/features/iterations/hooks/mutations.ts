import { useMutation, useQueryClient } from '@tanstack/react-query';
import {
  closeSprint,
  createSprint,
  removeSprint,
  startSprint,
  updateSprint,
  type SprintBody,
} from '../api/iterationsApi';
import { iterationsKeys } from '../api/queryKeys';

/**
 * Every write of the team's sprints reads the team's lists, options and outcomes again — on
 * success and on refusal alike, since a refusal (a lost race) says the page is stale.
 */
function useTeamWrite<V, R>(
  organizationId: string,
  teamId: string,
  write: (variables: V) => Promise<R>,
) {
  const client = useQueryClient();
  const refresh = () =>
    client.invalidateQueries({ queryKey: iterationsKeys.team(organizationId, teamId) });
  return useMutation({ mutationFn: write, onSettled: refresh });
}

export function useCreateSprint(organizationId: string, teamId: string) {
  return useTeamWrite(organizationId, teamId, (body: SprintBody) =>
    createSprint(organizationId, teamId, body),
  );
}

export function useUpdateSprint(organizationId: string, teamId: string) {
  return useTeamWrite(organizationId, teamId, (variables: { sprintId: string; body: SprintBody }) =>
    updateSprint(organizationId, teamId, variables.sprintId, variables.body),
  );
}

export function useStartSprint(organizationId: string, teamId: string) {
  return useTeamWrite(organizationId, teamId, (sprintId: string) =>
    startSprint(organizationId, teamId, sprintId),
  );
}

export function useRemoveSprint(organizationId: string, teamId: string) {
  return useTeamWrite(organizationId, teamId, (sprintId: string) =>
    removeSprint(organizationId, teamId, sprintId),
  );
}

export function useCloseSprint(organizationId: string, teamId: string) {
  return useTeamWrite(
    organizationId,
    teamId,
    (variables: { sprintId: string; destinationSprintId: string | null }) =>
      closeSprint(organizationId, teamId, variables.sprintId, variables.destinationSprintId),
  );
}
