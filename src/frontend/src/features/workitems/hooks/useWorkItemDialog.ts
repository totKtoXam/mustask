import { useCallback, useLayoutEffect, useRef } from 'react';
import { useLocation, useNavigate } from 'react-router-dom';

/** The address parameter the dialog lives in (BR-C-2): `?workitem=MSK-US-2`. */
export const WORK_ITEM_PARAM = 'workitem';

/**
 * The router-state key that marks an entry the card pushed (design § 4.1): how
 * many entries it has pushed on top of the page's own, so closing can go back
 * exactly that many.
 */
export const CARD_DEPTH_KEY = 'cardDepth';

/**
 * The router-state key that says where the card's run of entries started:
 * `'address'` when the entry under the first one it pushed is itself a dialog —
 * the address opened it — so going back by the depth lands on an entry that
 * still holds `?workitem=<first>` and a close has one more step to take.
 */
export const CARD_BASE_KEY = 'cardBase';
const BASE_ADDRESS = 'address';

function stateRecord(state: unknown): Record<string, unknown> {
  return typeof state === 'object' && state !== null ? (state as Record<string, unknown>) : {};
}

/** The entries the card pushed to reach this location: 0 for a page, or a dialog opened by address. */
function depthOf(state: unknown): number {
  const depth = stateRecord(state)[CARD_DEPTH_KEY];
  return typeof depth === 'number' && Number.isInteger(depth) && depth > 0 ? depth : 0;
}

/** Whether the card's run of entries to this location started on a dialog the address opened. */
function startedOnAddress(state: unknown): boolean {
  return stateRecord(state)[CARD_BASE_KEY] === BASE_ADDRESS;
}

/** The router state without the card's own marks, so a replace keeps whatever else the page held. */
function stateWithout(state: unknown, ...keys: string[]): Record<string, unknown> | undefined {
  const rest = Object.fromEntries(
    Object.entries(stateRecord(state)).filter(([key]) => !keys.includes(key)),
  );
  return Object.keys(rest).length > 0 ? rest : undefined;
}

/** The address the hook last saw, read by the moves when they run. */
interface Here {
  key: string;
  pathname: string;
  search: string;
  hash: string;
  state: unknown;
  identifier: string | null;
  depth: number;
}

/**
 * A close of a dialog the address opened, after it switched to a related item, goes back
 * onto the address-opened entry — which still holds `?workitem=<first>` — and then replaces
 * it without the parameter. This is the middle of that: the key of the entry the close left,
 * and — once the landing has been seen — the key of the entry it landed on, until the replace
 * has arrived too. It is a fact about the one history, not about a component: the close is
 * made by the dialog of the item it is leaving, and the landing is read by the host above it
 * — so it lives here, not in a ref.
 */
let closingRun: { from: string; landed: string | null } | null = null;

/** The address-opened entry a close has gone back onto, until its replace arrives. */
function isLandingOfClose(key: string, asked: string | null, depth: number): boolean {
  if (closingRun === null || asked === null || depth !== 0) {
    return false;
  }
  return closingRun.landed === null ? key !== closingRun.from : key === closingRun.landed;
}

export interface WorkItemDialogAddress {
  /** The identifier in `?workitem=`, as received; `null` while no dialog is open. */
  identifier: string | null;
  /** The entries the card has pushed on top of the page's own address. */
  depth: number;
  /**
   * Opens the dialog on `identifier` over the page, or switches the open one to
   * it: one more history entry either way, so Back shows the item before
   * (BR-C-1, BR-C-5).
   */
  open: (identifier: string) => void;
  /** The same as `open`: a switch to a related item, named for the place that asks. */
  switchTo: (identifier: string) => void;
  /**
   * Closes the dialog: back by the entries the card pushed, or — for a dialog the
   * address opened, which pushed none — a replace of the address without the
   * parameter, so Back still leaves the page (BR-C-2, BR-C-4). A dialog the address
   * opened that then switched to a related item does both: back by the entries it
   * pushed, which lands on the address-opened entry, and a replace of that entry
   * without the parameter. The edits' guard is the caller's; this only moves the
   * address.
   */
  close: () => void;
}

