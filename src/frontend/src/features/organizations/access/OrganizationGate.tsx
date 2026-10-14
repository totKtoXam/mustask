/* eslint-disable react-refresh/only-export-components -- the gate's context
   and the hook that reads it live beside the layout route that provides them. */
import { createContext, useCallback, useContext, useMemo } from 'react';
import { Outlet, useParams } from 'react-router-dom';
import { AccessCheckContext, bindAccessCheck, useAccessCheck } from '../../../shared/lib/access';
import type { AccessAnswer } from './accessAnswer';
import { useFocusRescue } from './useFocusRescue';
import { useOrganizationAccess } from './useOrganizationAccess';

/** What the gate knows of the access read, for the pages under it (design § 2.1). */
export interface GateState {
  /** The held answer: the codes, or the 404; `undefined` while nothing is held. */
  answer: AccessAnswer | undefined;
  /** A read is running. */
  fetching: boolean;
  /** The last read failed (not 404). */
  failed: boolean;
  /** Why it failed. */
  error: unknown;
  /** «Повторить»: reads the access again; the page is row 1 while it runs. */
  retry(): void;
}

const GateContext = createContext<GateState | null>(null);

/** For `OrganizationPage`: the state of the gate above it. */
export function useGate(): GateState {
  const gate = useContext(GateContext);
  if (gate === null) {
    throw new Error('OrganizationPage must be rendered under OrganizationGate.');
  }
  return gate;
}

function signatureOf(
  organizationId: string,
  state: Pick<GateState, 'answer' | 'fetching' | 'failed'>,
) {
  const { answer, fetching, failed } = state;
  const held =
    answer === undefined
      ? failed && !fetching
        ? 'failed'
        : 'nothing'
      : answer.kind === 'member'
        ? `member:${answer.permissions.join(',')}`
        : 'not-member';
  return `${organizationId}|${held}`;
}

/**
 * The layout route of every page under `/organizations/:organizationId/…`
 * (mod-016 design § 2.1, plan-frontend P-3). It reads the organization of the
 * address — the pathless layout sees the child route's `:organizationId` — and
 * provides to everything under it:
 *
 * - the shell's one check **bound to that organization** (design § 2.6), so a
 *   page asks `useAccessCheck()(code, scope)` and passes no organization, and
 *   a page of B is decided by B's codes at every frame whatever the switcher
 *   still shows;
 * - the state of the read, which `OrganizationPage` turns into one of the five
 *   rows of § 2.1.
 *
 * It lives across moves between pages of one organization, so a move re-reads
 * nothing. It also rescues focus (P-10): when the codes or the row change and
 * the control that had focus is gone, focus goes to the main region.
 */
export function OrganizationGate() {
  const { organizationId = '' } = useParams<{ organizationId: string }>();
  const query = useOrganizationAccess(organizationId);
  const shellCheck = useAccessCheck();
  const bound = useMemo(
    () => bindAccessCheck(shellCheck, organizationId),
    [shellCheck, organizationId],
  );

  const { refetch } = query;
  const retry = useCallback(() => {
    void refetch();
  }, [refetch]);

  const answer = query.data;
  const fetching = query.isFetching;
  const failed = query.isError;
  const error = query.error;
  const state = useMemo<GateState>(
    () => ({ answer, fetching, failed, error, retry }),
    [answer, fetching, failed, error, retry],
  );

  useFocusRescue(signatureOf(organizationId, state));

  return (
    <AccessCheckContext.Provider value={bound}>
      <GateContext.Provider value={state}>
        <Outlet />
      </GateContext.Provider>
    </AccessCheckContext.Provider>
  );
}
