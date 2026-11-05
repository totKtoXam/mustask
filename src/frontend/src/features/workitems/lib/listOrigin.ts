// How a work item's page knows it was opened from a row of the project's list (feat-003 BR-WS-5,
// design § 6, § 11): the row's link carries the list's own address in the router state, and a purge
// on the page returns to it — the filters kept, page 1 — instead of the unfiltered list. A page
// reached any other way (its address, another card's «Родитель») has no mark.
import { workItemsPath } from '../paths';

const LIST_ORIGIN_KEY = 'workItemsListOrigin';

/** The router state of a row's link: the list's address as it stands, filters included. */
export function listOriginState(pathname: string, search: string): Record<string, string> {
  return { [LIST_ORIGIN_KEY]: `${pathname}${search}` };
}

/**
 * The list address a page was opened from, or `null`. Only this project's list counts: a mark
 * that names another address (a stale entry, anything else in the state) is no mark.
 */
export function listOriginOf(
  state: unknown,
  organizationId: string,
  projectId: string,
): string | null {
  if (typeof state !== 'object' || state === null) {
    return null;
  }
  const origin = (state as Record<string, unknown>)[LIST_ORIGIN_KEY];
  if (typeof origin !== 'string') {
    return null;
  }
  const list = workItemsPath(organizationId, projectId);
  return origin === list || origin.startsWith(`${list}?`) ? origin : null;
}
