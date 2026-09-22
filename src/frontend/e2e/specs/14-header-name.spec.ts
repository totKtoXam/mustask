import { expect, test } from '@playwright/test';
import { signIn } from '../support/auth';

/**
 * The signed-in person's name is shown in the header, not empty and not the
 * "anonymous" fallback (`name` → `preferred_username` → `"anonymous"`).
 */
test("14. header shows the signed-in person's name", async ({ page }) => {
  await signIn(page, { returnPath: '/home' });

  // AppLayout.tsx has no test id for the name — it's the body2 Typography
  // right next to "Выйти" in the AppBar; MUI's variant class is stable.
  const name = await page.locator('.MuiAppBar-root .MuiTypography-body2').first().innerText();
  expect(name.trim().length).toBeGreaterThan(0);
  expect(name.trim()).not.toBe('anonymous');
});
