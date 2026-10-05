import { expect, test, type Browser, type Page } from '@playwright/test';
import { signIn } from '../support/auth';
import { createContext, runName, send } from '../support/organizationsApi';
import { createArea, createWorkItem, moveWorkItem, typeId } from '../support/workItemsApi';

/**
 * 22. Work items (mod-003 US-1..US-6) against the real backend.
 *
 * Every case creates what it needs the way spec 21 does — an organization with
 * one project through mod-002's API, an area under its root, and in 22.3 an
 * item through mod-003's — and `afterAll` deactivates each organization it
 * created, which hides everything beneath it: nothing is ever purged
 * (BR-M1-1) and work items have no deletion before mod-005, so the stand is
 * left as it was found. Names carry a run suffix, as spec 21's do.
 *
 * 22.1 picks the fixture user itself as the assignee: it is the one user the
 * stand's directory is sure to hold (mod-014 projects the realm's users, and
 * AppHost provisions this one as «E2E Fixture», `e2e-fixture@stand.local`).
 *
 * Nothing is injected and nothing on the session flow is stubbed
 * (`src/test/e2eSessionGuard.test.ts`). The create dialog's initial focus is
 * not asserted (tech-009: `FormDialog` under StrictMode).
 */

const created: string[] = [];

const main = (page: Page) => page.getByRole('main');
const mainNavigation = (page: Page) => page.getByRole('navigation', { name: 'Основное меню' });

/** A signed-in API session for clean-up, closed by the caller. */
async function apiSession(browser: Browser) {
  const context = await browser.newContext();
  const page = await context.newPage();
  await signIn(page);
  return { request: page.request, close: () => context.close() };
}

/** An organization with «Пример проекта» and its area «Отчёты», through the API. */
async function fixture(page: Page) {
  const context = await createContext(page.request, {
    organization: runName('Организация E2E'),
    project: 'Пример проекта',
  });
  created.push(context.organization.id);
  const reports = await createArea(
    page.request,
    context.organization.id,
    context.project.id,
    'Отчёты',
  );
  const list = `/organizations/${context.organization.id}/projects/${context.project.id}/workitems`;
  return { ...context, reports, list };
}

async function openDialog(page: Page, type: string) {
  await page.getByRole('button', { name: 'Новый рабочий элемент' }).first().click();
  await page.getByRole('menuitem', { name: type }).click();
  return page.getByRole('dialog', { name: 'Новый рабочий элемент' });
}

