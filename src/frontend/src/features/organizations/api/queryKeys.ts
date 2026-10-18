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
  /**
   * A project's linked teams (feat-028 § 2.4): the page's table, the shell's team level and
   * the work item pickers read this one key.
   */
  teams: (organizationId: string, projectId: string) =>
    [ROOT, 'teams', organizationId, projectId] as const,
  /** Every project's linked-team list of an organization: a team's rename or deletion refreshes them all. */
  projectTeamLists: (organizationId: string) => [ROOT, 'teams', organizationId] as const,
  /** The organization's own list of teams, with their projects (feat-028 § 2.1). */
  orgTeams: (organizationId: string) => [ROOT, 'org-teams', organizationId] as const,
  team: (organizationId: string, teamId: string) => [ROOT, 'team', organizationId, teamId] as const,
  /** Every team page of an organization. */
  teamPages: (organizationId: string) => [ROOT, 'team', organizationId] as const,
  /** A team's projects (§ 2.2). */
  teamProjects: (organizationId: string, teamId: string) =>
    [ROOT, 'team-projects', organizationId, teamId] as const,
  /** Every team's projects of an organization: a link or an unlink refreshes the ones on screen. */
  teamProjectLists: (organizationId: string) => [ROOT, 'team-projects', organizationId] as const,
  /** What one team's deletion would reassign, per project (§ 2.1). */
  deletionCounts: (organizationId: string, teamId: string) =>
    [ROOT, 'deletion-counts', organizationId, teamId] as const,
  /** What one unlink would reassign (§ 2.4). */
  unlinkCount: (organizationId: string, projectId: string, teamId: string) =>
    [ROOT, 'unlink-count', organizationId, projectId, teamId] as const,
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
  teamMembers: (organizationId: string, teamId: string) =>
    [ROOT, 'team-members', organizationId, teamId] as const,
  /** Every team-members list of an organization: a member's removal invalidates them all. */
  teamMembersOf: (organizationId: string) => [ROOT, 'team-members', organizationId] as const,
  /** The add-to-team search (§ 2.2). */
  teamMemberCandidates: (organizationId: string, teamId: string, text: string) =>
    [ROOT, 'team-member-candidates', organizationId, teamId, text] as const,
  /** The creation form's member search (feat-028 § 2.3a): a team that does not exist yet. */
  newTeamMemberCandidates: (organizationId: string, text: string) =>
    [ROOT, 'new-team-member-candidates', organizationId, text] as const,
};
