import { useMutation, useQueryClient, type QueryClient } from '@tanstack/react-query';
import {
  createWorkItem,
  deleteWorkItem,
  purgeWorkItem,
  restoreWorkItem,
  saveWorkItem,
  setWorkItemParent,
  type CreateWorkItemBody,
  type ParentChange,
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

/**
 * feat-001 § 3.2 for any item of the project: mod-006's backlog maps a row's
 * parent with it (BR-H-14). A write touches the child and its old and new
 * parents — their «Изменён», their `parent` and `children` — so every item
 * the form has read in the project, and every page of the list, is read
 * again when next shown. A refusal changes nothing.
 */
export function useSetWorkItemParent(organizationId: string, projectId: string) {
  const client = useQueryClient();
  return useMutation({
    mutationFn: ({ identifier, change }: { identifier: string; change: ParentChange }) =>
      setWorkItemParent(organizationId, projectId, identifier, change),
    onSuccess: () =>
      Promise.all([
        client.invalidateQueries({ queryKey: keys.items(organizationId, projectId) }),
        client.invalidateQueries({ queryKey: keys.lists(organizationId, projectId) }),
      ]),
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

// mod-005 api-contract § 2: the three writes of the recycle bin. Each moves the item's «Изменён» (or
// removes the item), so every page of the project's list is read again; the other items the form has
// read in the project — a parent's children, a child's parent — are stale as well (design § 7).

/** The key of one running write, so the card can tell that one is under way (design § 4: «Восстановить» busy). */
export function deletionMutationKey(kind: 'delete' | 'restore' | 'purge', identifier: string) {
  return ['workitems', kind, identifier] as const;
}

/** Reads every page of the project's list and every item of the project but `identifier` again. */
function invalidateAround(
  client: QueryClient,
  organizationId: string,
  projectId: string,
  identifier: string,
) {
  return Promise.all([
    client.invalidateQueries({ queryKey: keys.lists(organizationId, projectId) }),
    client.invalidateQueries({
      queryKey: keys.items(organizationId, projectId),
      predicate: (query) => query.queryKey[4] !== identifier,
    }),
  ]);
}

/** § 2.1: the answer is the item as stored — «Удалена», with its `deletion` — so the card shows it without another read. */
export function useDeleteWorkItem(organizationId: string, projectId: string, identifier: string) {
  const client = useQueryClient();
  return useMutation({
    mutationKey: deletionMutationKey('delete', identifier),
    mutationFn: () => deleteWorkItem(organizationId, projectId, identifier),
    onSuccess: (item) => {
      client.setQueryData(keys.item(organizationId, projectId, identifier), item);
      return invalidateAround(client, organizationId, projectId, identifier);
    },
  });
}

/** § 2.2: `stateId` is the state the dialog shows; the answer is the live item, `deletion: null`. */
export function useRestoreWorkItem(organizationId: string, projectId: string, identifier: string) {
  const client = useQueryClient();
  return useMutation({
    mutationKey: deletionMutationKey('restore', identifier),
    mutationFn: (stateId?: string) =>
      restoreWorkItem(organizationId, projectId, identifier, stateId),
    onSuccess: (item) => {
      client.setQueryData(keys.item(organizationId, projectId, identifier), item);
      return invalidateAround(client, organizationId, projectId, identifier);
    },
  });
}

/**
 * § 2.3: 204. The item's own cached read is marked stale and not read again — the answer would be a
 * not-found for a card that is about to close (the dialog) or leave (the page); the next visit to
 * its address reads afresh.
 */
export function usePurgeWorkItem(organizationId: string, projectId: string, identifier: string) {
  const client = useQueryClient();
  return useMutation({
    mutationKey: deletionMutationKey('purge', identifier),
    mutationFn: () => purgeWorkItem(organizationId, projectId, identifier),
    onSuccess: () =>
      Promise.all([
        client.invalidateQueries({
          queryKey: keys.item(organizationId, projectId, identifier),
          refetchType: 'none',
        }),
        invalidateAround(client, organizationId, projectId, identifier),
      ]),
  });
}
