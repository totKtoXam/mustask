// The grants, the «Наследовать доступ» switch and the served reads of mod-019 over HTTP, strictly per
// docs/features/mod-019-scoped-access/api-contract.md §§ 1-5: one function per route, camelCase. The
// grant's key in a path is the grantee's user id; a grantee is never the giver (BR-SA-19).
import { apiFetch } from '../../../shared/api/httpClient';
import type { GrantRoleCode } from '../../../shared/lib/roleBundles';
import type { UserCandidates, UserRef } from './membershipApi';
import { areaApiPath, organizationApiPath, projectApiPath, teamApiPath } from './organizationsApi';

/** The role of a grant: the same three words in every scope (§ 0). */
export type GrantRole = GrantRoleCode;

/** The three kinds of object that hold grants; a project node and a sub-area hold none (OQ-1 A). */
export type GrantScope = 'department' | 'project' | 'team';

/** § 1: one grant row; a project's list also carries the department's rows, `origin: "department"`. */
export interface GrantRow {
  user: UserRef;
  role: GrantRole;
  origin: 'held' | 'department';
  /** Set when `origin` is `department`. */
  department: { id: string; name: string } | null;
  givenBy: UserRef;
  givenAt: string;
}

/** § 1: a project's list says which department it inherits from and whether the switch is on; teams and top-level projects: `null`. */
export interface GrantInheritance {
  departmentId: string;
  departmentName: string;
  inheritsAccess: boolean;
}

export interface GrantList {
  items: GrantRow[];
  inheritance: GrantInheritance | null;
}

/** § 1: what turning the switch off would do now, read once when the dialog opens. */
export interface SwitchImpact {
  holderCount: number;
  callerIncluded: boolean;
  givenGrants: { user: UserRef; role: GrantRole; givenBy: UserRef }[];
  givenGrantsTotal: number;
}

const segment = encodeURIComponent;

/** `…/{areas|projects|teams}/{id}/grants`: the one place a grants address is built (§ 2). */
export function grantsApiPath(organizationId: string, scope: GrantScope, objectId: string): string {
  const base =
    scope === 'department'
      ? areaApiPath(organizationId, objectId)
      : scope === 'project'
        ? projectApiPath(organizationId, objectId)
        : teamApiPath(organizationId, objectId);
  return `${base}/grants`;
}

export function getGrants(
  organizationId: string,
  scope: GrantScope,
  objectId: string,
): Promise<GrantList> {
  return apiFetch(grantsApiPath(organizationId, scope, objectId));
}

export function giveGrant(
  organizationId: string,
  scope: GrantScope,
  objectId: string,
  body: { userId: string; role: GrantRole },
): Promise<GrantRow> {
  return apiFetch(grantsApiPath(organizationId, scope, objectId), { method: 'POST', body });
}

export function changeGrantRole(
  organizationId: string,
  scope: GrantScope,
  objectId: string,
  userId: string,
  role: GrantRole,
): Promise<GrantRow> {
  return apiFetch(`${grantsApiPath(organizationId, scope, objectId)}/${segment(userId)}`, {
    method: 'PUT',
    body: { role },
  });
}

export function revokeGrant(
  organizationId: string,
  scope: GrantScope,
  objectId: string,
  userId: string,
): Promise<void> {
  return apiFetch(`${grantsApiPath(organizationId, scope, objectId)}/${segment(userId)}`, {
    method: 'DELETE',
  });
}

/** § 2: the object-aware search; the caller and the object's holders are left out by the server. */
export async function searchGrantCandidates(
  organizationId: string,
  scope: GrantScope,
  objectId: string,
  text: string,
): Promise<UserCandidates> {
  const query = new URLSearchParams({ text });
  const answer = await apiFetch<{ items: UserRef[]; moreMatched: boolean }>(
    `${grantsApiPath(organizationId, scope, objectId).replace(/grants$/, 'grant-candidates')}?${query}`,
  );
  // `UserSearchField` takes the member search's shape (`UserCandidate`): an active member has a name.
  return {
    users: answer.items.map((user) => ({
      id: user.id,
      displayName: user.displayName ?? '',
      email: user.email ?? '',
    })),
    moreMatched: answer.moreMatched,
  };
}

/** § 3: turns the node's switch; off takes effect on the next request. */
export function setInheritsAccess(
  organizationId: string,
  areaId: string,
  value: boolean,
): Promise<{ inheritsAccess: boolean }> {
  return apiFetch(`${areaApiPath(organizationId, areaId)}/inherits-access`, {
    method: 'PUT',
    body: { value },
  });
}

export function getSwitchImpact(organizationId: string, areaId: string): Promise<SwitchImpact> {
  return apiFetch(`${areaApiPath(organizationId, areaId)}/inherits-access/impact`);
}

/** § 5: the ids of the nodes where the caller holds `organizations.area.write`, once. */
export function getWritableAreaIds(organizationId: string): Promise<string[]> {
  return apiFetch(`${organizationApiPath(organizationId)}/areas/writable-ids`);
}
