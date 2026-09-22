import { expect, test } from '@playwright/test';
import { signIn } from '../support/auth';
import { cookieWireSize, getSessionCookie, getSessionCookieParts } from '../support/cookies';

/**
 * A single `mustask.session`, no `mustask.sessionC1`/`C2`
 * parts, under 4000 bytes (ADR-0022 decision 1). The size is attached
 * to the report so the orchestrator can record it without re-running the
 * suite by hand.
 */
test('8. mustask.session is one cookie, under 4000 bytes', async ({ page, context }, testInfo) => {
  await signIn(page, { returnPath: '/home' });

  const parts = await getSessionCookieParts(context);
  expect(parts, 'mustask.session must not be split into mustask.sessionC1/C2').toHaveLength(0);

  const cookie = await getSessionCookie(context);
  expect(cookie, 'mustask.session must be set after sign-in').toBeTruthy();

  const size = cookieWireSize(cookie!);
  await testInfo.attach('mustask.session size', { body: `${size} bytes` });
  expect(size).toBeLessThan(4000);
});
