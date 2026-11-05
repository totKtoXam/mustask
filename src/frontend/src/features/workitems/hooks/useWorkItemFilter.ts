import { useCallback, useLayoutEffect, useMemo, useRef } from 'react';
import { useLocation, useNavigate } from 'react-router-dom';
import type { WorkItemListFilter } from '../api/workItemsApi';
import { filterOf, withFilter } from '../lib/workItemFilter';

/**
 * The list's four filters — «Поиск», «Статус», «Исполнитель», «Команда» — kept in the page address
 * (feat-003 BR-WS-11, design § 3), in the form mod-020 founded for `teamId`: one repeated parameter
 * per chosen value, the keyword as one, so a link, a reload and Back from a card carry them. Returns
 * the filter and the one writer.
 *
 * **One writer.** `setFilter(change)` replaces the dimensions it names and keeps the others, and
 * writes all four in a single `navigate` (replace: Back leaves the page instead of stepping through
 * filters; every other parameter, the hash and the router state are kept). It builds on the address
 * as the last write left it, not as the last render saw it: the router applies a change as a
 * transition, so a clear, or two changes made before it has rendered, never lose one dimension to
 * the other's write. An address that moves by another cause (Back, a link) is built on afresh.
 * `useTeamFilter` (mod-020) stays for the project's backlog and board; this hook only borrows its
 * parameter name and its reading of the team.
 *
 * The filter is memoised on its content, so it is not made anew when the address changes with
 * another parameter alone; that is an optimisation, not a promise, and a caller that needs to know
 * whether the filter changed compares the content (`filterContent`).
 */
export function useWorkItemFilter(): readonly [
  WorkItemListFilter,
  (change: Partial<WorkItemListFilter>) => void,
] {
  const { pathname, search, hash, state } = useLocation();
  const navigate = useNavigate();
  const content = JSON.stringify(filterOf(search));
  const filter = useMemo(() => JSON.parse(content) as WorkItemListFilter, [content]);

  // The router's address as the last render saw it.
  const route = useRef({ pathname, hash, state, navigate });
  useLayoutEffect(() => {
    route.current = { pathname, hash, state, navigate };
  });
  // The query strings written that the router has not rendered yet, oldest first, and the one it
  // rendered last.
  const pending = useRef<string[]>([]);
  const rendered = useRef(search);
  useLayoutEffect(() => {
    rendered.current = search;
    const caughtUp = pending.current.lastIndexOf(search);
    pending.current = caughtUp >= 0 ? pending.current.slice(caughtUp + 1) : [];
  }, [search]);

  const setFilter = useCallback((change: Partial<WorkItemListFilter>) => {
    const base = pending.current[pending.current.length - 1] ?? rendered.current;
    const next = withFilter(base, { ...filterOf(base), ...change });
    if (next === base) {
      return;
    }
    pending.current = [...pending.current, next];
    const { pathname: to, hash: fragment, state: routerState, navigate: go } = route.current;
    go({ pathname: to, search: next, hash: fragment }, { replace: true, state: routerState });
  }, []);

  return [filter, setFilter];
}
