import { useQuery } from '@tanstack/react-query';
import {
  getAreaTree,
  getOrganization,
  getOrganizations,
  getProject,
  getProjects,
  getTeam,
  getTeams,
  type ListView,
} from '../api/organizationsApi';
import { organizationsKeys } from '../api/queryKeys';

// One query hook per read (api-contract § 1–§ 4), on the keys the switcher
// shares (plan-frontend § 3.3).

export function useOrganizations(view: ListView) {
  return useQuery({
    queryKey: organizationsKeys.organizations(view),
    queryFn: () => getOrganizations(view),
  });
}

export function useOrganization(organizationId: string) {
  return useQuery({
    queryKey: organizationsKeys.organization(organizationId),
    queryFn: () => getOrganization(organizationId),
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
