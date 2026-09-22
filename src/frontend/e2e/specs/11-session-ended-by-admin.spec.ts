import { expect, test } from '@playwright/test';
import { signIn } from '../support/auth';
import { env } from '../support/env';
import { logoutUserSessions } from '../support/keycloakAdmin';
import { getSessionCookie } from '../support/cookies';

/**
 * An administrator ending the user's Keycloak
 * sessions makes the next real renewal answer 401 `session.expired` and
 * drop `mustask.session` (the renewed id-token is checked too, not just
 * the refresh grant itself).
 *
 * First real run (2026-09-11): the original spec signed in, ended
 * the sessions, then reloaded and expected the message immediately. It
 * cannot show up that way — `POST /api/session/token` serves the backend's
 * *cached* access token, without contacting Keycloak at all, whenever that
 * token still has more than 60s left (RENEW_LEAD_MS); this
 * is an accepted rule, not a bug. A reload right after the admin
 * sign-out only re-reads the cache. The revocation only becomes observable
 * at the next *real* renewal, within one access-token lifetime.
 *
 * The short lifetime needed to wait that renewal out in a
 * reasonable time is no longer this spec's own setting. `mustask-e2e`
 * deliberately lacks `manage-realm`/`view-realm` (it stays at exactly
 * `impersonation`+`manage-users`); AppHost sets the realm's
 * `accessTokenLifespan` to 120s at every stand start
 * (`KeycloakStandProvisioner.SetAccessTokenLifespanAsync`,
 * `KeycloakStandIdentifiers.AccessTokenLifespanSeconds` —
 * `docs/templates/examples/tech-159-e2e-without-password/plan-backend.md` §11).
 * `logoutUserSessions` below needs only
 * `manage-users`, which `mustask-e2e` does hold, and runs unconditionally.
 */
test.describe('11. session ended by an administrator', () => {
  test('next real renewal shows "Сессия истекла", mustask.session is gone', async ({ page, context }) => {
    // accessTokenLifespan is the stand's own 120s and the wait below is
    // past the 60s renewal lead time — past the config's default 60s test
    // timeout regardless of Keycloak/network speed.
    test.setTimeout(150_000);

    await signIn(page, { returnPath: '/home' });

    await logoutUserSessions(env().keycloakUrl, env().realm, env().user);

    // accessTokenLifespan is the stand's own 120s, renewal fires 60s
    // before expiry (RENEW_LEAD_MS, shared/api/session.ts) — wait past that
    // moment so the panel actually calls Keycloak instead of serving the
    // cached token (see the header comment).
    await expect(page.getByText('Сессия истекла, войдите заново')).toBeVisible({ timeout: 90_000 });
    expect(await getSessionCookie(context)).toBeUndefined();
  });
});
