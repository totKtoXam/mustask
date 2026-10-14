// Specs 27-30 (mod-016): Node-side access to the membership routes of
// `api-contract.md` § 1-§ 2 of mod-016-organization-access, used to set up and
// restore members around a test and to make the requests the panel never sends
// (a user's next write after a role change, an access read as another user).
//
// Every call goes through `organizationsApi.ts`'s `send`, so the token
// exchange, its rules (never logged, never handed to a page) and the prior
// `signIn(page, …)` it needs are that module's: the `request` a function takes
// is the `page.request` of the user it should act as. A spec that acts as an
// administrator and as a member holds two signed-in contexts and passes each
// one's request.
import type { APIRequestContext } from '@playwright/test';
import { env } from './env';
import { createEphemeralMember, type EphemeralMember } from './keycloakAdmin';
import { send } from './organizationsApi';

export type OrganizationRole = 'admin' | 'member' | 'viewer';

/** api-contract § 0.4. */
export interface UserRefDto {
  id: string;
  displayName: string | null;
  email: string | null;
  state: 'active' | 'inactive' | 'unknown';
}

/** api-contract § 1.2. */
export interface MemberDto {
  user: UserRefDto;
  role: OrganizationRole;
  addedBy: UserRefDto;
  addedAt: string;
  roleChangedBy: UserRefDto | null;
  roleChangedAt: string | null;
}

/** api-contract § 1.1. */
export interface AccessDto {
  role: OrganizationRole;
  permissions: string[];
}

/** api-contract § 1.3 and § 2.2. */
export interface CandidatesDto {
  users: { id: string; displayName: string; email: string }[];
  moreMatched: boolean;
}

const organizationPath = (organizationId: string) =>
  `/api/organizations/${encodeURIComponent(organizationId)}`;

/** mod-003 api-contract § 2: the base of the work-item routes of a project. */
export const workItemsPath = (organizationId: string, projectId: string) =>
  `${organizationPath(organizationId)}/projects/${encodeURIComponent(projectId)}/workitems`;

/** api-contract § 2: the base of every team-member route. */
export const teamPath = (organizationId: string, projectId: string, teamId: string) =>
  `${organizationPath(organizationId)}/projects/${encodeURIComponent(projectId)}/teams/${encodeURIComponent(teamId)}`;

async function call<T>(
  request: APIRequestContext,
  method: 'GET' | 'POST' | 'PUT' | 'DELETE' | 'PATCH',
  path: string,
  body?: unknown,
): Promise<T> {
  const response = await send(request, method, path, body);
  if (!response.ok()) {
    throw new Error(`${method} ${path} failed: HTTP ${response.status()} ${await response.text()}`);
  }
  return response.status() === 204 ? (undefined as T) : ((await response.json()) as T);
}

/** § 1.1: the caller's role and organization codes. */
export function accessOf(request: APIRequestContext, organizationId: string): Promise<AccessDto> {
  return call(request, 'GET', `${organizationPath(organizationId)}/access`);
}

/** § 1.2. */
export function members(request: APIRequestContext, organizationId: string): Promise<MemberDto[]> {
  return call(request, 'GET', `${organizationPath(organizationId)}/members`);
}

/** § 1.3. */
export function memberCandidates(
  request: APIRequestContext,
  organizationId: string,
  text: string,
): Promise<CandidatesDto> {
  return call(
    request,
    'GET',
    `${organizationPath(organizationId)}/member-candidates?text=${encodeURIComponent(text)}`,
  );
}

/** § 1.4. */
export function addMember(
  request: APIRequestContext,
  organizationId: string,
  userId: string,
  role: OrganizationRole,
): Promise<MemberDto> {
  return call(request, 'POST', `${organizationPath(organizationId)}/members`, { userId, role });
}

/** § 1.5. */
export function changeMemberRole(
  request: APIRequestContext,
  organizationId: string,
  userId: string,
  role: OrganizationRole,
): Promise<MemberDto> {
  return call(
    request,
    'PUT',
    `${organizationPath(organizationId)}/members/${encodeURIComponent(userId)}/role`,
    { role },
  );
}

/** § 1.6. */
export function removeMember(
  request: APIRequestContext,
  organizationId: string,
  userId: string,
): Promise<void> {
  return call(
    request,
    'DELETE',
    `${organizationPath(organizationId)}/members/${encodeURIComponent(userId)}`,
  );
}

/** § 2.1. */
export function teamMembers(
  request: APIRequestContext,
  organizationId: string,
  projectId: string,
  teamId: string,
): Promise<UserRefDto[]> {
  return call(request, 'GET', `${teamPath(organizationId, projectId, teamId)}/members`);
}

/** § 2.3. */
export function addTeamMember(
  request: APIRequestContext,
  organizationId: string,
  projectId: string,
  teamId: string,
  userId: string,
): Promise<UserRefDto> {
  return call(request, 'POST', `${teamPath(organizationId, projectId, teamId)}/members`, {
    userId,
  });
}

/** § 2.4. */
export function removeTeamMember(
  request: APIRequestContext,
  organizationId: string,
  projectId: string,
  teamId: string,
  userId: string,
): Promise<void> {
  return call(
    request,
    'DELETE',
    `${teamPath(organizationId, projectId, teamId)}/members/${encodeURIComponent(userId)}`,
  );
}

