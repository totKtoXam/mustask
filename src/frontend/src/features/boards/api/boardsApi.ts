// The boards module over HTTP, strictly per
// docs/features/mod-006-boards-backlogs/api-contract.md § 2–§ 3: one function
// per route, camelCase as all of Mustask.Api. Ids come from the address or
// from an answer and are sent as received, re-encoded in the path and never
// validated (§ 0.2): a malformed one answers its level's not-found code. The
// card move and the creation are mod-003's writes (§ 6), reached through that
// slice's barrel, not here.
import { apiFetch } from '../../../shared/api/httpClient';
import type {
  WorkItemAssignee,
  WorkItemStateReference,
  WorkItemTypeReference,
} from '../../workitems';
import type { BoardLevelCode } from '../lib/levels';

/** § 2.1: the team, and its default area's path (mod-002 DC-9) the dialog quotes. */
export interface BoardTeam {
  id: string;
  name: string;
  defaultArea: { id: string; path: string };
}

/** § 2.2: one type of the level, with the state a new item of it starts in. */
export interface BoardLevelType extends WorkItemTypeReference {
  initialState: WorkItemStateReference;
}

/** § 2.2: the level as read; `types` in the process's display order. */
export interface BoardLevel {
  code: string;
  name: string;
  types: BoardLevelType[];
}

/** § 2.3: a backlog row, a board card. */
export interface BacklogItem {
  /** «Порядок»: 1-based and dense over the backlog; on a card, its backlog position. */
  order: number;
  id: string;
  identifier: string;
  number: number;
  type: WorkItemTypeReference;
  title: string;
  /** `null`: «Не назначен». */
  assignee: WorkItemAssignee | null;
  state: WorkItemStateReference;
}

/** § 3.1's answer: the whole backlog in its order, `[]` when empty. */
export interface Backlog {
  team: BoardTeam;
  level: BoardLevel;
  items: BacklogItem[];
}

/** § 3.2's three positions (BR-M4-25). */
export type ReorderPosition = 'top' | 'bottom' | 'after';

/** § 3.2's body: `afterItemId` with `after` only. */
export type ReorderBody =
  | { itemId: string; position: 'top' | 'bottom' }
  | { itemId: string; position: 'after'; afterItemId: string };

/** § 3.3 `targets`: the state a drop of a card of the type writes here, `null` for none. */
export interface BoardColumnTarget {
  typeId: string;
  stateId: string | null;
}

/** § 3.3: one column, derived from the process on every read (BR-M4-2). */
export interface BoardColumn {
  /** `<category code>-<k>`, stable for the process. */
  key: string;
  /** The column's title: the category's label for k = 1, else the state it maps. */
  header: string;
  /** The column's category; its code is never shown. */
  category: { code: string; label: string };
  /** One entry per type of the level, in display order. */
  targets: BoardColumnTarget[];
  /** The cards, in the backlog's order; `order` is the backlog position. */
  items: BacklogItem[];
}

/** § 3.3's answer: every column, an empty one with `[]`. */
export interface Board {
  team: BoardTeam;
  level: BoardLevel;
  columns: BoardColumn[];
}

const segment = encodeURIComponent;

function teamApiPath(organizationId: string, projectId: string, teamId: string): string {
  return `/api/organizations/${segment(organizationId)}/projects/${segment(projectId)}/teams/${segment(teamId)}`;
}

function backlogApiPath(
  organizationId: string,
  projectId: string,
  teamId: string,
  level: BoardLevelCode,
): string {
  return `${teamApiPath(organizationId, projectId, teamId)}/backlogs/${level}`;
}

/** § 3.1: a team's backlog of one level. */
export function getBacklog(
  organizationId: string,
  projectId: string,
  teamId: string,
  level: BoardLevelCode,
): Promise<Backlog> {
  return apiFetch(backlogApiPath(organizationId, projectId, teamId, level));
}

/** § 3.2: 204 with no body; the panel reads § 3.1 again. */
export function reorderBacklog(
  organizationId: string,
  projectId: string,
  teamId: string,
  level: BoardLevelCode,
  body: ReorderBody,
): Promise<void> {
  return apiFetch(`${backlogApiPath(organizationId, projectId, teamId, level)}/reorder`, {
    method: 'POST',
    body,
  });
}

/** § 3.3: a team's board of one level. */
export function getBoard(
  organizationId: string,
  projectId: string,
  teamId: string,
  level: BoardLevelCode,
): Promise<Board> {
  return apiFetch(`${teamApiPath(organizationId, projectId, teamId)}/boards/${level}`);
}
