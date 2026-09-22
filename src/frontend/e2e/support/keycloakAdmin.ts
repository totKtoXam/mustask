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
 * The permission-gating spec (16): the fixture user is
 * deliberately full-admin (`KeycloakStandProvisioner`'s `GroupPath`,
 * `/Администраторы`) — every route in the app is open to it, so there is no
 * real "denied" case to observe through it alone. Rather than temporarily
 * changing the shared fixture's own group (a different suite/session may
 * depend on it staying admin for the whole time this stand is up), this
 * creates a throwaway user in the realm's own pre-existing
 * `/Сотрудники` group (`deploy/keycloak/realms/mustask-realm.json`) —
 * the `viewer` role, `dictionaries.languages.read` only, no
 * `dictionaries.languages.write`. `createUser`+group-membership is within
 * `mustask-e2e`'s existing `manage-users` role — no new realm-management
 * scope needed, no realm-wide setting touched. `deleteEphemeralUser` below
 * must be called from the same spec's own cleanup so nothing outlives the
 * test that created it.
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
const READER_GROUP_PATH = '/Сотрудники';

export interface EphemeralUser {
  id: string;
  username: string;
}

export async function createEphemeralReaderUser(keycloakUrl: string, realm: string): Promise<EphemeralUser> {
  return withStandFile(async (stand) => {
    const token = await getServiceAccountToken(keycloakUrl, realm, stand);
    const authHeader = { Authorization: `Bearer ${token}` };
    const username = `e2e-permission-check-${Date.now()}`;

    const createResponse = await fetch(`${nodeUrl(keycloakUrl)}/admin/realms/${realm}/users`, {
      method: 'POST',
      headers: { ...authHeader, 'Content-Type': 'application/json' },
      body: JSON.stringify({
        username,
        enabled: true,
        emailVerified: true,
        email: `${username}@stand.local`,
        firstName: 'E2E',
        lastName: 'Reader',
      }),
    });
    if (createResponse.status !== 201) {
      throw new Error(
        `Could not create the ephemeral reader user '${username}': HTTP ${createResponse.status} ${await createResponse.text()}`,
      );
    }
    // Keycloak returns the new user's id only in `Location`, no body.
    const location = createResponse.headers.get('location');
    const userId = location?.split('/').pop();
    if (!userId) {
      throw new Error(`Keycloak created '${username}' but returned no Location header to read its id from.`);
    }

    const groupPath = READER_GROUP_PATH.split('/')
      .filter((segment) => segment.length > 0)
      .map(encodeURIComponent)
      .join('/');
    const groupResponse = await fetch(`${nodeUrl(keycloakUrl)}/admin/realms/${realm}/group-by-path/${groupPath}`, {
      headers: authHeader,
    });
    if (!groupResponse.ok) {
      throw new Error(`Could not resolve group '${READER_GROUP_PATH}': HTTP ${groupResponse.status}`);
    }
    const group = (await groupResponse.json()) as { id: string };

    const joinResponse = await fetch(`${nodeUrl(keycloakUrl)}/admin/realms/${realm}/users/${userId}/groups/${group.id}`, {
      method: 'PUT',
      headers: authHeader,
    });
    if (!joinResponse.ok) {
      throw new Error(`Could not add '${username}' to group '${READER_GROUP_PATH}': HTTP ${joinResponse.status}`);
    }

    return { id: userId, username };
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
