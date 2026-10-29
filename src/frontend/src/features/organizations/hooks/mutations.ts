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
  createArea,
  describeArea,
  linkAreasToTeam,
  linkTeamsToArea,
  unlinkAreaFromTeam,
  unlinkTeamFromArea,
  createOrganization,
  createProject,
  createTeam,
  deactivateOrganization,
  deactivateProject,
  deleteArea,
  deleteTeam,
  describeProject,
  describeTeam,
  linkProjectsToTeam,
  linkTeamsToProject,
  moveArea,
  renameArea,
  renameOrganization,
  renameProject,
  renameTeam,
  restoreOrganization,
  restoreProject,
  setDefaultTeam,
  unlinkTeam,
  type CreateAreaBody,
  type CreateProjectBody,
  type CreateTeamBody,
} from '../api/organizationsApi';
import { organizationsKeys as keys } from '../api/queryKeys';

// One mutation hook per write group, each doing the invalidations of design
// § 3.7 (plan-frontend § 3.4). The switcher reads the same keys, so its
// levels follow every write without a reload. The tree is always read again
// after an area write, never patched.

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
    mutationFn: (body: CreateProjectBody) => createProject(organizationId, body),
    // A project that is created with teams changes what they work on (design § 13).
    onSuccess: () =>
      invalidate(
        client,
        keys.projects(organizationId, 'active'),
        ...linkListKeys(organizationId),
        ...areaLinkKeys(organizationId),
      ),
  });
}

export function useDescribeProject(organizationId: string, projectId: string) {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (description: string | null) =>
      describeProject(organizationId, projectId, description),
    onSuccess: () => invalidate(client, keys.project(organizationId, projectId)),
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
        // The project's node is renamed with it (ADR-0031).
        ...areaLinkKeys(organizationId),
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
      return invalidate(client, keys.projectLists(organizationId), ...areaLinkKeys(organizationId));
    },
  });
}

export function useRestoreProject(organizationId: string) {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (projectId: string) => restoreProject(organizationId, projectId),
    onSuccess: () =>
      invalidate(client, keys.projectLists(organizationId), ...areaLinkKeys(organizationId)),
  });
}

// feat-028 Teams, their links and the project's default (design § 13: a link, an unlink or a
// deletion refreshes the organization's teams, the team's projects, the project's teams — which
// the shell's team level and the work item pickers read — and the projects).

/** Every list a link can change, at the level of the organization. */
function linkListKeys(organizationId: string): (readonly unknown[])[] {
  return [
    keys.orgTeams(organizationId),
    keys.projectTeamLists(organizationId),
    keys.teamProjectLists(organizationId),
  ];
}

export function useCreateTeam(organizationId: string) {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (body: CreateTeamBody) => createTeam(organizationId, body),
    // A team created with projects is linked to them at once; its members are read on its page.
    onSuccess: () =>
      invalidate(client, ...linkListKeys(organizationId), ...areaLinkKeys(organizationId)),
  });
}

export function useRenameTeam(organizationId: string) {
  const client = useQueryClient();
  return useMutation({
    mutationFn: ({ teamId, name }: { teamId: string; name: string }) =>
      renameTeam(organizationId, teamId, name),
    // The new name shows wherever the team is listed (US-1).
    onSuccess: () =>
      invalidate(
        client,
        keys.teamPages(organizationId),
        ...linkListKeys(organizationId),
        keys.areaTeamLists(organizationId),
        keys.teamAreaLists(organizationId),
      ),
  });
}

export function useDescribeTeam(organizationId: string) {
  const client = useQueryClient();
  return useMutation({
    mutationFn: ({ teamId, description }: { teamId: string; description: string | null }) =>
      describeTeam(organizationId, teamId, description),
    onSuccess: () =>
      invalidate(client, keys.teamPages(organizationId), keys.orgTeams(organizationId)),
  });
}

/** mod-017 api-contract § 2.8 (feat-028: `project.write`'s): names the default team, or none. */
export function useSetDefaultTeam(organizationId: string, projectId: string) {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (teamId: string | null) => setDefaultTeam(organizationId, projectId, teamId),
    onSuccess: () => invalidate(client, keys.teams(organizationId, projectId)),
  });
}

export function useDeleteTeam(organizationId: string) {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (teamId: string) => deleteTeam(organizationId, teamId),
    onSuccess: (_result, teamId) => {
      // The team's address answers not found from now on; the page on screen is left at once,
      // so its reads are only marked stale. The items went to none and the settings naming it were cleared.
      for (const key of [
        keys.team(organizationId, teamId),
        keys.teamProjects(organizationId, teamId),
        keys.teamMembers(organizationId, teamId),
      ]) {
        void client.invalidateQueries({ queryKey: key, refetchType: 'none' });
      }
      return invalidate(
        client,
        keys.orgTeams(organizationId),
        keys.projectTeamLists(organizationId),
        // Its links to areas end with it (BR-AW-10).
        keys.areaTeamLists(organizationId),
      );
    },
  });
}

