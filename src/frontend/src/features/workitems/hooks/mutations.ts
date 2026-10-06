import { useMutation, useQueryClient } from '@tanstack/react-query';
import {
  createWorkItem,
  saveWorkItem,
  type CreateWorkItemBody,
  type SaveWorkItemBody,
} from '../api/workItemsApi';
import { workItemsKeys as keys } from '../api/queryKeys';

// The two writes of api-contract § 2.2 and § 2.4 with their invalidations:
// every page of the project's list is read again after either, because both
// move the item to the top (BR-M3-29: the latest change first).

/** § 2.2: a 201 and a replay's 200 both answer the item, so both land the same way. */
export function useCreateWorkItem(organizationId: string, projectId: string) {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (body: CreateWorkItemBody) => createWorkItem(organizationId, projectId, body),
    onSuccess: (item) => {
      client.setQueryData(keys.item(organizationId, projectId, item.identifier), item);
      return client.invalidateQueries({ queryKey: keys.lists(organizationId, projectId) });
    },
  });
}

/**
 * § 2.4 with `{ stateId }` alone, for any item of the project: mod-006's card
 * move (its api-contract § 6) — the same write as choosing the state on the
 * form, with the same cache updates.
 */
export function useSetWorkItemState(organizationId: string, projectId: string) {
  const client = useQueryClient();
  return useMutation({
    mutationFn: ({ identifier, stateId }: { identifier: string; stateId: string }) =>
      saveWorkItem(organizationId, projectId, identifier, { stateId }),
    onSuccess: (item) => {
      client.setQueryData(keys.item(organizationId, projectId, item.identifier), item);
      return client.invalidateQueries({ queryKey: keys.lists(organizationId, projectId) });
    },
  });
}

/** § 2.4: the answer is the item as stored, so the form shows it without another read. */
export function useSaveWorkItem(organizationId: string, projectId: string, identifier: string) {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (body: SaveWorkItemBody) =>
      saveWorkItem(organizationId, projectId, identifier, body),
    onSuccess: (item) => {
      client.setQueryData(keys.item(organizationId, projectId, identifier), item);
      return client.invalidateQueries({ queryKey: keys.lists(organizationId, projectId) });
    },
  });
}
