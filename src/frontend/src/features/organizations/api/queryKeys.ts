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
  /**
   * The caller's served access to an organization (mod-016 api-contract
   * § 1.1): its data is an `AccessAnswer` — codes, or the 404 as an answer.
   */
  access: (organizationId: string) => [ROOT, 'access', organizationId] as const,
  /** Every access entry, for the watcher's cache subscription. */
  accesses: [ROOT, 'access'] as const,
  /** The members of an organization (api-contract § 1.2). */
  members: (organizationId: string) => [ROOT, 'members', organizationId] as const,
  /** The add-member search (§ 1.3): one entry per text. */
  memberCandidates: (organizationId: string, text: string) =>
    [ROOT, 'member-candidates', organizationId, text] as const,
  /** The members of one team (§ 2.1). */
  teamMembers: (organizationId: string, projectId: string, teamId: string) =>
    [ROOT, 'team-members', organizationId, projectId, teamId] as const,
  /** Every team-members list of an organization: a member's removal invalidates them all. */
  teamMembersOf: (organizationId: string) => [ROOT, 'team-members', organizationId] as const,
  /** The add-to-team search (§ 2.2). */
  teamMemberCandidates: (organizationId: string, projectId: string, teamId: string, text: string) =>
    [ROOT, 'team-member-candidates', organizationId, projectId, teamId, text] as const,
};
