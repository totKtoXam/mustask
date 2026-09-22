import { expect, test } from '@playwright/test';
import { env } from '../support/env';

/**
 * A wrong password keeps the person on Keycloak's own
 * form with Keycloak's own error; `OnRemoteFailure` never fires for this
 * case, so there is no `?authError=` round trip back to the panel. Uses a
 * deliberately wrong, non-secret literal password — no real credential is
 * read by this spec at all (neither the Keycloak SSO cookie nor
 * `mustask.session` — this spec never signs in).
 *
 * Submitted against a literal username that is
 * *not* `env().user` (the E2E fixture identity every other spec signs in
 * as) — realm `mustask` has `bruteForceProtected: true`, and an unattended run
 * repeatedly needs the fixture account usable. The username need not exist:
 * Keycloak's stock form gives the same generic rejection either way (no
 * username enumeration).
 *
 * Asserted via ARIA state, not a markup id: this Keycloak version has no
 * `#input-error` element (seen live, 2026-09-11) — the error is
 * on the page as a message next to the form, and the username field is
 * marked invalid (`aria-invalid="true"`), which is what stays true across
 * theme/version changes.
 */
const NON_FIXTURE_USERNAME = 'e2e-wrong-password-probe';

test('3. wrong password stays on the Keycloak form, no ?authError= round trip', async ({ page }) => {
  await page.goto('/home');
  await page.locator('#username').waitFor({ state: 'visible', timeout: 30_000 });
  await page.locator('#username').fill(NON_FIXTURE_USERNAME);
  await page.locator('#password').fill('deliberately-wrong-password-e2e');
  await page.locator('#kc-login').click();

  // Still on Keycloak, with its own error — not redirected back to the panel.
  await expect(page.locator('#username')).toHaveAttribute('aria-invalid', 'true', { timeout: 15_000 });
  expect(page.url().startsWith(env().keycloakUrl)).toBe(true);
  expect(page.url()).not.toContain('authError');
});
