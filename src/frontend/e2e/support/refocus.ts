import type { Page } from '@playwright/test';

/**
 * The window returns to the user: the panel re-reads what it keeps per
 * window focus (mod-016 BR-M1-31 — the served access, the organization list)
 * with no reload and no navigation.
 *
 * TanStack Query's focus manager listens to exactly one event for this,
 * `visibilitychange` on `window` (query-core 5.102, `focusManager.js`), and
 * refetches the stale queries that opted in when it fires and the document is
 * not `hidden`. Headless Chromium reports every page `visible`, so dispatching
 * the event by hand is the signal a real return to the tab sends. Nothing on
 * the session flow is touched (`src/test/e2eSessionGuard.test.ts`).
 */
export async function refocus(page: Page): Promise<void> {
  await page.evaluate(() => window.dispatchEvent(new Event('visibilitychange')));
}
