import { useCallback, useMemo } from 'react';
import { useLocation, useNavigate } from 'react-router-dom';

/**
 * The address parameter the team filter lives in (mod-020, the human's answer to mod-017
 * OQ-9 B): repeated, one per chosen value, and named as api-contract § 3 names the read's, so
 * the filter has one vocabulary in the address and on the wire. A value is a team's id or
 * `none` (`NO_TEAM_FILTER_VALUE`).
 */
export const TEAM_FILTER_PARAM = 'teamId';

/**
 * The chosen values an address carries, in its order, each once. A blank value (`?teamId=`) names
 * nothing and is left out: a link with one shows the whole list. Any other value is kept as
 * written, whether it names a team of the project or not: BR-OT-8 reads a value that is no team of
 * the project as a filter that selects nothing, and the field's «Очистить фильтры» removes it.
 */
export function teamFilterOf(search: string): string[] {
  const values = new URLSearchParams(search).getAll(TEAM_FILTER_PARAM);
  return [...new Set(values.filter((value) => value !== ''))];
}

/**
 * The filter's part of an address, as `?teamId=…&teamId=…`, or nothing: what a link to the same
 * view at another level carries (mod-020: the filter survives a level change), and nothing else
 * the address holds — the card's `?workitem=` stays with the page.
 */
export function teamFilterSearch(search: string): string {
  const params = new URLSearchParams();
  for (const value of teamFilterOf(search)) {
    params.append(TEAM_FILTER_PARAM, value);
  }
  const text = params.toString();
  return text === '' ? '' : `?${text}`;
}

/**
 * The team filter of «Рабочие элементы» and of the project's backlog and board, kept in the page
 * address (mod-020; mod-017 BR-OT-8, OQ-9 B) — so a link carries it, a re-read and a level change
 * keep it, and leaving the page, or following a link without it, drops it. Returns the chosen team
 * ids and `none`, and the setter.
 *
 * The array is the same object while its content is: the address changes with the card's
 * `?workitem=` too, and a page that holds the array in a query key or a memoised row must not
 * see a new one for it. A change replaces the address's entry rather than adding one, so Back
 * leaves the page instead of stepping through filters; every other parameter, the hash and the
 * router state (the card's history marks) are kept.
 */
export function useTeamFilter(): readonly [string[], (teamIds: readonly string[]) => void] {
  const { pathname, search, hash, state } = useLocation();
  const navigate = useNavigate();
  const content = JSON.stringify(teamFilterOf(search));
  const teamIds = useMemo(() => JSON.parse(content) as string[], [content]);

  const setTeamIds = useCallback(
    (next: readonly string[]) => {
      const params = new URLSearchParams(search);
      params.delete(TEAM_FILTER_PARAM);
      for (const value of next) {
        params.append(TEAM_FILTER_PARAM, value);
      }
      const rest = params.toString();
      navigate({ pathname, search: rest === '' ? '' : `?${rest}`, hash }, { replace: true, state });
    },
    [navigate, pathname, search, hash, state],
  );

  return [teamIds, setTeamIds];
}
