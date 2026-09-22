import { expect, test } from '@playwright/test';
import { signIn } from '../support/auth';
import { env } from '../support/env';
import { getSessionCookie } from '../support/cookies';

/**
 * An anonymous visit to a protected route that always exists
 * (`/dictionaries/languages`) lands back on that route after sign-in, not on
 * `/home` (RequireAuth + AuthContext).
 *
 * Also carries the suite's one recorded observation of the real OIDC round
 * trip — deliberately only here, not on every spec that calls `signIn`: the
 * suite's own `signIn` (support/auth.ts) replaces only the credential-typing
 * step with an impersonated Keycloak SSO cookie; this spec proves that
 * replacement still drives the panel's real
 * flow (redirect to Keycloak's authorization endpoint, a callback request,
 * `Set-Cookie: mustask.session` on that response), not a bypass of it.
 */
test('1. deep link — signs in and lands on the requested route, not /home', async ({ page }) => {
  const { keycloakUrl, apiUrl, realm } = env();

  expect(await getSessionCookie(page.context()), 'mustask.session must not exist before the callback').toBeUndefined();

  const authEndpointNavigation = page.waitForRequest((request) =>
    request.url().startsWith(`${keycloakUrl}/realms/${realm}/protocol/openid-connect/auth`),
  );
  const callbackResponse = page.waitForResponse((response) =>
    response.url().startsWith(`${apiUrl}/api/session/callback`),
  );

  const [, callback] = await Promise.all([authEndpointNavigation, callbackResponse, signIn(page, { returnPath: '/dictionaries/languages' })]);

  await expect(page).toHaveURL(`${env().panelUrl}/dictionaries/languages`);

  const headers = await callback.headersArray();
  const setCookie = headers.filter((h) => h.name.toLowerCase() === 'set-cookie').map((h) => h.value);
  expect(setCookie.some((value) => value.startsWith('mustask.session=')), 'the callback response must carry Set-Cookie: mustask.session').toBe(
    true,
  );
});
