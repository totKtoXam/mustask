import { keepPreviousData, useQuery } from '@tanstack/react-query';
import { getAssigneeCandidates, getWorkItem, getWorkItems } from '../api/workItemsApi';
import { workItemsKeys } from '../api/queryKeys';

// One query hook per read of api-contract § 2.

/**
 * § 2.1: one page of the project's list, 1-based. Choosing another page keeps
 * the rows on screen until the next page arrives; the page that hosts it is
 * keyed by its project, so another project never shows this one's rows.
 */
export function useWorkItems(organizationId: string, projectId: string, page: number) {
  return useQuery({
    queryKey: workItemsKeys.list(organizationId, projectId, page),
    queryFn: () => getWorkItems(organizationId, projectId, page),
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
