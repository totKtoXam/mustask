import { useCallback } from 'react';
import { useWorkContext, type WorkContextObjectKind } from '../../../shared/lib/workContext';
import { organizationAreaPath, organizationTeamPath, projectGeneralPath } from '../paths';

/** The router state a link to an object carries when it reset a selector (D-3): the shell says it. */
export interface ObjectLinkState {
  resetNotice: string;
}

export interface ObjectLink {
  to: string;
  state?: ObjectLinkState;
}

/**
 * Where a row or a link to a department, team or project leads (ADR-0032 § 5, D-3): the shell's
 * context makes the object the most specific one and keeps the less specific selectors that fit.
 * Without a context (a stub) it is the object's plain address.
 */
export function useObjectLink(organizationId: string) {
  const { objectLink } = useWorkContext();
  return useCallback(
    (kind: WorkContextObjectKind, id: string): ObjectLink => {
      const link = objectLink?.(kind, id);
      if (link) {
        return link.notice ? { to: link.to, state: { resetNotice: link.notice } } : { to: link.to };
      }
      switch (kind) {
        case 'project':
          return { to: projectGeneralPath(organizationId, id) };
        case 'team':
          return { to: organizationTeamPath(organizationId, id) };
        case 'department':
          return { to: organizationAreaPath(organizationId, id) };
      }
    },
    [objectLink, organizationId],
  );
}
