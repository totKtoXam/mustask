import type { WorkContextSelection, WorkContextStatus } from '../../../shared/lib/workContext';
import type { AddressContext } from './addressContext';

/**
 * The switcher's resolution (design § 3.4–§ 3.6, plan-frontend § 4.2), pure:
 * the lists and their statuses, the address and the remembered or chosen
 * selection in; the effective selection, the per-level statuses and the
 * fallback out. The hook around it reads the lists and keeps the state.
 */

/** One level's list as read, in the order the server gives it. */
export interface LevelList {
  status: WorkContextStatus;
  items: readonly { id: string; name: string }[];
}

export const LOADING_LIST: LevelList = { status: 'loading', items: [] };

function holds(list: LevelList, id: string | undefined): boolean {
  return id !== undefined && list.items.some((item) => item.id === id);
}

/**
 * A level's effective id: the chosen one while its ready list holds it, else
 * the list's first entry (the project's first team is its default, design
 * § 3.5). A level that is loading or failed has none: a failure is not a
 * deletion, and the remembered id is kept (design § 3.2).
 */
export function effectiveId(list: LevelList, chosenId: string | undefined): string | undefined {
  if (list.status !== 'ready') {
    return undefined;
  }
  return holds(list, chosenId) ? chosenId : list.items[0]?.id;
}

/** What the address does to the selection once its lists are read. */
export type AddressSync =
  /** A list it needs is not read yet: decide later. */
  | { kind: 'wait' }
  /** The address carries nothing, or a carried level is not found: it sets nothing (mod-015 BR-18). */
  | { kind: 'none' }
  /** Every carried level is found: the carried levels take the address's ids. */
  | { kind: 'set'; selection: WorkContextSelection };

/**
 * The address's part (design § 3.4, P-1 (a)): when the address carries a
 * level and every carried level is found — its active list holds the id —
 * the carried levels take the address's ids. A level below the carried ones
 * keeps the current choice when it belongs to the new level above; the
 * resolution then gives it its first entry otherwise. A team's page carries
 * the organization and the project only (`addressContext`). A team-level
 * address — mod-006's backlogs and boards, `…/teams/:t/…` — carries the team
 * too, and sets it once the project's active teams hold it (mod-006 spec
 * § Screens and copy: the switcher follows the address).
 */
export function addressSync(input: {
  address: AddressContext;
  chosen: WorkContextSelection;
  organizations: LevelList;
  /** The address's organization's active projects, when it carries a project. */
  addressProjects: LevelList;
  /** The address's project's active teams, when it carries a team. */
  addressTeams?: LevelList;
}): AddressSync {
  const { address, chosen, organizations, addressProjects, addressTeams = LOADING_LIST } = input;
  if (!address.organizationId) {
    return { kind: 'none' };
  }
  if (organizations.status !== 'ready') {
    return { kind: 'wait' };
  }
  if (!holds(organizations, address.organizationId)) {
    return { kind: 'none' };
  }
  const sameOrganization = chosen.organizationId === address.organizationId;
  if (!address.projectId) {
    return {
      kind: 'set',
      selection: sameOrganization
        ? { ...chosen, organizationId: address.organizationId }
        : { organizationId: address.organizationId },
    };
  }
  if (addressProjects.status !== 'ready') {
    return { kind: 'wait' };
  }
  if (!holds(addressProjects, address.projectId)) {
    return { kind: 'none' };
  }
  const sameProject = sameOrganization && chosen.projectId === address.projectId;
  if (!address.teamId) {
    return {
      kind: 'set',
      selection: {
        organizationId: address.organizationId,
        projectId: address.projectId,
        teamId: sameProject ? chosen.teamId : undefined,
      },
    };
  }
  if (addressTeams.status !== 'ready') {
    return { kind: 'wait' };
  }
  if (!holds(addressTeams, address.teamId)) {
    return { kind: 'none' };
  }
  return {
    kind: 'set',
    selection: {
      organizationId: address.organizationId,
      projectId: address.projectId,
      teamId: address.teamId,
    },
  };
}

