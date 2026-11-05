import type { Page } from '@playwright/test';
import { env } from './env';
import { obtainKeycloakSsoCookies } from './impersonation';

const SIGN_IN_TIMEOUT = 30_000;

/**
 * Obtains a Keycloak SSO session for the E2E fixture
 * user through the stand-only client's impersonation right
 * (impersonation.ts) and injects it into this page's context, on Keycloak's
 * own origin only — no password is typed, none is read from the
 * environment. From here the panel walks its own ordinary login flow
 * unmodified: redirect to Keycloak's authorization endpoint,
 * silent SSO (the injected session is already valid), callback,
 * `Set-Cookie: mustask.session`. Same signature as before this task, so every
 * existing call site keeps working unchanged.
 *
 * **Waits for the callback response, not just "back on the panel's
 * origin".** Live-found while wiring this up: `returnPath` (`/home`,
 * `/dictionaries/languages`, ...) is already on the panel's own origin *before* the panel's
 * client-side code even starts the redirect-to-Keycloak-and-back round
 * trip, so a `waitForURL` predicate checking only "origin === panel" is
 * satisfied by the very first navigation, before the round trip has done
 * anything — a race that let the test proceed while still anonymous,
 * intermittently (whichever happened to run first). Waiting for the actual
 * `/api/session/callback` response first is the reliable signal that
 * `mustask.session` has been set; the origin wait afterward only settles the
 * final redirect leg back to `returnPath`.
 *
 * `options.username` defaults to the fixture user (`impersonation.ts`'s own
 * default) — the specs that need another identity (16, 19.9, 20.4, 21.6 and
 * 27-30) sign in as a throwaway user instead (`keycloakAdmin.ts`).
 */
export async function signIn(page: Page, options: { returnPath?: string; username?: string } = {}): Promise<void> {
  const { panelUrl, apiUrl } = env();
  const cookies = await obtainKeycloakSsoCookies(options.username);
  await page.context().addCookies(cookies);

  const callbackCompleted = page.waitForResponse((response) => response.url().startsWith(`${apiUrl}/api/session/callback`), {
    timeout: SIGN_IN_TIMEOUT,
  });
  await page.goto(options.returnPath ?? '/home');
  await callbackCompleted;
  await page.waitForURL((url) => url.origin === new URL(panelUrl).origin, { timeout: SIGN_IN_TIMEOUT });
}
