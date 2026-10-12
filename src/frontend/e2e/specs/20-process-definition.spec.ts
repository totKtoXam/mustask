import { randomUUID } from 'node:crypto';
import { expect, test, type Locator, type Page } from '@playwright/test';
import { signIn } from '../support/auth';
import { env, nodeUrl } from '../support/env';
import {
  createEphemeralUserWithoutGroup,
  deleteEphemeralUser,
  userExistsById,
} from '../support/keycloakAdmin';
import {
  createOrganization,
  deactivateOrganization,
  runName,
  send,
} from '../support/organizationsApi';

/**
 * 20. The read-only process definition (mod-001 US-2, design.md layout B).
 *
 * Runs against the process of an organization the spec creates in
 * `beforeAll` through mod-002's API: creation seeds its process (mod-002
 * api-contract § 1.3), and mod-002 D-h removed the Development stand's
 * demonstration organization. The organization is deactivated in `afterAll`
 * (nothing is purged, BR-M1-1). The expected values are api-contract
 * § 1.4's, written out here rather than read back from the panel, so the
 * spec compares the build with the contract, not the screen with itself.
 *
 * One fault is injected on purpose (20.3): the read is refused at the network
 * layer, as spec 19.8 does for the languages registry, to see the failed-read
 * state and its retry against the real backend. Nothing on the session flow
 * is touched (`src/test/e2eSessionGuard.test.ts`).
 *
 * 20.4 signs in a throwaway user in no group (it holds no code at all), the
 * way spec 16 signs in its reader, and deletes it in `finally`.
 */

/** The organization `beforeAll` creates, whose process the spec reads. */
let organizationId = '';
const processAddress = () => `/organizations/${organizationId}/settings/process`;

/** api-contract § 1.4: the five types in display order. */
const TYPES_IN_ORDER = ['Эпик', 'Фича', 'Пользовательская история', 'Баг', 'Задача'];

/** api-contract § 1.4: «Задача»'s states with their category labels. */
const TASK_STATES = [
  ['Новая', 'Новые'],
  ['Активна', 'В работе'],
  ['Закрыта', 'Завершённые'],
  ['Удалена', 'Удалённые'],
] as const;

const main = (page: Page) => page.getByRole('main');
const statesOf = (page: Page, typeName: string) =>
  page.getByRole('table', { name: `Статусы типа «${typeName}»` });

/** The read of any organization's process, on the API's origin. */
function isProcessRead(url: URL): boolean {
  return (
    url.origin === new URL(env().apiUrl).origin &&
    /^\/api\/organizations\/[^/]+\/process$/.test(url.pathname)
  );
}

async function openProcess(page: Page, options: { username?: string } = {}): Promise<void> {
  await signIn(page, { returnPath: processAddress(), username: options.username });
  await expect(page.getByRole('heading', { level: 1, name: 'Процесс' })).toBeVisible();
  await expect(main(page).getByRole('region')).toHaveCount(TYPES_IN_ORDER.length);
}

async function pageScrollsSideways(page: Page): Promise<boolean> {
  return page.evaluate(
    () => document.documentElement.scrollWidth > document.documentElement.clientWidth,
  );
}

async function top(locator: Locator): Promise<number> {
  const box = await locator.boundingBox();
  if (!box) throw new Error('not rendered');
  return Math.round(box.y);
}

