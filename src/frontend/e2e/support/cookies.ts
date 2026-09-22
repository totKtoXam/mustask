import type { BrowserContext, Cookie } from '@playwright/test';

/**
 * Strips Keycloak's own SSO cookies
 * (`KEYCLOAK_IDENTITY`/`KEYCLOAK_SESSION`, set by impersonation.ts) from a
 * `storageState().cookies` array before it seeds a second browser context —
 * spec 10 uses this so that a home page rendering in that second context can
 * only be explained by `mustask.session`, not a silent Keycloak SSO re-login.
 */
export function withoutKeycloakSsoCookies<T extends { name: string }>(cookies: T[]): T[] {
  return cookies.filter((cookie) => !cookie.name.startsWith('KEYCLOAK_'));
}

/** `mustask.session` or `undefined`. */
export async function getSessionCookie(context: BrowserContext): Promise<Cookie | undefined> {
  const cookies = await context.cookies();
  return cookies.find((cookie) => cookie.name === 'mustask.session');
}

/** Every `mustask.sessionC<n>` continuation part ASP.NET Core adds past ~4 KB per chunk. */
export async function getSessionCookieParts(context: BrowserContext): Promise<Cookie[]> {
  const cookies = await context.cookies();
  return cookies.filter((cookie) => cookie.name.startsWith('mustask.sessionC'));
}

/**
 * Byte size of the cookie as it travels on the wire (`name=value`, UTF-8) —
 * spec 8. Attribute bytes (`Path`, `SameSite`, ...) are not
 * counted: they don't round-trip back to the server on every request.
 */
export function cookieWireSize(cookie: Pick<Cookie, 'name' | 'value'>): number {
  return Buffer.byteLength(`${cookie.name}=${cookie.value}`, 'utf8');
}
