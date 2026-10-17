// What a backlog or a board is of (feat-039 BR-PB-1): one team's, over the items
// its areas own (mod-006), or the whole project's, over every item of the project
// whatever its team, in the project's own order (BR-PB-2, BR-PB-4). The pages, the
// table, the columns and the hooks take the scope instead of a team's id, so the
// project's views are the team's views over another read.
import type { Backlog, Board, ProjectBacklog, ProjectBoard } from '../api/boardsApi';

export type BoardScope =
  { readonly kind: 'team'; readonly teamId: string } | { readonly kind: 'project' };

/** The whole project's scope: it carries nothing but itself, so one value serves. */
export const PROJECT_SCOPE: BoardScope = { kind: 'project' };

/**
 * A team's scope. A page keeps one value for as long as it stands (the callbacks that
 * depend on it must not change with every render), so it makes it once per team.
 */
export function teamScope(teamId: string): BoardScope {
  return { kind: 'team', teamId };
}

/** The team's id, or `null` for the whole project: what a hook's dependencies compare. */
export function scopeTeamId(scope: BoardScope): string | null {
  return scope.kind === 'team' ? scope.teamId : null;
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

/**
 * What a creation needs of the answer — the team and the level — or `undefined` for the
 * project's answer: a creation from a project view starts with the project's default
 * team, which `mod-017-work-item-owning-team` brings, so there is none (BR-PB-6).
 */
export function creationSurface<T extends SurfaceRead>(
  read: T | undefined,
): Extract<T, { team: unknown }> | undefined {
  return read !== undefined && 'team' in read ? (read as Extract<T, { team: unknown }>) : undefined;
}
