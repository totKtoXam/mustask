import type { ReactNode } from 'react';
import { ObjectAccess } from '../../../shared/lib/objectAccessProvider';
import { useRoleFloor } from './useRoleFloor';

/**
 * Wraps a page that lists objects of several kinds and whose rows carry no served `codes` (the organization's
 * teams, its project list's link dialog, the tree's link of teams): a code decided at a project, a team or a
 * department is asked of the role's floor, never of the organization answer (mod-019 BR-SA-18, F-1). The page
 * under-offers a holder of a grant on one object — the object's own page offers that grant's controls — and
 * never offers what the server would refuse by the role.
 */
export function RoleFloorAccess({
  organizationId,
  children,
}: {
  organizationId: string;
  children: ReactNode;
}) {
  const floor = useRoleFloor(organizationId);
  return <ObjectAccess codes={floor}>{children}</ObjectAccess>;
}
