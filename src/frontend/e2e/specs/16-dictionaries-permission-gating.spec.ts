import { expect, test } from '@playwright/test';
import { signIn } from '../support/auth';
import { env } from '../support/env';
import { createEphemeralReaderUser, deleteEphemeralUser, userExistsById } from '../support/keycloakAdmin';

/**
 * Permission gating end to end, through a real Keycloak access token rather
 * than the fabricated `resource_access.mustask-api.roles` payload every
 * mocked test builds by hand (via
 * `fakeAccessTokenWithPermissions` in `testFixtures.ts`) — proving
 * `tokenClaims.ts` reads the token shape Keycloak actually issues, not just
 * the shape the unit suite assumes it does, and that a direct URL into a
 * write-gated screen is refused by the real route gate, not only by a
 * fabricated one.
 *
 * The fixture user (`e2e-fixture`) is deliberately full-admin
 * (`KeycloakStandProvisioner`'s `GroupPath`, `/Администраторы` — every
 * `dictionaries.*` code the realm defines) precisely so the rest of this
 * suite's session/login specs have a stable, always-permitted identity —
 * which means there is no "denied" case left to observe through it. Rather
 * than temporarily changing the shared fixture's own group (a different
 * session's e2e run, or a human, may depend on it staying admin for as long
 * as this stand is up — the same reason this suite never touches realm
 * settings a spec did not itself set), this spec creates a throwaway user in
 * the realm's own pre-existing read-only group instead
 * (`deploy/keycloak/realms/mustask-realm.json`'s `viewer` role:
 * `dictionaries.languages.read`, no `dictionaries.languages.write`) — created
 * and deleted by this spec alone (`keycloakAdmin.ts`'s
 * `createEphemeralReaderUser`/`deleteEphemeralUser`), within `mustask-e2e`'s
 * existing `manage-users` right — no new realm-management scope, no
 * realm-wide setting touched, nothing left behind whether the test passes or
 * fails.
 */
test.describe('16. dictionaries permission gating, real token, real realm', () => {
  test('a read-only role sees the languages registry but is denied a write-gated screen by direct URL', async ({ page }) => {
    const { keycloakUrl, realm } = env();
    const reader = await createEphemeralReaderUser(keycloakUrl, realm);
    try {
      // Positive: `languages.read` alone opens the registry — the
      // route-level gate and the read side of the page.
      await signIn(page, { username: reader.username, returnPath: '/dictionaries/languages' });
      await expect(page.getByRole('heading', { name: 'Языки' })).toBeVisible();
      await expect(page.getByRole('table', { name: 'Реестр языков' })).toBeVisible();
      // No write action is offered to a reader — the per-action gate, not
      // just the route-level one.
      await expect(page.getByRole('button', { name: 'Добавить язык' })).toHaveCount(0);

      // Negative: a direct URL into a screen this role may not open —
      // `LanguageFormPage` requires `languages.write`, which the reader lacks.
      await page.goto('/dictionaries/languages/new');
      await expect(page.getByText('Доступ ограничен')).toBeVisible();
      await expect(page.getByRole('heading', { name: 'Добавить язык' })).toHaveCount(0);
    } finally {
      await deleteEphemeralUser(keycloakUrl, realm, reader.id);
      // Not just "the DELETE call was accepted" — confirm the user is
      // actually gone from the realm, so a silent no-op delete cannot leave
      // a throwaway identity behind.
      expect(await userExistsById(keycloakUrl, realm, reader.id)).toBe(false);
    }
  });
});
