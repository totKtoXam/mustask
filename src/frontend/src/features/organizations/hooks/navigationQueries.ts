import { useQuery } from '@tanstack/react-query';
import { getOverview, getSelectorOffers } from '../api/navigationApi';
import { navigationKeys } from '../api/queryKeys';

// One query hook per read of feat-043 (plan-frontend § 3). `selector-offers` is read once and
// serves the four selectors and the lists; every write refreshes it (see `invalidate` in
// mutations.ts).

export function useOverview(organizationId: string) {
  return useQuery({
    queryKey: navigationKeys.overview(organizationId),
    queryFn: () => getOverview(organizationId),
  });
}

export function useSelectorOffers(
  organizationId: string,
  options: { enabled?: boolean; refetchOnWindowFocus?: boolean } = {},
) {
  return useQuery({
    queryKey: navigationKeys.selectorOffers(organizationId),
    queryFn: () => getSelectorOffers(organizationId),
    enabled: options.enabled ?? true,
    // The shell passes `true`: another user's link change reaches an open tab on its next focus.
    refetchOnWindowFocus: options.refetchOnWindowFocus,
  });
}
