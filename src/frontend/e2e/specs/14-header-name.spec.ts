import { expect, test } from '@playwright/test';
import { signIn } from '../support/auth';

/**
 * The signed-in person's name reaches the user menu, and is not empty. The
 * panel reads `name`, then `preferred_username`, and falls back to the empty
 * string (`src/shared/api/tokenClaims.ts`).
 *
 * Since mod-015 (US-2, C-1) the top bar shows no name as text: the user menu
 * button carries it in its accessible name, «Профиль: <имя>», shows the
 * initials, and the open menu shows the name itself. With an empty name the
 * button is named «Профиль», with no colon (design § 3.3), so the
 * `/^Профиль: /` locator below finds nothing and the spec fails: that
 * locator is the guard against a missing name.
 */
test("14. the user menu carries the signed-in person's name", async ({ page }) => {
  await signIn(page, { returnPath: '/home' });

  const banner = page.getByRole('banner');
  const profile = banner.getByRole('button', { name: /^Профиль: / });
  await expect(profile).toBeVisible();

  const accessibleName = (await profile.getAttribute('aria-label')) ?? '';
  const name = accessibleName.replace(/^Профиль: /, '').trim();
  expect(name.length).toBeGreaterThan(0);

  // The bar itself shows no display name as text.
  await expect(banner.getByText(name, { exact: true })).toHaveCount(0);

  await profile.click();
  const menu = page.getByRole('menu');
  await expect(menu.getByText(name, { exact: true })).toBeVisible();
  await expect(menu.getByRole('menuitem', { name: 'Выйти' })).toBeVisible();
});
