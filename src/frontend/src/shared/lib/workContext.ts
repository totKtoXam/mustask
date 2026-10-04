import { createContext, useContext } from 'react';

/**
 * The organization / project / team context (mod-015 BR-11, BR-18,
 * design § 5). The shell's switcher reads it from a context source; this
 * task ships a fixed placeholder source, and mod-002 replaces it with real
 * lists behind the same interface (design § 5.7). Later slices read the
 * current context through `useWorkContext()`.
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

/** The hand-over to mod-002 (plan-frontend.md § 4.3). */
export interface WorkContextSource {
  status: WorkContextStatus;
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
  /** «Повторить» in the failed state. */
  retry?(): void;
}

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
