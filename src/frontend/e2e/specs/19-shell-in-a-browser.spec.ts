import {
  expect,
  test,
  type APIRequestContext,
  type Browser,
  type Locator,
  type Page,
} from '@playwright/test';
import { signIn } from '../support/auth';
import { env } from '../support/env';
import { createEphemeralReaderUser, deleteEphemeralUser } from '../support/keycloakAdmin';
import {
  createContext,
  deactivateAllOrganizations,
  deactivateOrganization,
  restoreOrganizations,
  runName,
  type ContextFixture,
} from '../support/organizationsApi';

/**
 * 19. The application shell in a real browser (mod-015, added by QA at
 * stage 7).
 *
 * The component suite checks the shell's structure in jsdom, which has no
 * layout engine. These are the facts only a browser shows: computed sizes and
 * colours, the frame pinned while the page scrolls, no sideways page scroll
 * down to the 640 px floor (D-8), the ellipsis in the switcher, the focus
 * ring, keyboard paths through real focus management, reduced motion, and
 * the network the shell causes.
 *
 * The expected values are design.md's (§ 2.1 colour roles, § 2.5 layout
 * tokens), written out here rather than read back from the theme, so the
 * spec compares the build with the approved design, not the theme with
 * itself.
 *
 * The fixture user is a dictionaries administrator (see spec 16's header);
 * 19.9 signs in a throwaway reader the way spec 16 does.
 *
 * One fault is injected on purpose (19.8): the languages request is held
 * back, then refused at the network layer, to see the loading and error
 * states against the real backend. Nothing on the session flow is touched
 * (`src/test/e2eSessionGuard.test.ts`).
 *
 * Since mod-002 the switcher reads real organizations (its D-o) and
 * «Настройки» opens the organization's «Общие» once one is chosen (its
 * design § 2.1). So the cases run in two groups (mod-002 plan-frontend § 11,
 * P-5): the «Языки» cases with no active organization — the group
 * deactivates the active ones and restores them after — and the switcher
 * cases on two organizations the group creates, «Пример организации 1 …»
 * (project «Пример проекта 1», teams «Пример проекта 1» and «Пример команды
 * 2») and «Пример организации 2 …» (project «Пример проекта 3»), deactivated
 * after. A project's first team is named after it (mod-002 BR-M1-9).
 */

/** Runs `action` with a signed-in API session of its own, closed after. */
async function withApi(
  browser: Browser,
  action: (request: APIRequestContext) => Promise<unknown>,
): Promise<void> {
  const context = await browser.newContext();
  try {
    const page = await context.newPage();
    await signIn(page);
    await action(page.request);
  } finally {
    await context.close();
  }
}

let restoreAfterNoContext: string[] = [];
let restoreAfterContext: string[] = [];
let first: ContextFixture | undefined;
let second: ContextFixture | undefined;

/** The switcher's three accessible names on the first organization (the first entries). */
function firstLevels(): string[] {
  return [
    `Организация: ${first?.organization.name ?? ''}`,
    'Проект: Пример проекта 1',
    'Команда: Пример проекта 1',
  ];
}

const DESIGN = {
  applicationBackground: 'rgb(248, 248, 248)',
  navigationSurface: 'rgb(235, 235, 235)',
  contentSurface: 'rgb(255, 255, 255)',
  tableHeader: 'rgb(243, 243, 243)',
  rowHover: 'rgb(240, 240, 240)',
  accent: 'rgb(0, 120, 212)',
  accentContrastText: 'rgb(255, 255, 255)',
  topBarHeight: 48,
  navigationWidth: 240,
  railWidth: 48,
  settingsNavigationWidth: 240,
  maxRowHeight: 40,
  bodyFontSize: '14px',
  contextNameMaxWidth: { medium: 160, narrow: 96 },
  focusRingWidth: 2,
} as const;

const mainNavigation = (page: Page) => page.getByRole('navigation', { name: 'Основное меню' });
const settingsNavigation = (page: Page) => page.getByRole('navigation', { name: 'Меню настроек' });
const switcher = (page: Page) => page.getByRole('group', { name: 'Контекст работы' });
const menuButton = (page: Page) =>
  page.getByRole('banner').getByRole('button', { name: 'Меню', exact: true });

/** The drawer paper holding a navigation region: the surface whose width and colour count. */
function paperOf(page: Page, navigation: Locator): Locator {
  return page.locator('.MuiDrawer-paper').filter({ has: navigation });
}

async function width(locator: Locator): Promise<number> {
  const box = await locator.boundingBox();
  if (!box) throw new Error('not rendered');
  return box.width;
}

async function backgroundOf(locator: Locator): Promise<string> {
  return locator.evaluate((element) => getComputedStyle(element).backgroundColor);
}

async function pageScrollsSideways(page: Page): Promise<boolean> {
  return page.evaluate(
    () => document.documentElement.scrollWidth > document.documentElement.clientWidth,
  );
}

async function openPage(page: Page, path: string, heading: string): Promise<void> {
  await signIn(page, { returnPath: path });
  await expect(page.getByRole('heading', { level: 1, name: heading })).toBeVisible();
}

interface FocusFacts {
  name: string;
  ring: boolean;
  visible: boolean;
}

/**
 * The focused element's name and whether it shows a focus indicator: the
 * 2 px accent outline (design § 2.6), or for a text field or select the 2 px
 * accent border of its outline (design § 2.2).
 */
async function focusedFacts(page: Page): Promise<FocusFacts> {
  return page.evaluate(
    ({ accent, ringWidth }) => {
      const element = document.activeElement as HTMLElement | null;
      if (!element || element === document.body) {
        return { name: '', ring: false, visible: false };
      }
      const label = (element as HTMLInputElement).labels?.[0]?.textContent;
      const name = (element.getAttribute('aria-label') ?? label ?? element.textContent ?? '')
        .replace(/\s+/g, ' ')
        .trim();
      const style = getComputedStyle(element);
      let ring =
        style.outlineStyle !== 'none' &&
        parseFloat(style.outlineWidth) >= ringWidth &&
        style.outlineColor === accent;
      const field = element.closest('.MuiOutlinedInput-root');
      if (!ring && field) {
        const outline = field.querySelector('.MuiOutlinedInput-notchedOutline');
        const border = outline ? getComputedStyle(outline) : null;
        ring =
          border !== null &&
          parseFloat(border.borderTopWidth) >= ringWidth &&
          border.borderTopColor === accent;
      }
      const rect = element.getBoundingClientRect();
      const visible =
        rect.width > 1 &&
        rect.height > 1 &&
        rect.bottom > 0 &&
        rect.right > 0 &&
        rect.top < window.innerHeight &&
        rect.left < window.innerWidth;
      return { name, ring, visible };
    },
    { accent: DESIGN.accent, ringWidth: DESIGN.focusRingWidth },
  );
}

