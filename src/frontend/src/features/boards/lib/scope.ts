// What a backlog or a board is of (feat-039 BR-PB-1): one team's, over the items it owns in every
// project it works on (mod-006; feat-040 BR-TS-2), or one project's, over every item of the project
// whatever its team, in the project's own order (BR-PB-2, BR-PB-4). The pages, the table, the
// columns and the hooks take the scope instead of a team's id, so the project's views are the
// team's views over another read. A team's scope names the team alone — the address has no project
// (feat-040 BR-TS-1) — and every call about one item goes to the item's own project.
import type { Backlog, Board, BoardProject, ProjectBacklog, ProjectBoard } from '../api/boardsApi';

export type BoardScope =
  | { readonly kind: 'team'; readonly teamId: string }
  | { readonly kind: 'project'; readonly projectId: string };

/**
 * A team's scope. A page keeps one value for as long as it stands (the callbacks that
 * depend on it must not change with every render), so it makes it once per team.
 */
export function teamScope(teamId: string): BoardScope {
  return { kind: 'team', teamId };
}

/** The whole project's scope; made once per project, as the team's. */
export function projectScope(projectId: string): BoardScope {
  return { kind: 'project', projectId };
}

/** The team's id, or `null` for a project: what a hook's dependencies compare. */
export function scopeTeamId(scope: BoardScope): string | null {
  return scope.kind === 'team' ? scope.teamId : null;
}

/** The project's id, or `null` for a team: a team's views have no project of their own. */
export function scopeProjectId(scope: BoardScope): string | null {
  return scope.kind === 'project' ? scope.projectId : null;
}

/** The two filters a page's address can carry, each as the values it holds. */
export interface AddressFilters {
  teamIds: readonly string[];
  projectIds: readonly string[];
}

/**
 * The values a view is filtered by: on the project's views the team filter (mod-017 BR-OT-8,
 * mod-020), on a team's the project filter (feat-040 BR-TS-8). The other dimension a link or a
 * hand-edited address may carry narrows nothing and is not sent: a team is its own filter on its
 * views, and the project's views have no project filter.
 */
export function filterIdsOf(scope: BoardScope, fromAddress: AddressFilters): readonly string[] {
  return scope.kind === 'project' ? fromAddress.teamIds : fromAddress.projectIds;
}

/** A backlog's or a board's answer under either scope: `team` on a team's, `project` on the project's. */
export type SurfaceRead = Backlog | ProjectBacklog | Board | ProjectBoard;

/** The name the head's `h1` reads from the answer: the team's, or the project's (design § 3.3). */
export function readName(read: SurfaceRead | undefined): string | undefined {
  if (read === undefined) {
    return undefined;
  }
  return 'team' in read ? read.team.name : read.project.name;
}

/** The team's linked active projects of a team view's answer; none for the project's. */
export function readProjects(read: SurfaceRead | undefined): readonly BoardProject[] | undefined {
  return read !== undefined && 'projects' in read ? read.projects : undefined;
}

/**
 * What a creation needs of the answer: the level, whose types it offers, and — on a team's views —
 * the projects it may create in (feat-040 BR-TS-9), which a project's views lack: the project is
 * the page's.
 */
export function creationSurface(
  read: SurfaceRead | undefined,
): { level: SurfaceRead['level']; projects?: readonly BoardProject[] } | undefined {
  if (read === undefined) {
    return undefined;
  }
  const projects = readProjects(read);
  return projects === undefined ? { level: read.level } : { level: read.level, projects };
}
