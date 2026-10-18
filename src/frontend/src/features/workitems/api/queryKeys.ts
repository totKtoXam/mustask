// TanStack Query keys of the slice, `['workitems', <entity>, …]` (frontend.md
// "Data and state"). Every page of a project's list sits under one prefix, so
// a create or a save refreshes them all with one invalidation.
import { NO_LIST_FILTER, type WorkItemListFilter } from './workItemsApi';

const ROOT = 'workitems';

/** The filter as a key part: each dimension is a set, so the order the values were chosen in is not part of it. */
function filterKey(filter: WorkItemListFilter) {
  return {
    teamId: [...filter.teamId].sort(),
    state: [...filter.state].sort(),
    assigneeId: [...filter.assigneeId].sort(),
    search: filter.search,
  };
}

export const workItemsKeys = {
  all: [ROOT] as const,
  /** Every page of a project's list, and the options of its «Исполнитель» filter (they follow the same writes). */
  lists: (organizationId: string, projectId: string) =>
    [ROOT, 'list', organizationId, projectId] as const,
  /** One page under a filter (mod-017 § 3, feat-003 § 3): the chosen values, sorted, are part of the key. */
  list: (
    organizationId: string,
    projectId: string,
    page: number,
    filter: WorkItemListFilter = NO_LIST_FILTER,
  ) => [ROOT, 'list', organizationId, projectId, page, filterKey(filter)] as const,
  /**
   * feat-003 § 2.2: the «Исполнитель» filter's options under the chosen states. Under the list's
   * prefix, so every write that reads the list again reads them again (an assignee changed, an item
   * deleted); the string where a page number stands tells the two apart.
   */
  assigneeOptions: (organizationId: string, projectId: string, states: readonly string[]) =>
    [ROOT, 'list', organizationId, projectId, 'assignee-options', [...states].sort()] as const,
  /** Every item of a project the form has read. */
  items: (organizationId: string, projectId: string) =>
    [ROOT, 'item', organizationId, projectId] as const,
  item: (organizationId: string, projectId: string, identifier: string) =>
    [ROOT, 'item', organizationId, projectId, identifier] as const,
  assigneeCandidates: (organizationId: string, text: string) =>
    [ROOT, 'assigneeCandidates', organizationId, text] as const,
  /** feat-001 § 3.4: one item's parent search; the form's and the dialog's never share. */
  parentCandidates: (
    organizationId: string,
    projectId: string,
    identifier: string,
    levelAbove: boolean,
    text: string,
  ) => [ROOT, 'parentCandidates', organizationId, projectId, identifier, levelAbove, text] as const,
};
