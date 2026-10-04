import { createContext, useContext } from 'react';

/**
 * The level a permission code is evaluated at (BR-8; doc-006 constraint 1):
 * instance codes travel in the access token (ADR-0018); organization-scoped
 * ones come from mod-016 against the current context.
 */
export type PermissionScope = 'instance' | 'organization' | 'project' | 'team';

/** Whether the signed-in user holds `code` at `scope`. */
export type AccessCheck = (code: string, scope: PermissionScope) => boolean;

const denyEverything: AccessCheck = () => false;

/**
 * The one permission check navigation and pages share (BR-8): neither keeps
 * a copy of its answers, so when the source changes — a token renewal, a
 * context switch, from mod-016 a refresh of served codes — both re-render
 * from the same function. The shell provides it; outside the shell it fails
 * closed.
 */
export const AccessCheckContext = createContext<AccessCheck>(denyEverything);

export function useAccessCheck(): AccessCheck {
  return useContext(AccessCheckContext);
}
