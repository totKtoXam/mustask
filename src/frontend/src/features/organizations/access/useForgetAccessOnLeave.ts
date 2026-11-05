import { useEffect, useRef } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import { focusMainRegion } from '../../../shared/lib/mainRegion';
import { forgetAccess } from './accessCache';

/**
 * For a page whose action ends the caller's access to its organization — the
 * caller removes themselves (mod-016 design § 3.7), or deactivates the
 * organization — and then leaves for a page outside the organization.
 * `leave()` is called when the action succeeded, before the navigation; the
 * organization's served access then leaves the tab's memory once the page has
 * unmounted — and with it the organization gate above it — so the gate never
 * reads a 404 it has no use for (plan-frontend § 3.1, § 3.3). With
 * `focusMain`, focus goes to the main region as the page goes: the control
 * that had it is gone (design § 2.2).
 */
export function useForgetAccessOnLeave(
  organizationId: string,
  { focusMain = false }: { focusMain?: boolean } = {},
): { leave(): void } {
  const client = useQueryClient();
  const left = useRef(false);

  useEffect(
    () => () => {
      if (left.current) {
        left.current = false;
        forgetAccess(client, organizationId);
        if (focusMain) {
          focusMainRegion();
        }
      }
    },
    [client, organizationId, focusMain],
  );

  return {
    leave() {
      left.current = true;
    },
  };
}
