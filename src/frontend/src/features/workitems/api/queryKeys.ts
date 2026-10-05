// TanStack Query keys of the slice, `['workitems', <entity>, …]` (frontend.md
// "Data and state"). Every page of a project's list sits under one prefix, so
// a create or a save refreshes them all with one invalidation.

const ROOT = 'workitems';

export const workItemsKeys = {
  all: [ROOT] as const,
  /** Every page of a project's list. */
  lists: (organizationId: string, projectId: string) =>
    [ROOT, 'list', organizationId, projectId] as const,
  list: (organizationId: string, projectId: string, page: number) =>
    [ROOT, 'list', organizationId, projectId, page] as const,
  item: (organizationId: string, projectId: string, identifier: string) =>
    [ROOT, 'item', organizationId, projectId, identifier] as const,
  assigneeCandidates: (organizationId: string, text: string) =>
    [ROOT, 'assigneeCandidates', organizationId, text] as const,
};
