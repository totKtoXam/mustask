// Keycloak admin REST calls the suite still makes itself — authorized
// through the stand-only client `mustask-e2e`'s own service-account token
// (ADR-0023), never an admin password.
//
// `mustask-e2e` holds only the realm-management roles `impersonation` and
// `manage-users` (src/backend/Mustask.AppHost/Provisioning/KeycloakStandProvisioner.cs,
// `ImpersonationRoles`) — enough for `logoutUserSessions` below. It
// deliberately does **not** hold `view-realm`/`manage-realm`: specs 6, 9 and
// 11 used to shorten `accessTokenLifespan` through this module, which
// 403'd on exactly that gap. AppHost now sets that value itself at every
// stand start (`KeycloakStandProvisioner.SetAccessTokenLifespanAsync`) —
// this module has no realm-representation read/write left, and none should
// be added back: a suite that can write realm configuration is one refactor
// away from doing it again for convenience.

import { randomUUID } from 'node:crypto';
import { nodeUrl } from './env';
import { StaleStandCredentialError, withStandFile } from './standFile';
import type { StandFile } from './standFile';

async function getServiceAccountToken(keycloakUrl: string, realm: string, stand: StandFile): Promise<string> {
  const response = await fetch(`${nodeUrl(keycloakUrl)}/realms/${realm}/protocol/openid-connect/token`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
    body: new URLSearchParams({
      grant_type: 'client_credentials',
      client_id: stand.clientId,
      client_secret: stand.clientSecret,
    }),
  });
  // 401 here is Keycloak's `unauthorized_client` shape — a stale secret,
  // the one signal `withStandFile` (standFile.ts) retries on. Any other
  // status is a different, real failure and must not be retried silently.
  if (response.status === 401) {
    throw new StaleStandCredentialError(`'mustask-e2e' rejected as unauthorized_client: HTTP 401 ${await response.text()}`);
  }
  if (!response.ok) {
    throw new Error(`'mustask-e2e' could not obtain a service-account token: HTTP ${response.status} ${await response.text()}`);
  }
  const body = (await response.json()) as { access_token: string };
  return body.access_token;
}

/**
 * Ends every session of `username` (Users → Sessions
 * → "Sign out" in the console). Goes through `withStandFile` — same reason
 * as `impersonation.ts`: this call runs right after `signIn()` in spec 11,
 * shortly after specs 5/6 may have restarted the `keycloak` container, the
 * one window `withStandFile`'s retry still covers (`standFile.ts`'s own
 * header comment has the full account).
 */
export async function logoutUserSessions(keycloakUrl: string, realm: string, username: string): Promise<void> {
  return withStandFile(async (stand) => {
    const token = await getServiceAccountToken(keycloakUrl, realm, stand);
    const authHeader = { Authorization: `Bearer ${token}` };

    const usersResponse = await fetch(
      `${nodeUrl(keycloakUrl)}/admin/realms/${realm}/users?username=${encodeURIComponent(username)}&exact=true`,
      { headers: authHeader },
    );
    if (!usersResponse.ok) {
      throw new Error(`Failed to look up user '${username}': HTTP ${usersResponse.status}`);
    }
    const users = (await usersResponse.json()) as Array<{ id: string }>;
    const user = users[0];
    if (!user) {
      throw new Error(`User '${username}' not found in realm '${realm}'.`);
    }

    const logoutResponse = await fetch(`${nodeUrl(keycloakUrl)}/admin/realms/${realm}/users/${user.id}/logout`, {
      method: 'POST',
      headers: authHeader,
    });
    if (!logoutResponse.ok) {
      throw new Error(`Failed to end sessions for '${username}': HTTP ${logoutResponse.status}`);
    }
  });
}

