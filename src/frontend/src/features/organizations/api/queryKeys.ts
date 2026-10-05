// TanStack Query keys of the slice, `['organizations', <entity>, …]`
// (frontend.md "Data and state", plan-frontend § 3.3). The switcher's three
// levels use the same keys as the pages that read the same lists, so one
// request serves both and one invalidation refreshes both.
import type { ListView } from './organizationsApi';

const ROOT = 'organizations';

export const organizationsKeys = {
  all: [ROOT] as const,
  organizations: (view: ListView) => [ROOT, 'organizations', view] as const,
  organization: (organizationId: string) => [ROOT, 'organization', organizationId] as const,
  /** Every project list of an organization, both tabs. */
  projectLists: (organizationId: string) => [ROOT, 'projects', organizationId] as const,
  projects: (organizationId: string, view: ListView) =>
    [ROOT, 'projects', organizationId, view] as const,
  project: (organizationId: string, projectId: string) =>
    [ROOT, 'project', organizationId, projectId] as const,
  teams: (organizationId: string, projectId: string) =>
    [ROOT, 'teams', organizationId, projectId] as const,
  team: (organizationId: string, projectId: string, teamId: string) =>
    [ROOT, 'team', organizationId, projectId, teamId] as const,
  /** Every team page of a project. */
  teamPages: (organizationId: string, projectId: string) =>
    [ROOT, 'team', organizationId, projectId] as const,
  areas: (organizationId: string, projectId: string) =>
    [ROOT, 'areas', organizationId, projectId] as const,
};