/**
 * The membership of the user whose e-mail is `email` (case-insensitive) — how a
 * spec finds the fixture user's id, which `signIn` never hands out: the token
 * stays inside `organizationsApi.ts`. The caller must read members.
 */
export async function memberByEmail(
  request: APIRequestContext,
  organizationId: string,
  email: string,
): Promise<MemberDto> {
  const found = (await members(request, organizationId)).find(
    (member) => member.user.email?.toLowerCase() === email.toLowerCase(),
  );
  if (!found) {
    throw new Error(`Organization ${organizationId} has no member with the e-mail ${email}.`);
  }
  return found;
}

/** The fixture user's membership (`e2e-fixture@stand.local`, set by AppHost's provisioner). */
export function fixtureMember(
  request: APIRequestContext,
  organizationId: string,
): Promise<MemberDto> {
  return memberByEmail(request, organizationId, `${env().user}@stand.local`);
}

/**
 * The assignee of a work item as the read gives it (mod-003 api-contract § 1.1,
 * with `isMember` of mod-016 api-contract § 3.3), `null` for «Не назначен».
 */
export async function assigneeOf(
  request: APIRequestContext,
  organizationId: string,
  projectId: string,
  identifier: string,
): Promise<{ id: string; displayName: string | null; isMember?: boolean } | null> {
  const item = await call<{
    assignee: { id: string; displayName: string | null; isMember?: boolean } | null;
  }>(
    request,
    'GET',
    `${organizationPath(organizationId)}/projects/${encodeURIComponent(projectId)}/workitems/${encodeURIComponent(identifier)}`,
  );
  return item.assignee;
}

const POLL_INTERVAL_MS = 1_000;
const DIRECTORY_TIMEOUT_MS = 60_000;

/**
 * Polls `read` once a second until `done` accepts its answer, or fails after
 * `timeoutMs` with `what`. The user projection is fed by Keycloak's event
 * listener asynchronously (ADR-0025), so a user created or edited through the
 * admin REST reaches the member search a moment later, not at once.
 */
async function pollUntil<T>(
  read: () => Promise<T>,
  done: (answer: T) => boolean,
  what: string,
  timeoutMs = DIRECTORY_TIMEOUT_MS,
): Promise<T> {
  const deadline = Date.now() + timeoutMs;
  for (;;) {
    const answer = await read();
    if (done(answer)) return answer;
    if (Date.now() >= deadline) {
      throw new Error(`${what} did not happen within ${Math.round(timeoutMs / 1000)} s.`);
    }
    await new Promise((resolve) => setTimeout(resolve, POLL_INTERVAL_MS));
  }
}

/**
 * Waits until the member search of `organizationId`, read by the administrator
 * `request` acts as, offers `user` — the projection holds them, active
 * (api-contract § 1.3). Fails after 60 s naming the text searched.
 */
export async function awaitInDirectory(
  request: APIRequestContext,
  organizationId: string,
  user: Pick<EphemeralMember, 'id' | 'token'>,
): Promise<void> {
  await pollUntil(
    () => memberCandidates(request, organizationId, user.token),
    (answer) => answer.users.some((candidate) => candidate.id === user.id),
    `The user projection offering «${user.token}»`,
  );
}

/**
 * Waits until the member search no longer offers `user`: their account is
 * disabled and the projection has received it (a disabled user is not a
 * candidate, api-contract § 1.3).
 */
export async function awaitNotOffered(
  request: APIRequestContext,
  organizationId: string,
  user: Pick<EphemeralMember, 'id' | 'token'>,
): Promise<void> {
  await pollUntil(
    () => memberCandidates(request, organizationId, user.token),
    (answer) => answer.users.every((candidate) => candidate.id !== user.id),
    `The user projection withdrawing «${user.token}» from the member search`,
  );
}

/** Waits until the members list reads `userId` in `state` (a disabled member turns `inactive`). */
export async function awaitMemberState(
  request: APIRequestContext,
  organizationId: string,
  userId: string,
  state: UserRefDto['state'],
): Promise<void> {
  await pollUntil(
    () => members(request, organizationId),
    (list) => list.some((member) => member.user.id === userId && member.user.state === state),
    `Member ${userId} turning ${state}`,
  );
}

/**
 * A throwaway user (`keycloakAdmin.ts`'s `createEphemeralMember`, in no group),
 * waited for in the directory and added to `organizationId` with `role` by the
 * administrator `request` acts as. The caller deletes the user in its cleanup
 * (`deleteEphemeralUsers`).
 */
export async function addEphemeralMember(
  request: APIRequestContext,
  organizationId: string,
  label: string,
  role: OrganizationRole = 'member',
): Promise<EphemeralMember> {
  const { keycloakUrl, realm } = env();
  const user = await createEphemeralMember(keycloakUrl, realm, label);
  await awaitInDirectory(request, organizationId, user);
  await addMember(request, organizationId, user.id, role);
  return user;
}

/**
 * A throwaway user in the directory but in no organization yet — a candidate
 * for the add dialog — waited for through `organizationId`'s search.
 */
export async function createDirectoryUser(
  request: APIRequestContext,
  organizationId: string,
  label: string,
): Promise<EphemeralMember> {
  const { keycloakUrl, realm } = env();
  const user = await createEphemeralMember(keycloakUrl, realm, label);
  await awaitInDirectory(request, organizationId, user);
  return user;
}