/**
 * The throwaway users of the suite (specs 16, 19.9, 20.4, 21.6 and 27-30). The
 * fixture user is deliberately full-admin (`KeycloakStandProvisioner`'s
 * `GroupPath`, `/Администраторы`) — every route in the app is open to it, so
 * there is no real "denied" case to observe through it alone. Rather than
 * temporarily changing the shared fixture's own group (a different
 * suite/session may depend on it staying admin for the whole time this stand
 * is up), a spec creates a throwaway user and deletes it in its own cleanup
 * (`deleteEphemeralUser`, then `userExistsById`; `deleteEphemeralUsers` does
 * both) so nothing outlives the test that created it.
 *
 * Since mod-016 (D-7) the realm has no `/Сотрудники` group and no `viewer`
 * role: a user in NO group is the instance-level reader — it reads the
 * dictionaries through the realm's default roles and holds nothing else. It
 * holds no organization right either; those are never in the token and come
 * from a membership (ADR-0027), which a spec gives it through the API
 * (`membersApi.ts`). Creating a user is within `mustask-e2e`'s existing
 * `manage-users` role — no new realm-management scope needed, no realm-wide
 * setting touched.
 *
 * **The profile must be complete, not just `username`+`enabled`.** Live
 * finding (spec 16's first real run): realm `mustask`'s declarative user
 * profile requires `email`/`firstName`/`lastName`, and a user created
 * without them signs in fine at Keycloak but is then interrupted by a
 * required "Update Account Information" action instead of redirecting back
 * to the panel — `signIn()`'s own `waitForResponse` on
 * `/api/session/callback` times out, because that callback never happens.
 * Mirrors exactly the fields `KeycloakStandProvisioner
 * .CreateOrUpdateFixtureUserAsync` (`src/backend/Mustask.AppHost/Provisioning/
 * KeycloakStandProvisioner.cs`) already sets for `e2e-fixture` for the same
 * reason (its own comment cites the identical Keycloak behaviour) —
 * `emailVerified: true` closes the one required action a verified email
 * would otherwise leave pending; no `requiredActions` field is sent at all,
 * matching the fixture (a freshly created user's default is already `[]`
 * unless something asks for a specific action, and nothing here does).
 */
export interface EphemeralUser {
  id: string;
  username: string;
}

/**
 * Creates a throwaway user with the complete profile the realm requires (see
 * above) and returns its id, which Keycloak gives only in `Location`. Joins
 * no group: the callers decide that.
 */
async function createProfiledUser(
  keycloakUrl: string,
  realm: string,
  authHeader: { Authorization: string },
  user: { username: string; lastName: string; description: string },
): Promise<string> {
  const { username, lastName, description } = user;
  const createResponse = await fetch(`${nodeUrl(keycloakUrl)}/admin/realms/${realm}/users`, {
    method: 'POST',
    headers: { ...authHeader, 'Content-Type': 'application/json' },
    body: JSON.stringify({
      username,
      enabled: true,
      emailVerified: true,
      email: `${username}@stand.local`,
      firstName: 'E2E',
      lastName,
    }),
  });
  if (createResponse.status !== 201) {
    throw new Error(
      `Could not create the ephemeral ${description} '${username}': HTTP ${createResponse.status} ${await createResponse.text()}`,
    );
  }
  // Keycloak returns the new user's id only in `Location`, no body.
  const location = createResponse.headers.get('location');
  const userId = location?.split('/').pop();
  if (!userId) {
    throw new Error(`Keycloak created '${username}' but returned no Location header to read its id from.`);
  }
  return userId;
}

/**
 * A throwaway user in no group at all (specs 16, 19.9, 20.4, 21.6, 27.8): its
 * token carries `dictionaries.languages.read` from the realm's default roles and
 * nothing else — the reader of the dictionaries (D-7) and a non-member of every
 * organization. Replaces the former reader user in the `/Сотрудники` group, which
 * the realm no longer has.
 */
export async function createEphemeralUserWithoutGroup(keycloakUrl: string, realm: string): Promise<EphemeralUser> {
  return withStandFile(async (stand) => {
    const token = await getServiceAccountToken(keycloakUrl, realm, stand);
    const authHeader = { Authorization: `Bearer ${token}` };
    const username = `e2e-no-group-${Date.now()}`;
    const userId = await createProfiledUser(keycloakUrl, realm, authHeader, {
      username,
      lastName: 'NoGroup',
      description: 'user without a group',
    });
    return { id: userId, username };
  });
}

/**
 * A throwaway user for the membership specs (27-30, mod-016): a complete
 * profile, in no group, so its token carries no organization right of any kind
 * (ADR-0027) — whatever it may do in an organization it may do as a member of
 * it, added through the API. The name is «E2E <label> <token>», unique per run
 * (names are shown, searched and compared by it), where `token` is 8 hex digits
 * that also appear in the e-mail's user part: a search text that finds this
 * user and no other. Same `manage-users` right and the same cleanup duty
 * (`deleteEphemeralUsers`) as the others.
 */
export interface EphemeralMember extends EphemeralUser {
  email: string;
  displayName: string;
  /** 8 hex digits in the display name; typed into a user search, they find exactly this user. */
  token: string;
}

