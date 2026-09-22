import { expect, test } from '@playwright/test';
import { signIn } from '../support/auth';
import { env } from '../support/env';
import { getLanguageByCode, replaceLanguage } from '../support/dictionariesApi';
import { SEED_LANGUAGES } from '../support/dictionariesSeed';
import { resolveKeycloakContainer, startContainer, stopContainer } from '../support/docker';

/**
 * Keycloak goes down mid-work, with a filled form open: the failed renewal
 * shows "Сервис аутентификации недоступен, повторите позже."
 * (`apiErrorText.ts`, `AuthUnavailableError`) and the form keeps its values
 * — not the generic 503 text, and not a jump back to the anonymous screen.
 *
 * Reaching the renewal path deterministically needs a short
 * `accessTokenLifespan` — the same trick as spec 9. This is not this spec's
 * own setting to make: `mustask-e2e` deliberately lacks
 * `manage-realm`/`view-realm` (it stays at exactly
 * `impersonation`+`manage-users`), so AppHost itself sets the realm's
 * `accessTokenLifespan` to 120s at every stand start
 * (`KeycloakStandProvisioner.SetAccessTokenLifespanAsync`,
 * `KeycloakStandIdentifiers.AccessTokenLifespanSeconds`). This spec only
 * *waits out* that already-short lifespan; it never writes the realm.
 *
 * The open form is the language edit form (`/dictionaries/languages/kk`,
 * `LanguageFormPage.tsx`) — assumes E2E_USER has
 * `dictionaries.languages.write` (true for the seeded full-admin fixture
 * identity). The save is expected to fail before it reaches the API; should
 * it land anyway, `afterEach` restores `kk`'s seeded endonym through the API
 * once Keycloak is back, so nothing outlives the spec.
 *
 * The panel's own readiness monitor (`/health`) latches ready and never
 * flips back on a mere Keycloak outage (RealmReadinessMonitor.cs, ~line 129)
 * — this spec never depended on that signal (it drives the 503 through the
 * stand's own short access token instead), but `stopContainer` no longer
 * waits on `/health` either way; the outage is confirmed by polling the
 * realm endpoint directly, bounded at ~30s.
 */
test.describe('6. mid-work renewal fails while Keycloak is down, form keeps its values', () => {
  test.skip(!env().allowDocker, 'E2E_ALLOW_DOCKER is not set — skipping the spec that stops the Keycloak container.');

  let containerName: string | undefined;
  // Set only if the save's own `PUT` actually came back 2xx — the one case
  // that needs undoing. Keyed on the response rather than re-reading the row
  // unconditionally, so a Keycloak that is still booting after `afterEach`'s
  // restart cannot turn a passing run into a failed cleanup.
  let persisted = false;

  test.afterEach(async ({ page }) => {
    if (containerName) {
      await startContainer(containerName);
    }
    if (!persisted) {
      return;
    }
    // Safety net: the save below must not have persisted, but it did — put
    // `kk`'s seeded endonym back (the seeder never updates existing rows,
    // ADR-0021 §5).
    const current = await getLanguageByCode(page.request, 'kk');
    if (current && current.endonym !== SEED_LANGUAGES.kk.endonym) {
      await replaceLanguage(
        page.request,
        'kk',
        { endonym: SEED_LANGUAGES.kk.endonym, sortOrder: current.sortOrder, isActive: current.isActive },
        current.version,
      );
    }
  });

  test('renewal 503 mid-submit keeps the form and its values', async ({ page }) => {
    // Covers the fixed pre-submit wait, the bounded outage-confirmation wait
    // and Keycloak's boot time on the afterEach start side.
    test.setTimeout(240_000);

    await signIn(page, { returnPath: '/dictionaries/languages/kk' });

    await expect(page.getByText('Язык: kk')).toBeVisible();
    page.on('response', (response) => {
      if (response.request().method() === 'PUT' && response.url().includes('/api/dictionaries/languages/kk') && response.ok()) {
        persisted = true;
      }
    });
    const marker = `e2e-${Date.now()}`;
    await page.getByLabel('Эндоним').fill(marker);

    containerName = await resolveKeycloakContainer();
    await stopContainer(containerName);

    // accessTokenLifespan is the stand's own 120s, renewal fires 60s
    // before expiry (RENEW_LEAD_MS, shared/api/session.ts) — submit once
    // we're past that.
    await page.waitForTimeout(65_000);
    await page.getByRole('button', { name: 'Сохранить' }).click();

    await expect(page.getByText('Сервис аутентификации недоступен, повторите позже.')).toBeVisible({
      timeout: 15_000,
    });
    await expect(page.getByLabel('Эндоним')).toHaveValue(marker);
  });
});
