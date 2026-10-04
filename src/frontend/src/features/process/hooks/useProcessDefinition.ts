import { useQuery } from '@tanstack/react-query';
import { getProcessDefinition } from '../api/processApi';

/** `[feature, entity, params]` (frontend.md "Data and state"; design § Hand-offs). */
export function processDefinitionQueryKey(organizationId: string) {
  return ['process', 'definition', organizationId] as const;
}

/**
 * The organization's process definition. An MVP process never changes, so
 * the answer is kept for the session (`staleTime: Infinity`, design § States,
 * Content); no retry and no refetch on focus are the client's defaults.
 */
export function useProcessDefinition(organizationId: string) {
  return useQuery({
    queryKey: processDefinitionQueryKey(organizationId),
    queryFn: () => getProcessDefinition(organizationId),
    staleTime: Infinity,
  });
}
