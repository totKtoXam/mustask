import { useQuery } from '@tanstack/react-query';
import {
  getAreaTree,
  getDeletionCounts,
  getOrganization,
  getOrganizationTeams,
  getOrganizations,
  getProject,
  getProjects,
  getTeam,
  getTeamProjects,
  getTeams,
  getUnlinkCount,
  type ListView,
} from '../api/organizationsApi';
import {
  getMembers,
  getTeamMembers,
  searchMemberCandidates,
  searchTeamMemberCandidates,
} from '../api/membershipApi';
import { organizationsKeys } from '../api/queryKeys';

// One query hook per read (api-contract § 1–§ 4), on the keys the switcher
// shares (plan-frontend § 3.3).

export function useOrganizations(view: ListView) {
  return useQuery({
    queryKey: organizationsKeys.organizations(view),
    queryFn: () => getOrganizations(view),
  });
}

export function useOrganization(organizationId: string, options: { enabled?: boolean } = {}) {
  return useQuery({
    queryKey: organizationsKeys.organization(organizationId),
    queryFn: () => getOrganization(organizationId),
    enabled: options.enabled ?? true,
  });
}

export function useProjects(
  organizationId: string,
  view: ListView,
  options: { enabled?: boolean; fresh?: boolean } = {},
) {
  return useQuery({
    queryKey: organizationsKeys.projects(organizationId, view),
    queryFn: () => getProjects(organizationId, view),
    enabled: options.enabled ?? true,
    refetchOnMount: options.fresh ? 'always' : true,
  });
}

export function useProject(organizationId: string, projectId: string) {
  return useQuery({
    queryKey: organizationsKeys.project(organizationId, projectId),
    queryFn: () => getProject(organizationId, projectId),
  });
}

/**
 * The project's linked teams, by name with their default mark (feat-028 api-contract § 1.3).
 * `fresh`: read again on every mount even when the cache holds an answer — the creation dialog
 * starts with the default team as it stands when it opens, never a cached one (mod-017 design § 4).
 */
export function useTeams(
  organizationId: string,
  projectId: string,
  options: { enabled?: boolean; fresh?: boolean } = {},
) {
  return useQuery({
    queryKey: organizationsKeys.teams(organizationId, projectId),
    queryFn: () => getTeams(organizationId, projectId),
    enabled: options.enabled ?? true,
    refetchOnMount: options.fresh ? 'always' : true,
  });
}

/**
 * The organization's teams with their active projects (feat-028 § 2.1). `fresh`: a dialog
 * that offers them as options reads them again when it opens (design § 6.1).
 */
export function useOrganizationTeams(
  organizationId: string,
  options: { enabled?: boolean; fresh?: boolean } = {},
) {
  return useQuery({
    queryKey: organizationsKeys.orgTeams(organizationId),
    queryFn: () => getOrganizationTeams(organizationId),
    enabled: options.enabled ?? true,
    refetchOnMount: options.fresh ? 'always' : true,
  });
}

export function useTeam(organizationId: string, teamId: string) {
  return useQuery({
    queryKey: organizationsKeys.team(organizationId, teamId),
    queryFn: () => getTeam(organizationId, teamId),
  });
}

/** A team's active projects, by name (feat-028 § 2.2). */
export function useTeamProjects(organizationId: string, teamId: string, enabled = true) {
  return useQuery({
    queryKey: organizationsKeys.teamProjects(organizationId, teamId),
    queryFn: () => getTeamProjects(organizationId, teamId),
    enabled,
  });
}

/**
 * feat-028 § 2.1: the items a deletion would reassign, per linked project, read when the
 * confirmation opens and kept for no longer (`gcTime: 0`): the next opening counts afresh.
 * Never retried: a failure is the dialog's «Повторить».
 */
export function useDeletionCounts(organizationId: string, teamId: string) {
  return useQuery({
    queryKey: organizationsKeys.deletionCounts(organizationId, teamId),
    queryFn: () => getDeletionCounts(organizationId, teamId),
    gcTime: 0,
    retry: false,
  });
}

/** feat-028 § 2.4: the items an unlink would reassign in the project; read as the deletion's are. */
export function useUnlinkCount(organizationId: string, projectId: string, teamId: string) {
  return useQuery({
    queryKey: organizationsKeys.unlinkCount(organizationId, projectId, teamId),
    queryFn: () => getUnlinkCount(organizationId, projectId, teamId),
    gcTime: 0,
    retry: false,
  });
}

export function useAreaTree(
  organizationId: string,
  projectId: string,
  options: { enabled?: boolean } = {},
) {
  return useQuery({
    queryKey: organizationsKeys.areas(organizationId, projectId),
    queryFn: () => getAreaTree(organizationId, projectId),
    enabled: options.enabled ?? true,
  });
}

// mod-016 api-contract § 1.2–§ 1.3 and § 2.1–§ 2.2.

/** The members of an organization (§ 1.2): a plain array, read whole. */
export function useMembers(organizationId: string, options: { enabled?: boolean } = {}) {
  return useQuery({
    queryKey: organizationsKeys.members(organizationId),
    queryFn: () => getMembers(organizationId),
    enabled: options.enabled ?? true,
  });
}

/** The add-member search (§ 1.3), asked only from 2 characters on (`enabled`). */
export function useMemberCandidates(organizationId: string, text: string, enabled: boolean) {
  return useQuery({
    queryKey: organizationsKeys.memberCandidates(organizationId, text),
    queryFn: () => searchMemberCandidates(organizationId, text),
    enabled,
  });
}

/** A team's members (§ 2.1), at the team's address. */
export function useTeamMembers(organizationId: string, teamId: string) {
  return useQuery({
    queryKey: organizationsKeys.teamMembers(organizationId, teamId),
    queryFn: () => getTeamMembers(organizationId, teamId),
  });
}

/** The add-to-team search (§ 2.2), asked only from 2 characters on (`enabled`). */
export function useTeamMemberCandidates(
  organizationId: string,
  teamId: string,
  text: string,
  enabled: boolean,
) {
  return useQuery({
    queryKey: organizationsKeys.teamMemberCandidates(organizationId, teamId, text),
    queryFn: () => searchTeamMemberCandidates(organizationId, teamId, text),
    enabled,
  });
}