/**
 * The work item dialog's place in the address (US-1, US-6): `?workitem=<ID>` on
 * the page's own address, and the history depth that lets one close path go back
 * the right number of entries.
 *
 * It reads the address and moves it; what the dialog shows, and whether a close
 * is allowed while edits stand, belong to the host (the shell's `guard` and
 * `useGuardedLeave`). Leaving the page's address — another team, another level —
 * drops the parameter by itself, since the new address has none.
 */
export function useWorkItemDialog(): WorkItemDialogAddress {
  const { pathname, search, hash, state, key } = useLocation();
  const navigate = useNavigate();
  const received = new URLSearchParams(search).get(WORK_ITEM_PARAM);
  const asked = received !== null && received !== '' ? received : null;
  const askedDepth = asked === null ? 0 : depthOf(state);
  // The address-opened entry a close has just gone back onto is already on its way out: the
  // dialog reads it as closed, so it fades out where it stands instead of remounting on the
  // first item for the frames before the replace arrives.
  const leaving = isLandingOfClose(key, asked, askedDepth);
  const identifier = leaving ? null : asked;
  const depth = leaving ? 0 : askedDepth;

  // The moves read the address as it is when they run, not as it was when they were
  // made: so they keep one identity while the dialog opens, switches and closes, and a
  // memoised backlog row or card that holds `open` is not redrawn by the very
  // navigation it started.
  const at = useRef<Here>({
    key,
    pathname,
    search,
    hash,
    state,
    identifier: asked,
    depth: askedDepth,
  });
  useLayoutEffect(() => {
    at.current = { key, pathname, search, hash, state, identifier: asked, depth: askedDepth };
  });

  const replaceWithoutParameter = useCallback(
    (from: Here) => {
      const params = new URLSearchParams(from.search);
      params.delete(WORK_ITEM_PARAM);
      const rest = params.toString();
      navigate(
        { pathname: from.pathname, search: rest === '' ? '' : `?${rest}`, hash: from.hash },
        {
          replace: true,
          state: stateWithout(from.state, CARD_DEPTH_KEY, CARD_BASE_KEY),
        },
      );
    },
    [navigate],
  );

  const open = useCallback(
    (next: string) => {
      const current = at.current;
      const params = new URLSearchParams(current.search);
      params.set(WORK_ITEM_PARAM, next);
      // Over a dialog the address opened — or over a run of entries that started on one —
      // the card's run has that dialog under it, and a close must also strip its parameter.
      const onAddress =
        current.identifier !== null && (current.depth === 0 || startedOnAddress(current.state));
      navigate(
        { pathname: current.pathname, search: `?${params.toString()}`, hash: current.hash },
        {
          state: {
            ...stateWithout(current.state, CARD_BASE_KEY),
            [CARD_DEPTH_KEY]: current.depth + 1,
            ...(onAddress ? { [CARD_BASE_KEY]: BASE_ADDRESS } : {}),
          },
        },
      );
    },
    [navigate],
  );

  const close = useCallback(() => {
    const current = at.current;
    if (current.identifier === null) {
      return;
    }
    if (current.depth > 0) {
      closingRun = startedOnAddress(current.state) ? { from: current.key, landed: null } : null;
      navigate(-current.depth);
      return;
    }
    replaceWithoutParameter(current);
  }, [navigate, replaceWithoutParameter]);

  // The second step of such a close, once the address-opened entry has arrived: it still
  // holds `?workitem=<first>`, which is replaced away so the dialog does not stay open on the
  // item the user started from. Whatever arrives after the close spends the mark, so a stale
  // one never strips a later dialog; only an entry of the address-opened shape (a parameter,
  // no card depth) is stripped.
  useLayoutEffect(() => {
    if (closingRun === null || closingRun.from === key) {
      return;
    }
    if (closingRun.landed !== null) {
      if (closingRun.landed !== key) {
        closingRun = null;
      }
      return;
    }
    const here = at.current;
    if (here.identifier !== null && here.depth === 0) {
      closingRun.landed = key;
      replaceWithoutParameter(here);
    } else {
      closingRun = null;
    }
  }, [key, replaceWithoutParameter]);

  return { identifier, depth, open, switchTo: open, close };
}
