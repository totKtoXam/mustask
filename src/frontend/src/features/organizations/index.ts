// Slice barrel (frontend.md "Slice rules"): what `app/` registers — the
// pages and their layout route for the router, the routes and landing
// builders and the permission codes for the navigation model, and the
// switcher's context source (mod-002 D-b, D-n, D-o). `app/` imports this
// slice through this file only.
export { OrganizationsSection } from './components/OrganizationsSection';
export { useOrganizationsContextSource } from './context/useOrganizationsContextSource';
export { AreasPage } from './pages/AreasPage';
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
  ORGANIZATIONS_PATH,
  ORGANIZATIONS_ROUTE,
  ORGANIZATION_GENERAL_ROUTE,
  PROJECTS_ROUTE,
  PROJECT_GENERAL_ROUTE,
  TEAMS_ROUTE,
  TEAM_ROUTE,
  areasPath,
  organizationGeneralPath,
  projectGeneralPath,
  projectsPath,
  teamsPath,
} from './paths';
export { organizationsPermissions } from './permissions';