test.describe('22. work items (mod-003)', () => {
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

  test('22.1 happy path: created from the list, on top with its identifier, every field changed on its form and kept after a reload (US-1..US-5)', async ({
    page,
  }) => {
    await signIn(page, { returnPath: '/home' });
    const { project, list } = await fixture(page);

    // US-2: the empty list, its entry current in the main navigation.
    await page.goto(list);
    await expect(page.getByRole('heading', { level: 1, name: 'Рабочие элементы' })).toBeVisible();
    await expect(
      main(page).getByRole('heading', { level: 2, name: 'Рабочих элементов пока нет' }),
    ).toBeVisible();
    await expect(
      mainNavigation(page).getByRole('link', { name: 'Рабочие элементы' }),
    ).toHaveAttribute('aria-current', 'page');

    // US-1: «Баг» with only a title; the dialog names the state and the root area.
    const dialog = await openDialog(page, 'Баг');
    await expect(dialog).toContainText(
      'Рабочий элемент будет создан в статусе «Новая» в области «Пример проекта». Остальные поля заполняются в его форме.',
    );
    await dialog.getByRole('textbox', { name: 'Заголовок' }).fill('  Падение при входе  ');
    await dialog.getByRole('button', { name: 'Создать' }).click();

    const identifier = `${project.key}-BUG-1`;
    await expect(page.getByText(`Рабочий элемент ${identifier} создан.`)).toBeVisible();
    await expect(dialog).toHaveCount(0);
    await expect(page).toHaveURL(new RegExp(`${list}$`));
    const table = page.getByRole('table', { name: 'Рабочие элементы' });
    const first = table.getByRole('row').nth(1);
    await expect(first).toContainText(identifier);
    await expect(first).toContainText('Падение при входе');
    await expect(first).toContainText('Не назначен');
    await expect(first).toContainText('Новая');
    await expect(first).toContainText('Пример проекта');

    // US-3: its form at its address, the project's entry still marked.
    await first.getByRole('link', { name: 'Падение при входе' }).click();
    await expect(page).toHaveURL(new RegExp(`${list}/${identifier}$`));
    await expect(page.getByRole('heading', { level: 1, name: `Баг ${identifier}` })).toBeVisible();
    await expect(page).toHaveTitle(`Баг ${identifier} — Mustask`);
    await expect(
      mainNavigation(page).getByRole('link', { name: 'Рабочие элементы' }),
    ).toHaveAttribute('aria-current', 'true');
    const save = page.getByRole('button', { name: 'Сохранить' });
    await expect(save).toBeDisabled();

    // US-3..US-5: title, description, state, assignee and area in one save.
    const title = page.getByRole('textbox', { name: 'Заголовок' });
    const description = page.getByRole('textbox', { name: 'Описание' });
    const status = page.getByRole('combobox', { name: 'Статус' });
    const assignee = page.getByRole('combobox', { name: 'Исполнитель' });
    const area = page.getByRole('combobox', { name: 'Область' });
    await title.fill('Падение при входе через SSO');
    await description.fill('<b>важно</b>\nшаг 2');
    await status.click();
    await page.getByRole('option', { name: 'Разрешена' }).click();
    await assignee.click();
    await assignee.fill('e2e-fixture@');
    await page.getByRole('option', { name: /E2E Fixture/ }).click();
    await area.click();
    await page.getByRole('option', { name: 'Отчёты' }).click();
    await expect(save).toBeEnabled();
    await save.click();

    await expect(page.getByText('Изменения сохранены.')).toBeVisible();
    await expect(save).toBeDisabled();

    await page.reload();
    await expect(title).toHaveValue('Падение при входе через SSO');
    await expect(description).toHaveValue('<b>важно</b>\nшаг 2');
    await expect(status).toContainText('Разрешена');
    await expect(assignee).toHaveValue('E2E Fixture');
    await expect(area).toHaveValue('Отчёты');
    await expect(page.getByRole('heading', { level: 1, name: `Баг ${identifier}` })).toBeVisible();

    // The trail leads back; the row carries the stored values.
    await page
      .getByRole('navigation', { name: 'Путь к странице' })
      .getByRole('link', { name: 'Рабочие элементы' })
      .click();
    await expect(first).toContainText(identifier);
    await expect(first).toContainText('Падение при входе через SSO');
    await expect(first).toContainText('E2E Fixture');
    await expect(first).toContainText('Разрешена');
    await expect(first).toContainText('Отчёты');
  });

  test('22.2 failure: an unknown identifier answers «Рабочий элемент не найден»; an empty title is refused in the dialog with no request (US-3, US-1)', async ({
    page,
  }) => {
    await signIn(page, { returnPath: '/home' });
    const { project, list } = await fixture(page);

    await page.goto(`${list}/${project.key}-BUG-999`);
    await expect(
      page.getByRole('heading', { level: 1, name: 'Рабочий элемент не найден' }),
    ).toBeVisible();
    await expect(page.getByText('Проверьте адрес или вернитесь на главную.')).toBeVisible();
    await expect(page.getByRole('link', { name: 'На главную' })).toBeVisible();

    const writes: string[] = [];
    page.on('request', (request) => {
      if (request.method() === 'POST' && request.url().includes('/workitems')) {
        writes.push(request.url());
      }
    });
    await page.goto(list);
    const dialog = await openDialog(page, 'Задача');
    await dialog.getByRole('button', { name: 'Создать' }).click();

    await expect(dialog.getByText('Введите заголовок.')).toBeVisible();
    await expect(dialog.getByRole('textbox', { name: 'Заголовок' })).toHaveAttribute(
      'aria-invalid',
      'true',
    );
    await expect(dialog).toBeVisible();
    expect(writes).toEqual([]);
  });

  test('22.3 an area that holds a work item is not deleted: mod-002’s dialog refuses with its text and stays (US-6)', async ({
    page,
  }) => {
    await signIn(page, { returnPath: '/home' });
    const { organization, project, reports } = await fixture(page);
    const item = await createWorkItem(page.request, organization.id, project.id, {
      typeId: await typeId(page.request, organization.id, 'TASK'),
      title: 'Отчёт за квартал',
    });
    await moveWorkItem(page.request, organization.id, project.id, item.identifier, reports.id);

    await page.goto(`/organizations/${organization.id}/projects/${project.id}/settings/areas`);
    const tree = page.getByRole('treegrid', { name: 'Области' });
    await tree.getByRole('button', { name: 'Действия: Отчёты' }).click();
    await page.getByRole('menuitem', { name: 'Удалить' }).click();
    const dialog = page.getByRole('dialog', { name: 'Удаление области' });
    await dialog.getByRole('button', { name: 'Удалить область' }).click();

    await expect(dialog.getByRole('alert')).toContainText(
      'Нельзя удалить область, в которой есть рабочие элементы (включая удалённые) или вложенные области',
    );
    await expect(dialog).toBeVisible();
    await dialog.getByRole('button', { name: 'Отмена' }).click();
    await expect(tree.getByRole('row').filter({ hasText: 'Отчёты' })).toHaveCount(1);
  });
});
