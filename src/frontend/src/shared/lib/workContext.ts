import { createContext, useContext } from 'react';

/**
 * The work context the shell holds and pages read through `useWorkContext()`
 * (feat-043, ADR-0032 §§ 1, 5, 6): the organization plus at most one department, one team and
 * one project. The address is its source (the most specific object in the prefix, the others in
 * the query); the remembered selection fills a page that carries no organization. The shell's
 * provider (`app/navigation/useNavigationSource`) reads the organizations and `selector-offers`
 * and hands them over in this shape; there is no default team and no team level of its own
 * (mod-020's fallback and feat-040's interim level are retired).
 */

export interface WorkContextTeam {
  id: string;
  name: string;
  /** The user is in this team (api-contract of feat-043 § 2). */
  isMember?: boolean;
}

export interface WorkContextProject {
  id: string;
  name: string;
}

export interface WorkContextOrganization {
  id: string;
  name: string;
  /** The effective organization's active projects (from `selector-offers`); empty for the others. */
  projects: readonly WorkContextProject[];
  /** The effective organization's active teams, the user's own first; empty for the others. */
  teams: readonly WorkContextTeam[];
}

export interface WorkContextSelection {
  organizationId?: string;
  departmentId?: string;
  projectId?: string;
  teamId?: string;
  /**
   * The chosen project's node, the address of «Структура» (ADR-0031 rule 5): read from the
   * project's own subtree, because the offers carry no node id. Absent until it is read.
   */
  structureId?: string;
  /** A project's node or a sub-area is open (`areas/:areaId` that is no department): its id. */
  nodeId?: string;
}

/** The kinds a row or a link opens (the selectors' kinds). */
export type WorkContextObjectKind = 'department' | 'team' | 'project';

/**
 * Where a row or a link to an object leads (ADR-0032 § 5, D-3): the object becomes the most
 * specific one, the less specific selectors that fit stay in the query, the others reset and
 * the notice says so (`null`: nothing was reset).
 */
export interface WorkContextObjectLink {
  to: string;
  notice: string | null;
}

/** The chosen object of each selector, or `null` for «все». */
export interface WorkContextObjects {
  department: string | null;
  team: string | null;
  project: string | null;
}

/** `GET …/selector-offers` (feat-043 api-contract § 2), as far as the selectors need it. */
export interface WorkContextOffers {
  departments: readonly { id: string; name: string }[];
  projects: readonly {
    id: string;
    name: string;
    key: string;
    departmentId: string | null;
    teamIds: readonly string[];
  }[];
  teams: readonly {
    id: string;
    name: string;
    isMember: boolean;
    departmentIds: readonly string[];
  }[];
}

export type WorkContextStatus = 'loading' | 'failed' | 'ready';

/** The levels below the organization, which load and fail together (one read of the offers). */
export type WorkContextLowerLevel = 'project' | 'team';

/**
 * The fallback notice (ADR-0032 § 6): a remembered organization or object is gone. `names` is
 * what is selected instead, top-down, empty when nothing is left; `id` tells one notice from
 * the next.
 */
export interface WorkContextFallback {
  id: number;
  names: readonly string[];
}

export interface WorkContextSource {
  /** The organization list's status. */
  status: WorkContextStatus;
  /** The offers' status: the department, team and project selectors load and fail together. */
  levelStatus?: Partial<Record<WorkContextLowerLevel, WorkContextStatus>>;
  /** The user's active organizations as the server orders them. */
  organizations: readonly WorkContextOrganization[];
  selection: WorkContextSelection;
  /** `selector-offers` of the effective organization; absent while it loads or after it failed. */
  offers?: WorkContextOffers;
  /**
   * `false` for a stub that stands for no real lists: organization-scoped navigation entries
   * stay hidden until a real context exists and the organization's served codes are read.
   */
  isReal: boolean;
  /** The organization list page. Undefined: «Все организации» is not shown. */
  organizationListPath?: string;
  /** «Повторить» of the organization list. */
  retry?(): void;
  /** «Повторить» of the offers. */
  retryOffers?(): void;
  /** «Все организации»: forgets the remembered selection (the shell moves the page). */
  clearOrganizations?(): void;
  /** The fallback notice to show, or none. */
  fallback?: WorkContextFallback | null;
  /** The notice was shown and closed. */
  dismissFallback?(): void;
  /** The address of an object opened from a row or a link; absent in a stub: the plain path. */
  objectLink?(kind: WorkContextObjectKind, id: string): WorkContextObjectLink | null;
}

/** The selection as the selectors' objects (`null`: «все»). */
export function objectsOf(selection: WorkContextSelection): WorkContextObjects {
  return {
    department: selection.departmentId ?? null,
    team: selection.teamId ?? null,
    project: selection.projectId ?? null,
  };
}

const NO_CONTEXT: WorkContextSource = {
  status: 'ready',
  organizations: [],
  selection: {},
  isReal: false,
};

/** The shell provides the source; outside the shell there is no context. */
export const WorkContextContext = createContext<WorkContextSource>(NO_CONTEXT);

export function useWorkContext(): WorkContextSource {
  return useContext(WorkContextContext);
}
