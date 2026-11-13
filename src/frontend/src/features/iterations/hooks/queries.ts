import { useInfiniteQuery, useQuery } from '@tanstack/react-query';
import {
  getClosePreview,
  getClosedSprint,
  getDynamics,
  getOutcomeItems,
  getParticipants,
  getSprints,
  SPRINT_PAGE_SIZE,
  type OutcomeQuery,
} from '../api/iterationsApi';
import { iterationsKeys } from '../api/queryKeys';

/** The team's list, 200 a page by the cursor (§ 2.1); «Показать ещё» appends the next page. */
export function useSprintList(organizationId: string, teamId: string) {
  return useInfiniteQuery({
    queryKey: iterationsKeys.list(organizationId, teamId),
    queryFn: ({ pageParam }) => getSprints(organizationId, teamId, { before: pageParam }),
    initialPageParam: null as number | null,
    getNextPageParam: (last) => last.nextBefore,
  });
}

/** The team's Active and Planned sprints: the card's options and the close dialog's. */
export function useOpenSprints(
  organizationId: string,
  teamId: string | null,
  options: { enabled?: boolean } = {},
) {
  return useQuery({
    queryKey: iterationsKeys.open(organizationId, teamId ?? ''),
    queryFn: () => getSprints(organizationId, teamId ?? '', { open: true }),
    enabled: (options.enabled ?? true) && teamId !== null,
  });
}

/** § 2.6, read when the close dialog opens and kept for no longer. */
export function useClosePreview(organizationId: string, teamId: string, sprintId: string) {
  return useQuery({
    queryKey: iterationsKeys.preview(organizationId, teamId, sprintId),
    queryFn: () => getClosePreview(organizationId, teamId, sprintId),
    gcTime: 0,
    retry: false,
  });
}

/** § 2.8. */
export function useClosedSprint(organizationId: string, teamId: string, sprintId: string) {
  return useQuery({
    queryKey: iterationsKeys.closed(organizationId, teamId, sprintId),
    queryFn: () => getClosedSprint(organizationId, teamId, sprintId),
    retry: false,
  });
}

/** § 2.9 «Динамика». */
export function useDynamics(
  organizationId: string,
  teamId: string,
  sprintId: string,
  query: OutcomeQuery,
  options: { enabled?: boolean } = {},
) {
  return useQuery({
    queryKey: iterationsKeys.dynamics(organizationId, teamId, sprintId, query),
    queryFn: () => getDynamics(organizationId, teamId, sprintId, query),
    enabled: options.enabled ?? true,
    retry: false,
  });
}

/** § 2.9 «Итоги по участникам»: 200 rows a page by the offset. */
export function useParticipants(
  organizationId: string,
  teamId: string,
  sprintId: string,
  query: OutcomeQuery,
  options: { enabled?: boolean } = {},
) {
  return useInfiniteQuery({
    queryKey: iterationsKeys.participants(organizationId, teamId, sprintId, query),
    queryFn: ({ pageParam }) => getParticipants(organizationId, teamId, sprintId, query, pageParam),
    initialPageParam: 0,
    getNextPageParam: (last, all) => {
      const loaded = all.reduce((sum, page) => sum + page.rows.length, 0);
      return loaded < last.totalRows && last.rows.length === SPRINT_PAGE_SIZE ? loaded : undefined;
    },
    enabled: options.enabled ?? true,
    retry: false,
  });
}

/** § 2.9 «Итог спринта»: the done items, 200 rows a page. */
export function useOutcomeItems(
  organizationId: string,
  teamId: string,
  sprintId: string,
  options: { enabled?: boolean } = {},
) {
  return useInfiniteQuery({
    queryKey: iterationsKeys.items(organizationId, teamId, sprintId),
    queryFn: ({ pageParam }) => getOutcomeItems(organizationId, teamId, sprintId, pageParam),
    initialPageParam: 0,
    getNextPageParam: (last, all) => {
      const loaded = all.reduce((sum, page) => sum + page.items.length, 0);
      return loaded < last.totalItems && last.items.length === SPRINT_PAGE_SIZE
        ? loaded
        : undefined;
    },
    enabled: options.enabled ?? true,
    retry: false,
  });
}
