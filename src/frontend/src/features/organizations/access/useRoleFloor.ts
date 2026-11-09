import { useMemo } from 'react';
import { roleBundles } from '../../../shared/lib/roleBundles';
import { useOrganizationAccess } from './useOrganizationAccess';

/**
 * The codes the caller's organization role gives on every object of the organization, from the role-bundle
 * contract (ADR-0038): the floor of what the caller holds anywhere — a grant adds to it, never takes from it.
 * `undefined` until the role is read, or for a caller who is not a member.
 */
export function useRoleFloor(organizationId: string): readonly string[] | undefined {
  const answer = useOrganizationAccess(organizationId).data;
  const role = answer?.kind === 'member' ? answer.role : undefined;
  return useMemo(
    () => (role === undefined ? undefined : roleBundles.organizationRoles[role]),
    [role],
  );
}
