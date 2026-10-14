// The membership and team-member routes over HTTP, strictly per
// docs/features/mod-016-organization-access/api-contract.md § 1.2–§ 1.6 and
// § 2: one function per route, camelCase as all of Mustask.Api. Ids come from
// the address or from an answer and are re-encoded in the path as received
// (§ 0.3). Every list is a plain array in the server's order, rendered as
// received (§ 0.5): no paging, no sorting, no filter.
import { apiFetch } from '../../../shared/api/httpClient';
import type { OrganizationRole } from '../lib/roles';
import { organizationApiPath, projectApiPath } from './organizationsApi';

/** § 0.4: how the projection knows a user. */
export type UserState = 'active' | 'inactive' | 'unknown';

/** § 0.4: the one shape that names a person in every list. */
export interface UserRef {
  id: string;
  /** `null` only when `state` is `unknown`. */
  displayName: string | null;
  /** `""` for a known user with none; `null` only when `unknown`. */
  email: string | null;
  state: UserState;
}

/** § 1.2: one membership. */
export interface Member {
  user: UserRef;
  role: OrganizationRole;
  addedBy: UserRef;
  addedAt: string;
  /** `null` while the role never changed. */
  roleChangedBy: UserRef | null;
  roleChangedAt: string | null;
}

/** § 1.3: a user the add search offers. `email` is `""` when the user has none. */
export interface UserCandidate {
  id: string;
  displayName: string;
  email: string;
}

/** § 1.3: at most 20 users, and whether more matched. */
export interface UserCandidates {
  users: UserCandidate[];
  moreMatched: boolean;
}

const segment = encodeURIComponent;

/** `/api/organizations/{o}/members`. */
export function membersApiPath(organizationId: string): string {
  return `${organizationApiPath(organizationId)}/members`;
}

function teamApiPath(organizationId: string, projectId: string, teamId: string): string {
  return `${projectApiPath(organizationId, projectId)}/teams/${segment(teamId)}`;
}

/**
 * `…/teams/{t}/members`: the one place a team's address is built (plan-frontend
 * § 3.2), so the section follows the team wherever feat-028 puts it.
 */
export function teamMembersApiPath(
  organizationId: string,
  projectId: string,
  teamId: string,
): string {
  return `${teamApiPath(organizationId, projectId, teamId)}/members`;
}

// § 1 Organization members

export function getMembers(organizationId: string): Promise<Member[]> {
  return apiFetch(membersApiPath(organizationId));
}

/** § 1.3: under 2 characters the server answers nobody; the panel does not ask. */
export function searchMemberCandidates(
  organizationId: string,
  text: string,
): Promise<UserCandidates> {
  const query = new URLSearchParams({ text });
  return apiFetch(`${organizationApiPath(organizationId)}/member-candidates?${query}`);
}

export function addMember(
  organizationId: string,
  body: { userId: string; role: OrganizationRole },
): Promise<Member> {
  return apiFetch(membersApiPath(organizationId), { method: 'POST', body });
}

export function changeMemberRole(
  organizationId: string,
  userId: string,
  role: OrganizationRole,
): Promise<Member> {
  return apiFetch(`${membersApiPath(organizationId)}/${segment(userId)}/role`, {
    method: 'PUT',
    body: { role },
  });
}

export function removeMember(organizationId: string, userId: string): Promise<void> {
  return apiFetch(`${membersApiPath(organizationId)}/${segment(userId)}`, { method: 'DELETE' });
}

// § 2 Team members

export function getTeamMembers(
  organizationId: string,
  projectId: string,
  teamId: string,
): Promise<UserRef[]> {
  return apiFetch(teamMembersApiPath(organizationId, projectId, teamId));
}

/** § 2.2: active members of the organization only. */
export function searchTeamMemberCandidates(
  organizationId: string,
  projectId: string,
  teamId: string,
  text: string,
): Promise<UserCandidates> {
  const query = new URLSearchParams({ text });
  return apiFetch(`${teamApiPath(organizationId, projectId, teamId)}/member-candidates?${query}`);
}

export function addTeamMember(
  organizationId: string,
  projectId: string,
  teamId: string,
  userId: string,
): Promise<UserRef> {
  return apiFetch(teamMembersApiPath(organizationId, projectId, teamId), {
    method: 'POST',
    body: { userId },
  });
}

export function removeTeamMember(
  organizationId: string,
  projectId: string,
  teamId: string,
  userId: string,
): Promise<void> {
  return apiFetch(`${teamMembersApiPath(organizationId, projectId, teamId)}/${segment(userId)}`, {
    method: 'DELETE',
  });
}
