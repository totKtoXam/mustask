// The work items module over HTTP, strictly per
// docs/features/mod-003-work-item-core/api-contract.md § 1–§ 2: one function
// per route, camelCase as all of Mustask.Api. Ids and the identifier come from
// the address or from an answer and are sent as received, re-encoded in the
// path and never validated (§ 0.2): a malformed one answers its level's
// not-found code. The process and area reads the screens also use are mod-001's
// and mod-002's, unchanged (§ 3), through those slices' barrels. feat-001's
// api-contract § 2–§ 3 adds the hierarchy: the item's `parent` and
// `children`, the save's `parent`, the «Родитель» picker's search, and the
// parent write on its own that a team's backlog maps a row with (§ 3.2).
// feat-022's api-contract § 1–§ 3 adds the 17 fields per type to the item and
// to the save.
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
  /**
   * mod-016 api-contract § 3.3: the assignee is a member of the item's organization
   * now (any role; the flag reads the membership alone, BR-M1-32). Typed and not
   * shown: the marks «неактивен» and «не в организации» are feat-017's (D-4), and
   * until it ships an ex-member assignee reads like any other. Absent on a
   * candidate of the picker's search, which is members only.
   */
  isMember?: boolean;
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

/**
 * feat-022 api-contract § 1, § 2: the planning, classification, estimation and
 * text fields. Every item carries all 17; a field its type does not have
 * (`fieldsOf`, by `type.appearance`) is always `null`. On its own type:
 * `priority`, a bug's `severity` and `valueArea` are never `null`, a text is
 * `""` when empty, any other field is `null` when empty. Numbers come in their
 * shortest form, dates as `YYYY-MM-DD`, list values as their Latin keys.
 */
export interface WorkItemFields {
  /** 1..4, 1 the highest; every type has it. */
  priority: number;
  /** 1..3. */
  risk: number | null;
  storyPoints: number | null;
  effort: number | null;
  /** An integer. */
  businessValue: number | null;
  timeCriticality: number | null;
  startDate: string | null;
  targetDate: string | null;
  /** 1..4. */
  severity: number | null;
  /** `Deployment`, `Design`, `Development`, `Documentation`, `Requirements`, `Testing`. */
  activity: string | null;
  originalEstimate: number | null;
  remainingWork: number | null;
  completedWork: number | null;
  /** `Business` or `Architectural`. */
  valueArea: string | null;
  acceptanceCriteria: string | null;
  reproSteps: string | null;
  systemInfo: string | null;
}

/** feat-022 § 1's JSON names. */
export type WorkItemFieldName = keyof WorkItemFields;

/** § 1.1: the form's item — what create, read and save answer. */
export interface WorkItem extends WorkItemFields {
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

/**
 * § 1.2: a row of the list; feat-001 § 2.2 leaves it without the hierarchy,
 * feat-022 § 2 without the fields per type (BR-M3-40).
 */
export type WorkItemListItem = Omit<
  WorkItem,
  | 'projectId'
  | 'description'
  | 'createdBy'
  | 'createdAt'
  | 'parent'
  | 'children'
  | WorkItemFieldName
>;

/** § 2.1: ADR-0015's `Paging<T>`; `count` is the project's item count. */
export interface WorkItemPage {
  count: number;
  data: WorkItemListItem[];
}

/** § 2.1: the list's fixed page size; the route takes `page` only. */
export const WORK_ITEMS_PAGE_SIZE = 50;

/** § 2.2's body; feat-022 § 3.1: none of the fields per type (BR-M3-40). */
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
  /**
   * feat-027 api-contract § 3: the item the new one is created beneath — the
   * backlog row whose «+» opened the inline add row. `ParentChange`'s own
   * member, flat; absent: no parent, exactly as before. With it the item takes
   * the parent's area, so `teamId` is not sent beside it.
   */
  parentId?: string;
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
 * feat-022 § 3.2: how a field other than the three texts is sent —
 * `{ value: null }` empties it (an always-set one takes its default).
 */
export interface FieldValue<T> {
  value: T | null;
}

/** feat-022 § 3.2: the three type texts, sent as strings — `""` empties one. */
export type WorkItemTextFieldName = 'acceptanceCriteria' | 'reproSteps' | 'systemInfo';

/** feat-022 § 3.2: every other field of § 1, sent in a `{ value }` wrapper. */
export type WorkItemValueFieldName = Exclude<WorkItemFieldName, WorkItemTextFieldName>;

/** feat-022 § 3.2's 17 optional members: absent leaves the field unchanged. */
export type SaveFieldsBody = {
  [Name in WorkItemValueFieldName]?: FieldValue<NonNullable<WorkItemFields[Name]>>;
} & {
  [Name in WorkItemTextFieldName]?: string;
};

/**
 * § 2.4's body: only the fields that changed. `assignee: { userId: null }`
 * clears the assignee; an absent `assignee` keeps it. feat-001 § 3.3: an
 * absent `parent` leaves the parent as it is, whoever changed it meanwhile.
 * feat-022 § 3.2 adds the fields per type, by the same rule.
 */
export interface SaveWorkItemBody extends SaveFieldsBody {
  title?: string;
  description?: string;
  stateId?: string;
  assignee?: { userId: string | null };
  areaId?: string;
  parent?: ParentChange;
}

/** feat-001 § 3.2's answer: the item's parent as stored after the write, `null` for none. */
export interface ParentWriteResult {
  parent: WorkItemRef | null;
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

/**
 * feat-001 § 3.2: the parent write on its own — sets, replaces or clears the
 * item's parent in one write (BR-H-3), refused with `parent_changed` when the
 * stored parent is not `seenParentId` (BR-H-4).
 */
export function setWorkItemParent(
  organizationId: string,
  projectId: string,
  identifier: string,
  change: ParentChange,
): Promise<ParentWriteResult> {
  return apiFetch(`${workItemApiPath(organizationId, projectId, identifier)}/parent`, {
    method: 'PUT',
    body: change,
  });
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
