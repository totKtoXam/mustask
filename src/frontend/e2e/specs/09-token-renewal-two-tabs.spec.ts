import { expect, test } from '@playwright/test';
import { signIn } from '../support/auth';
import { env } from '../support/env';

/**
 * Two tabs renew independently without either showing
 * "Сессия истекла": the server serializes renewal by `sid`, the panel
 * itself does not coordinate tabs at all. This is also the only live check
 * that the renewed id-token is verified too.
 *
 * Needs a short `accessTokenLifespan` — the default 900s is too slow to
 * wait out. This is not this spec's own setting to
 * make or restore. `mustask-e2e` deliberately lacks `manage-realm`/`view-realm`
 * (it stays at exactly `impersonation`+`manage-users`), so AppHost itself
 * sets the realm's `accessTokenLifespan` to 120s at every stand start
 * (`KeycloakStandProvisioner.SetAccessTokenLifespanAsync`,
 * `KeycloakStandIdentifiers.AccessTokenLifespanSeconds` —
 * `docs/templates/examples/tech-159-e2e-without-password/plan-backend.md` §11). The
 * `120` below is that same constant, named here so a future change to
 * either side is not free to forget the other.
 */
test.describe('9. token renewal across two tabs', () => {
  test('renews in both tabs without "Сессия истекла"', async ({ context }) => {
    // The body waits 75s past the renewal moment alone — past the config's
    // default 60s test timeout regardless of Keycloak/network speed.
    test.setTimeout(120_000);

    const tab1 = await context.newPage();
    await signIn(tab1, { returnPath: '/home' });
    const tab2 = await context.newPage();
    await tab2.goto('/home');
    // getByText('Главная') is not strict-mode safe: it matches both the nav
    // link and the page heading.
    await expect(tab2.getByRole('heading', { name: 'Главная' })).toBeVisible();

    let tokenCalls = 0;
    const tokenUrl = `${env().apiUrl}/api/session/token`;
    context.on('request', (request) => {
      if (request.url() === tokenUrl) {
        tokenCalls += 1;
      }
    });

    // accessTokenLifespan is the stand's own 120s, renewal fires 60s
    // before expiry (RENEW_LEAD_MS, shared/api/session.ts) — wait past that
    // moment in both tabs.
    await tab1.waitForTimeout(75_000);

    await expect(tab1.getByText('Сессия истекла, войдите заново')).toHaveCount(0);
    await expect(tab2.getByText('Сессия истекла, войдите заново')).toHaveCount(0);
    // Ideally exactly one renewal call per tab; >=1
    // keeps the assertion robust to timer jitter between the two tabs.
    expect(tokenCalls).toBeGreaterThanOrEqual(1);
  });
});
