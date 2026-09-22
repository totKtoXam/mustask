// Specs 17-18: small locator helpers shared by the dictionaries write-path
// specs, so the same trick isn't reinvented per file.
import type { Locator, Page, Response } from '@playwright/test';

/** A registry `<tr>` whose text contains the given value (e.g. a code column) — same pattern spec 15 already uses ad hoc, named here for reuse. */
export function registryRow(table: Locator, text: string): Locator {
  return table.locator('tbody tr').filter({ hasText: text });
}

/**
 * Waits for a specific mutation's own HTTP response — not a DOM echo of it.
 * A switch's label or a controlled `TextField`'s typed value updates the
 * instant it is clicked/typed, before the network round trip that actually
 * persists it has even started or finished; navigating away (`page.goto`,
 * `page.reload`) before that round trip settles cancels the request outright
 * (observed live while writing these specs — a save's own `PUT` showing
 * `status: -1` in the trace, meaning it was aborted mid-flight, not
 * rejected by the server). Call this *before* the click that triggers the
 * request — the same ordering `e2e/support/auth.ts`'s `signIn` already uses
 * for `/api/session/callback`.
 */
export function waitForApiResponse(page: Page, method: string, urlSubstring: string): Promise<Response> {
  return page.waitForResponse((response) => response.request().method() === method && response.url().includes(urlSubstring));
}