/** Links existing teams to a project, from the project's end (§ 2.4). */
export function useLinkTeamsToProject(organizationId: string, projectId: string) {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (teamIds: readonly string[]) =>
      linkTeamsToProject(organizationId, projectId, teamIds),
    onSuccess: () => invalidate(client, ...linkListKeys(organizationId)),
  });
}

/** Links existing projects to a team, from the team's end (§ 2.2). */
export function useLinkProjectsToTeam(organizationId: string, teamId: string) {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (projectIds: readonly string[]) =>
      linkProjectsToTeam(organizationId, teamId, projectIds),
    onSuccess: () => invalidate(client, ...linkListKeys(organizationId)),
  });
}

/**
 * The unlink (SC-09), from either end: the items the team owned in the project went to the
 * destination, the setting naming the team was cleared, the link is gone.
 */
export function useUnlinkTeam(organizationId: string) {
  const client = useQueryClient();
  return useMutation({
    mutationFn: ({
      projectId,
      teamId,
      destinationTeamId,
    }: {
      projectId: string;
      teamId: string;
      destinationTeamId: string | null;
    }) => unlinkTeam(organizationId, projectId, teamId, destinationTeamId),
    onSuccess: () => invalidate(client, ...linkListKeys(organizationId)),
  });
}

// mod-018 The organization's areas. Any area or link write reads again the tree, the pages, the
// link lists and the project's and team's areas, never patching them (design § 13).

/** Every list an area or a link write can change. */
function areaLinkKeys(organizationId: string): (readonly unknown[])[] {
  return [
    keys.areas(organizationId),
    keys.areaPages(organizationId),
    keys.areaTeamLists(organizationId),
    keys.projectAreaTrees(organizationId),
    keys.teamAreaLists(organizationId),
  ];
}

function useAreaWrite<T, R>(organizationId: string, write: (variables: T) => Promise<R>) {
  const client = useQueryClient();
  return useMutation({
    mutationFn: write,
    onSuccess: () => invalidate(client, ...areaLinkKeys(organizationId)),
  });
}

export function useCreateArea(organizationId: string) {
  return useAreaWrite(organizationId, (body: CreateAreaBody) => createArea(organizationId, body));
}

export function useRenameArea(organizationId: string) {
  return useAreaWrite(organizationId, ({ areaId, name }: { areaId: string; name: string }) =>
    renameArea(organizationId, areaId, name),
  );
}

export function useDescribeArea(organizationId: string) {
  return useAreaWrite(
    organizationId,
    ({ areaId, description }: { areaId: string; description: string | null }) =>
      describeArea(organizationId, areaId, description),
  );
}

export function useMoveArea(organizationId: string) {
  return useAreaWrite(
    organizationId,
    ({ areaId, parentId }: { areaId: string; parentId: string | null }) =>
      moveArea(organizationId, areaId, parentId),
  );
}

export function useDeleteArea(organizationId: string) {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (areaId: string) => deleteArea(organizationId, areaId),
    onSuccess: (_result, areaId) => {
      // The area's address answers not found from now on; the page on screen is left at once.
      void client.invalidateQueries({
        queryKey: keys.area(organizationId, areaId),
        refetchType: 'none',
      });
      return invalidate(client, ...areaLinkKeys(organizationId));
    },
  });
}

export function useLinkTeamsToArea(organizationId: string, areaId: string) {
  return useAreaWrite(organizationId, (teamIds: readonly string[]) =>
    linkTeamsToArea(organizationId, areaId, teamIds),
  );
}

export function useLinkAreasToTeam(organizationId: string, teamId: string) {
  return useAreaWrite(organizationId, (areaIds: readonly string[]) =>
    linkAreasToTeam(organizationId, teamId, areaIds),
  );
}

/** The two unlinks of design § 6.2, each from the end whose page it is. */

export function useUnlinkTeamFromArea(organizationId: string, areaId: string) {
  return useAreaWrite(organizationId, (teamId: string) =>
    unlinkTeamFromArea(organizationId, areaId, teamId),
  );
}

export function useUnlinkAreaFromTeam(organizationId: string, teamId: string) {
  return useAreaWrite(organizationId, (areaId: string) =>
    unlinkAreaFromTeam(organizationId, teamId, areaId),
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

export function useAddTeamMember(organizationId: string, teamId: string) {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (userId: string) => addTeamMember(organizationId, teamId, userId),
    // One list of members serves every project the team works on (BR-OL-11): the project lists'
    // `isMember` marks are read again too.
    onSuccess: () =>
      invalidate(
        client,
        keys.teamMembers(organizationId, teamId),
        keys.projectTeamLists(organizationId),
      ),
  });
}

export function useRemoveTeamMember(organizationId: string, teamId: string) {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (userId: string) => removeTeamMember(organizationId, teamId, userId),
    onSuccess: () =>
      invalidate(
        client,
        keys.teamMembers(organizationId, teamId),
        keys.projectTeamLists(organizationId),
      ),
  });
}
