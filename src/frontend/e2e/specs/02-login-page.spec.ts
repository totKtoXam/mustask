import { expect, test } from '@playwright/test';
import { env } from '../support/env';

/**
 * The Keycloak login page uses the `mustask` theme (a
 * palette on top of the stock `keycloak.v2` markup — deploy/keycloak/themes/mustask
 * only overrides CSS) and is Russian by default, with a language switch
 * (realm import: internationalizationEnabled, supportedLocales ru/kk/en,
 * defaultLocale ru). Does not sign in — only reaches the login form.
 *
 * `defaultLocale: ru` only applies when none of the browser's languages
 * match a supported locale — Chromium's default `en-US` matches the realm's
 * `en`, so the config sets `locale: 'ru-RU'` (matching an operator's actual
 * browser) for the whole suite. Without it this spec correctly observes
 * English, not a bug.
 *
 * Env: same optional URL/user variables as spec 1. This spec never calls `signIn`, so it starts with neither the Keycloak SSO cookie
 * nor `mustask.session` — Playwright gives every test its own fresh context by
 * default, and nothing here injects either cookie.
 */
test('2. login page — mustask theme, Russian by default, language switch', async ({ page }) => {
  await page.goto('/home');
  await page.locator('#username').waitFor({ state: 'visible', timeout: 30_000 });

  expect(page.url().startsWith(env().keycloakUrl)).toBe(true);
  expect(page.url()).toContain(`/realms/${env().realm}/`);
  await expect(page.locator('html')).toHaveAttribute('lang', 'ru');

  // Language switch — stock keycloak.v2 markup, not our own code, and not
  // asserted by id: this Keycloak version renders it as a single combobox
  // with one option per supported locale (ru/kk/en, realm import) — seen
  // live as `combobox "languages"` with 3 `option`s (2026-09-11).
  const localeSwitch = page.getByRole('combobox');
  await expect(localeSwitch).toBeVisible();
  await expect(localeSwitch.getByRole('option')).toHaveCount(3);
});
