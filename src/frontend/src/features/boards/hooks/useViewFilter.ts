import {
  filterSearchOf,
  filterValuesOf,
  useAddressFilter,
} from '../../../shared/lib/addressFilter';
import { useTeamFilter } from '../../workitems';
import { filterIdsOf, type BoardScope } from '../lib/scope';

/**
 * The address parameter the project filter of a team's views lives in (feat-040 BR-TS-8, OQ-2 A):
 * repeated, one per chosen project, in the one form the shared filter specification gives every
 * dimension (mod-020 founded it for the team filter) and named as the API names the read's query
 * parameter (api-contract § 3.1), so the filter has one vocabulary in the address and on the wire.
 */
export const PROJECT_FILTER_PARAM = 'projectId';

/** The chosen projects an address carries, in its order, each once; a blank value names nothing. */
export function projectFilterOf(search: string): string[] {
  return filterValuesOf(search, PROJECT_FILTER_PARAM);
}

/**
 * The project filter's part of an address, as `?projectId=…&projectId=…`, or nothing: what a link to
 * the same view at another level, or to the other page of the team, carries (BR-TS-8) — and nothing
 * else the address holds, the card's `?workitem=` staying with the page.
 */
export function projectFilterSearch(search: string): string {
  return filterSearchOf(search, PROJECT_FILTER_PARAM);
}

/** The project filter of a team's backlog and board, kept in the page address. See `useAddressFilter`. */
export function useProjectFilter(): readonly [string[], (projectIds: readonly string[]) => void] {
  return useAddressFilter(PROJECT_FILTER_PARAM);
}

/**
 * The one filter a backlog or a board has, whatever it is the view of: the project filter on a
 * team's views (feat-040 BR-TS-8), the team filter on the project's (mod-017 BR-OT-8, mod-020).
 * The other dimension an address may carry narrows nothing, is not sent, and is left in the address
 * as it is. Returns the chosen values and the setter of that dimension.
 */
export function useViewFilter(
  scope: BoardScope,
): readonly [readonly string[], (values: readonly string[]) => void] {
  const [teamIds, setTeamIds] = useTeamFilter();
  const [projectIds, setProjectIds] = useProjectFilter();
  return [
    filterIdsOf(scope, { teamIds, projectIds }),
    scope.kind === 'project' ? setTeamIds : setProjectIds,
  ];
}
