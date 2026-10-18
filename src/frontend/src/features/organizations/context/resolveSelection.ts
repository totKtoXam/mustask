import {
  defaultEntry,
  type WorkContextSelection,
  type WorkContextStatus,
} from '../../../shared/lib/workContext';
import type { TeamListItem } from '../api/organizationsApi';
import type { AddressContext } from './addressContext';

/**
 * The switcher's resolution (design § 3.4–§ 3.6, plan-frontend § 4.2), pure:
 * the lists and their statuses, the address and the remembered or chosen
 * selection in; the effective selection, the per-level statuses and the
 * fallback out. The hook around it reads the lists and keeps the state.
 *
 * The team level is the organization's, whatever its projects (feat-040 BR-TS-10).
 * `chosen.teamId` is a team **chosen** — in the switcher, in the head's menu or by a team
 * address; the effective team is that one while the organization's teams hold it, else the
 * level's default entry, the **fallback**, which is derived every time and never becomes a
 * choice by being shown.
 */

/** One level's list as read, in the order the server gives it. */
export interface LevelList {
  status: WorkContextStatus;
  /**
   * `isMember` and `isDefault` ride the team level only: the user is in the
   * team (feat-040 api-contract § 4.1), and the effective project's settings
   * name it, or it is the project's only team (the list's mark; mod-020). The
   * levels above carry none.
   */
  items: readonly { id: string; name: string; isMember?: boolean; isDefault?: boolean }[];
}

export const LOADING_LIST: LevelList = { status: 'loading', items: [] };

/**
 * The organization's teams as the team level reads them (feat-040 BR-TS-10, doc-013 OQ-3 A): the
 * user's own first, each group in the server's order (by name), with no headings. `defaultTeamId`
 * is the effective project's default team, if the project's linked teams are read and name one.
 */
export function organizationTeamLevel(
  teams: readonly Pick<TeamListItem, 'id' | 'name' | 'isMember'>[],
  defaultTeamId?: string,
): LevelList['items'] {
  const level = teams.map((team) => ({
    id: team.id,
    name: team.name,
    isMember: Boolean(team.isMember),
    isDefault: team.id === defaultTeamId,
  }));
  return [...level.filter((team) => team.isMember), ...level.filter((team) => !team.isMember)];
}

function holds(list: LevelList, id: string | undefined): boolean {
  return id !== undefined && list.items.some((item) => item.id === id);
}

/**
 * A level's effective id: the chosen one while its ready list holds it, else
 * the list's default entry — the first entry, and for the team level the effective
 * project's default team, else the first one the user is a member of
 * (`defaultEntry`; mod-020, mod-016 D-5, design § 8).
 * A level that is loading or failed has none: a failure is not a deletion, and
 * the remembered id is kept (design § 3.2).
 */
export function effectiveId(list: LevelList, chosenId: string | undefined): string | undefined {
  if (list.status !== 'ready') {
    return undefined;
  }
  return holds(list, chosenId) ? chosenId : defaultEntry(list.items)?.id;
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
 * the carried levels take the address's ids. A level the address does not carry
 * keeps the current choice when it belongs to the new level above; the
 * resolution then gives it its first entry otherwise.
 *
 * A project address (`…/projects/:p/…`) sets the organization and the project and keeps a
 * chosen team of the same organization. A team address (`…/teams/:t/…`, feat-040 BR-TS-1)
 * sets the organization and the team once the organization's active teams hold it, and leaves
 * the project level at its current selection — after an organization change, at its first
 * entry (mod-002 BR-M1-24).
 */
export function addressSync(input: {
  address: AddressContext;
  chosen: WorkContextSelection;
  organizations: LevelList;
  /** The address's organization's active projects, when it carries a project. */
  addressProjects: LevelList;
  /** The address's organization's active teams, when it carries a team. */
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
  if (address.teamId) {
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
        projectId: sameOrganization ? chosen.projectId : undefined,
        teamId: address.teamId,
      },
    };
  }
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
  return {
    kind: 'set',
    selection: {
      organizationId: address.organizationId,
      projectId: address.projectId,
      // A project change keeps a chosen team (feat-040 BR-TS-10).
      teamId: sameOrganization ? chosen.teamId : undefined,
    },
  };
}

export interface ResolveInput {
  chosen: WorkContextSelection;
  organizations: LevelList;
  /** The active projects of an organization, as far as they are read. */
  projectsOf(organizationId: string): LevelList;
  /** The active teams of an organization, as far as they are read: the team level. */
  teamsOf(organizationId: string): LevelList;
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
   * level below settled: what to remember instead (a team only when it was chosen and is
   * still held), and the notice's names (design § 3.6). `names` is `null` when the gone
   * entry's level is left empty: the switcher's own text says it and no notice is shown.
   */
  replacement: { selection: WorkContextSelection; names: readonly string[] | null } | null;
  /**
   * The team the user chose, while the organization's teams hold it: what is remembered and kept
   * across a project change. A fallback team is not it.
   */
  chosenTeamId: string | undefined;
}

function nameOf(list: LevelList, id: string | undefined): string | undefined {
  return list.items.find((item) => item.id === id)?.name;
}

/**
 * The effective selection, top-down (plan-frontend § 4.2):
 *
 * - each level keeps its chosen id while its ready list holds it, else takes
 *   its default entry (the first; for the team level the effective project's default team,
 *   else the user's own first, `defaultEntry`), and the levels below take theirs;
 * - a level falls back when it had a chosen id, its parent is the one that id
 *   was chosen under, and its ready list no longer holds it. The
 *   replacement is reported once every level has settled, naming the
 *   levels that hold an entry; a level left empty is reported without
 *   names. No chosen id (a first visit) never falls back. The team's parent is the
 *   organization (feat-040 BR-TS-10).
 */
export function resolveSelection(input: ResolveInput): Resolution {
  const { chosen, organizations } = input;
  const organizationId = effectiveId(organizations, chosen.organizationId);
  const projects = organizationId ? input.projectsOf(organizationId) : undefined;
  const projectId = projects ? effectiveId(projects, chosen.projectId) : undefined;
  const teams = organizationId ? input.teamsOf(organizationId) : undefined;
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
  const chosenTeamId = teams && holds(teams, chosen.teamId) ? chosen.teamId : undefined;

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
    teams?.status === 'ready' &&
    organizationId === chosen.organizationId &&
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
    replacement = {
      // A team that was never chosen is a fallback: it is derived again, not remembered.
      selection: { organizationId, projectId, teamId: chosenTeamId },
      names: leftEmpty ? null : names,
    };
  }

  return {
    selection,
    status: organizations.status,
    levelStatus,
    settled,
    replacement,
    chosenTeamId,
  };
}

export function sameSelection(first: WorkContextSelection, second: WorkContextSelection): boolean {
  return (
    first.organizationId === second.organizationId &&
    first.projectId === second.projectId &&
    first.teamId === second.teamId
  );
}
