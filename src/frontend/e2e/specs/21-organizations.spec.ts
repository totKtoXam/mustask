import { expect, test, type Browser, type Page } from '@playwright/test';
import { signIn } from '../support/auth';
import { env } from '../support/env';
import {
  createEphemeralUserWithoutGroup,
  deleteEphemeralUser,
  userExistsById,
} from '../support/keycloakAdmin';
import {
  createContext,
  createOrganization,
  deactivateOrganization,
  runKey,
  runName,
  send,
} from '../support/organizationsApi';

/**
 * 21. Organizations, projects, teams and areas (mod-002 US-1..US-8) against
 * the real backend.
 *
 * The fixture user is in «Администраторы», whose realm role `admin` carries
 * the create right (mod-002 D-i); the realm import gained it, so the run that
 * counts follows the stand reset of D-j. Names carry a run suffix: names are
 * unique instance-wide and nothing is ever purged (BR-M1-1), so every
 * organization a case creates is deactivated in `afterAll`.
 *
 * One fault is injected on purpose (21.5): the switcher's projects read is
 * refused at the network layer, as 19.8 and 20.3 do. Nothing on the session
 * flow is stubbed (`src/test/e2eSessionGuard.test.ts`).
 *
 * Adapted for mod-016 (organization access; D-3, D-7): the fixture user creates
 * every organization a case opens, so it is that organization's administrator
 * and keeps every right these cases use. 21.1's settings list gains
 * «Участники» (US-4); 21.6's user in no group meets the empty list of
 * US-2 — «Вы пока не состоите ни в одной организации» with «Попросите
 * администратора организации добавить вас.» and no «Создать организацию» —
 * and the direct create still answers 403
 * `organizations.organization.create_forbidden` (the instance code is the
 * token's, ADR-0027). 21.3's deactivated list shows the organization to its
 * administrator (BR-M1-35). 21.2, 21.4, 21.5, 21.7 and 21.8 are unchanged.
 */

const created: string[] = [];

const switcher = (page: Page) => page.getByRole('group', { name: 'Контекст работы' });
const settingsNavigation = (page: Page) => page.getByRole('navigation', { name: 'Меню настроек' });
const main = (page: Page) => page.getByRole('main');

/** A signed-in API session for set-up, closed by the caller. */
async function apiSession(browser: Browser) {
  const context = await browser.newContext();
  const page = await context.newPage();
  await signIn(page);
  return { request: page.request, close: () => context.close() };
}

function organizationIdIn(url: string): string {
  const match = /\/organizations\/([^/]+)\//.exec(new URL(url).pathname);
  if (!match?.[1]) throw new Error(`No organization in ${url}`);
  return decodeURIComponent(match[1]);
}

/** Opens the organization list through the switcher, its only way in (D-d). */
async function openOrganizationList(page: Page) {
  const none = switcher(page).getByRole('link', { name: 'Нет организаций' });
  const current = switcher(page).getByRole('button', { name: /^Организация:/ });
  // The shell and «Главная» render before the organizations read answers, so
  // a one-shot `isVisible()` on the link took «no link yet» for «the switcher
  // has an organization» and then waited the whole test for a button that an
  // empty stand never shows. Wait for either answer, then choose.
  await expect(none.or(current)).toBeVisible();
  if (await none.isVisible()) {
    await none.click();
  } else {
    await current.click();
    await page.getByRole('menuitem', { name: 'Все организации' }).click();
  }
  await expect(page).toHaveURL(/\/organizations$/);
}

