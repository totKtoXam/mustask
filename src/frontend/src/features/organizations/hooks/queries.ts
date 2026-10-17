import { useQuery } from '@tanstack/react-query';
import {
  getAreaTree,
  getOrganization,
  getOrganizations,
  getOwnedItemsCount,
  getProject,
  getProjects,
  getTeam,
  getTeams,
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

export function useProjects(organizationId: string, view: ListView) {
  return useQuery({
    queryKey: organizationsKeys.projects(organizationId, view),
    queryFn: () => getProjects(organizationId, view),
  });
}

export function useProject(organizationId: string, projectId: string) {
  return useQuery({
    queryKey: organizationsKeys.project(organizationId, projectId),
    queryFn: () => getProject(organizationId, projectId),
  });
}

export function useTeams(
  organizationId: string,
  projectId: string,
  options: { enabled?: boolean } = {},
) {
  return useQuery({
    queryKey: organizationsKeys.teams(organizationId, projectId),
    queryFn: () => getTeams(organizationId, projectId),
    enabled: options.enabled ?? true,
  });
}

export function useTeam(organizationId: string, projectId: string, teamId: string) {
  return useQuery({
    queryKey: organizationsKeys.team(organizationId, projectId, teamId),
    queryFn: () => getTeam(organizationId, projectId, teamId),
  });
}

/**
 * mod-017 api-contract § 2.9: the items a deletion would reassign, read when the
 * confirmation opens and kept for no longer (`gcTime: 0`): the next opening counts
 * afresh. Never retried: a failure is the dialog's «Повторить».
 */
export function useOwnedItemsCount(organizationId: string, projectId: string, teamId: string) {
  return useQuery({
    queryKey: organizationsKeys.ownedItemsCount(organizationId, projectId, teamId),
    queryFn: () => getOwnedItemsCount(organizationId, projectId, teamId),
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
export function useMembers(organizationId: string) {
  return useQuery({
    queryKey: organizationsKeys.members(organizationId),
    queryFn: () => getMembers(organizationId),
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

/** A team's members (§ 2.1). */
export function useTeamMembers(organizationId: string, projectId: string, teamId: string) {
  return useQuery({
    queryKey: organizationsKeys.teamMembers(organizationId, projectId, teamId),
    queryFn: () => getTeamMembers(organizationId, projectId, teamId),
  });
}

/** The add-to-team search (§ 2.2), asked only from 2 characters on (`enabled`). */
export function useTeamMemberCandidates(
  organizationId: string,
  projectId: string,
  teamId: string,
  text: string,
  enabled: boolean,
) {
  return useQuery({
    queryKey: organizationsKeys.teamMemberCandidates(organizationId, projectId, teamId, text),
    queryFn: () => searchTeamMemberCandidates(organizationId, projectId, teamId, text),
    enabled,
  });
}
