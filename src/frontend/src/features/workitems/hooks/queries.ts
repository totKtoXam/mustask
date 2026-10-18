import { useCallback } from 'react';
import { keepPreviousData, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  NO_LIST_FILTER,
  getAssigneeCandidates,
  getAssigneeFilterOptions,
  getParentCandidates,
  getWorkItem,
  getWorkItems,
  type WorkItem,
  type WorkItemListFilter,
} from '../api/workItemsApi';
import { workItemsKeys } from '../api/queryKeys';

// One query hook per read of api-contract § 2, and feat-001's § 3.4.

/**
 * § 2.1: one page of the project's list, 1-based, under the filter (mod-017 § 3, feat-003 § 3).
 * Choosing another page or another filter keeps the rows on screen until the next answer arrives;
 * the page that hosts it is keyed by its project, so another project never shows this one's rows.
 */
export function useWorkItems(
  organizationId: string,
  projectId: string,
  page: number,
  filter: WorkItemListFilter = NO_LIST_FILTER,
) {
  return useQuery({
    queryKey: workItemsKeys.list(organizationId, projectId, page, filter),
    queryFn: () => getWorkItems(organizationId, projectId, page, filter),
    placeholderData: keepPreviousData,
  });
}

/**
 * feat-003 § 2.2: the people the «Исполнитель» filter offers under the chosen states. The server
 * decides whether a chosen state brings in the people of deleted items; a change of the states
 * reads again and the previous options stay on screen until the answer arrives (design § 2.3).
 */
export function useAssigneeFilterOptions(
  organizationId: string,
  projectId: string,
  states: readonly string[],
) {
  return useQuery({
    queryKey: workItemsKeys.assigneeOptions(organizationId, projectId, states),
    queryFn: () => getAssigneeFilterOptions(organizationId, projectId, states),
    placeholderData: keepPreviousData,
  });
}

/** § 2.3: the form's item; its first answer validates the whole address. */
export function useWorkItem(organizationId: string, projectId: string, identifier: string) {
  return useQuery({
    queryKey: workItemsKeys.item(organizationId, projectId, identifier),
    queryFn: () => getWorkItem(organizationId, projectId, identifier),
  });
}

/**
 * § 2.3 asked again now: the form after `workitems.link.parent_changed`
 * (feat-001 spec § Screens and copy). An answer replaces the item's cache
 * entry, so the form shows it; a failure is the caller's alone and leaves the
 * entry as it was — through the cache it would turn the loaded form into the
 * read's error frame and lose the user's draft.
 */
export function useRefreshWorkItem(
  organizationId: string,
  projectId: string,
  identifier: string,
): () => Promise<WorkItem> {
  const client = useQueryClient();
  return useCallback(async () => {
    const fresh = await getWorkItem(organizationId, projectId, identifier);
    client.setQueryData(workItemsKeys.item(organizationId, projectId, identifier), fresh);
    return fresh;
  }, [client, organizationId, projectId, identifier]);
}

/**
 * § 2.5: the candidates for `text`, asked only from 2 characters on — below
 * that the server answers nobody, and the picker shows its hint instead.
 */
export function useAssigneeCandidates(organizationId: string, text: string, enabled: boolean) {
  return useQuery({
    queryKey: workItemsKeys.assigneeCandidates(organizationId, text),
    queryFn: () => getAssigneeCandidates(organizationId, text),
    enabled,
  });
}

/**
 * feat-001 § 3.4: the candidates for this item's parent matching `text` — an
 * empty text asks for the 20 most recently changed. Asked once `enabled`: the
 * picker enables it when its list first opens, so a form that never opens it
 * sends nothing.
 */
export function useParentCandidates(
  organizationId: string,
  projectId: string,
  identifier: string,
  text: string,
  options: { enabled: boolean; levelAbove?: boolean },
) {
  const levelAbove = options.levelAbove ?? false;
  return useQuery({
    queryKey: workItemsKeys.parentCandidates(
      organizationId,
      projectId,
      identifier,
      levelAbove,
      text,
    ),
    queryFn: () => getParentCandidates(organizationId, projectId, identifier, text, { levelAbove }),
    enabled: options.enabled,
  });
}
