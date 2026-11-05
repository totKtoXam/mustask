// Slice barrel (frontend.md "Slice rules"): what `app/` registers — the
// pages and their layout route for the router, the routes and landing
// builders and the permission codes for the navigation model, and the
// reads the shell's selectors are built from (feat-043).
// `app/` imports this slice through this file only.
//
// mod-016 adds the served-access layer: the gate and the page wrapper that
// put every organization page in the one state order of its design § 2.1, the
// shell's check built from the served codes, the watcher that re-reads them,
// and the access read the shell makes for the selected organization.
export { OrganizationGate } from './access/OrganizationGate';
export { OrganizationPage } from './access/OrganizationPage';
export type { OrganizationPageProps } from './access/OrganizationPage';
export { useAccessWatcher } from './access/useAccessWatcher';
export { useOrganizationAccess } from './access/useOrganizationAccess';
export { useServedAccessCheck } from './access/servedAccess';
export { RESCUE_SECTION_LABEL } from './lib/rescueTexts';
export { OrganizationsSection } from './components/OrganizationsSection';
export { AllOrganizationsPage } from './pages/AllOrganizationsPage';
export { AreaPage } from './pages/AreaPage';
export { AreasPage } from './pages/AreasPage';
export { MembersPage } from './pages/MembersPage';
export { OrganizationGeneralPage } from './pages/OrganizationGeneralPage';
export { OrganizationListPage } from './pages/OrganizationListPage';
export { OrganizationOverviewPage } from './pages/OrganizationOverviewPage';
export { OrganizationTeamsPage } from './pages/OrganizationTeamsPage';
export { ProjectGeneralPage } from './pages/ProjectGeneralPage';
export { ProjectListPage } from './pages/ProjectListPage';
export { TeamPage } from './pages/TeamPage';
export { TeamsPage } from './pages/TeamsPage';
export {
  ADMIN_ORGANIZATIONS_PATH,
  AREA_PREFIX_ROUTE,
  AREA_PROJECTS_ROUTE,
  AREA_SETTINGS_ROUTE,
  AREA_TEAMS_ROUTE,
  DEACTIVATED_ORGANIZATIONS_ROUTE,
  MEMBERS_ROUTE,
  ORGANIZATIONS_PATH,
  ORGANIZATIONS_ROUTE,
  ORGANIZATION_GENERAL_ROUTE,
  ORGANIZATION_OVERVIEW_ROUTE,
  ORG_AREAS_ROUTE,
  ORG_AREA_ROUTE,
  ORG_TEAMS_ROUTE,
  ORG_TEAM_ROUTE,
  PROJECTS_ROUTE,
  PROJECT_GENERAL_ROUTE,
  PROJECT_PREFIX_ROUTE,
  PROJECT_SETTINGS_ROUTE,
  TEAMS_ROUTE,
  TEAM_MEMBERS_ROUTE,
  TEAM_PREFIX_ROUTE,
  TEAM_PROJECTS_ROUTE,
  TEAM_SETTINGS_ROUTE,
  membersPath,
  organizationAreaPath,
  organizationAreasPath,
  organizationGeneralPath,
  organizationOverviewPath,
  organizationTeamPath,
  organizationTeamsPath,
  projectGeneralPath,
  projectsPath,
  teamsPath,
} from './paths';
export { organizationsPermissions } from './permissions';
// feat-043: the shell's selectors read the active organizations and `selector-offers`.
export { useOrganizations, useProjects, useTeamProjects, useTeams } from './hooks/queries';
export { useSelectorOffers } from './hooks/navigationQueries';
export { useObjectLink } from './hooks/useObjectLink';
// The shell resolves `areas/:id` against the offers (a project's node or a sub-area).
export { useArea } from './hooks/queries';
export { notFoundLevel } from './lib/notFoundLevel';
export { organizationsKeys } from './api/queryKeys';
export type { SelectorOffers } from './api/navigationApi';
// mod-017: the work item's «Команда» picker and the team filter (workitems), and the creation
// dialog of the project's views, read the project's teams on this slice's key.
// mod-003: the work item's «Область» offers the project's subtree, on this slice's key.
export { useProjectAreaTree } from './hooks/queries';
export type { AreaNode } from './api/organizationsApi';
export type { DefaultMark, ProjectTeam } from './api/organizationsApi';
// feat-040: a team's creation dialog offers its linked projects as «Проект» (read afresh at every
// opening), and the card dialog of a team's view finds an identifier's project among the
// organization's active projects by key (BR-TS-4, api-contract § 7).
export type { ProjectListItem, TeamProject } from './api/organizationsApi';
