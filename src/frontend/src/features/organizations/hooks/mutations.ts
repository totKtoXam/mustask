import { useMutation, useQueryClient, type QueryClient } from '@tanstack/react-query';
import { rereadAccess } from '../access/accessCache';
import { rememberRole } from '../access/roleMemory';
import {
  addMember,
  addTeamMember,
  changeMemberRole,
  removeMember,
  removeTeamMember,
} from '../api/membershipApi';
import type { OrganizationRole } from '../lib/roles';
import {
  addTeam,
  createArea,
  createOrganization,
  createProject,
  deactivateOrganization,
  deactivateProject,
  deleteArea,
  deleteTeam,
  moveArea,
  renameArea,
  renameOrganization,
  renameProject,
  renameTeam,
  restoreOrganization,
  restoreProject,
  setAreaOwner,
  setTeamDefaultArea,
} from '../api/organizationsApi';
import { organizationsKeys as keys } from '../api/queryKeys';

// One mutation hook per write group, each doing the invalidations of design
// § 3.7 (plan-frontend § 3.4). The switcher reads the same keys, so its
// levels follow every write without a reload. The tree is always read again
// after an area write, never patched: a write can move a subtree's resolved
// owners (api-contract § 4.1).

function invalidate(client: QueryClient, ...queryKeys: (readonly unknown[])[]): Promise<unknown> {
  return Promise.all(queryKeys.map((queryKey) => client.invalidateQueries({ queryKey })));
}

// § 1 Organizations

export function useCreateOrganization() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (name: string) => createOrganization(name),
    onSuccess: () => invalidate(client, keys.organizations('active')),
  });
}

export function useRenameOrganization(organizationId: string) {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (name: string) => renameOrganization(organizationId, name),
    onSuccess: () =>
      invalidate(client, keys.organization(organizationId), keys.organizations('active')),
  });
}

export function useDeactivateOrganization(organizationId: string) {
  const client = useQueryClient();
  return useMutation({
    mutationFn: () => deactivateOrganization(organizationId),
    onSuccess: () => {
      // Its address answers not found from now on. The page on screen is
      // left at once, so its read is only marked stale, not sent again.
      void client.invalidateQueries({
        queryKey: keys.organization(organizationId),
        refetchType: 'none',
      });
      return invalidate(client, keys.organizations('active'), keys.organizations('deactivated'));
    },
  });
}

export function useRestoreOrganization() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (organizationId: string) => restoreOrganization(organizationId),
    onSuccess: () =>
      invalidate(client, keys.organizations('active'), keys.organizations('deactivated')),
  });
}

// § 2 Projects

export function useCreateProject(organizationId: string) {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (body: { name: string; key: string }) => createProject(organizationId, body),
    onSuccess: () => invalidate(client, keys.projects(organizationId, 'active')),
  });
}

export function useRenameProject(organizationId: string, projectId: string) {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (name: string) => renameProject(organizationId, projectId, name),
    onSuccess: () =>
      invalidate(
        client,
        keys.project(organizationId, projectId),
        keys.projects(organizationId, 'active'),
        // The root area takes the project's name (BR-M1-11).
        keys.areas(organizationId, projectId),
      ),
  });
}

export function useDeactivateProject(organizationId: string, projectId: string) {
  const client = useQueryClient();
  return useMutation({
    mutationFn: () => deactivateProject(organizationId, projectId),
    onSuccess: () => {
      void client.invalidateQueries({
        queryKey: keys.project(organizationId, projectId),
        refetchType: 'none',
      });
      return invalidate(client, keys.projectLists(organizationId));
    },
  });
}

export function useRestoreProject(organizationId: string) {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (projectId: string) => restoreProject(organizationId, projectId),
    onSuccess: () => invalidate(client, keys.projectLists(organizationId)),
  });
}

// § 3 Teams

export function useAddTeam(organizationId: string, projectId: string) {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (name: string) => addTeam(organizationId, projectId, name),
    onSuccess: () =>
      invalidate(
        client,
        keys.teams(organizationId, projectId),
        keys.areas(organizationId, projectId),
      ),
  });
}

export function useRenameTeam(organizationId: string, projectId: string) {
  const client = useQueryClient();
  return useMutation({
    mutationFn: ({ teamId, name }: { teamId: string; name: string }) =>
      renameTeam(organizationId, projectId, teamId, name),
    onSuccess: () =>
      invalidate(
        client,
        keys.teams(organizationId, projectId),
        keys.teamPages(organizationId, projectId),
        // Owner and default names in the tree.
        keys.areas(organizationId, projectId),
      ),
  });
}

export function useSetTeamDefaultArea(organizationId: string, projectId: string) {
  const client = useQueryClient();
  return useMutation({
    mutationFn: ({ teamId, areaId }: { teamId: string; areaId: string }) =>
      setTeamDefaultArea(organizationId, projectId, teamId, areaId),
    onSuccess: () =>
      invalidate(
        client,
        keys.teams(organizationId, projectId),
        keys.teamPages(organizationId, projectId),
        keys.areas(organizationId, projectId),
      ),
  });
}

