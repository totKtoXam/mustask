import { expect, test } from '@playwright/test';
import { signIn } from '../support/auth';
import { env } from '../support/env';
import { getSessionCookie } from '../support/cookies';

/**
 * `mustask.session` attributes via `context.cookies()`
 * (not by hand in DevTools), and `Path` scoping via `request.allHeaders()`
 * on a real cross-route request.
 */
test('7. mustask.session — HttpOnly/SameSite=Lax/Path=/api/session, not sent to other API routes', async ({
  page,
  context,
}) => {
  await signIn(page, { returnPath: '/home' });

  const cookie = await getSessionCookie(context);
  expect(cookie, 'mustask.session must be set after sign-in').toBeTruthy();
  expect(cookie!.httpOnly).toBe(true);
  expect(cookie!.sameSite).toBe('Lax');
  expect(cookie!.path).toBe('/api/session');

  const dictionariesUrl = `${env().apiUrl}/api/dictionaries/languages`;
  const [dictionariesRequest] = await Promise.all([
    page.waitForRequest((request) => request.url() === dictionariesUrl),
    page.evaluate((url) => fetch(url, { credentials: 'include' }).catch(() => undefined), dictionariesUrl),
  ]);
  const headers = await dictionariesRequest.allHeaders();
  expect(headers['cookie'] ?? '').not.toContain('mustask.session');
});