export interface ResolveInput {
  chosen: WorkContextSelection;
  organizations: LevelList;
  /** The active projects of an organization, as far as they are read. */
  projectsOf(organizationId: string): LevelList;
  /** The active teams of a project, as far as they are read. */
  teamsOf(organizationId: string, projectId: string): LevelList;
}

export interface Resolution {
  /** What the switcher shows and the shell's navigation derives from. */
  selection: WorkContextSelection;
  /** The organization level's status: the source's `status`. */
  status: WorkContextStatus;
  /** The lower levels' statuses, `ready` where there is no level to read. */
  levelStatus: { project: WorkContextStatus; team: WorkContextStatus };
  /** Every level is read (ready, or empty because the level above has nothing). */
  settled: boolean;
  /**
   * A remembered or chosen entry that is gone and is now replaced, every
   * level below settled: what to remember instead, and the notice's names
   * (design § 3.6). `names` is `null` when the gone entry's level is left
   * empty: the switcher's own text says it and no notice is shown.
   */
  replacement: { selection: WorkContextSelection; names: readonly string[] | null } | null;
}

function nameOf(list: LevelList, id: string | undefined): string | undefined {
  return list.items.find((item) => item.id === id)?.name;
}

/**
 * The effective selection, top-down (plan-frontend § 4.2):
 *
 * - each level keeps its chosen id while its ready list holds it, else takes
 *   the first entry, and the levels below take their first entries;
 * - a level falls back when it had a chosen id, its parent is the one that id
 *   was chosen under, and its ready list no longer holds it. The
 *   replacement is reported once every level below has settled, naming the
 *   levels that hold an entry; a level left empty is reported without
 *   names. No chosen id (a first visit) never falls back.
 */
export function resolveSelection(input: ResolveInput): Resolution {
  const { chosen, organizations } = input;
  const organizationId = effectiveId(organizations, chosen.organizationId);
  const projects = organizationId ? input.projectsOf(organizationId) : undefined;
  const projectId = projects ? effectiveId(projects, chosen.projectId) : undefined;
  const teams = organizationId && projectId ? input.teamsOf(organizationId, projectId) : undefined;
  const teamId = teams ? effectiveId(teams, chosen.teamId) : undefined;

  const levelStatus = {
    project: projects?.status ?? 'ready',
    team: teams?.status ?? 'ready',
  } as const;
  const settled =
    organizations.status === 'ready' &&
    levelStatus.project === 'ready' &&
    levelStatus.team === 'ready';
  const selection: WorkContextSelection = { organizationId, projectId, teamId };

  // Which level, if any, lost its chosen entry under the parent it was chosen in.
  const organizationGone =
    organizations.status === 'ready' &&
    chosen.organizationId !== undefined &&
    !holds(organizations, chosen.organizationId);
  const projectGone =
    !organizationGone &&
    projects?.status === 'ready' &&
    organizationId === chosen.organizationId &&
    chosen.projectId !== undefined &&
    !holds(projects, chosen.projectId);
  const teamGone =
    !organizationGone &&
    !projectGone &&
    teams?.status === 'ready' &&
    projectId === chosen.projectId &&
    chosen.teamId !== undefined &&
    !holds(teams, chosen.teamId);

  let replacement: Resolution['replacement'] = null;
  if ((organizationGone || projectGone || teamGone) && settled) {
    const leftEmpty =
      (organizationGone && organizationId === undefined) ||
      (projectGone && projectId === undefined) ||
      (teamGone && teamId === undefined);
    const names = [
      nameOf(organizations, organizationId),
      projects ? nameOf(projects, projectId) : undefined,
      teams ? nameOf(teams, teamId) : undefined,
    ].filter((name): name is string => name !== undefined);
    replacement = { selection, names: leftEmpty ? null : names };
  }

  return {
    selection,
    status: organizations.status,
    levelStatus,
    settled,
    replacement,
  };
}

export function sameSelection(first: WorkContextSelection, second: WorkContextSelection): boolean {
  return (
    first.organizationId === second.organizationId &&
    first.projectId === second.projectId &&
    first.teamId === second.teamId
  );
}
