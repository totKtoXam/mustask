// Specs 20 and 27-30 (mod-016): a second (third) signed-in browser context for
// another user, next to the `page` fixture of the fixture user. Each one walks
// the panel's own OIDC flow through `signIn` (impersonation of that user,
// ADR-0023) — nothing is stubbed or injected on the session flow
// (`src/test/e2eSessionGuard.test.ts`).
import type { Browser, BrowserContext, Page } from '@playwright/test';
import { signIn } from './auth';

export const DESKTOP = { width: 1440, height: 900 } as const;

export interface Session {
  context: BrowserContext;
  page: Page;
  /** Closes the context; call it in `finally`. */
  close(): Promise<void>;
}

/**
 * A fresh browser context signed in as `username` (the fixture user when
 * absent), opened on `returnPath` (`/home` when absent). A fresh context has
 * no storage, so nothing the panel remembers per user (the remembered
 * selection, mod-002 US-8) is there.
 */
export async function openSession(
  browser: Browser,
  options: {
    username?: string;
    returnPath?: string;
    viewport?: { width: number; height: number };
  } = {},
): Promise<Session> {
  const context = await browser.newContext({ viewport: options.viewport ?? DESKTOP });
  try {
    const page = await context.newPage();
    await signIn(page, { username: options.username, returnPath: options.returnPath });
    return { context, page, close: () => context.close() };
  } catch (error) {
    await context.close();
    throw error;
  }
}
