import { expect, test } from '@playwright/test';
import { signIn } from '../support/auth';

/**
 * Sign-out lands on `/signed-out` with "Вы вышли из
 * системы", and opening the panel again shows Keycloak's own password form
 * rather than a silent re-sign-in — proof the Keycloak session itself
 * really ended, not just our cookie.
 *
 * Sign-in, sign-out and the re-open below
 * all run against the one `page`/context this test starts with — nothing
 * here creates a new context or clears a cookie in between, so seeing
 * Keycloak's form again is evidence the Keycloak session itself ended, not
 * an artifact of starting fresh.
 */
test('4. sign-out lands on /signed-out, re-opening asks for the password again', async ({ page }) => {
  await signIn(page, { returnPath: '/home' });

  // Since mod-015 (US-2, C-2) «Выйти» is an item of the user menu.
  await page.getByRole('banner').getByRole('button', { name: /^Профиль/ }).click();
  await page.getByRole('menuitem', { name: 'Выйти' }).click();
  await page.waitForURL('**/signed-out', { timeout: 30_000 });
  await expect(page.getByText('Вы вышли из системы.')).toBeVisible();

  await page.goto('/home');
  await expect(page.locator('#username')).toBeVisible({ timeout: 30_000 });
});
