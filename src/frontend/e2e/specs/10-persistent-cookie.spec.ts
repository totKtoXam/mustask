import { expect, test } from '@playwright/test';
import { signIn } from '../support/auth';
import { getSessionCookie, withoutKeycloakSsoCookies } from '../support/cookies';

/**
 * `mustask.session` survives closing the browser:
 * its `expires` is set from `refresh_expires_in` (~8h), not tied to the
 * browser session. A fresh context built from the saved storage state
 * stands in for "close and reopen the browser whole".
 *
 * The second context is built from the
 * first context's `storageState`, but with the Keycloak SSO cookie stripped
 * out first (`withoutKeycloakSsoCookies`) — carrying it over unmodified
 * would leave a silent Keycloak SSO re-login able to explain the home page
 * rendering, which would prove nothing about `mustask.session` specifically
 * (the mistake an earlier draft of this spec made). `mustask.session` itself is
 * carried over, so the second context's rendered home page can only be
 * explained by it — and the spec asserts the cookie is present, not only
 * that the home page renders.
 */
test('10. mustask.session is persistent (~8h ahead) and survives a fresh browser context', async ({ browser }) => {
  const context1 = await browser.newContext();
  const page1 = await context1.newPage();
  await signIn(page1, { returnPath: '/home' });

  const cookie = await getSessionCookie(context1);
  expect(cookie, 'mustask.session must be set after sign-in').toBeTruthy();
  expect(cookie!.expires).toBeGreaterThan(0); // not a session cookie

  const hoursAhead = (cookie!.expires - Date.now() / 1000) / 3600;
  expect(hoursAhead).toBeGreaterThan(6);
  expect(hoursAhead).toBeLessThan(10);

  const storageState = await context1.storageState();
  await context1.close();

  const context2 = await browser.newContext({
    storageState: { ...storageState, cookies: withoutKeycloakSsoCookies(storageState.cookies) },
  });
  expect(
    await getSessionCookie(context2),
    'mustask.session must have been carried over into the second context',
  ).toBeTruthy();

  const page2 = await context2.newPage();
  await page2.goto('/home');
  // getByText('Главная') is not strict-mode safe: it matches both the nav
  // link and the page heading.
  await expect(page2.getByRole('heading', { name: 'Главная' })).toBeVisible({ timeout: 15_000 });
  await context2.close();
});
