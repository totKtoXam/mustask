// The two reads of feat-043, strictly per
// docs/features/feat-043-area-project-team-navigation/api-contract.md §§ 1-2: «Обзор» and the
// selectors' offers. Both are bare objects, camelCase; a list is rendered as received.
import { apiFetch } from '../../../shared/api/httpClient';
import { organizationApiPath } from './organizationsApi';

/** § 1: a department in the first rows of «Обзор». */
export interface OverviewDepartment {
  id: string;
  name: string;
}

export interface OverviewProject {
  id: string;
  name: string;
  key: string;
}

export interface OverviewTeam {
  id: string;
  name: string;
  isMember: boolean;
}

/** § 1: counts of every object, the first five by name, and all of the caller's own teams. */
export interface Overview {
  departmentCount: number;
  projectCount: number;
  teamCount: number;
  departments: OverviewDepartment[];
  projects: OverviewProject[];
  teams: OverviewTeam[];
  myTeams: OverviewTeam[];
}

/** § 2: a project with its place and the active teams linked to it. */
export interface OfferedProject {
  id: string;
  name: string;
  key: string;
  /** `null` for a project at the top level. */
  departmentId: string | null;
  departmentName: string | null;
  teamIds: string[];
  /** mod-021 api-contract § 3: the stored palette key. */
  color?: string;
}

/** § 2 with mod-021 § 3: a team with the department it stands in (its place, BR-TD-1). */
export interface OfferedTeam {
  id: string;
  name: string;
  isMember: boolean;
  departmentId: string;
  /** mod-021 api-contract § 3: the stored palette key. */
  color?: string;
}

/** § 2: everything the selectors and the lists «Проекты» and «Команды» need, in one read. */
export interface SelectorOffers {
  departments: { id: string; name: string; color?: string }[];
  projects: OfferedProject[];
  teams: OfferedTeam[];
}

/** § 1: `GET …/overview`. */
export function getOverview(organizationId: string): Promise<Overview> {
  return apiFetch(`${organizationApiPath(organizationId)}/overview`);
}

/** § 2: `GET …/selector-offers`. */
export function getSelectorOffers(organizationId: string): Promise<SelectorOffers> {
  return apiFetch(`${organizationApiPath(organizationId)}/selector-offers`);
}
