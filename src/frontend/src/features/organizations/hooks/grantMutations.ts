import { useMutation, useQueryClient, type QueryClient } from '@tanstack/react-query';
import {
  changeGrantRole,
  giveGrant,
  revokeGrant,
  setInheritsAccess,
  type GrantRole,
  type GrantScope,
} from '../api/grantsApi';
import { navigationKeys, organizationsKeys as keys } from '../api/queryKeys';

// The writes of mod-019 and the re-reads design § 7 names: a write on an object re-reads that object (its
// served codes ride on it) and its lists — the caller's own grant included — and the selectors' offers.

/** The query keys of the object a grant list belongs to: its page read carries the codes. */
function objectKeys(organizationId: string, scope: GrantScope, objectId: string) {
  switch (scope) {
    case 'department':
      return [keys.area(organizationId, objectId), keys.areas(organizationId)];
    case 'project':
      return [keys.project(organizationId, objectId), keys.projectLists(organizationId)];
    case 'team':
      return [keys.team(organizationId, objectId), keys.orgTeams(organizationId)];
  }
}

/** Re-reads the object's codes, its grants and what the shell offers (design § 7, ADR-0029 § 10). */
export function rereadObject(
  client: QueryClient,
  organizationId: string,
  scope: GrantScope,
  objectId: string,
): Promise<unknown> {
  return Promise.all(
    [
      keys.grants(organizationId, scope, objectId),
      ...objectKeys(organizationId, scope, objectId),
      navigationKeys.all,
    ].map((queryKey) => client.invalidateQueries({ queryKey })),
  );
}

export function useGiveGrant(organizationId: string, scope: GrantScope, objectId: string) {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (body: { userId: string; role: GrantRole }) =>
      giveGrant(organizationId, scope, objectId, body),
    onSuccess: () => rereadObject(client, organizationId, scope, objectId),
  });
}

export function useChangeGrantRole(organizationId: string, scope: GrantScope, objectId: string) {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (variables: { userId: string; role: GrantRole }) =>
      changeGrantRole(organizationId, scope, objectId, variables.userId, variables.role),
    onSuccess: () => rereadObject(client, organizationId, scope, objectId),
  });
}

export function useRevokeGrant(organizationId: string, scope: GrantScope, objectId: string) {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (userId: string) => revokeGrant(organizationId, scope, objectId, userId),
    onSuccess: () => rereadObject(client, organizationId, scope, objectId),
  });
}

/**
 * «Наследовать доступ» (US-2): a switch writes the node, which re-reads the node's page, the project it
 * belongs to and the project's grants (the inherited rows follow the switch).
 */
export function useSetInheritsAccess(organizationId: string, areaId: string) {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (value: boolean) => setInheritsAccess(organizationId, areaId, value),
    onSuccess: () =>
      Promise.all(
        [
          keys.area(organizationId, areaId),
          keys.areas(organizationId),
          keys.projectAreaTrees(organizationId),
          [...keys.all, 'project', organizationId],
          keys.grantLists(organizationId),
          navigationKeys.all,
        ].map((queryKey) => client.invalidateQueries({ queryKey })),
      ),
  });
}
