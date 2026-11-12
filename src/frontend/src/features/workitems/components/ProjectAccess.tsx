import type { ReactNode } from 'react';
import { useProjectCodes } from '../../organizations';
import { ObjectAccess } from '../../../shared/lib/objectAccessProvider';

/**
 * Wraps what shows or changes the work items of one project: everything below asks `useAccessCheck()` and
 * the dialogs' locks, and gets the project's served `codes` for the work-item codes (mod-019 BR-SA-18, F-1),
 * never the organization answer, which does not carry them. Until the project list is read, or when it does
 * not hold the project, the codes are absent and every control fails closed.
 */
export function ProjectAccess({
  organizationId,
  projectId,
  children,
  pending,
}: {
  organizationId: string;
  projectId: string;
  children: ReactNode;
  /** Drawn instead of the children until the project list is read, for a surface that would flash read-only. */
  pending?: ReactNode;
}) {
  const { codes, isPending } = useProjectCodes(organizationId);
  if (isPending && pending !== undefined) {
    return <>{pending}</>;
  }
  return <ObjectAccess codes={codes?.get(projectId)}>{children}</ObjectAccess>;
}