export async function createEphemeralMember(
  keycloakUrl: string,
  realm: string,
  label: string,
): Promise<EphemeralMember> {
  return withStandFile(async (stand) => {
    const token = await getServiceAccountToken(keycloakUrl, realm, stand);
    const authHeader = { Authorization: `Bearer ${token}` };
    const unique = randomUUID().slice(0, 8);
    const username = `e2e-member-${unique}`;
    const lastName = `${label} ${unique}`;
    const userId = await createProfiledUser(keycloakUrl, realm, authHeader, {
      username,
      lastName,
      description: 'member user',
    });
    return {
      id: userId,
      username,
      email: `${username}@stand.local`,
      // The projection's rule (mod-014 BR-UP-3): first name, a space, last name.
      displayName: `E2E ${lastName}`,
      token: unique,
    };
  });
}

/**
 * Disables or enables an account (Users → Enabled in the console): the
 * «неактивен» case of mod-016 US-1 and US-3. Reads the user and writes the whole
 * representation back with `enabled` changed — a partial `PUT` could drop
 * profile attributes of the realm's declarative user profile. Within
 * `mustask-e2e`'s `manage-users` right. The user projection learns of it
 * through Keycloak's admin event, a moment later (ADR-0025): wait for it with
 * `membersApi.ts`'s `awaitNotOffered` or `awaitMemberState`.
 */
export async function setUserEnabled(
  keycloakUrl: string,
  realm: string,
  userId: string,
  enabled: boolean,
): Promise<void> {
  return withStandFile(async (stand) => {
    const token = await getServiceAccountToken(keycloakUrl, realm, stand);
    const authHeader = { Authorization: `Bearer ${token}` };
    const address = `${nodeUrl(keycloakUrl)}/admin/realms/${realm}/users/${userId}`;
    const read = await fetch(address, { headers: authHeader });
    if (!read.ok) {
      throw new Error(`Could not read user '${userId}': HTTP ${read.status}`);
    }
    const representation = (await read.json()) as Record<string, unknown>;
    const write = await fetch(address, {
      method: 'PUT',
      headers: { ...authHeader, 'Content-Type': 'application/json' },
      body: JSON.stringify({ ...representation, enabled }),
    });
    if (!write.ok) {
      throw new Error(
        `Could not set user '${userId}' enabled=${enabled}: HTTP ${write.status} ${await write.text()}`,
      );
    }
  });
}

/** Best-effort by design (spec cleanup, `finally`) — a 404 (already gone) is not a failure. */
export async function deleteEphemeralUser(keycloakUrl: string, realm: string, userId: string): Promise<void> {
  return withStandFile(async (stand) => {
    const token = await getServiceAccountToken(keycloakUrl, realm, stand);
    const response = await fetch(`${nodeUrl(keycloakUrl)}/admin/realms/${realm}/users/${userId}`, {
      method: 'DELETE',
      headers: { Authorization: `Bearer ${token}` },
    });
    if (!response.ok && response.status !== 404) {
      throw new Error(`Could not delete ephemeral user '${userId}': HTTP ${response.status}`);
    }
  });
}

/** Confirms `deleteEphemeralUser` actually removed the user (spec cleanup — "gone", not just "deletion request accepted"). */
export async function userExistsById(keycloakUrl: string, realm: string, userId: string): Promise<boolean> {
  return withStandFile(async (stand) => {
    const token = await getServiceAccountToken(keycloakUrl, realm, stand);
    const response = await fetch(`${nodeUrl(keycloakUrl)}/admin/realms/${realm}/users/${userId}`, {
      headers: { Authorization: `Bearer ${token}` },
    });
    if (response.status === 404) {
      return false;
    }
    if (!response.ok) {
      throw new Error(`Could not check whether ephemeral user '${userId}' still exists: HTTP ${response.status}`);
    }
    return true;
  });
}

/**
 * The cleanup of the membership specs: deletes every user in `users` and
 * confirms each is gone, so a failed delete cannot leave a throwaway identity
 * behind. Runs them all before it throws, naming the ones left. MUSTASK's user
 * projection keeps each such user's record, held `Deleted` with the name and
 * e-mail last received (mod-014 BR-UP-5), as it keeps every deleted user's:
 * one row per throwaway user, shown «неактивен» where the user was a member.
 */
export async function deleteEphemeralUsers(
  keycloakUrl: string,
  realm: string,
  users: readonly Pick<EphemeralUser, 'id'>[],
): Promise<void> {
  const left: string[] = [];
  for (const user of users) {
    try {
      await deleteEphemeralUser(keycloakUrl, realm, user.id);
      if (await userExistsById(keycloakUrl, realm, user.id)) left.push(user.id);
    } catch {
      left.push(user.id);
    }
  }
  if (left.length > 0) {
    throw new Error(`Throwaway users were not deleted from realm '${realm}': ${left.join(', ')}`);
  }
}
