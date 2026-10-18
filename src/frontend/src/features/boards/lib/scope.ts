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

const NO_TEAM_IDS: readonly string[] = [];

/**
 * The team ids a view is filtered by (mod-017 BR-OT-8): those the page address carries on the
 * project's views (mod-020), none on a team's — the team is the filter there, and a `teamId`
 * left in its address by hand or by a link narrows nothing and is not sent.
 */
export function filterTeamIds(
  scope: BoardScope,
  fromAddress: readonly string[],
): readonly string[] {
  return scope.kind === 'project' ? fromAddress : NO_TEAM_IDS;
}

/** A backlog's or a board's answer under either scope: `team` on a team's, `project` on the project's. */
export type SurfaceRead = Backlog | ProjectBacklog | Board | ProjectBoard;

/**
 * What a creation starts with (mod-017 BR-OT-3): on a team's backlog or board, that team; on
 * the project's, nothing — the dialog takes the project's default team.
 */
export function startTeamOf(scope: BoardScope): string | undefined {
  return scope.kind === 'team' ? scope.teamId : undefined;
}

/** The name the head's `h1` reads from the answer: the team's, or the project's (design § 3.3). */
export function readName(read: SurfaceRead | undefined): string | undefined {
  if (read === undefined) {
    return undefined;
  }
  return 'team' in read ? read.team.name : read.project.name;
}

/** What a creation needs of the answer: the level, whose types it offers. */
export function creationSurface(
  read: SurfaceRead | undefined,
): { level: SurfaceRead['level'] } | undefined {
  return read === undefined ? undefined : { level: read.level };
}
