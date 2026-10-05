import { createContext, useContext } from 'react';

/**
 * The organization / project / team context (mod-015 BR-11, BR-18,
 * design § 5). The shell's switcher reads it from a context source; mod-015
 * shipped a fixed placeholder source, and mod-002 supplies the real lists
 * behind the same interface (design § 5.7), extended by mod-002 D-n with a
 * status and a retry per level and the fallback notice. Later slices read
 * the current context through `useWorkContext()`.
 */

export type WorkContextLevel = 'organization' | 'project' | 'team';

export interface WorkContextTeam {
  id: string;
  name: string;
}

export interface WorkContextProject {
  id: string;
  name: string;
  /** In the source's order; the project's first team is its default (D-19). */
  teams: readonly WorkContextTeam[];
}

export interface WorkContextOrganization {
  id: string;
  name: string;
  projects: readonly WorkContextProject[];
}

export interface WorkContextSelection {
  organizationId?: string;
  projectId?: string;
  teamId?: string;
}

export type WorkContextStatus = 'loading' | 'failed' | 'ready';

/** The levels below the first, which load and fail on their own (mod-002 D-n). */
export type WorkContextLowerLevel = Exclude<WorkContextLevel, 'organization'>;

/**
 * mod-015 § 5.4's fallback notice: the names of the selection that replaced
 * a remembered or chosen one that is gone, top-down, a level with nothing in
 * it left out. `id` tells one notice from the next.
 */
export interface WorkContextFallback {
  id: number;
  names: readonly string[];
}

/**
 * The hand-over to mod-002 (mod-015 plan-frontend.md § 4.3), as amended by
 * mod-002 D-n and D-o (mod-015 design § 5.7).
 */
export interface WorkContextSource {
  /** The organization level's status: the whole switcher's before mod-002. */
  status: WorkContextStatus;
  /**
   * The project and team levels, each loading or failing on its own once the
   * level above is shown (mod-002 D-n, design § 3.2). Absent, or a level
   * absent: ready.
   */
  levelStatus?: Partial<Record<WorkContextLowerLevel, WorkContextStatus>>;
  /** Ordered by the source; the switcher renders them as given. */
  organizations: readonly WorkContextOrganization[];
  selection: WorkContextSelection;
  /**
   * `false` for the placeholder dataset: organization-, project- and
   * team-scoped navigation entries stay hidden until a real context exists
   * (BR-8 interim clause, D-18).
   */
  isReal: boolean;
  /**
   * The organization list page. Undefined: «Все организации» is not shown
   * and «Нет организаций» is plain text (BR-11, D-20).
   */
  organizationListPath?: string;
  /** Chooses an entry at a level and resets the levels below to their first entries. */
  select(level: WorkContextLevel, id: string): void;
  /** «Повторить» of the level that failed; no level: the organization level. */
  retry?(level?: WorkContextLevel): void;
  /** The fallback notice to show, or none (mod-015 § 5.4). */
  fallback?: WorkContextFallback | null;
  /** The notice was shown and closed. */
  dismissFallback?(): void;
}

/**
 * Where a choice in the switcher moves the page (mod-002 D-o, design § 2.2):
 * `app/` builds it from the navigation model and hands it to the source,
 * because a slice does not import `app/`.
 */
export type SwitchTargetResolver = (request: {
  /** The address on screen. */
  pathname: string;
  /** The levels the address carries, with its own ids. */
  carried: WorkContextSelection;
  /** The selection after the choice. */
  next: WorkContextSelection;
}) => string;

/** The selection resolved to its entries; a level with nothing to choose is undefined. */
export interface WorkContext {
  organization?: WorkContextOrganization;
  project?: WorkContextProject;
  team?: WorkContextTeam;
}

function pick<T extends { id: string }>(
  entries: readonly T[],
  id: string | undefined,
): T | undefined {
  return entries.find((entry) => entry.id === id) ?? entries[0];
}

/**
 * Resolves a selection against the lists. An id the lists do not hold falls
 * back to the level's first entry, so the switcher always shows a choice
 * when there is one; mod-002 owns the notice for a remembered selection that
 * is gone (design § 5.4).
 */
export function resolveWorkContext(
  organizations: readonly WorkContextOrganization[],
  selection: WorkContextSelection,
): WorkContext {
  const organization = pick(organizations, selection.organizationId);
  const project = organization ? pick(organization.projects, selection.projectId) : undefined;
  const team = project ? pick(project.teams, selection.teamId) : undefined;
  return { organization, project, team };
}

function selectionOf(context: WorkContext): WorkContextSelection {
  return {
    organizationId: context.organization?.id,
    projectId: context.project?.id,
    teamId: context.team?.id,
  };
}

/** The first organization, its first project and that project's first team. */
export function firstSelection(
  organizations: readonly WorkContextOrganization[],
): WorkContextSelection {
  return selectionOf(resolveWorkContext(organizations, {}));
}

/**
 * Chooses `id` at `level` and resets every level below it to its first entry
 * (BR-11): the first team is the project's default until mod-016 (D-19).
 */
export function selectInWorkContext(
  organizations: readonly WorkContextOrganization[],
  selection: WorkContextSelection,
  level: WorkContextLevel,
  id: string,
): WorkContextSelection {
  switch (level) {
    case 'organization':
      return selectionOf(resolveWorkContext(organizations, { organizationId: id }));
    case 'project':
      return selectionOf(
        resolveWorkContext(organizations, {
          organizationId: selection.organizationId,
          projectId: id,
        }),
      );
    case 'team':
      return selectionOf(
        resolveWorkContext(organizations, {
          organizationId: selection.organizationId,
          projectId: selection.projectId,
          teamId: id,
        }),
      );
  }
}

const NO_CONTEXT: WorkContextSource = {
  status: 'ready',
  organizations: [],
  selection: {},
  isReal: false,
  select: () => {},
};

/** The shell provides the source; outside the shell there is no context. */
export const WorkContextContext = createContext<WorkContextSource>(NO_CONTEXT);

export function useWorkContext(): WorkContextSource {
  return useContext(WorkContextContext);
}
