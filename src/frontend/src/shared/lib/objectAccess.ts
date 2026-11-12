import { useMemo } from 'react';
import { useAccessCheck, type AccessCheck } from './access';
import { isOrganizationScope } from './roleBundles';

/**
 * The check of a page that shows one object — a project, a team, a department or a node (mod-019 BR-SA-18,
 * design § 6). An organization or instance code is asked of the page's bound check, as before; a code decided
 * at a narrower scope is asked of the object's own served `codes`, never of the organization answer, which
 * does not carry what a grant gives. While the object is unread (`codes` absent) the narrower codes fail
 * closed: unknown is not granted.
 */
export function objectAccessCheck(
  base: AccessCheck,
  codes: readonly string[] | null | undefined,
): AccessCheck {
  return (code, scope, organizationId) => {
    if (scope === 'instance' || isOrganizationScope(code)) {
      return base(code, scope, organizationId);
    }
    return codes?.includes(code) ?? false;
  };
}

export function useObjectAccess(codes: readonly string[] | null | undefined): AccessCheck {
  const base = useAccessCheck();
  return useMemo(() => objectAccessCheck(base, codes), [base, codes]);
}
