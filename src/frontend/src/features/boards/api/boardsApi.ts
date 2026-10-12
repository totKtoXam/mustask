// The boards module over HTTP, strictly per
// docs/features/mod-006-boards-backlogs/api-contract.md § 2–§ 3: one function
// per route, camelCase as all of Mustask.Api. Ids come from the address or
// from an answer and are sent as received, re-encoded in the path and never
// validated (§ 0.2): a malformed one answers its level's not-found code. The
// card move and the creation are mod-003's writes (§ 6), reached through that
// slice's barrel, not here. feat-001's api-contract § 4 adds the backlog's
// tree: each row's `parent` and its `children`; the board does not change
// (BR-H-17). feat-022's api-contract § 5 adds «Приоритет» to every row, nested
// ones included, and to every card. feat-027's api-contract § 1–§ 2 makes that
// tree the whole one — every level below the page's, at full depth, in the one
// answer — numbers only the top-level rows, and adds `effort`, `businessValue`
// and `valueArea` to every row.
import { apiFetch } from '../../../shared/api/httpClient';
import type {
  WorkItemAssignee,
  WorkItemRef,
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

/**
 * § 2.3: a board card; on the backlog, a row's own fields — feat-001 § 4 and
 * feat-027 § 2 add its parent, its tree and its three planning values.
 */
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
  /** feat-022 § 5: the item's «Приоритет», 1 (the highest) to 4. */
  priority: number;
}

/**
 * feat-027 api-contract § 2.2: a nested row — mod-006's item without `order`
 * (a nested row has no «Порядок», BR-T-2) — with its parent, the three planning
 * values and its own children, recursively.
 */
export interface BacklogTreeItem extends Omit<BacklogItem, 'order'> {
  /**
   * feat-027 § 2.1: «Трудоёмкость» in its shortest form (`2.5`, `20`), or
   * `null` — empty, or a type without the field (only an epic and a feature
   * have it).
   */
  effort: number | null;
  /** feat-027 § 2.1: «Бизнес-ценность», an integer, or `null` as `effort`. */
  businessValue: number | null;
  /**
   * feat-027 § 2.1: «Вид ценности», the list key `Business` or
   * `Architectural`; `null` on a task and a bug, which do not have it.
   */
  valueArea: string | null;
  /**
   * The stored parent, whatever its level or team (BR-H-13), or `null` when
   * there is none or it is in «Удалена» (BR-H-8). The table draws no «Родитель»
   * column (feat-027 D-7); the panel keeps this for a drop's seen parent
   * (BR-H-4). On a nested row it is always the row it stands beneath.
   */
  parent: WorkItemRef | null;
  /**
   * The row's children by feat-027 § 1 — its direct children on the team,
   * live, of any level below its own or its own, each with its own children,
   * at most 64 rows deep (a chain of parents holds at most 64 items, D-9;
   * api-contract § 1) — in that section's order; `[]` when there is none, so
   * a row has an expand control exactly when this is not empty (BR-T-7).
   */
  children: BacklogTreeItem[];
}

/** § 3.1 with feat-027 § 2.1: a top-level row, with its «Порядок» over the top-level rows. */
export interface BacklogRow extends BacklogTreeItem {
  order: number;
}

/**
 * § 3.1's answer: the page's top-level rows in their order, each with the whole
 * tree beneath it (feat-027 § 2); `[]` when empty.
 */
export interface Backlog {
  team: BoardTeam;
  level: BoardLevel;
  items: BacklogRow[];
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
  /** The column's title: the name of the state it maps for the first type, in display order, that maps one (bug-004 D-3). */
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
