import { expect, test } from '@playwright/test';
import { signIn } from '../support/auth';
import { env } from '../support/env';
import { getSessionCookie } from '../support/cookies';
import { resolveKeycloakContainer, startContainer, stopContainer } from '../support/docker';

/**
 * Signing out while Keycloak is stopped: the backend
 * cannot reach `end_session_endpoint`, so `mustask.session` stays and the panel
 * shows "Выход не завершён…"; "Повторить
 * выход" completes once Keycloak is back.
 * Env: E2E_ALLOW_DOCKER=1 — resolves and stops/starts the `keycloak`
 * container by the exact name AppHost assigned it (the stand file's `keycloakContainerName`, not a `docker ps` prefix match);
 * skips without the flag.
 *
 * First real run (2026-09-11): `startContainer` returning (it
 * waits for Keycloak itself, see docker.ts) does not guarantee the very next
 * logout call succeeds — a 503 right after boot is still possible at the
 * edge. "Повторить выход" is clicked in a bounded retry loop rather than
 * once.
 *
 * Second real run (2026-09-11): the panel's own readiness monitor
 * (`/health`) latches ready and never flips back on a mere Keycloak outage
 * (RealmReadinessMonitor.cs, ~line 129), so `stopContainer` no longer waits
 * on it — it confirms the outage by polling the realm endpoint directly.
 * That wait is now bounded at ~30s instead of up to 90s.
 */
test.describe('5. sign-out while Keycloak is down', () => {
  test.skip(!env().allowDocker, 'E2E_ALLOW_DOCKER is not set — skipping the spec that stops the Keycloak container.');

  let containerName: string | undefined;

  test.afterEach(async () => {
    if (containerName) {
      await startContainer(containerName);
    }
  });

  test('shows logout_failed while Keycloak is down, "Повторить выход" completes once it is back', async ({
    page,
    context,
  }) => {
    // Covers the bounded outage-confirmation wait, Keycloak's boot time on
    // the start side and the bounded "Повторить выход" retry loop.
    test.setTimeout(240_000);

    await signIn(page, { returnPath: '/home' });

    containerName = await resolveKeycloakContainer();
    await stopContainer(containerName);

    await page.getByRole('button', { name: 'Выйти' }).click();
    await expect(page.getByText('Выход не завершён: сервис аутентификации недоступен.')).toBeVisible({
      timeout: 15_000,
    });
    expect(await getSessionCookie(context)).toBeTruthy();

    await startContainer(containerName);

    const retryButton = page.getByRole('button', { name: 'Повторить выход' });
    const deadline = Date.now() + 60_000;
    for (;;) {
      await retryButton.click();
      try {
        await page.waitForURL('**/signed-out', { timeout: 10_000 });
        break;
      } catch (error) {
        if (Date.now() > deadline) {
          throw error;
        }
      }
    }
  });
});