export function useDeleteTeam(organizationId: string, projectId: string) {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (teamId: string) => deleteTeam(organizationId, projectId, teamId),
    onSuccess: (_result, teamId) => {
      void client.invalidateQueries({
        queryKey: keys.team(organizationId, projectId, teamId),
        refetchType: 'none',
      });
      return invalidate(
        client,
        keys.teams(organizationId, projectId),
        keys.areas(organizationId, projectId),
      );
    },
  });
}

// § 4 Areas

function useAreaWrite<T, R>(
  organizationId: string,
  projectId: string,
  write: (variables: T) => Promise<R>,
  alsoTeams: boolean,
) {
  const client = useQueryClient();
  return useMutation({
    mutationFn: write,
    onSuccess: () =>
      alsoTeams
        ? invalidate(
            client,
            keys.areas(organizationId, projectId),
            // A default area's path can change.
            keys.teams(organizationId, projectId),
            keys.teamPages(organizationId, projectId),
          )
        : invalidate(client, keys.areas(organizationId, projectId)),
  });
}

export function useCreateArea(organizationId: string, projectId: string) {
  return useAreaWrite(
    organizationId,
    projectId,
    (body: { parentId: string; name: string; ownerTeamId: string | null }) =>
      createArea(organizationId, projectId, body),
    true,
  );
}

export function useRenameArea(organizationId: string, projectId: string) {
  return useAreaWrite(
    organizationId,
    projectId,
    ({ areaId, name }: { areaId: string; name: string }) =>
      renameArea(organizationId, projectId, areaId, name),
    true,
  );
}

export function useMoveArea(organizationId: string, projectId: string) {
  return useAreaWrite(
    organizationId,
    projectId,
    ({ areaId, parentId }: { areaId: string; parentId: string }) =>
      moveArea(organizationId, projectId, areaId, parentId),
    true,
  );
}

export function useSetAreaOwner(organizationId: string, projectId: string) {
  return useAreaWrite(
    organizationId,
    projectId,
    ({ areaId, ownerTeamId }: { areaId: string; ownerTeamId: string | null }) =>
      setAreaOwner(organizationId, projectId, areaId, ownerTeamId),
    false,
  );
}

export function useDeleteArea(organizationId: string, projectId: string) {
  return useAreaWrite(
    organizationId,
    projectId,
    (areaId: string) => deleteArea(organizationId, projectId, areaId),
    false,
  );
}

// mod-016 members (api-contract § 1.4–§ 1.6) and team members (§ 2.3–§ 2.4)

export function useAddMember(organizationId: string) {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (body: { userId: string; role: OrganizationRole }) =>
      addMember(organizationId, body),
    onSuccess: () => invalidate(client, keys.members(organizationId)),
  });
}

/**
 * `selfId` is the caller's own user id: a role the caller gives themselves
 * applies to their next request, so the new role is remembered *before* the
 * served codes are read again — that action's own notice speaks and the
 * role-change notice stays silent (design § 2.4) — and the codes are read at
 * once (BR-M1-31, design § 3.6): with no reload the controls the role no
 * longer allows go.
 */
export function useChangeMemberRole(organizationId: string, selfId: string | undefined) {
  const client = useQueryClient();
  return useMutation({
    mutationFn: ({ userId, role }: { userId: string; role: OrganizationRole }) =>
      changeMemberRole(organizationId, userId, role),
    onSuccess: (_member, { userId, role }) => {
      const reads = [invalidate(client, keys.members(organizationId))];
      if (selfId !== undefined && userId === selfId) {
        rememberRole(client, organizationId, role);
        reads.push(rereadAccess(client, organizationId));
      }
      return Promise.all(reads);
    },
  });
}

/**
 * Removing someone else re-reads the members and the organization's team
 * members (the user left every team, BR-M1-36). Removing oneself leaves the
 * page: the members list is only marked stale — its read would answer
 * «Организация не найдена» — and the organization lists are read, so the
 * switcher falls back (design § 3.7).
 */
export function useRemoveMember(organizationId: string, selfId: string | undefined) {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (userId: string) => removeMember(organizationId, userId),
    onSuccess: (_result, userId) => {
      if (selfId !== undefined && userId === selfId) {
        void client.invalidateQueries({
          queryKey: keys.members(organizationId),
          refetchType: 'none',
        });
        void client.invalidateQueries({
          queryKey: keys.teamMembersOf(organizationId),
          refetchType: 'none',
        });
        return invalidate(client, keys.organizations('active'));
      }
      return invalidate(client, keys.members(organizationId), keys.teamMembersOf(organizationId));
    },
  });
}

export function useAddTeamMember(organizationId: string, projectId: string, teamId: string) {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (userId: string) => addTeamMember(organizationId, projectId, teamId, userId),
    onSuccess: () => invalidate(client, keys.teamMembers(organizationId, projectId, teamId)),
  });
}

export function useRemoveTeamMember(organizationId: string, projectId: string, teamId: string) {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (userId: string) => removeTeamMember(organizationId, projectId, teamId, userId),
    onSuccess: () => invalidate(client, keys.teamMembers(organizationId, projectId, teamId)),
  });
}
