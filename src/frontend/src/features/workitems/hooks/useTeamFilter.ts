import {
  filterSearchOf,
  filterValuesOf,
  useAddressFilter,
} from '../../../shared/lib/addressFilter';

/**
 * The address parameter the team filter lives in (mod-020, the human's answer to mod-017
 * OQ-9 B): repeated, one per chosen value, and named as api-contract § 3 names the read's, so
 * the filter has one vocabulary in the address and on the wire. A value is a team's id or
 * `none` (`NO_TEAM_FILTER_VALUE`). The project filter of a team's views follows the same form
 * (feat-040 BR-TS-8) with the parameter `projectId`.
 */
export const TEAM_FILTER_PARAM = 'teamId';

/**
 * The chosen values an address carries, in its order, each once. A blank value (`?teamId=`) names
 * nothing and is left out: a link with one shows the whole list. Any other value is kept as
 * written, whether it names a team of the project or not: BR-OT-8 reads a value that is no team of
 * the project as a filter that selects nothing, and the field's «Очистить фильтры» removes it.
 */
export function teamFilterOf(search: string): string[] {
  return filterValuesOf(search, TEAM_FILTER_PARAM);
}

/**
 * The filter's part of an address, as `?teamId=…&teamId=…`, or nothing: what a link to the same
 * view at another level carries (mod-020: the filter survives a level change), and nothing else
 * the address holds — the card's `?workitem=` stays with the page.
 */
export function teamFilterSearch(search: string): string {
  return filterSearchOf(search, TEAM_FILTER_PARAM);
}

/**
 * The team filter of «Рабочие элементы» and of the project's backlog and board, kept in the page
 * address (mod-020; mod-017 BR-OT-8, OQ-9 B) — so a link carries it, a re-read and a level change
 * keep it, and leaving the page, or following a link without it, drops it. Returns the chosen team
 * ids and `none`, and the setter. See `useAddressFilter` for the address rules.
 */
export function useTeamFilter(): readonly [string[], (teamIds: readonly string[]) => void] {
  return useAddressFilter(TEAM_FILTER_PARAM);
}
