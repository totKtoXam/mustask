import { expect, test } from '@playwright/test';
import { signIn } from '../support/auth';
import { env } from '../support/env';

/**
 * The `Authorization` header (the access token
 * itself, not the cookie — that never carries it) stays at or under 3072
 * bytes for a user with the full set of rights.
 */
test('13. access token stays at or under 3072 bytes', async ({ page }, testInfo) => {
  await signIn(page, { returnPath: '/home' });

  // Any business call carries the header; the languages registry is the one
  // screen that fires such a call on load.
  const languagesUrl = `${env().apiUrl}/api/dictionaries/languages`;
  const [request] = await Promise.all([
    page.waitForRequest((req) => req.url().startsWith(`${languagesUrl}?`)),
    page.goto('/dictionaries/languages'),
  ]);

  const headers = await request.allHeaders();
  const authorization = headers['authorization'];
  expect(authorization, 'Authorization header must be present on a business API call').toBeTruthy();

  const size = Buffer.byteLength(authorization!, 'utf8');
  await testInfo.attach('authorization header size', { body: `${size} bytes` });
  expect(size).toBeLessThanOrEqual(3072);
});
