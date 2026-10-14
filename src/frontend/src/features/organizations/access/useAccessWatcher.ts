import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import { organizationsKeys } from '../api/queryKeys';
import { roleChangeNotice, type OrganizationRole } from '../lib/roles';
import type { AccessAnswer } from './accessAnswer';
import { holdNotMember, rereadAccess } from './accessCache';
import { forgetRole, lastRole, rememberRole } from './roleMemory';
import { scopedFailureOf } from './scopedFailure';
import { accessOrganizationOf } from './servedAccess';

export interface AccessWatcherOptions {
  /** The switcher's fallback notice is on screen: it wins, and no role change is raised (DC-11). */
  fallbackShown: boolean;
  /** The name of an organization the switcher lists; `undefined` while it does not (yet) (P-11). */
  organizationName(organizationId: string): string | undefined;
}

/** The role-change notice to show (design § 2.4). */
export interface RoleChangeNotice {
  /** Tells one notice from the next. */
  id: number;
  text: string;
}

interface PendingRoleChange {
  id: number;
  organizationId: string;
  role: OrganizationRole;
}

/**
 * The shell's three duties around served access (mod-016 plan-frontend P-4,
 * design § 2.2, § 2.4), in one subscription to the query and mutation caches
 * of whatever client the tree provides:
 *
 * 1. **A scoped failure.** A 403, or any 404, from a call under
 *    `/api/organizations/<id>/…` other than the access read says the caller's
 *    access may have changed (BR-M1-31): the access is read again, in the
 *    background. A 404 «Организация не найдена» first makes the tab hold that
 *    answer, so the page is the full-page frame at once (DC-12).
 * 2. **A turn to "not a member".** The first time an organization's access
 *    answers 404 — from codes, from nothing, or from a call's 404 — the
 *    organization list is read once, so the switcher falls back (P-15). A
 *    second 404 for the same organization is not a turn: the re-read that
 *    follows a call's 404 starts no second list read.
 * 3. **A role change.** A 200 whose role differs from the one last read for
 *    that organization — never the first read — raises the role-change
 *    notice, unless the fallback notice is shown (DC-11). It names the
 *    organization from the switcher's list, and waits for the name when the
 *    list does not hold it yet; it is dropped if the access turns "not a
 *    member" meanwhile (P-11).
 *
 * The notice is the shell's to render; it closes through `dismissRoleChange`
 * (a host that shows its own notice claims the corner: `shared/lib/noticeCorner`).
 */
export function useAccessWatcher({ fallbackShown, organizationName }: AccessWatcherOptions): {
  roleChange: RoleChangeNotice | null;
  dismissRoleChange(): void;
} {
  const client = useQueryClient();
  const [pending, setPending] = useState<PendingRoleChange | null>(null);
  const noticeIds = useRef(0);
  const fallbackShownRef = useRef(fallbackShown);

  useEffect(() => {
    fallbackShownRef.current = fallbackShown;
  }, [fallbackShown]);

  useEffect(() => {
    // What each organization's held answer was at the last event: a turn is a
    // change *to* `not-member`.
    const held = new Map<string, AccessAnswer['kind']>();
    for (const query of client.getQueryCache().findAll({ queryKey: organizationsKeys.accesses })) {
      const organizationId = accessOrganizationOf(query.queryKey);
      const answer = query.state.data as AccessAnswer | undefined;
      if (organizationId !== null && answer !== undefined) {
        held.set(organizationId, answer.kind);
      }
    }

    const onAnswer = (organizationId: string, answer: AccessAnswer | undefined) => {
      if (answer === undefined) {
        return;
      }
      const before = held.get(organizationId);
      held.set(organizationId, answer.kind);
      if (answer.kind === 'not-member') {
        setPending((current) => (current?.organizationId === organizationId ? null : current));
        if (before !== 'not-member') {
          void client.invalidateQueries(
            { queryKey: organizationsKeys.organizations('active') },
            { cancelRefetch: false },
          );
        }
        return;
      }
      const previous = lastRole(client, organizationId);
      rememberRole(client, organizationId, answer.role);
      if (previous !== undefined && previous !== answer.role && !fallbackShownRef.current) {
        noticeIds.current += 1;
        setPending({ id: noticeIds.current, organizationId, role: answer.role });
      }
    };

    const onFailure = (error: unknown) => {
      const failure = scopedFailureOf(error);
      if (failure === null) {
        return;
      }
      if (failure.organizationNotFound) {
        holdNotMember(client, failure.organizationId);
      }
      void rereadAccess(client, failure.organizationId, { inFlight: 'join' });
    };

    const stopQueries = client.getQueryCache().subscribe((event) => {
      const organizationId = accessOrganizationOf(event.query.queryKey);
      if (event.type === 'removed') {
        if (organizationId !== null) {
          held.delete(organizationId);
          forgetRole(client, organizationId);
          setPending((current) => (current?.organizationId === organizationId ? null : current));
        }
        return;
      }
      if (event.type !== 'updated') {
        return;
      }
      if (organizationId !== null) {
        // The access read's own failures change nothing (P-2): a 404 is its
        // answer, anything else leaves what is held.
        if (event.action.type === 'success') {
          onAnswer(organizationId, event.query.state.data as AccessAnswer | undefined);
        }
        return;
      }
      if (event.action.type === 'error') {
        onFailure(event.action.error);
      }
    });
    const stopMutations = client.getMutationCache().subscribe((event) => {
      if (event.type === 'updated' && event.action.type === 'error') {
        onFailure(event.action.error);
      }
    });
    return () => {
      stopQueries();
      stopMutations();
    };
  }, [client]);

  // The fallback notice wins: while it is shown no role change is raised, and
  // one that waited is dropped (adjusting state while rendering, as the
  // switcher's own bookkeeping does).
  if (fallbackShown && pending !== null) {
    setPending(null);
  }

  const name = pending ? organizationName(pending.organizationId) : undefined;
  const roleChange = useMemo<RoleChangeNotice | null>(
    () =>
      pending && name !== undefined && !fallbackShown
        ? { id: pending.id, text: roleChangeNotice(name, pending.role) }
        : null,
    [pending, name, fallbackShown],
  );
  const dismissRoleChange = useCallback(() => setPending(null), []);
  return { roleChange, dismissRoleChange };
}
