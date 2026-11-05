// The aggregate views' reads, strictly per docs/features/feat-048-aggregate-level-views/api-contract.md
// (not the design's table: § 4 lists the departures — flat by-project rows, `column` on the cell
// read, `/workitems/groups` and `/workitems/rows` without a level). All are organization-scoped GETs.
import { apiFetch } from '../../../shared/api/httpClient';
import type {
  WorkItemAssignee,
  WorkItemRef,
  WorkItemStateReference,
  WorkItemTeam,
  WorkItemTypeReference,
} from '../../workitems';
import type { BoardLevelCode } from '../lib/levels';
import type { BoardColumn } from './boardsApi';

export type Grouping = 'none' | 'department' | 'project' | 'team';
export type AggregateView = 'backlogs' | 'boards' | 'workitems';

/** The selectors a read is narrowed by (ADR-0032): ids, or none for «все». */
export interface AggregateSelectors {
  departmentId?: string;
  teamId?: string;
  projectId?: string;
}

/** feat-003's filters, accepted by every aggregate read (OQ-5 A); the lists use them. */
export interface AggregateFilter {
  state?: readonly string[];
  assigneeId?: readonly string[];
  search?: string;
}

/** api-contract § 2.1: one header of the grouped count read. */
/** The project board's column fields (BoardColumn without its items) and the cell's count. */
export type AggregateColumnRead = Omit<BoardColumn, 'items'> & { count: number };

export interface GroupHeader {
  kind: Grouping;
  /** `null`: «Без департамента» / «Без команды», and the one group of `none`. */
  id: string | null;
  key?: string;
  name: string | null;
  count: number;
  total: number;
  /** Boards only: the count of every column, key `<Category>-<index>`. */
  columns?: AggregateColumnRead[];
}

export interface GroupsRead {
  grouping: Grouping;
  groups: GroupHeader[];
}

export interface AggregateProject {
  id: string;
  key?: string;
  name: string;
}

/** api-contract § 2.2: a flat row; the tree is not paged by a cursor. */
export interface AggregateRow {
  id: string;
  identifier: string;
  number: number;
  type: WorkItemTypeReference;
  title: string;
  assignee: WorkItemAssignee | null;
  state: WorkItemStateReference;
  priority: number;
  team: WorkItemTeam | null;
  project: AggregateProject;
  parent: WorkItemRef | null;
  changedAt: string;
}

/** api-contract § 2.3: a card carries its checklist. */
export interface AggregateCard extends AggregateRow {
  checklist: unknown[];
}

export interface RowsPage {
  rows: AggregateRow[];
  nextCursor: string | null;
}

export interface CardsPage {
  cards: AggregateCard[];
  nextCursor: string | null;
}

/** The value of `group` for «Без департамента» / «Без команды» (api-contract § 1). */
export const NO_GROUP = 'none';

const segment = encodeURIComponent;

function base(organizationId: string): string {
  return `/api/organizations/${segment(organizationId)}`;
}

function viewPath(organizationId: string, view: AggregateView, level: BoardLevelCode): string {
  return view === 'workitems'
    ? `${base(organizationId)}/workitems`
    : `${base(organizationId)}/${view}/${level}`;
}

/** The members of every aggregate read: selectors, filters, grouping. */
export function aggregateQuery(
  selectors: AggregateSelectors,
  filter: AggregateFilter = {},
  extra: Record<string, string | undefined> = {},
): URLSearchParams {
  const params = new URLSearchParams();
  if (selectors.departmentId) params.append('departmentId', selectors.departmentId);
  if (selectors.teamId) params.append('teamId', selectors.teamId);
  if (selectors.projectId) params.append('projectId', selectors.projectId);
  for (const state of filter.state ?? []) params.append('state', state);
  for (const id of filter.assigneeId ?? []) params.append('assigneeId', id);
  if (filter.search) params.append('search', filter.search);
  for (const [name, value] of Object.entries(extra)) {
    if (value !== undefined) params.append(name, value);
  }
  return params;
}

/** § 2.1: the grouped counts. */
export function getGroups(
  organizationId: string,
  view: AggregateView,
  level: BoardLevelCode,
  grouping: Grouping,
  selectors: AggregateSelectors,
  filter?: AggregateFilter,
): Promise<GroupsRead> {
  const query = aggregateQuery(selectors, filter, { grouping });
  return apiFetch(`${viewPath(organizationId, view, level)}/groups?${query}`);
}

export interface PageRequest {
  grouping: Grouping;
  /** `GroupHeader.id`, or `NO_GROUP` for the group of no department / no team; absent for `none`. */
  group?: string;
  cursor?: string | null;
  limit?: number;
}

/** § 2.2 and § 2.4: a page of one group of a backlog or of «Рабочие элементы». */
export function getRows(
  organizationId: string,
  view: 'backlogs' | 'workitems',
  level: BoardLevelCode,
  request: PageRequest,
  selectors: AggregateSelectors,
  filter?: AggregateFilter,
): Promise<RowsPage> {
  const query = aggregateQuery(selectors, filter, {
    grouping: request.grouping,
    group: request.grouping === 'none' ? undefined : request.group,
    limit: request.limit === undefined ? undefined : String(request.limit),
    cursor: request.cursor ?? undefined,
  });
  return apiFetch(`${viewPath(organizationId, view, level)}/rows?${query}`);
}

/** § 2.3: a page of one cell (a group × a column). */
export function getCells(
  organizationId: string,
  level: BoardLevelCode,
  request: PageRequest & { column: string },
  selectors: AggregateSelectors,
  filter?: AggregateFilter,
): Promise<CardsPage> {
  const query = aggregateQuery(selectors, filter, {
    grouping: request.grouping,
    group: request.grouping === 'none' ? undefined : request.group,
    column: request.column,
    limit: request.limit === undefined ? undefined : String(request.limit),
    cursor: request.cursor ?? undefined,
  });
  return apiFetch(`${viewPath(organizationId, 'boards', level)}/cells?${query}`);
}

/** A board's columns as the process gives them (boards read), for a lane's heads. */
export type { BoardColumn };