test.describe('21. organizations, projects, teams and areas (mod-002)', () => {
  test.use({ viewport: { width: 1440, height: 900 } });

  test.afterAll(async ({ browser }) => {
    if (created.length === 0) return;
    const session = await apiSession(browser);
    try {
      for (const id of created) {
        await send(session.request, 'POST', `/api/organizations/${id}/deactivate`);
      }
    } finally {
      await session.close();
    }
  });

  test('21.1 happy path: an organization created from «Все организации» opens its «Проекты», with the switcher on it (US-1, US-8)', async ({
    page,
  }) => {
    await signIn(page, { returnPath: '/home' });
    await expect(page.getByRole('heading', { level: 1, name: 'Главная' })).toBeVisible();
    await openOrganizationList(page);

    const name = runName('Организация E2E');
    await page.getByRole('button', { name: 'Создать организацию' }).first().click();
    const dialog = page.getByRole('dialog', { name: 'Новая организация' });
    await dialog.getByRole('textbox', { name: 'Название' }).fill(name);
    await dialog.getByRole('button', { name: 'Создать' }).click();

    await expect(page).toHaveURL(/\/organizations\/[^/]+\/settings\/projects$/);
    created.push(organizationIdIn(page.url()));
    await expect(page.getByText(`Организация «${name}» создана.`)).toBeVisible();
    await expect(page.getByRole('heading', { level: 2, name: 'Пока нет проектов' })).toBeVisible();
    await expect(switcher(page).getByRole('button', { name: /^Организация:/ })).toHaveAttribute(
      'aria-label',
      `Организация: ${name}`,
    );
    await expect(switcher(page).getByText('Нет проектов')).toBeVisible();
    // mod-016 US-4: «Участники» sits between «Проекты» and «Процесс» for every role.
    await expect(settingsNavigation(page).getByRole('link')).toContainText([
      'Общие',
      'Проекты',
      'Участники',
      'Процесс',
    ]);
  });

  test('21.2 happy path: a project, its «Процесс» link, a team, its default area, the area tree and the team’s deletion (US-3..US-7)', async ({
    page,
  }) => {
    await signIn(page, { returnPath: '/home' });
    const organization = await createOrganization(page.request, runName('Организация E2E'));
    created.push(organization.id);

    // «Новый проект» on the organization's «Проекты».
    await page.goto(`/organizations/${organization.id}/settings/projects`);
    await page.getByRole('button', { name: 'Создать проект' }).first().click();
    const create = page.getByRole('dialog', { name: 'Новый проект' });
    await create.getByRole('textbox', { name: 'Название' }).fill('Пример проекта');
    await create.getByRole('textbox', { name: 'Ключ' }).fill(runKey('P'));
    await create.getByRole('button', { name: 'Создать' }).click();
    await expect(page.getByText('Проект «Пример проекта» создан.')).toBeVisible();

    // Its «Общие» and the read-only «Процесс» link to mod-001's screen.
    await page
      .getByRole('table', { name: 'Активные проекты' })
      .getByRole('link', { name: 'Пример проекта' })
      .click();
    await expect(page.getByRole('heading', { level: 1, name: 'Общие' })).toBeVisible();
    await main(page).getByRole('link', { name: 'Процесс' }).click();
    await expect(page.getByRole('heading', { level: 1, name: 'Процесс' })).toBeVisible();
    await expect(settingsNavigation(page).getByRole('link', { name: 'Процесс' })).toHaveAttribute(
      'aria-current',
      'page',
    );
    await expect(page.getByRole('navigation', { name: 'Путь к странице' })).toContainText(
      'Организация',
    );

    // «Команды»: add «Платформа».
    await settingsNavigation(page).getByRole('link', { name: 'Команды' }).click();
    await page.getByRole('button', { name: 'Добавить команду' }).click();
    const add = page.getByRole('dialog', { name: 'Новая команда' });
    await add.getByRole('textbox', { name: 'Название' }).fill('Платформа');
    await add.getByRole('button', { name: 'Добавить' }).click();
    await expect(page.getByText('Команда «Платформа» добавлена.')).toBeVisible();

    // «Области»: «API» under «Платформа», owned by it; «Черновик» moved, then deleted.
    await settingsNavigation(page).getByRole('link', { name: 'Области' }).click();
    const tree = page.getByRole('treegrid', { name: 'Области' });
    await tree
      .getByRole('row')
      .filter({ hasText: /^Платформа/ })
      .first()
      .click();
    await page.getByRole('button', { name: 'Новая дочерняя область' }).click();
    const createArea = page.getByRole('dialog', { name: 'Новая область' });
    await expect(createArea).toContainText('Родительская область: Платформа');
    await createArea.getByRole('textbox', { name: 'Название' }).fill('API');
    await createArea.getByRole('combobox', { name: 'Владелец' }).click();
    await page.getByRole('option', { name: 'Платформа' }).click();
    await createArea.getByRole('button', { name: 'Создать' }).click();
    await expect(page.getByText('Область «API» создана.')).toBeVisible();

    await page.getByRole('button', { name: 'Новая область' }).first().click();
    const draft = page.getByRole('dialog', { name: 'Новая область' });
    await draft.getByRole('textbox', { name: 'Название' }).fill('Черновик');
    await draft.getByRole('button', { name: 'Создать' }).click();
    await expect(tree.getByRole('row').filter({ hasText: 'Черновик' })).toContainText(
      'унаследовано',
    );

    await tree.getByRole('button', { name: 'Действия: Черновик' }).click();
    await page.getByRole('menuitem', { name: 'Переместить' }).click();
    const move = page.getByRole('dialog', { name: 'Перемещение области' });
    await move.getByRole('combobox', { name: 'Новая родительская область' }).click();
    await page.getByRole('option', { name: 'Платформа / API' }).click();
    await move.getByRole('button', { name: 'Переместить' }).click();
    await expect(page.getByText('Область «Черновик» перемещена.')).toBeVisible();
    await expect(tree.getByRole('row').filter({ hasText: 'Черновик' })).toHaveAttribute(
      'aria-level',
      '4',
    );

    await tree.getByRole('button', { name: 'Действия: Черновик' }).click();
    await page.getByRole('menuitem', { name: 'Удалить' }).click();
    await page
      .getByRole('dialog', { name: 'Удаление области' })
      .getByRole('button', { name: 'Удалить область' })
      .click();
    await expect(page.getByText('Область «Черновик» удалена.')).toBeVisible();

    // «Команды»: «Платформа»'s default area becomes «Платформа / API». The
    // area «Платформа» has its own «Действия: Платформа» in the tree, which
    // stays on screen for a moment after the link is clicked, so the row's
    // menu is taken from the teams table, once it shows.
    await settingsNavigation(page).getByRole('link', { name: 'Команды' }).click();
    const teamsTable = page.getByRole('table', { name: 'Команды' });
    await expect(teamsTable).toBeVisible();
    await teamsTable.getByRole('button', { name: 'Действия: Платформа' }).click();
    await page.getByRole('menuitem', { name: 'Изменить область по умолчанию' }).click();
    const defaultArea = page.getByRole('dialog', { name: 'Область по умолчанию' });
    await defaultArea.getByRole('combobox', { name: 'Область' }).click();
    await page.getByRole('option', { name: 'Платформа / API' }).click();
    await defaultArea.getByRole('button', { name: 'Сохранить' }).click();
    await expect(page.getByText('Область по умолчанию изменена.')).toBeVisible();
    await expect(teamsTable.getByRole('row').filter({ hasText: 'Платформа /' })).toContainText(
      'Платформа / API',
    );

    // «Платформа» deleted with US-7's text; its areas are the first team's.
    await teamsTable.getByRole('button', { name: 'Действия: Платформа' }).click();
    await page.getByRole('menuitem', { name: 'Удалить команду' }).click();
    const remove = page.getByRole('dialog', { name: 'Удаление команды' });
    await expect(remove).toContainText(
      'Удалить команду «Платформа»? Её области перейдут команде «Пример проекта». Восстановить команду будет нельзя.',
    );
    await remove.getByRole('button', { name: 'Удалить команду' }).click();
    await expect(teamsTable.getByRole('link')).toHaveText(['Пример проекта']);

    await settingsNavigation(page).getByRole('link', { name: 'Области' }).click();
    await page.getByRole('button', { name: 'Развернуть «Платформа»' }).click();
    await expect(tree.getByRole('row').filter({ hasText: /^API/ })).toContainText('Пример проекта');
  });

  test('21.3 deactivate and restore: a project, then the organization; its address answers not found meanwhile (US-2, US-3)', async ({
    page,
  }) => {
    await signIn(page, { returnPath: '/home' });
    const fixture = await createContext(page.request, {
      organization: runName('Организация E2E'),
      project: 'Пример проекта',
    });
    created.push(fixture.organization.id);
    const { organization, project } = fixture;

    await page.goto(`/organizations/${organization.id}/projects/${project.id}/settings/general`);
    await page.getByRole('button', { name: 'Деактивировать' }).click();
    await page
      .getByRole('dialog', { name: 'Деактивировать проект?' })
      .getByRole('button', { name: 'Деактивировать' })
      .click();
    await expect(page).toHaveURL(/\/settings\/projects\/deactivated$/);
    await page.getByRole('button', { name: 'Восстановить «Пример проекта»' }).click();
    await expect(page.getByText('Проект «Пример проекта» восстановлен.')).toBeVisible();

    await page.goto(`/organizations/${organization.id}/settings/general`);
    await page.getByRole('button', { name: 'Деактивировать' }).click();
    const confirm = page.getByRole('dialog', { name: 'Деактивировать организацию?' });
    await expect(confirm.getByRole('button', { name: 'Отмена' })).toBeFocused();
    await confirm.getByRole('button', { name: 'Деактивировать' }).click();
    await expect(page).toHaveURL(/\/organizations\/deactivated$/);
    await expect(page.getByRole('table', { name: 'Деактивированные организации' })).toContainText(
      organization.name,
    );

    await page.goto(`/organizations/${organization.id}/settings/general`);
    await expect(
      page.getByRole('heading', { level: 1, name: 'Организация не найдена' }),
    ).toBeVisible();

    await page.goto('/organizations/deactivated');
    await page.getByRole('button', { name: `Восстановить «${organization.name}»` }).click();
    await expect(page.getByText(`Организация «${organization.name}» восстановлена.`)).toBeVisible();
    await page.getByRole('tab', { name: 'Активные' }).click();
    await page.getByRole('link', { name: organization.name }).click();
    await expect(page.getByRole('table', { name: 'Активные проекты' })).toContainText(
      'Пример проекта',
    );
  });

  test('21.4 failure: a taken name on «Название»; the key `prm` refused in the browser with no request (US-1, US-3)', async ({
    page,
  }) => {
    await signIn(page, { returnPath: '/home' });
    const organization = await createOrganization(page.request, runName('Организация E2E'));
    created.push(organization.id);

    await page.goto('/organizations');
    await page.getByRole('button', { name: 'Создать организацию' }).first().click();
    const dialog = page.getByRole('dialog', { name: 'Новая организация' });
    await dialog.getByRole('textbox', { name: 'Название' }).fill(organization.name.toLowerCase());
    await dialog.getByRole('button', { name: 'Создать' }).click();
    await expect(dialog.getByText(`Название «${organization.name}» уже занято`)).toBeVisible();
    await dialog.getByRole('button', { name: 'Отмена' }).click();

    const writes: string[] = [];
    page.on('request', (request) => {
      if (request.method() === 'POST' && request.url().includes('/projects')) {
        writes.push(request.url());
      }
    });
    await page.goto(`/organizations/${organization.id}/settings/projects`);
    await page.getByRole('button', { name: 'Создать проект' }).first().click();
    const project = page.getByRole('dialog', { name: 'Новый проект' });
    await project.getByRole('textbox', { name: 'Название' }).fill('Пример проекта');
    await project.getByRole('textbox', { name: 'Ключ' }).fill('prm');
    await project.getByRole('button', { name: 'Создать' }).click();
    await expect(
      project.getByText(
        'Ключ проекта: от 2 до 10 латинских заглавных букв и цифр, начиная с буквы',
      ),
    ).toBeVisible();
    expect(writes).toEqual([]);
  });

  test('21.5 failure: the switcher’s projects read refused, «Повторить» recovers it (US-8 "The lists fail to load")', async ({
    page,
  }) => {
    await signIn(page, { returnPath: '/home' });
    const fixture = await createContext(page.request, {
      organization: runName('Организация E2E'),
      project: 'Пример проекта',
    });
    created.push(fixture.organization.id);

    const isProjectsRead = (url: URL) =>
      url.origin === new URL(env().apiUrl).origin &&
      /^\/api\/organizations\/[^/]+\/projects$/.test(url.pathname);
    await page.route(isProjectsRead, (route) => route.abort('connectionrefused'));
    await page.goto(`/organizations/${fixture.organization.id}/settings/general`);

    await expect(switcher(page).getByText('Не удалось загрузить проекты')).toBeVisible();
    await page.unroute(isProjectsRead);
    await switcher(page).getByRole('button', { name: 'Повторить' }).click();

    await expect(switcher(page).getByText('Не удалось загрузить проекты')).toHaveCount(0);
    await expect(switcher(page).getByRole('button', { name: /^Проект:/ })).toHaveAttribute(
      'aria-label',
      'Проект: Пример проекта',
    );
  });

  test('21.6 a user in no group: the empty list says they belong to nothing and asks for no «Создать организацию»; a direct create answers 403 «Недостаточно прав» (US-1 "Without the right"; mod-016 US-2 "A user who belongs to no organization sees an explicit empty state")', async ({
    browser,
  }) => {
    const { keycloakUrl, realm } = env();
    const user = await createEphemeralUserWithoutGroup(keycloakUrl, realm);
    const context = await browser.newContext({ viewport: { width: 1440, height: 900 } });
    try {
      const page = await context.newPage();
      await signIn(page, { returnPath: '/organizations', username: user.username });
      await expect(page.getByRole('heading', { level: 1, name: 'Организации' })).toBeVisible();
      // The list holds only the user's own organizations (D-3) — none (design § 5).
      await expect(
        page.getByRole('heading', { level: 2, name: 'Вы пока не состоите ни в одной организации' }),
      ).toBeVisible();
      await expect(
        page.getByText('Попросите администратора организации добавить вас.', { exact: true }),
      ).toBeVisible();
      await expect(page.getByRole('button', { name: 'Создать организацию' })).toHaveCount(0);

      const response = await send(page.request, 'POST', '/api/organizations', {
        name: runName('Без права'),
      });
      expect(response.status()).toBe(403);
      const body = (await response.json()) as { errorCode?: string; detail?: string };
      expect(body.errorCode).toBe('organizations.organization.create_forbidden');
      expect(body.detail).toBe('Недостаточно прав');
    } finally {
      await context.close();
      await deleteEphemeralUser(keycloakUrl, realm, user.id);
      expect(await userExistsById(keycloakUrl, realm, user.id)).toBe(false);
    }
  });

  test('21.7 the selection is remembered across a reload, and falls back with its notice once gone (US-8)', async ({
    page,
  }) => {
    await signIn(page, { returnPath: '/home' });
    const keep = await createOrganization(page.request, runName('Организация E2E'));
    const gone = await createContext(page.request, {
      organization: runName('Организация E2E'),
      project: 'Пример проекта',
    });
    created.push(keep.id, gone.organization.id);

    await page.goto(`/organizations/${gone.organization.id}/settings/general`);
    const level = switcher(page).getByRole('button', { name: /^Организация:/ });
    await expect(level).toHaveAttribute('aria-label', `Организация: ${gone.organization.name}`);
    // The address sync is stored once every level is read (design § 3.4,
    // `useOrganizationsContextSource`): leaving before the team level arrives
    // aborts its read and nothing is remembered. The first team bears the
    // project's name.
    await expect(switcher(page).getByRole('button', { name: /^Команда:/ })).toHaveAttribute(
      'aria-label',
      'Команда: Пример проекта',
    );

    await page.goto('/home');
    await page.reload();
    await expect(level).toHaveAttribute('aria-label', `Организация: ${gone.organization.name}`);

    await deactivateOrganization(page.request, gone.organization.id);
    await page.reload();
    await expect(page.getByText(/^Ранее выбранный контекст недоступен\. Выбрано: /)).toBeVisible();
    await expect(level).not.toHaveAttribute('aria-label', `Организация: ${gone.organization.name}`);
  });

  test('21.8 the tree registry by keyboard in a browser, with a visible focus ring (US-5, design § 11.6)', async ({
    page,
  }) => {
    await signIn(page, { returnPath: '/home' });
    const fixture = await createContext(page.request, {
      organization: runName('Организация E2E'),
      project: 'Пример проекта',
      teams: ['Платформа'],
    });
    created.push(fixture.organization.id);

    await page.goto(
      `/organizations/${fixture.organization.id}/projects/${fixture.project.id}/settings/areas`,
    );
    const tree = page.getByRole('treegrid', { name: 'Области' });
    const rows = tree.locator('[role="row"][aria-level]');
    await expect(rows).toHaveCount(2);

    await rows.first().focus();
    const ring = await rows.first().evaluate((element) => {
      const style = getComputedStyle(element);
      return { style: style.outlineStyle, width: parseFloat(style.outlineWidth) };
    });
    expect(ring.style).not.toBe('none');
    expect(ring.width).toBeGreaterThanOrEqual(2);

    await page.keyboard.press('ArrowDown');
    await expect(rows.nth(1)).toBeFocused();
    await page.keyboard.press('ArrowLeft');
    await expect(rows.first()).toBeFocused();
    await page.keyboard.press('ArrowLeft');
    await expect(rows.first()).toHaveAttribute('aria-expanded', 'false');
    await page.keyboard.press('ArrowRight');
    await expect(rows.first()).toHaveAttribute('aria-expanded', 'true');
    await page.keyboard.press('ArrowDown');
    await page.keyboard.press('Enter');
    await expect(rows.nth(1)).toHaveAttribute('aria-selected', 'true');

    await page.keyboard.press('Shift+F10');
    await expect(page.getByRole('menu')).toBeVisible();
    await page.keyboard.press('Escape');
    await expect(page.getByRole('menu')).toHaveCount(0);
    await expect(tree.getByRole('button', { name: 'Действия: Платформа' })).toBeFocused();
  });
});
