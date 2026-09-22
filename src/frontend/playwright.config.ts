import { defineConfig, devices } from '@playwright/test';

// The project's Playwright harness — E2E against the real backend, real
// Keycloak, no mocks. The suite signs in through the
// stand-only client's impersonation right (ADR-0023), not a typed password — an agent
// may run it unattended under the conditions fixed in
// src/frontend/AGENTS.md § E2E test suite (the live source; not restated
// here to avoid a second copy that can drift).
const panelUrl = (process.env.E2E_PANEL_URL ?? 'http://localhost:3000').replace(/\/$/, '');

export default defineConfig({
  testDir: './e2e/specs',
  globalSetup: './e2e/global-setup.ts',
  timeout: 60_000,
  expect: { timeout: 10_000 },
  // The specs share one Keycloak realm and one user, and some of them
  // stop/start the keycloak container — no concurrency, in file or across
  // files. (accessTokenLifespan is AppHost's own to set, not a spec's.)
  fullyParallel: false,
  workers: 1,
  retries: 0,
  reporter: [
    ['list'],
    ['json', { outputFile: 'e2e-results/results.json' }],
    ['html', { outputFolder: 'e2e-results/html', open: 'never' }],
  ],
  use: {
    baseURL: panelUrl,
    trace: 'retain-on-failure',
    // Playwright's `screenshot` option has no literal 'retain-on-failure'
    // value (only `trace` does) — 'only-on-failure' is the closest match:
    // a screenshot is kept exactly when the test failed.
    screenshot: 'only-on-failure',
    // First real run (2026-09-11): Chromium's default
    // Accept-Language is `en-US`, and the `mustask` realm supports `en`, so
    // Keycloak correctly served English — `defaultLocale: ru` only applies
    // when none of the browser's languages match a supported locale.
    // `ru-RU` matches an operator's actual browser and lets the suite
    // observe the realm's real default.
    locale: 'ru-RU',
  },
  projects: [
    {
      name: 'chromium',
      use: { ...devices['Desktop Chrome'] },
    },
  ],
});
