// The work items module over HTTP, strictly per
// docs/features/mod-003-work-item-core/api-contract.md § 1–§ 2: one function
// per route, camelCase as all of Mustask.Api. Ids and the identifier come from
// the address or from an answer and are sent as received, re-encoded in the
// path and never validated (§ 0.2): a malformed one answers its level's
// not-found code. The process and area reads the screens also use are mod-001's
// and mod-002's, unchanged (§ 3), through those slices' barrels.
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
}

/** § 1.2: a row of the list. */
export type WorkItemListItem = Omit<
  WorkItem,
  'projectId' | 'description' | 'createdBy' | 'createdAt'
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
}

/**
 * § 2.4's body: only the fields that changed. `assignee: { userId: null }`
 * clears the assignee; an absent `assignee` keeps it.
 */
export interface SaveWorkItemBody {
  title?: string;
  description?: string;
  stateId?: string;
  assignee?: { userId: string | null };
  areaId?: string;
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