test.describe('20. the read-only process definition (mod-001)', () => {
  test.use({ viewport: { width: 1440, height: 900 } });

  test.beforeAll(async ({ browser }) => {
    const context = await browser.newContext();
    try {
      const page = await context.newPage();
      await signIn(page);
      organizationId = (await createOrganization(page.request, runName('Процесс E2E'))).id;
    } finally {
      await context.close();
    }
  });

  test.afterAll(async ({ browser }) => {
    if (!organizationId) return;
    const context = await browser.newContext();
    try {
      const page = await context.newPage();
      await signIn(page);
      await deactivateOrganization(page.request, organizationId);
    } finally {
      await context.close();
    }
  });

  test('20.1 happy path: a new organization’s seeded process in full, read-only (US-2 "A seeded process is shown in full")', async ({
    page,
  }) => {
    await openProcess(page);

    await expect(main(page).getByRole('heading', { level: 2 })).toHaveText(TYPES_IN_ORDER);
    for (const name of TYPES_IN_ORDER) {
      await expect(
        main(page).getByRole('heading', { level: 2, name }).locator('svg'),
        `the type mark of «${name}»`,
      ).toBeVisible();
    }

    const taskRows = statesOf(page, 'Задача').locator('tbody tr');
    await expect(taskRows).toHaveCount(TASK_STATES.length);
    for (const [index, [state, label]] of TASK_STATES.entries()) {
      await expect(taskRows.nth(index)).toContainText(`${index + 1}`);
      await expect(taskRows.nth(index)).toContainText(state);
      await expect(taskRows.nth(index)).toContainText(label);
    }
    await expect(taskRows.nth(0)).toContainText('Начальный');

    // bug-004 D-3 (it replaced doc-010 D-4's "one label, two categories"): one name, one
    // meaning — «Разрешена» is «Решённые» on every type that has it, the story's as the bug's.
    for (const name of ['Эпик', 'Фича', 'Пользовательская история', 'Баг']) {
      await expect(
        statesOf(page, name).locator('tbody tr').filter({ hasText: 'Разрешена' }),
        `«Разрешена» of «${name}»`,
      ).toContainText('Решённые');
    }

    // The same rule on the contract itself (mod-001 api-contract § 1.2): no state name has two
    // categories anywhere in the process, and «Разрешена» is `Resolved` on the four types.
    const read = await send(page.request, 'GET', `/api/organizations/${organizationId}/process`);
    expect(read.ok(), 'the process read').toBe(true);
    const process = (await read.json()) as {
      workItemTypes: { key: string; states: { name: string; category: { code: string } }[] }[];
    };
    const categoriesOf = new Map<string, Set<string>>();
    for (const type of process.workItemTypes) {
      for (const state of type.states) {
        categoriesOf.set(
          state.name,
          (categoriesOf.get(state.name) ?? new Set<string>()).add(state.category.code),
        );
      }
    }
    expect(
      [...categoriesOf].filter(([, codes]) => codes.size > 1).map(([name]) => name),
      'state names that stand in two categories',
    ).toEqual([]);
    for (const key of ['EPIC', 'FEAT', 'US', 'BUG']) {
      const resolved = process.workItemTypes
        .find((type) => type.key === key)
        ?.states.find((state) => state.name === 'Разрешена');
      expect(resolved?.category.code, `«Разрешена» of ${key}`).toBe('Resolved');
    }

    await expect(page).toHaveTitle('Процесс — Mustask');
    // BR-M2-13: nothing on the screen changes the process.
    await expect(main(page).getByRole('button')).toHaveCount(0);
    await expect(main(page).getByRole('link')).toHaveCount(0);
    await expect(main(page).getByRole('textbox')).toHaveCount(0);
  });

  test('20.2 not found: an unknown and a malformed identifier get «Организация не найдена»; «На главную» opens «Главная» (US-2 "An identifier with no process is not found")', async ({
    page,
  }) => {
    const unknown = `/organizations/${randomUUID()}/settings/process`;
    await signIn(page, { returnPath: unknown });

    for (const address of [unknown, '/organizations/not-a-uuid/settings/process']) {
      if (new URL(page.url()).pathname !== address) await page.goto(address);
      await expect(
        page.getByRole('heading', { level: 1, name: 'Организация не найдена' }),
        address,
      ).toBeVisible();
      await expect(page).toHaveTitle('Организация не найдена — Mustask');
      await expect(page.getByRole('table')).toHaveCount(0);
      await expect(page.getByRole('heading', { name: 'Процесс' })).toHaveCount(0);
      expect(new URL(page.url()).pathname).toBe(address);
    }

    await page.getByRole('link', { name: 'На главную' }).click();
    await expect(page).toHaveURL(/\/home$/);
    await expect(page.getByRole('heading', { level: 1, name: 'Главная' })).toBeVisible();
  });

  test('20.3 a failed read, then «Повторить»: the alert, never not found; the retry shows the process with focus on the title (US-2 "A failed read is not taken for a missing organization")', async ({
    page,
  }) => {
    await page.route(isProcessRead, (route) => route.abort('connectionrefused'));
    await signIn(page, { returnPath: processAddress() });

    const alert = page.getByRole('alert');
    await expect(alert).toBeVisible();
    await expect(alert).toContainText('Не удалось загрузить процесс');
    await expect(page.getByRole('heading', { level: 1, name: 'Процесс' })).toBeVisible();
    await expect(page.getByText('Организация не найдена')).toHaveCount(0);
    await expect(main(page).getByRole('region')).toHaveCount(0);

    await page.unroute(isProcessRead);
    await alert.getByRole('button', { name: 'Повторить' }).click();

    await expect(main(page).getByRole('region')).toHaveCount(TYPES_IN_ORDER.length);
    await expect(page.getByRole('alert')).toHaveCount(0);
    await expect(page.getByRole('heading', { level: 1, name: 'Процесс' })).toBeFocused();
  });

  test('20.4 a user holding no code sees the same definition as the fixture user (US-2 "Every signed-in user sees the same definition")', async ({
    page,
    browser,
  }) => {
    await openProcess(page);
    const fixtureView = await main(page).innerText();

    const { keycloakUrl, realm } = env();
    const user = await createEphemeralUserWithoutGroup(keycloakUrl, realm);
    const context = await browser.newContext({ viewport: { width: 1440, height: 900 } });
    try {
      const userPage = await context.newPage();
      await openProcess(userPage, { username: user.username });
      await expect(userPage.getByText('Доступ ограничен')).toHaveCount(0);

      expect(await main(userPage).innerText()).toBe(fixtureView);
    } finally {
      await context.close();
      await deleteEphemeralUser(keycloakUrl, realm, user.id);
      expect(await userExistsById(keycloakUrl, realm, user.id)).toBe(false);
    }
  });

  test('20.5 a visitor who is not signed in is sent to sign in, and the read refuses a request without a token (US-2 "A visitor who is not signed in sees nothing")', async ({
    page,
  }) => {
    const { keycloakUrl, apiUrl, realm } = env();
    const reads: string[] = [];
    page.on('request', (request) => {
      if (isProcessRead(new URL(request.url()))) reads.push(request.url());
    });

    const signInPage = page.waitForRequest((request) =>
      request.url().startsWith(`${keycloakUrl}/realms/${realm}/protocol/openid-connect/auth`),
    );
    await page.goto(processAddress());
    await signInPage;
    await page.waitForURL((url) => url.origin === new URL(keycloakUrl).origin);

    expect(reads).toEqual([]);
    await expect(page.getByRole('heading', { name: 'Процесс' })).toHaveCount(0);

    const response = await fetch(`${nodeUrl(apiUrl)}/api/organizations/${organizationId}/process`);
    expect(response.status).toBe(401);
    expect(await response.text()).not.toContain('workItemTypes');
  });

  test('20.6 layout in a browser: two cards side by side at 1280 px, one column at 640 px, no sideways page scroll (design § Layout)', async ({
    page,
  }) => {
    await page.setViewportSize({ width: 1280, height: 900 });
    await openProcess(page);
    const sections = main(page).getByRole('region');

    expect(await top(sections.nth(0))).toBe(await top(sections.nth(1)));
    expect(await pageScrollsSideways(page)).toBe(false);

    await page.setViewportSize({ width: 640, height: 900 });
    await expect
      .poll(async () => (await top(sections.nth(1))) > (await top(sections.nth(0))))
      .toBe(true);
    expect(await pageScrollsSideways(page)).toBe(false);
    // A table wider than its card scrolls inside its own container.
    for (const name of TYPES_IN_ORDER) {
      const container = page
        .locator('.MuiTableContainer-root')
        .filter({ has: statesOf(page, name) });
      const overflow = await container.evaluate((element) => ({
        wider: element.scrollWidth > element.clientWidth,
        overflowX: getComputedStyle(element).overflowX,
      }));
      if (overflow.wider) expect(['auto', 'scroll'], name).toContain(overflow.overflowX);
    }
  });
});