/** Tabs `count` times and returns what each stop was. */
async function tabStops(page: Page, count: number): Promise<FocusFacts[]> {
  const stops: FocusFacts[] = [];
  for (let index = 0; index < count; index += 1) {
    await page.keyboard.press('Tab');
    stops.push(await focusedFacts(page));
  }
  return stops;
}

test.describe('19. the application shell in a browser (mod-015)', () => {
  test.use({ viewport: { width: 1440, height: 900 } });

  test.describe('with no organization context: «Настройки» opens «Языки»', () => {
    test.beforeAll(async ({ browser }) => {
      await withApi(browser, async (request) => {
        restoreAfterNoContext = await deactivateAllOrganizations(request);
      });
    });

    test.afterAll(async ({ browser }) => {
      await withApi(browser, (request) => restoreOrganizations(request, restoreAfterNoContext));
    });

    test('19.1 «Языки» at 1440×900: designed density and colours, requests only to the stand (US-11, BR-2, BR-3, BR-17, US-8)', async ({
      page,
    }) => {
      const { panelUrl, apiUrl, keycloakUrl } = env();
      const origins = new Set<string>();
      page.on('request', (request) => {
        const url = new URL(request.url());
        if (url.protocol === 'http:' || url.protocol === 'https:') origins.add(url.origin);
      });

      await openPage(page, '/dictionaries/languages', 'Языки');
      const table = page.getByRole('table', { name: 'Реестр языков' });
      await expect(table).toBeVisible();

      // BR-3: base text 14 px, top bar 48 px, every registry row at most 40 px.
      expect(await page.evaluate(() => getComputedStyle(document.body).fontSize)).toBe(
        DESIGN.bodyFontSize,
      );
      const banner = page.getByRole('banner');
      expect((await banner.boundingBox())?.height).toBe(DESIGN.topBarHeight);
      const rowHeights = await table
        .locator('tbody tr')
        .evaluateAll((rows) => rows.map((row) => row.getBoundingClientRect().height));
      expect(rowHeights.length).toBeGreaterThan(0);
      for (const height of rowHeights) expect(height).toBeLessThanOrEqual(DESIGN.maxRowHeight);

      // BR-2: the colour roles on their surfaces.
      expect(await page.evaluate(() => getComputedStyle(document.body).backgroundColor)).toBe(
        DESIGN.applicationBackground,
      );
      expect(await backgroundOf(paperOf(page, mainNavigation(page)))).toBe(
        DESIGN.navigationSurface,
      );
      expect(await backgroundOf(paperOf(page, settingsNavigation(page)))).toBe(
        DESIGN.navigationSurface,
      );
      expect(await backgroundOf(banner)).toBe(DESIGN.contentSurface);
      expect(
        await backgroundOf(page.locator('.MuiTableContainer-root').filter({ has: table })),
      ).toBe(DESIGN.contentSurface);

      // US-6: the one primary action, filled in the accent, at the right of the title.
      const add = page.getByRole('button', { name: 'Добавить язык' });
      expect(await backgroundOf(add)).toBe(DESIGN.accent);
      expect(await add.evaluate((element) => getComputedStyle(element).color)).toBe(
        DESIGN.accentContrastText,
      );
      const titleBox = await page.getByRole('heading', { level: 1, name: 'Языки' }).boundingBox();
      const addBox = await add.boundingBox();
      expect(addBox && titleBox && addBox.x > titleBox.x + titleBox.width).toBe(true);

      // US-8: the five filters in a single band at 1440 px, between the header and the table.
      const filterBar = page.getByRole('group', { name: 'Фильтры' });
      const filterTops = await filterBar
        .locator('.MuiFormControl-root')
        .evaluateAll((fields) =>
          fields.map((field) => Math.round(field.getBoundingClientRect().top)),
        );
      expect(filterTops).toHaveLength(5);
      expect(new Set(filterTops).size).toBe(1);
      const barBox = await filterBar.boundingBox();
      const tableBox = await table.boundingBox();
      expect(titleBox && barBox && tableBox && titleBox.y < barBox.y && barBox.y < tableBox.y).toBe(
        true,
      );

      // US-8: a header row distinct from the rows; the row under the pointer highlighted.
      expect(await backgroundOf(table.locator('thead th').first())).toBe(DESIGN.tableHeader);
      const firstRow = table.locator('tbody tr').first();
      await firstRow.hover();
      await expect.poll(() => backgroundOf(firstRow)).toBe(DESIGN.rowHover);

      // BR-17 / NFR Network: nothing but the panel, Mustask.Api and Keycloak.
      const allowed = new Set([panelUrl, apiUrl, keycloakUrl].map((url) => new URL(url).origin));
      expect([...origins].filter((origin) => !allowed.has(origin))).toEqual([]);
    });

    test('19.2 the top bar and navigation stay while the page scrolls; the page header scrolls away (US-2, US-6)', async ({
      page,
    }) => {
      await page.setViewportSize({ width: 1440, height: 260 });
      await openPage(page, '/dictionaries/languages', 'Языки');
      await expect(page.getByRole('table', { name: 'Реестр языков' })).toBeVisible();

      const scrollable = await page.evaluate(
        () => document.documentElement.scrollHeight - document.documentElement.clientHeight,
      );
      expect(scrollable, 'the page must be taller than the window for this check').toBeGreaterThan(
        100,
      );
      await page.evaluate(() => window.scrollTo(0, document.documentElement.scrollHeight));
      await expect.poll(() => page.evaluate(() => window.scrollY)).toBeGreaterThan(100);

      const banner = await page.getByRole('banner').boundingBox();
      expect(banner?.y).toBe(0);
      expect(banner?.height).toBe(DESIGN.topBarHeight);
      for (const navigation of [mainNavigation(page), settingsNavigation(page)]) {
        const box = await paperOf(page, navigation).boundingBox();
        expect(box?.y).toBe(DESIGN.topBarHeight);
        await expect(navigation).toBeInViewport();
      }
      const title = await page.getByRole('heading', { level: 1, name: 'Языки' }).boundingBox();
      expect(title && title.y + title.height <= DESIGN.topBarHeight).toBe(true);
    });

    test('19.3 widths: 1280 expanded, 1279 and 1024 a rail, 1023 the overlay; no sideways page scroll at 1024 and 640 (US-4, BR-10, D-8)', async ({
      page,
    }) => {
      await page.setViewportSize({ width: 1280, height: 800 });
      await openPage(page, '/home', 'Главная');
      const mainPaper = paperOf(page, mainNavigation(page));

      await expect.poll(() => width(mainPaper)).toBe(DESIGN.navigationWidth);
      await page.setViewportSize({ width: 1279, height: 800 });
      await expect.poll(() => width(mainPaper)).toBe(DESIGN.railWidth);
      await page.setViewportSize({ width: 1024, height: 800 });
      await expect.poll(() => width(mainPaper)).toBe(DESIGN.railWidth);
      await expect(menuButton(page)).toHaveCount(0);
      await page.setViewportSize({ width: 1023, height: 800 });
      await expect(menuButton(page)).toBeVisible();
      await expect(mainNavigation(page)).toHaveCount(0);
      expect(await pageScrollsSideways(page)).toBe(false);

      // 1024 px with the navigation kept expanded, outside and inside settings.
      await page.setViewportSize({ width: 1024, height: 800 });
      await page.getByRole('button', { name: 'Развернуть меню' }).click();
      await expect.poll(() => width(mainPaper)).toBe(DESIGN.navigationWidth);
      expect(await pageScrollsSideways(page)).toBe(false);
      await mainNavigation(page).getByRole('link', { name: 'Настройки' }).click();
      await expect(page.getByRole('table', { name: 'Реестр языков' })).toBeVisible();
      await expect.poll(() => width(mainPaper)).toBe(DESIGN.railWidth);
      expect(await width(paperOf(page, settingsNavigation(page)))).toBe(
        DESIGN.settingsNavigationWidth,
      );
      expect(await pageScrollsSideways(page)).toBe(false);

      // 640 px, the floor: no page scroll sideways on any page of this task; a
      // table wider than the content scrolls inside its own container.
      await page.setViewportSize({ width: 640, height: 800 });
      await expect(menuButton(page)).toBeVisible();
      expect(await pageScrollsSideways(page)).toBe(false);
      const container = page.locator('.MuiTableContainer-root').filter({
        has: page.getByRole('table', { name: 'Реестр языков' }),
      });
      const overflow = await container.evaluate((element) => ({
        wider: element.scrollWidth > element.clientWidth,
        overflowX: getComputedStyle(element).overflowX,
      }));
      if (overflow.wider) expect(['auto', 'scroll']).toContain(overflow.overflowX);

      for (const [path, heading] of [
        ['/dictionaries/languages/new', 'Добавить язык'],
        ['/dictionaries/languages/ru', 'Язык: ru'],
        ['/home', 'Главная'],
        ['/no-such-page', 'Страница не найдена'],
      ] as const) {
        await page.goto(path);
        await expect(page.getByRole('heading', { level: 1, name: heading })).toBeVisible();
        expect(await pageScrollsSideways(page), path).toBe(false);
      }
    });

    test('19.6 collapse, the kept choice and reduced motion (US-4, BR-10)', async ({ page }) => {
      await openPage(page, '/home', 'Главная');
      const mainPaper = paperOf(page, mainNavigation(page));
      await expect.poll(() => width(mainPaper)).toBe(DESIGN.navigationWidth);

      await page.getByRole('button', { name: 'Свернуть меню' }).click();
      await expect.poll(() => width(mainPaper)).toBe(DESIGN.railWidth);
      await expect(page.getByRole('button', { name: 'Развернуть меню' })).toHaveAttribute(
        'aria-expanded',
        'false',
      );

      await page.reload();
      await expect(page.getByRole('heading', { level: 1, name: 'Главная' })).toBeVisible();
      await expect.poll(() => width(mainPaper)).toBe(DESIGN.railWidth);
      const second = await page.context().newPage();
      await second.goto('/home');
      await expect(second.getByRole('heading', { level: 1, name: 'Главная' })).toBeVisible();
      await expect
        .poll(() => width(paperOf(second, mainNavigation(second))))
        .toBe(DESIGN.railWidth);
      await second.close();

      // Motion allowed: the width change is animated (guards the next check
      // against a vacuous pass).
      const duration = () =>
        mainPaper.evaluate((element) => getComputedStyle(element).transitionDuration);
      expect(await duration()).not.toMatch(/^0s(, 0s)*$/);

      // Reduced motion: no transition, the width is final at once.
      await page.emulateMedia({ reducedMotion: 'reduce' });
      expect(await duration()).toMatch(/^0s(, 0s)*$/);
      await page.getByRole('button', { name: 'Развернуть меню' }).click();
      expect(await width(mainPaper)).toBe(DESIGN.navigationWidth);

      // …and the overlay opens without sliding.
      await page.setViewportSize({ width: 900, height: 800 });
      await menuButton(page).click();
      const overlayPaper = paperOf(page, mainNavigation(page));
      await expect(overlayPaper).toBeVisible();
      expect(
        await overlayPaper.evaluate((element) => getComputedStyle(element).transitionDuration),
      ).toMatch(/^0s(, 0s)*$/);
      // Without a transition the overlay still hands focus to the main region
      // once an entry is chosen (BR-10).
      await mainNavigation(page).getByRole('link', { name: 'Настройки' }).click();
      await expect(page.getByRole('table', { name: 'Реестр языков' })).toBeVisible();
      await expect(mainNavigation(page)).toHaveCount(0);
      await expect(page.locator('#main-content')).toBeFocused();
    });

    test('19.7a the overlay below 1024 px is not covered by the top bar; «Главная» can be chosen in it (US-4, BR-10, BR-19)', async ({
      page,
    }) => {
      await page.setViewportSize({ width: 900, height: 800 });
      await openPage(page, '/dictionaries/languages', 'Языки');
      await expect(mainNavigation(page)).toHaveCount(0);

      // On a settings page: the rail with «Настройки» current and «Меню настроек».
      await menuButton(page).click();
      await expect(mainNavigation(page).getByRole('link', { name: 'Настройки' })).toHaveAttribute(
        'aria-current',
        'true',
      );
      await expect(settingsNavigation(page).getByRole('link', { name: 'Языки' })).toHaveAttribute(
        'aria-current',
        'page',
      );

      // Nothing of the open overlay may sit under another layer: the topmost
      // element at the centre of «Главная» and of the settings title is that
      // control itself — on a settings page and outside settings. Measured once
      // the paper has slid in (225 ms): while it slides, «Главная» is still
      // partly off-screen, and that says nothing about the stacking.
      const uncovered = (target: Locator) =>
        target.evaluate((element) => {
          const box = element.getBoundingClientRect();
          const top = document.elementFromPoint(box.x + box.width / 2, box.y + box.height / 2);
          return top !== null && element.contains(top);
        });
      const overlaySettled = () =>
        expect
          .poll(async () => (await paperOf(page, mainNavigation(page)).boundingBox())?.x, {
            message: 'the overlay paper has slid in to the left edge',
          })
          .toBe(0);
      const home = mainNavigation(page).getByRole('link', { name: 'Главная' });
      await overlaySettled();
      expect
        .soft(await uncovered(home), '«Главная» in the rail is topmost at its own centre')
        .toBe(true);
      expect
        .soft(
          await uncovered(settingsNavigation(page).getByText('Настройки', { exact: true })),
          'the title «Настройки» of «Меню настроек» is topmost at its own centre',
        )
        .toBe(true);
      await page.keyboard.press('Escape');
      await page.goto('/home');
      await expect(page.getByRole('heading', { level: 1, name: 'Главная' })).toBeVisible();
      // Measured before opening: an open overlay hides the rest of the page
      // from the accessibility tree, the top bar included.
      const bar = await page.getByRole('banner').boundingBox();
      const profile = await page
        .getByRole('banner')
        .getByRole('button', { name: /^Профиль/ })
        .boundingBox();
      if (!bar || !profile) throw new Error('the top bar is not rendered');
      await menuButton(page).click();
      await overlaySettled();
      expect
        .soft(
          await uncovered(home),
          '«Главная» in the overlay outside settings is topmost at its own centre',
        )
        .toBe(true);

      // A click outside the overlay closes it (BR-10); an empty stretch of the
      // top bar, left of the user menu, is outside it too.
      await page.mouse.click(profile.x - 40, bar.y + bar.height / 2);
      await expect
        .soft(mainNavigation(page), 'a click on an empty stretch of the top bar closes the overlay')
        .toHaveCount(0, { timeout: 2_000 });
      if ((await mainNavigation(page).count()) === 0) await menuButton(page).click();
      await expect(mainNavigation(page)).toBeVisible();

      // Choosing «Главная» there: closes, opens the page, focus in its main region.
      await home.click({ timeout: 5_000 });
      await expect(page).toHaveURL(/\/home$/);
      await expect(mainNavigation(page)).toHaveCount(0);
      await expect(page.locator('#main-content')).toBeFocused();
    });

    test('19.7 the overlay below 1024 px: trail in the header, focus on close and on choosing, widening (US-4, BR-10, US-12)', async ({
      page,
    }) => {
      await page.setViewportSize({ width: 900, height: 800 });
      await openPage(page, '/dictionaries/languages', 'Языки');
      await expect(mainNavigation(page)).toHaveCount(0);

      // Below 1024 px the trail stands in the page header, once.
      await expect(page.getByRole('navigation', { name: 'Путь к странице' })).toHaveCount(1);
      await expect(
        page.getByRole('main').getByRole('navigation', { name: 'Путь к странице' }),
      ).toHaveCount(1);

      // Choosing an entry (one clear of the top bar, see 19.7a): closes, opens
      // the page, focus in its main region.
      await menuButton(page).click();
      await mainNavigation(page).getByRole('link', { name: 'Настройки' }).click();
      await expect(mainNavigation(page)).toHaveCount(0);
      await expect(page.locator('#main-content')).toBeFocused();
      await page.getByRole('banner').getByRole('link', { name: 'Mustask' }).click();
      await expect(page.getByRole('heading', { level: 1, name: 'Главная' })).toBeVisible();

      // Escape and a click outside return focus to «Меню».
      await menuButton(page).click();
      await expect(mainNavigation(page)).toBeVisible();
      await page.keyboard.press('Escape');
      await expect(mainNavigation(page)).toHaveCount(0);
      await expect(menuButton(page)).toBeFocused();
      await menuButton(page).click();
      await expect(mainNavigation(page)).toBeVisible();
      await page.locator('.MuiBackdrop-root').click({ position: { x: 700, y: 400 } });
      await expect(mainNavigation(page)).toHaveCount(0);
      await expect(menuButton(page)).toBeFocused();

      // Widening with the overlay open leaves no backdrop, scroll lock or
      // hidden page behind.
      await menuButton(page).click();
      await expect(mainNavigation(page)).toBeVisible();
      await page.setViewportSize({ width: 1440, height: 800 });
      await expect(page.locator('.MuiBackdrop-root')).toHaveCount(0);
      await expect(mainNavigation(page)).toBeVisible();
      await expect
        .poll(() => width(paperOf(page, mainNavigation(page))))
        .toBe(DESIGN.navigationWidth);
      expect(await page.evaluate(() => getComputedStyle(document.body).overflow)).not.toBe(
        'hidden',
      );
      await expect(page.locator('#root')).not.toHaveAttribute('aria-hidden', 'true');
      await page.getByRole('banner').getByRole('link', { name: 'Mustask' }).click();
      await expect(page).toHaveURL(/\/home$/);
    });

    test('19.8 moving between pages keeps the document; not found keeps the address; loading and error of «Языки» (US-2, US-7, BR-13)', async ({
      page,
    }) => {
      const { apiUrl } = env();
      await openPage(page, '/home', 'Главная');

      // No document reload on navigation (NFR Network): a marker set on window survives.
      await page.evaluate(() => {
        (window as unknown as { qaMarker?: string }).qaMarker = 'kept';
      });
      const marker = () =>
        page.evaluate(() => (window as unknown as { qaMarker?: string }).qaMarker ?? null);
      await mainNavigation(page).getByRole('link', { name: 'Настройки' }).click();
      await expect(page.getByRole('heading', { level: 1, name: 'Языки' })).toBeVisible();
      await page.getByRole('banner').getByRole('link', { name: 'Mustask' }).click();
      await expect(page.getByRole('heading', { level: 1, name: 'Главная' })).toBeVisible();
      expect(await marker()).toBe('kept');
      await expect(page).toHaveTitle('Главная — Mustask');

      // A trailing slash names the same page (qa.md D-1): its entry is marked as
      // the page, and the trail holds no link to the page itself.
      const trailLinks = page
        .getByRole('navigation', { name: 'Путь к странице' })
        .getByRole('link');
      for (const [path, heading, entry] of [
        ['/home/', 'Главная', mainNavigation(page).getByRole('link', { name: 'Главная' })],
        [
          '/dictionaries/languages/',
          'Языки',
          settingsNavigation(page).getByRole('link', { name: 'Языки' }),
        ],
      ] as const) {
        await page.goto(path);
        await expect(page.getByRole('heading', { level: 1, name: heading })).toBeVisible();
        await expect(entry, `«${heading}» marked as the page at ${path}`).toHaveAttribute(
          'aria-current',
          'page',
        );
        await expect(trailLinks, `no trail link to the page itself at ${path}`).toHaveCount(0);
      }

      // Unknown addresses: the frame in the shell, the address kept, «На главную»;
      // no page is current there, so no settings navigation and no entry marked
      // (qa.md D-2, BR-13, BR-19).
      for (const path of ['/no-such-page', '/dictionaries', '/dictionaries/languages/ru/extra']) {
        await page.goto(path);
        await expect(
          page.getByRole('heading', { level: 1, name: 'Страница не найдена' }),
        ).toBeVisible();
        await expect(page).toHaveTitle('Страница не найдена — Mustask');
        expect(new URL(page.url()).pathname).toBe(path);
        await expect(page.getByRole('banner')).toBeVisible();
        await expect(settingsNavigation(page), `no «Меню настроек» at ${path}`).toHaveCount(0);
        await expect(
          mainNavigation(page).locator('[aria-current]'),
          `no entry marked at ${path}`,
        ).toHaveCount(0);
        await expect(
          mainNavigation(page).getByRole('link', { name: 'Главная' }),
          `the main navigation as kept (expanded at 1440 px) at ${path}`,
        ).toBeVisible();
        expect(await width(paperOf(page, mainNavigation(page)))).toBe(DESIGN.navigationWidth);
      }
      await page.getByRole('link', { name: 'На главную' }).click();
      await expect(page).toHaveURL(/\/home$/);
      await expect(page).toHaveTitle('Главная — Mustask');

      // Loading: the languages answer is held back.
      const isLanguages = (url: URL) =>
        url.origin === new URL(apiUrl).origin && url.pathname === '/api/dictionaries/languages';
      let release: () => void = () => {};
      const held = new Promise<void>((resolve) => {
        release = resolve;
      });
      await page.route(isLanguages, async (route) => {
        await held;
        await route.continue();
      });
      await mainNavigation(page).getByRole('link', { name: 'Настройки' }).click();
      const loading = page.getByRole('status', { name: 'Загрузка данных' });
      await expect(loading).toBeVisible();
      await expect(loading.getByRole('columnheader', { name: 'Код' })).toBeVisible();
      await expect(page.getByRole('heading', { level: 1, name: 'Языки' })).toBeVisible();
      await expect(page.getByRole('group', { name: 'Фильтры' })).toBeVisible();
      await expect(page.getByRole('progressbar')).toHaveCount(0);
      release();
      await expect(page.getByRole('table', { name: 'Реестр языков' })).toBeVisible();
      await expect(loading).toHaveCount(0);
      await page.unroute(isLanguages);

      // Error: the languages request is refused at the network layer; «Повторить» recovers.
      await page.route(isLanguages, (route) => route.abort('connectionrefused'));
      await page.goto('/dictionaries/languages');
      const error = page.getByRole('alert');
      await expect(error).toBeVisible();
      await expect(error.getByRole('button', { name: 'Повторить' })).toBeVisible();
      await expect(page.getByRole('heading', { level: 1, name: 'Языки' })).toBeVisible();
      await expect(page.getByRole('group', { name: 'Фильтры' })).toBeVisible();
      await page.unroute(isLanguages);
      await error.getByRole('button', { name: 'Повторить' }).click();
      await expect(page.getByRole('table', { name: 'Реестр языков' })).toBeVisible();
      await expect(page.getByRole('alert')).toHaveCount(0);
    });

    test('19.9 a reader’s page header is as high as an administrator’s (US-6, BR-9)', async ({
      page,
      browser,
    }) => {
      await openPage(page, '/dictionaries/languages', 'Языки');
      await expect(page.getByRole('button', { name: 'Добавить язык' })).toBeVisible();
      const header = (target: Page) => target.locator('.PageHeader-root');
      const administratorHeight = (await header(page).boundingBox())?.height;

      const { keycloakUrl, realm } = env();
      const reader = await createEphemeralReaderUser(keycloakUrl, realm);
      const context = await browser.newContext({ viewport: { width: 1440, height: 900 } });
      try {
        const readerPage = await context.newPage();
        await signIn(readerPage, {
          username: reader.username,
          returnPath: '/dictionaries/languages',
        });
        await expect(readerPage.getByRole('heading', { level: 1, name: 'Языки' })).toBeVisible();
        await expect(readerPage.getByRole('button', { name: 'Добавить язык' })).toHaveCount(0);
        expect((await header(readerPage).boundingBox())?.height).toBe(administratorHeight);
      } finally {
        await context.close();
        await deleteEphemeralUser(keycloakUrl, realm, reader.id);
      }
    });
  });

  test.describe('with an organization context: the switcher on real lists', () => {
    test.beforeAll(async ({ browser }) => {
      await withApi(browser, async (request) => {
        restoreAfterContext = await deactivateAllOrganizations(request);
        first = await createContext(request, {
          organization: runName('Пример организации 1'),
          project: 'Пример проекта 1',
          teams: ['Пример команды 2'],
        });
        second = await createContext(request, {
          organization: runName('Пример организации 2'),
          project: 'Пример проекта 3',
        });
      });
    });

    test.afterAll(async ({ browser }) => {
      await withApi(browser, async (request) => {
        for (const fixture of [first, second]) {
          if (fixture) await deactivateOrganization(request, fixture.organization.id);
        }
        await restoreOrganizations(request, restoreAfterContext);
      });
    });

    test('19.4 the top bar at 1024 and 640 px: names cut with an ellipsis, kept whole in name and tooltip, nothing overlaps (US-5, US-12, BR-11)', async ({
      page,
    }) => {
      await page.setViewportSize({ width: 1024, height: 800 });
      await openPage(page, '/home', 'Главная');
      const levels = switcher(page).getByRole('button');
      await expect(levels).toHaveCount(3);

      // 1024 px: a 100-character name stays on one line, cut with an ellipsis
      // at the design's 160 px. The panel's build carries no such name, so it
      // is put into the rendered label for the measurement.
      const nameSpan = levels.first().locator(':scope > span').first();
      const cut = await nameSpan.evaluate((element) => {
        element.textContent = 'Очень длинное название организации '.repeat(3).trim();
        const style = getComputedStyle(element);
        return {
          client: element.clientWidth,
          scroll: element.scrollWidth,
          height: element.getBoundingClientRect().height,
          lineHeight: parseFloat(style.lineHeight),
          textOverflow: style.textOverflow,
          whiteSpace: style.whiteSpace,
        };
      });
      expect(cut.textOverflow).toBe('ellipsis');
      expect(cut.whiteSpace).toBe('nowrap');
      expect(cut.client).toBeLessThanOrEqual(DESIGN.contextNameMaxWidth.medium);
      expect(cut.scroll).toBeGreaterThan(cut.client);
      expect(cut.height).toBeLessThanOrEqual(cut.lineHeight + 1);

      // 640 px: the organizations' own names are cut at 96 px and kept
      // whole in the accessible name; «Меню», the mark, the levels and the
      // user menu stand side by side inside the window.
      await page.reload();
      await page.setViewportSize({ width: 640, height: 800 });
      await expect(page.getByRole('heading', { level: 1, name: 'Главная' })).toBeVisible();
      await expect(menuButton(page)).toBeVisible();
      for (const [index, full] of firstLevels().entries()) {
        const level = levels.nth(index);
        await expect(level).toHaveAttribute('aria-label', full);
        const span = await level
          .locator(':scope > span')
          .first()
          .evaluate((element) => ({ client: element.clientWidth, scroll: element.scrollWidth }));
        expect(span.client).toBeLessThanOrEqual(DESIGN.contextNameMaxWidth.narrow);
        expect(span.scroll, `${full} is cut, not wrapped`).toBeGreaterThan(span.client);
      }
      const banner = page.getByRole('banner');
      const controls = [
        menuButton(page),
        banner.getByRole('link', { name: 'Mustask' }),
        levels.nth(0),
        levels.nth(1),
        levels.nth(2),
        banner.getByRole('button', { name: /^Профиль/ }),
      ];
      const boxes = [];
      for (const control of controls) {
        const box = await control.boundingBox();
        if (!box) throw new Error('a top-bar control is not rendered');
        boxes.push(box);
      }
      for (let index = 1; index < boxes.length; index += 1) {
        const previous = boxes[index - 1];
        const current = boxes[index];
        expect(previous && current && previous.x + previous.width <= current.x + 0.5).toBe(true);
      }
      const last = boxes[boxes.length - 1];
      expect(last && last.x + last.width <= 640).toBe(true);
      expect(await pageScrollsSideways(page)).toBe(false);

      // The full name as a tooltip, on hover and on keyboard focus.
      await levels.first().hover();
      await expect(page.getByRole('tooltip')).toHaveText(first?.organization.name ?? '');
      await page.mouse.move(600, 600);
      await expect(page.getByRole('tooltip')).toHaveCount(0);
      await banner.getByRole('link', { name: 'Mustask' }).focus();
      await page.keyboard.press('Tab');
      await expect(levels.first()).toBeFocused();
      await expect(page.getByRole('tooltip')).toHaveText(first?.organization.name ?? '');
    });

    test('19.5 keyboard: skip link, tab order with a visible focus ring, menus (US-12)', async ({
      page,
    }) => {
      await openPage(page, '/home', 'Главная');
      await page.goto('/home');
      await expect(page.getByRole('heading', { level: 1, name: 'Главная' })).toBeVisible();

      // The skip link is the first stop, shown while focused; Enter moves focus to main.
      const [skip] = await tabStops(page, 1);
      expect(skip).toEqual({ name: 'Перейти к содержимому', ring: true, visible: true });
      await page.keyboard.press('Enter');
      await expect(page.locator('#main-content')).toBeFocused();
      expect(new URL(page.url()).hash).toBe('');

      // «Главная» at 1440 px: from the skip link, the listed order. A project
      // is chosen, so mod-003's project-scoped «Рабочие элементы» stands in the
      // work group after «Главная» (mod-003 spec § Navigation); a team is
      // chosen too, so mod-006's team-scoped «Канбан» (named «Доски» until
      // feat-001 BR-H-16) and «Бэклоги» follow it (mod-006 spec § Navigation).
      await page.goto('/home');
      await expect(page.getByRole('heading', { level: 1, name: 'Главная' })).toBeVisible();
      const home = await tabStops(page, 12);
      expect(home.map((stop) => stop.name)).toEqual([
        'Перейти к содержимому',
        'Mustask',
        ...firstLevels(),
        expect.stringMatching(/^Профиль/),
        'Главная',
        'Рабочие элементы',
        'Канбан',
        'Бэклоги',
        'Настройки',
        'Свернуть меню',
      ]);
      for (const stop of home)
        expect(stop, `focus ring on «${stop.name}»`).toMatchObject({ ring: true, visible: true });

      // «Языки» for the same user: the rail, «Языки», then the page's controls.
      await page.goto('/dictionaries/languages');
      await expect(page.getByRole('table', { name: 'Реестр языков' })).toBeVisible();
      // With an organization and a project chosen, «Меню настроек» holds
      // their entries before «Справочники» (mod-002 design § 2.1).
      const languages = await tabStops(page, 20);
      expect(languages.map((stop) => stop.name)).toEqual([
        'Перейти к содержимому',
        'Mustask',
        ...firstLevels(),
        expect.stringMatching(/^Профиль/),
        'Главная',
        'Рабочие элементы',
        'Канбан',
        'Бэклоги',
        'Настройки',
        'Общие',
        'Проекты',
        'Процесс',
        'Общие',
        'Команды',
        'Области',
        'Языки',
        'Добавить язык',
        'Код',
      ]);
      for (const stop of languages)
        expect(stop, `focus ring on «${stop.name}»`).toMatchObject({ ring: true, visible: true });

      // Switcher menus: open on the current entry, arrows move, Enter chooses,
      // Escape returns focus; the only requests are the chosen organization's
      // projects and its first project's teams, one level at a time (mod-002
      // design § 3.1).
      const requests: string[] = [];
      const record = (request: { url(): string }) => requests.push(request.url());
      page.on('request', record);
      const levels = switcher(page).getByRole('button');
      await levels.first().focus();
      await page.keyboard.press('Enter');
      await expect(
        page.getByRole('menuitemradio', { name: first?.organization.name ?? '' }),
      ).toBeFocused();
      await expect(
        page.getByRole('menuitemradio', { name: first?.organization.name ?? '' }),
      ).toHaveAttribute('aria-checked', 'true');
      await page.keyboard.press('ArrowDown');
      await expect(
        page.getByRole('menuitemradio', { name: second?.organization.name ?? '' }),
      ).toBeFocused();
      await page.keyboard.press('Enter');
      await expect(page.getByRole('menu')).toHaveCount(0);
      await expect(levels.nth(0)).toHaveAttribute(
        'aria-label',
        `Организация: ${second?.organization.name ?? ''}`,
      );
      await expect(levels.nth(1)).toHaveAttribute('aria-label', 'Проект: Пример проекта 3');
      await expect(levels.nth(2)).toHaveAttribute('aria-label', 'Команда: Пример проекта 3');
      await expect(levels.nth(0)).toBeFocused();
      await page.keyboard.press(' ');
      await expect(page.getByRole('menu')).toBeVisible();
      await page.keyboard.press('Escape');
      await expect(page.getByRole('menu')).toHaveCount(0);
      await expect(levels.nth(0)).toBeFocused();

      // US-5: «Языки» ignores the context — same address, same rows, no new request.
      expect(new URL(page.url()).pathname).toBe('/dictionaries/languages');
      await expect(page.getByRole('table', { name: 'Реестр языков' })).toBeVisible();
      page.off('request', record);
      const secondId = second?.organization.id ?? '';
      expect(
        requests
          .filter((url) => !url.includes('/api/session/token'))
          .map((url) => new URL(url).pathname),
      ).toEqual([
        `/api/organizations/${secondId}/projects`,
        `/api/organizations/${secondId}/projects/${second?.project.id ?? ''}/teams`,
      ]);

      // The user menu by keyboard: ArrowDown opens on «Выйти», Escape returns focus.
      const profile = page.getByRole('banner').getByRole('button', { name: /^Профиль/ });
      await profile.focus();
      await page.keyboard.press('ArrowDown');
      await expect(page.getByRole('menuitem', { name: 'Выйти' })).toBeFocused();
      await page.keyboard.press('Escape');
      await expect(profile).toBeFocused();

      // A reload keeps the choice: it is remembered per user (mod-002 US-8).
      await page.reload();
      await expect(levels.first()).toHaveAttribute(
        'aria-label',
        `Организация: ${second?.organization.name ?? ''}`,
      );
    });

    test('19.10 keyboard below 1024 px: «Меню» right after the skip link, the overlay opened, closed and used by keyboard (US-4, US-12, BR-10)', async ({
      page,
    }) => {
      await page.setViewportSize({ width: 900, height: 800 });
      await openPage(page, '/dictionaries/languages/new', 'Добавить язык');
      await page.goto('/dictionaries/languages/new');
      await expect(page.getByRole('heading', { level: 1, name: 'Добавить язык' })).toBeVisible();

      // Design § 12.3: below 1024 px «Меню» comes first after the skip link;
      // the trail stands in the page header (§ 3.6), so its link «Языки» is
      // the first stop in the main region, after the user menu. The form puts
      // focus in «Код» when it opens, so the walk starts at the skip link
      // (the first stop of every page, 19.5).
      await page.getByRole('link', { name: 'Перейти к содержимому' }).focus();
      const stops = await tabStops(page, 7);
      expect(stops.map((stop) => stop.name)).toEqual([
        'Меню',
        'Mustask',
        ...firstLevels(),
        expect.stringMatching(/^Профиль/),
        'Языки',
      ]);
      for (const stop of stops)
        expect(stop, `focus ring on «${stop.name}»`).toMatchObject({ ring: true, visible: true });

      // Enter on «Меню» opens the overlay with focus inside it; Escape closes
      // it and returns focus to «Меню» (BR-10).
      const menu = menuButton(page);
      await menu.focus();
      await page.keyboard.press('Enter');
      await expect(mainNavigation(page)).toBeVisible();
      // The open overlay is modal: the rest of the page, the top bar included,
      // is out of the accessibility tree (as 19.7a measures), so «Меню» is
      // looked up past that to read its state.
      await expect(
        page
          .getByRole('banner', { includeHidden: true })
          .getByRole('button', { name: 'Меню', exact: true, includeHidden: true }),
      ).toHaveAttribute('aria-expanded', 'true');
      const focusInOverlay = () =>
        page.evaluate(() =>
          Array.from(document.querySelectorAll('.MuiDrawer-paper')).some((paper) =>
            paper.contains(document.activeElement),
          ),
        );
      await expect.poll(focusInOverlay).toBe(true);
      await page.keyboard.press('Escape');
      await expect(mainNavigation(page)).toHaveCount(0);
      await expect(menu).toBeFocused();

      // US-4 "Narrow window", by keyboard: open again, reach «Главная» with
      // Tab, choose it with Enter — the overlay closes, «Главная» opens and
      // focus is in its main region.
      await page.keyboard.press('Enter');
      await expect(mainNavigation(page)).toBeVisible();
      await expect.poll(focusInOverlay).toBe(true);
      const home = mainNavigation(page).getByRole('link', { name: 'Главная' });
      for (let index = 0; index < 6; index += 1) {
        if (await home.evaluate((element) => element === document.activeElement)) break;
        await page.keyboard.press('Tab');
      }
      await expect(home).toBeFocused();
      await page.keyboard.press('Enter');
      await expect(page).toHaveURL(/\/home$/);
      await expect(page.getByRole('heading', { level: 1, name: 'Главная' })).toBeVisible();
      await expect(mainNavigation(page)).toHaveCount(0);
      await expect(page.locator('#main-content')).toBeFocused();
    });

    test('19.11 the top bar at 1280 and 1024 px with a trail link: the link is a stop before the user menu; long names cut at the design width, nothing overlaps (US-2, US-5, BR-6, BR-11)', async ({
      page,
    }) => {
      await page.setViewportSize({ width: 1280, height: 800 });
      await openPage(page, '/dictionaries/languages/new', 'Добавить язык');
      await page.goto('/dictionaries/languages/new');
      await expect(page.getByRole('heading', { level: 1, name: 'Добавить язык' })).toBeVisible();
      const banner = page.getByRole('banner');
      const trail = banner.getByRole('navigation', { name: 'Путь к странице' });
      await expect(trail.getByRole('link', { name: 'Языки' })).toBeVisible();

      // Design § 12.3: a trail link sits between the switcher and the user
      // menu. The walk starts at the skip link, as in 19.10.
      await page.getByRole('link', { name: 'Перейти к содержимому' }).focus();
      const stops = await tabStops(page, 6);
      expect(stops.map((stop) => stop.name)).toEqual([
        'Mustask',
        ...firstLevels(),
        'Языки',
        expect.stringMatching(/^Профиль/),
      ]);
      for (const stop of stops)
        expect(stop, `focus ring on «${stop.name}»`).toMatchObject({ ring: true, visible: true });

      // The worst case the design allows for (BR-11; design § 2.5, § 3.6): all
      // three levels carry a name longer than the design width. The build's
      // fixture's names are short, so long ones are put into the rendered
      // labels for the measurement.
      const levels = switcher(page).getByRole('button');
      for (const [width, max] of [
        [1280, 200],
        [1024, 160],
      ] as const) {
        await page.setViewportSize({ width, height: 800 });
        await expect(trail).toBeVisible();
        const cuts = await levels.evaluateAll((buttons) =>
          buttons.map((button) => {
            const span = button.querySelector(':scope > span');
            if (!span) return { client: -1, scroll: -1 };
            span.textContent = 'Очень длинное название уровня контекста '.repeat(3).trim();
            return { client: span.clientWidth, scroll: span.scrollWidth };
          }),
        );
        expect(cuts).toHaveLength(3);
        for (const cut of cuts) {
          expect(cut.client, `name width at ${width} px`).toBeLessThanOrEqual(max);
          expect(cut.scroll, `name cut at ${width} px`).toBeGreaterThan(cut.client);
        }
        const controls = [
          banner.getByRole('link', { name: 'Mustask' }),
          levels.nth(0),
          levels.nth(1),
          levels.nth(2),
          trail,
          banner.getByRole('button', { name: /^Профиль/ }),
        ];
        const boxes = [];
        for (const control of controls) {
          const box = await control.boundingBox();
          if (!box) throw new Error('a top-bar control is not rendered');
          boxes.push(box);
        }
        for (let index = 1; index < boxes.length; index += 1) {
          const previous = boxes[index - 1];
          const current = boxes[index];
          expect
            .soft(
              previous && current && previous.x + previous.width <= current.x + 0.5,
              `top-bar control ${index} clear of the one before it at ${width} px`,
            )
            .toBe(true);
        }
        const last = boxes[boxes.length - 1];
        expect
          .soft(last && last.x + last.width <= width, `user menu inside ${width} px`)
          .toBe(true);
        expect
          .soft((await banner.boundingBox())?.height, `top bar height at ${width} px`)
          .toBe(DESIGN.topBarHeight);
        await page.reload();
        await expect(page.getByRole('heading', { level: 1, name: 'Добавить язык' })).toBeVisible();
      }
    });

    test('19.12 the shell raises no script error and no blocked aria-hidden while it is used (US-2..US-5, US-12)', async ({
      page,
    }) => {
      const problems: string[] = [];
      page.on('pageerror', (error) => problems.push(`pageerror: ${error.message}`));
      page.on('console', (message) => {
        const text = message.text();
        if (message.type() === 'error' && !text.startsWith('Failed to load resource')) {
          problems.push(`console.error: ${text}`);
        }
        if (/aria-hidden/i.test(text)) problems.push(`console.${message.type()}: ${text}`);
      });

      await openPage(page, '/home', 'Главная');
      // With an organization chosen, «Настройки» opens its «Общие» (mod-002
      // design § 2.1); «Языки» is reached through «Меню настроек». Until the
      // switcher's lists arrive no organization is chosen and «Настройки»
      // still leads to «Языки», so the click waits for the three levels.
      await expect(switcher(page).getByRole('button')).toHaveCount(3);
      await mainNavigation(page).getByRole('link', { name: 'Настройки' }).click();
      await expect(page.getByRole('heading', { level: 1, name: 'Общие' })).toBeVisible();
      await settingsNavigation(page).getByRole('link', { name: 'Языки' }).click();
      await expect(page.getByRole('table', { name: 'Реестр языков' })).toBeVisible();
      await page.getByRole('button', { name: 'Добавить язык' }).click();
      await expect(page.getByRole('heading', { level: 1, name: 'Добавить язык' })).toBeVisible();
      const levels = switcher(page).getByRole('button');
      await levels.first().click();
      await page.getByRole('menuitemradio', { name: second?.organization.name ?? '' }).click();
      await expect(page.getByRole('menu')).toHaveCount(0);
      const profile = page.getByRole('banner').getByRole('button', { name: /^Профиль/ });
      await profile.click();
      await expect(page.getByRole('menuitem', { name: 'Выйти' })).toBeVisible();
      await page.keyboard.press('Escape');
      await expect(page.getByRole('menu')).toHaveCount(0);

      await page.setViewportSize({ width: 900, height: 800 });
      await menuButton(page).click();
      await expect(mainNavigation(page)).toBeVisible();
      await page.keyboard.press('Escape');
      await expect(mainNavigation(page)).toHaveCount(0);
      await menuButton(page).click();
      await mainNavigation(page).getByRole('link', { name: 'Настройки' }).click();
      await expect(page.getByRole('heading', { level: 1, name: 'Общие' })).toBeVisible();
      await page.goto('/no-such-page');
      await expect(
        page.getByRole('heading', { level: 1, name: 'Страница не найдена' }),
      ).toBeVisible();

      expect(problems).toEqual([]);
    });
  });
});
