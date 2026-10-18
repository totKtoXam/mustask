import { useCallback, useMemo } from 'react';
import { useLocation, useNavigate } from 'react-router-dom';

// A filter dimension kept in the page address, in the one form the shared filter specification
// gives every dimension (mod-020 founded it for the team, feat-040 BR-TS-8 follows it for the
// project): the parameter repeated, one per chosen value, named as the API names the read's
// query parameter. The pages of one slice name their dimension; the helpers below do the rest.

/**
 * The chosen values an address carries for `param`, in its order, each once. A blank value
 * (`?teamId=`) names nothing and is left out: a link with one shows the whole list. Any other
 * value is kept as written, whether it names something the page holds or not — a filter that
 * names nothing selects nothing, and «Очистить фильтры» removes it.
 */
export function filterValuesOf(search: string, param: string): string[] {
  const values = new URLSearchParams(search).getAll(param);
  return [...new Set(values.filter((value) => value !== ''))];
}

/**
 * The filter's part of an address for the given dimensions, as `?teamId=…&teamId=…`, or
 * nothing: what a link to the same view at another level carries, and nothing else the address
 * holds — the card's `?workitem=` stays with the page.
 */
export function filterSearchOf(search: string, ...params: string[]): string {
  const next = new URLSearchParams();
  for (const param of params) {
    for (const value of filterValuesOf(search, param)) {
      next.append(param, value);
    }
  }
  const text = next.toString();
  return text === '' ? '' : `?${text}`;
}

/**
 * One filter dimension of a page, kept in its address — so a link carries it, a re-read and a
 * level change keep it, and leaving the page, or following a link without it, drops it. Returns
 * the chosen values and the setter.
 *
 * The array is memoised on its content, so it is not made anew when the address changes with the
 * card's `?workitem=` alone. That is an optimisation, not a promise (React may drop a memo's
 * cache): a caller that needs to know whether the filter changed compares the content. A change
 * replaces the address's entry rather than adding one, so Back leaves the page instead of
 * stepping through filters; every other parameter, the hash and the router state (the card's
 * history marks) are kept.
 */
export function useAddressFilter(
  param: string,
): readonly [string[], (values: readonly string[]) => void] {
  const { pathname, search, hash, state } = useLocation();
  const navigate = useNavigate();
  const content = JSON.stringify(filterValuesOf(search, param));
  const values = useMemo(() => JSON.parse(content) as string[], [content]);

  const setValues = useCallback(
    (next: readonly string[]) => {
      const params = new URLSearchParams(search);
      params.delete(param);
      for (const value of next) {
        params.append(param, value);
      }
      const rest = params.toString();
      navigate({ pathname, search: rest === '' ? '' : `?${rest}`, hash }, { replace: true, state });
    },
    [navigate, param, pathname, search, hash, state],
  );

  return [values, setValues];
}
