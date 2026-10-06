// The work items module over HTTP, strictly per
// docs/features/mod-003-work-item-core/api-contract.md § 1–§ 2: one function
// per route, camelCase as all of Mustask.Api. Ids and the identifier come from
// the address or from an answer and are sent as received, re-encoded in the
// path and never validated (§ 0.2): a malformed one answers its level's
// not-found code. The process and area reads the screens also use are mod-001's
// and mod-002's, unchanged (§ 3), through those slices' barrels. feat-001's
// api-contract § 2–§ 3 adds the hierarchy: the item's `parent` and
// `children`, the save's `parent`, and the «Родитель» picker's search.
import { apiFetch } from '../../../shared/api/httpClient';

/** § 1.1 `type`: as mod-001's process read gives it. */
export interface WorkItemTypeReference {
  id: string;
  key: string;
  name: string;
  /** The appearance token the theme draws (`epic`, `feature`, `story`, `task`, `bug`). */
  appearance: string;
}

/** § 1.1 `state.category`: the code is never shown, the label is. */
export interface WorkItemStateCategory {
  code: string;
  label: string;
}

/** § 1.1 `state`. */
export interface WorkItemStateReference {
  id: string;
  name: string;
  category: WorkItemStateCategory;
}

/**
 * § 1.1 `assignee`: `displayName` and `email` are both `null` when the user
 * directory does not know the id («Неизвестный пользователь»); `email` is `""`
 * for a known user without one.
 */
export interface WorkItemAssignee {
  id: string;
  displayName: string | null;
  email: string | null;
}

/** § 1.1 `area`: its current path (mod-002 DC-9), the root by its own name. */
export interface WorkItemArea {
  id: string;
  path: string;
}

/**
 * feat-001 api-contract § 2.1: a parent, a child or a picker option. Every
 * field means what it means in `WorkItem`.
 */
export interface WorkItemRef {
  id: string;
  identifier: string;
  type: WorkItemTypeReference;
  title: string;
  state: WorkItemStateReference;
}

/** § 1.1: the form's item — what create, read and save answer. */
export interface WorkItem {
  id: string;
  identifier: string;
  number: number;
  projectId: string;
  type: WorkItemTypeReference;
  title: string;
  /** `""` when empty, never `null` (§ 0.6). */
  description: string;
  state: WorkItemStateReference;
  /** `null`: «Не назначен». */
  assignee: WorkItemAssignee | null;
  area: WorkItemArea;
  createdBy: string;
  createdAt: string;
  changedAt: string;
  /**
   * feat-001 § 2.2: the stored parent, `null` for «Нет родителя». A parent in
   * «Удалена» is answered too, with that state.
   */
  parent: WorkItemRef | null;
  /**
   * feat-001 § 2.2: the direct children, those in «Удалена» excepted, by level
   * rank descending, then by number — shown as received; `[]` for none.
   */
  children: WorkItemRef[];
}

/** § 1.2: a row of the list; feat-001 § 2.2 leaves it without the hierarchy. */
export type WorkItemListItem = Omit<
  WorkItem,
  'projectId' | 'description' | 'createdBy' | 'createdAt' | 'parent' | 'children'
>;

/** § 2.1: ADR-0015's `Paging<T>`; `count` is the project's item count. */
export interface WorkItemPage {
  count: number;
  data: WorkItemListItem[];
}

/** § 2.1: the list's fixed page size; the route takes `page` only. */
export const WORK_ITEMS_PAGE_SIZE = 50;

/** § 2.2's body. */
export interface CreateWorkItemBody {
  /** The submission's idempotency key (BR-M3-26), the same on every retry of it. */
  requestId: string;
  typeId: string;
  title: string;
  /**
   * mod-006 api-contract § 7: the team whose backlog or board the item is
   * created from — it takes that team's default area. Absent: the root.
   */
  teamId?: string;
}

/**
 * feat-001 § 2.3: one parent write. `parentId: null` clears the parent;
 * `seenParentId` is the parent the user saw — the form's loaded `parent.id` —
 * or `null` when they saw none (BR-H-4).
 */
