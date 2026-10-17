// Slice barrel (frontend.md "Slice rules"): what `app/` registers — the
// pages and their layout route for the router, the routes and landing
// builders and the permission codes for the navigation model, and the
// switcher's context source (mod-002 D-b, D-n, D-o). `app/` imports this
// slice through this file only.
//
// mod-016 adds the served-access layer: the gate and the page wrapper that
// put every organization page in the one state order of its design § 2.1, the
// shell's check built from the served codes, the watcher that re-reads them,
// and the access read the shell makes for the switcher's selection.
export { OrganizationGate } from './access/OrganizationGate';
export { OrganizationPage } from './access/OrganizationPage';
export type { OrganizationPageProps } from './access/OrganizationPage';
export { useAccessWatcher } from './access/useAccessWatcher';
export { useOrganizationAccess } from './access/useOrganizationAccess';
export { useServedAccessCheck } from './access/servedAccess';
export { OrganizationsSection } from './components/OrganizationsSection';
export { useOrganizationsContextSource } from './context/useOrganizationsContextSource';
export { AreasPage } from './pages/AreasPage';
export { MembersPage } from './pages/MembersPage';
export { OrganizationGeneralPage } from './pages/OrganizationGeneralPage';
export { OrganizationListPage } from './pages/OrganizationListPage';
export { ProjectGeneralPage } from './pages/ProjectGeneralPage';
export { ProjectListPage } from './pages/ProjectListPage';
export { TeamPage } from './pages/TeamPage';
export { TeamsPage } from './pages/TeamsPage';
export {
  AREAS_ROUTE,
  DEACTIVATED_ORGANIZATIONS_ROUTE,
  DEACTIVATED_PROJECTS_ROUTE,
  MEMBERS_ROUTE,
  ORGANIZATIONS_PATH,
  ORGANIZATIONS_ROUTE,
  ORGANIZATION_GENERAL_ROUTE,
  PROJECTS_ROUTE,
  PROJECT_GENERAL_ROUTE,
  TEAMS_ROUTE,
  TEAM_ROUTE,
  areasPath,
  membersPath,
  organizationGeneralPath,
  projectGeneralPath,
  projectsPath,
  teamsPath,
} from './paths';
export { organizationsPermissions } from './permissions';
// mod-003's «Область» picker and its create dialog's root path read the area
// tree unchanged (its api-contract § 3), on this slice's key.
export { useAreaTree } from './hooks/queries';
export type { AreaNode } from './api/organizationsApi';
// mod-017: the work item's «Команда» picker and the team filter (workitems), and the creation
// dialog of the project's views, read the project's teams on this slice's key.
export { useTeams } from './hooks/queries';
export type { DefaultMark, Team, TeamInList } from './api/organizationsApi';