export interface ParentChange {
  parentId: string | null;
  seenParentId: string | null;
}

/**
 * § 2.4's body: only the fields that changed. `assignee: { userId: null }`
 * clears the assignee; an absent `assignee` keeps it. feat-001 § 3.3: an
 * absent `parent` leaves the parent as it is, whoever changed it meanwhile.
 */
export interface SaveWorkItemBody {
  title?: string;
  description?: string;
  stateId?: string;
  assignee?: { userId: string | null };
  areaId?: string;
  parent?: ParentChange;
}

/** § 2.5: one candidate; `email` is `""` when the user has none. */
export interface AssigneeCandidate {
  id: string;
  displayName: string;
  email: string;
}

/** § 2.5's answer: at most 20 users, and whether more matched. */
export interface AssigneeCandidates {
  users: AssigneeCandidate[];
  moreMatched: boolean;
}

/**
 * feat-001 § 3.4's answer: at most 20 items, the most recently changed first,
 * and whether more matched. «Нет родителя» is the panel's own option.
 */
export interface ParentCandidates {
  items: WorkItemRef[];
  moreMatched: boolean;
}

const segment = encodeURIComponent;

function organizationApiPath(organizationId: string): string {
  return `/api/organizations/${segment(organizationId)}`;
}

/** `…/workitems`, the base of § 2.1–§ 2.4. */
export function workItemsApiPath(organizationId: string, projectId: string): string {
  return `${organizationApiPath(organizationId)}/projects/${segment(projectId)}/workitems`;
}

function workItemApiPath(organizationId: string, projectId: string, identifier: string): string {
  return `${workItemsApiPath(organizationId, projectId)}/${segment(identifier)}`;
}

/** § 2.1: one page of the project's list, 1-based. */
export function getWorkItems(
  organizationId: string,
  projectId: string,
  page: number,
): Promise<WorkItemPage> {
  return apiFetch(`${workItemsApiPath(organizationId, projectId)}?page=${page}`);
}

/** § 2.2: 201 creates; 200 is a replay of the same `requestId` — the same item either way. */
export function createWorkItem(
  organizationId: string,
  projectId: string,
  body: CreateWorkItemBody,
): Promise<WorkItem> {
  return apiFetch(workItemsApiPath(organizationId, projectId), { method: 'POST', body });
}

/** § 2.3. */
export function getWorkItem(
  organizationId: string,
  projectId: string,
  identifier: string,
): Promise<WorkItem> {
  return apiFetch(workItemApiPath(organizationId, projectId, identifier));
}

/** § 2.4: the item as stored after the save. */
export function saveWorkItem(
  organizationId: string,
  projectId: string,
  identifier: string,
  body: SaveWorkItemBody,
): Promise<WorkItem> {
  return apiFetch(workItemApiPath(organizationId, projectId, identifier), {
    method: 'PATCH',
    body,
  });
}

/** § 2.5: organization-level; under 2 characters the server answers nobody. */
export function getAssigneeCandidates(
  organizationId: string,
  text: string,
): Promise<AssigneeCandidates> {
  const query = new URLSearchParams({ text });
  return apiFetch(`${organizationApiPath(organizationId)}/workitems/assignee-candidates?${query}`);
}

/** Options of the parent search. */
export interface ParentSearchOptions {
  /**
   * feat-001 § 3.4: only the level directly above the item's own — the
   * backlog's dialog «Изменить родителя» (BR-H-14). The form never sends it.
   */
  levelAbove?: boolean;
}

/**
 * feat-001 § 3.4: the candidates for this item's parent whose identifier or
 * title contains `text`; an empty text matches every candidate.
 */
export function getParentCandidates(
  organizationId: string,
  projectId: string,
  identifier: string,
  text: string,
  options: ParentSearchOptions = {},
): Promise<ParentCandidates> {
  const query = new URLSearchParams({ text });
  if (options.levelAbove) {
    query.set('levelAbove', 'true');
  }
  return apiFetch(
    `${workItemApiPath(organizationId, projectId, identifier)}/parent-candidates?${query}`,
  );
}
