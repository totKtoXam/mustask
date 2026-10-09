import { expect, test, type Browser, type Locator, type Page } from '@playwright/test';
import { signIn } from '../support/auth';
import { createContext, runName, send } from '../support/organizationsApi';
import { createWorkItem, setParent, typeId, type WorkItemDto } from '../support/workItemsApi';

/**
 * 24. The work item hierarchy (feat-001 US-1..US-6; its spec § E2E) against
 * the real backend.
 *
 * Every case creates what it needs the way specs 22 and 23 do — an
 * organization with one project through mod-002's API, its items through
 * mod-003's create and, where a case starts from a tree, their links through
 * feat-001's parent write — and `afterAll` deactivates each organization it
 * created, which hides everything beneath it: nothing is ever purged
 * (BR-M1-1) and work items have no deletion before mod-005, so the stand is
 * left as it was found. Items created without a team land in the project's
 * root, which the first team owns, so they are that team's backlog rows.
 *
 * The cases go through the menu and keyboard paths — the row's «Ещё
 * действия», the level drop-down — which are the complete ones (BR-H-14,
 * BR-H-15). US-7 has no case here: no item can be deleted through the panel
 * before mod-005 (spec § E2E).
 *
 * Nothing is injected and nothing on the session flow is stubbed
 * (`src/test/e2eSessionGuard.test.ts`). The dialog's initial focus is not
 * asserted (tech-009 D-3: `FormDialog` under StrictMode).
 */

const created: string[] = [];

const main = (page: Page) => page.getByRole('main');
const mainNavigation = (page: Page) => page.getByRole('navigation', { name: 'Основное меню' });
const backlogTable = (page: Page) => page.getByRole('table', { name: 'Бэклог' });
const levelButton = (page: Page) => page.getByRole('button', { name: /^Уровень: / });

/** A signed-in API session for clean-up, closed by the caller. */
async function apiSession(browser: Browser) {
  const context = await browser.newContext();
  const page = await context.newPage();
  await signIn(page);
  return { request: page.request, close: () => context.close() };
}

/** An organization with «Пример проекта», through the API; its first team's prefix. */
async function fixture(page: Page) {
  const context = await createContext(page.request, {
    organization: runName('Организация E2E'),
    project: 'Пример проекта',
  });
  created.push(context.organization.id);
  const firstTeam = context.teams.find((team) => team.isFirstTeam);
  if (!firstTeam) throw new Error('The project has no first team.');
  const prefix = `/organizations/${context.organization.id}/projects/${context.project.id}`;
  const teamPrefix = `${prefix}/teams/${firstTeam.id}`;
  const types = new Map<string, string>();

  /** Creates an item of the type `key` (`EPIC`, `FEAT`, `US`, `BUG`, `TASK`) through the API. */
  async function item(key: string, title: string, parent?: WorkItemDto): Promise<WorkItemDto> {
    let type = types.get(key);
    if (!type) {
      type = await typeId(page.request, context.organization.id, key);
      types.set(key, type);
    }
    const made = await createWorkItem(page.request, context.organization.id, context.project.id, {
      typeId: type,
      title,
    });
    if (parent) {
      await setParent(
        page.request,
        context.organization.id,
        context.project.id,
        made.identifier,
        parent.id,
      );
    }
    return made;
  }

  return { ...context, prefix, teamPrefix, item };
}

/** A backlog row's cells: «Порядок» 0, the handle 1, ID 2, … «Родитель» 5. */
const ID_CELL = 2;
const PARENT_CELL = 5;

/** «<ID>» for a top-level row, «  <ID>» per tier for a nested one, top to bottom. */
async function outline(page: Page): Promise<string[]> {
  const rows = backlogTable(page).locator('tbody tr');
  const count = await rows.count();
  const result: string[] = [];
  for (let index = 0; index < count; index += 1) {
    const row = rows.nth(index);
    const tier = Number((await row.getAttribute('data-tier')) ?? '0');
    const identifier = (await row.getByRole('cell').nth(ID_CELL).innerText()).trim();
    result.push(`${'  '.repeat(tier)}${identifier}`);
  }
  return result;
}

/** A backlog row by its identifier, in its own ID cell (a «Родитель» cell may read it too). */
function row(page: Page, identifier: string): Locator {
  return backlogTable(page)
    .locator('tbody tr')
    .filter({
      has: page.locator(`td:nth-child(${ID_CELL + 1})`, { hasText: new RegExp(`^${identifier}$`) }),
    });
}

/** A row's «Родитель» cell. */
function parentCell(page: Page, identifier: string): Locator {
  return row(page, identifier).getByRole('cell').nth(PARENT_CELL);
}

/** The form's «Родитель»: types `text` and picks the line starting with `identifier`. */
async function pickParent(scope: Page | Locator, page: Page, text: string, identifier: string) {
  const field = scope.getByRole('combobox', { name: 'Родитель' });
  await field.click();
  await field.fill(text);
  await page.getByRole('option', { name: new RegExp(`^${identifier} `) }).click();
}

test.describe('24. the work item hierarchy (feat-001)', () => {
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

  test('24.1 a story given a feature on its form, replaced by another feature, cleared; the feature’s «Дочерние элементы» follows (US-1, US-3)', async ({
    page,
  }) => {
    await signIn(page, { returnPath: '/home' });
    const { prefix, item } = await fixture(page);
    const byLink = await item('FEAT', 'Вход по ссылке');
    const sso = await item('FEAT', 'Вход через SSO');
    const story = await item('US', 'Письмо со ссылкой');
    const form = (identifier: string) => `${prefix}/workitems/${identifier}`;
    const related = page.getByRole('region', { name: 'Связанная работа' });
    const field = related.getByRole('combobox', { name: 'Родитель' });
    const save = page.getByRole('button', { name: 'Сохранить' });

    // US-1: «Нет родителя» at first, the helper naming the allowed types.
    await page.goto(form(story.identifier));
    await expect(
      page.getByRole('heading', { level: 1, name: `Пользовательская история ${story.identifier}` }),
    ).toBeVisible();
    await expect(field).toHaveValue('Нет родителя');
    await expect(field).toHaveAccessibleDescription(
      'Родителем может быть: Эпик, Фича, Пользовательская история, Баг.',
    );

    // Set: the first feature, by part of its title.
    await pickParent(related, page, 'ссылк', byLink.identifier);
    await expect(field).toHaveValue(`${byLink.identifier} Вход по ссылке`);
    await save.click();
    await expect(page.getByText('Изменения сохранены.')).toBeVisible();
    await expect(related.getByRole('link', { name: `Открыть ${byLink.identifier}` })).toBeVisible();
    await page.reload();
    await expect(field).toHaveValue(`${byLink.identifier} Вход по ссылке`);

    // US-3: the feature lists the story.
    await page.goto(form(byLink.identifier));
    await expect(related.getByRole('list', { name: 'Дочерние элементы (1)' })).toContainText(
      story.identifier,
    );

    // Replace: the other feature, in one save.
    await page.goto(form(story.identifier));
    await expect(field).toHaveValue(`${byLink.identifier} Вход по ссылке`);
    await pickParent(related, page, sso.identifier, sso.identifier);
    await save.click();
    await expect(page.getByText('Изменения сохранены.')).toBeVisible();
    await expect(field).toHaveValue(`${sso.identifier} Вход через SSO`);
    await page.goto(form(byLink.identifier));
    await expect(related.getByText('Дочерних элементов нет.')).toBeVisible();
    await page.goto(form(sso.identifier));
    await expect(related.getByRole('list', { name: 'Дочерние элементы (1)' })).toContainText(
      story.identifier,
    );

    // Clear: «Нет родителя».
    await page.goto(form(story.identifier));
    await field.click();
    await page.getByRole('option', { name: 'Нет родителя' }).click();
    await save.click();
    await expect(page.getByText('Изменения сохранены.')).toBeVisible();
    await expect(field).toHaveValue('Нет родителя');
    await expect(related.getByRole('link', { name: /^Открыть / })).toHaveCount(0);
    await page.goto(form(sso.identifier));
    await expect(related.getByText('Дочерних элементов нет.')).toBeVisible();
  });

  test('24.2 failure: a «Задача» chain refuses the cycle with its path; a «Фича» is not offered as an epic’s parent (US-2)', async ({
    page,
  }) => {
    await signIn(page, { returnPath: '/home' });
    const { prefix, item } = await fixture(page);
    const first = await item('TASK', 'Подготовить окружение');
    const second = await item('TASK', 'Настроить почтовый сервис', first);
    const third = await item('TASK', 'Проверить доставку', second);
    const epic = await item('EPIC', 'Вход и регистрация');
    const feature = await item('FEAT', 'Вход по ссылке');
    const related = page.getByRole('region', { name: 'Связанная работа' });
    const field = related.getByRole('combobox', { name: 'Родитель' });

    // The cycle: the first task given the third, which is nested in it.
    await page.goto(`${prefix}/workitems/${first.identifier}`);
    await expect(field).toHaveValue('Нет родителя');
    await pickParent(related, page, third.identifier, third.identifier);
    await page.getByRole('button', { name: 'Сохранить' }).click();
    await expect(field).toHaveAccessibleDescription(
      `Нельзя выбрать родителем ${third.identifier}: он вложен в ${first.identifier} (${first.identifier} → ${second.identifier} → ${third.identifier}).`,
    );
    await expect(field).toHaveValue(`${third.identifier} Проверить доставку`);
    await page.reload();
    await expect(field).toHaveValue('Нет родителя');

    // An epic's parent can only be an epic: the feature is not offered.
    await page.goto(`${prefix}/workitems/${epic.identifier}`);
    await expect(field).toHaveAccessibleDescription('Родителем может быть: Эпик.');
    await field.click();
    await field.fill(feature.identifier);
    await expect(page.getByRole('option', { name: 'Ничего не найдено' })).toBeVisible();
    await expect(
      page.getByRole('option', { name: new RegExp(`^${feature.identifier} `) }),
    ).toHaveCount(0);
  });

  test('24.3 «Эпики»: an epic expanded to its features and a feature to its stories, collapsed; a reorder of epics keeps the children (US-4)', async ({
    page,
  }) => {
    await signIn(page, { returnPath: '/home' });
    const { teamPrefix, item } = await fixture(page);
    const signIn1 = await item('EPIC', 'Вход и регистрация');
    const reports = await item('EPIC', 'Отчётность');
    const sso = await item('FEAT', 'Вход через SSO', signIn1);
    const byLink = await item('FEAT', 'Вход по ссылке', signIn1);
    const letter = await item('US', 'Письмо со ссылкой', byLink);
    const expiry = await item('BUG', 'Ссылка истекает раньше срока', byLink);

    // Rows start collapsed; only the epic with children shows a control.
    await page.goto(`${teamPrefix}/backlogs/epics`);
    await expect(levelButton(page)).toHaveAccessibleName('Уровень: Эпики');
    await expect.poll(() => outline(page)).toEqual([signIn1.identifier, reports.identifier]);
    const expandEpic = page.getByRole('button', { name: `Развернуть ${signIn1.identifier}` });
    await expect(expandEpic).toHaveAttribute('aria-expanded', 'false');
    await expect(
      page.getByRole('button', { name: `Развернуть ${reports.identifier}` }),
    ).toHaveCount(0);

    // Expanded by keyboard: its features, one tier in, «Родитель» the epic, focus kept.
    await expandEpic.focus();
    await page.keyboard.press('Enter');
    const collapseEpic = page.getByRole('button', { name: `Свернуть ${signIn1.identifier}` });
    await expect(collapseEpic).toBeFocused();
    await expect(collapseEpic).toHaveAttribute('aria-expanded', 'true');
    await expect
      .poll(() => outline(page))
      .toEqual([
        signIn1.identifier,
        `  ${sso.identifier}`,
        `  ${byLink.identifier}`,
        reports.identifier,
      ]);
    await expect(parentCell(page, sso.identifier)).toHaveText(signIn1.identifier);
    await expect(row(page, sso.identifier).getByRole('cell').first()).toHaveText('');
    await expect(page.getByRole('button', { name: `Переместить ${sso.identifier}` })).toHaveCount(
      0,
    );

    // A feature one tier further: its story and bug.
    await page.getByRole('button', { name: `Развернуть ${byLink.identifier}` }).click();
    await expect
      .poll(() => outline(page))
      .toEqual([
        signIn1.identifier,
        `  ${sso.identifier}`,
        `  ${byLink.identifier}`,
        `    ${letter.identifier}`,
        `    ${expiry.identifier}`,
        reports.identifier,
      ]);

    // Collapsed: only the epics again.
    await collapseEpic.click();
    await expect.poll(() => outline(page)).toEqual([signIn1.identifier, reports.identifier]);

    // A reorder moves the epic with its shown children, and keeps them shown.
    await page.getByRole('button', { name: `Развернуть ${signIn1.identifier}` }).click();
    await page.getByRole('button', { name: `Переместить ${reports.identifier}` }).click();
    await page
      .getByRole('menu', { name: `Переместить ${reports.identifier}` })
      .getByRole('menuitem', { name: 'Выше' })
      .click();
    await expect(page.getByRole('status').filter({ hasText: 'перемещён' })).toHaveText(
      `${reports.identifier} перемещён: позиция 1 из 2.`,
    );
    const reordered = [
      reports.identifier,
      signIn1.identifier,
      `  ${sso.identifier}`,
      `  ${byLink.identifier}`,
      `    ${letter.identifier}`,
      `    ${expiry.identifier}`,
    ];
    await expect.poll(() => outline(page)).toEqual(reordered);
    // Read again after the reorder: still expanded; a reload starts collapsed.
    await expect(
      page.getByRole('button', { name: `Свернуть ${signIn1.identifier}` }),
    ).toBeVisible();
    await page.reload();
    await expect.poll(() => outline(page)).toEqual([reports.identifier, signIn1.identifier]);
  });

  test('24.4 «Истории»: «Изменить родителя…» to a feature, the epic’s tree shows it; «Убрать родителя» (US-5)', async ({
    page,
  }) => {
    await signIn(page, { returnPath: '/home' });
    const { teamPrefix, item } = await fixture(page);
    const epic = await item('EPIC', 'Вход и регистрация');
    const feature = await item('FEAT', 'Вход через SSO', epic);
    const story = await item('US', 'Запомнить устройство');

    await page.goto(`${teamPrefix}/backlogs/stories`);
    await expect.poll(() => outline(page)).toEqual([story.identifier]);
    await expect(parentCell(page, story.identifier)).toHaveText('Нет');

    // The menu by keyboard: Enter opens on «Изменить родителя…», Enter chooses.
    const actions = page.getByRole('button', { name: `Ещё действия: ${story.identifier}` });
    await actions.focus();
    await page.keyboard.press('Enter');
    const menu = page.getByRole('menu', { name: `Ещё действия: ${story.identifier}` });
    await expect(menu.getByRole('menuitem', { name: 'Изменить родителя…' })).toBeFocused();
    await expect(menu.getByRole('menuitem', { name: 'Убрать родителя' })).toBeDisabled();
    await page.keyboard.press('Enter');

    const dialog = page.getByRole('dialog', { name: 'Изменить родителя' });
    await expect(dialog).toContainText(
      `Пользовательская история ${story.identifier} «Запомнить устройство»`,
    );
    const field = dialog.getByRole('combobox', { name: 'Родитель' });
    await expect(field).toHaveAccessibleDescription(
      'Предлагаются элементы уровня «Фичи». Родителя другого уровня можно выбрать в форме рабочего элемента.',
    );
    await expect(dialog.getByRole('button', { name: 'Сохранить' })).toBeDisabled();
    // The field takes focus as the dialog opens; under StrictMode the dev
    // server's first focus is not to be relied on (tech-009 D-3), so it is
    // clicked first.
    await field.click();
    await page.keyboard.type('sso');
    const option = page.getByRole('option', { name: new RegExp(`^${feature.identifier} `) });
    await expect(option).toBeVisible();
    // Only the level above is offered: the epic is not.
    await expect(
      page.getByRole('option', { name: new RegExp(`^${epic.identifier} `) }),
    ).toHaveCount(0);
    await page.keyboard.press('ArrowDown');
    await page.keyboard.press('Enter');
    await expect(field).toHaveValue(`${feature.identifier} Вход через SSO`);
    await dialog.getByRole('button', { name: 'Сохранить' }).click();

    await expect(dialog).toHaveCount(0);
    await expect(page.getByRole('alert')).toContainText(
      `У ${story.identifier} теперь родитель ${feature.identifier}.`,
    );
    await expect(parentCell(page, story.identifier)).toHaveText(feature.identifier);
    await expect(actions).toBeFocused();

    // The epic's tree shows the story beneath the feature.
    await levelButton(page).click();
    await page.getByRole('menuitem', { name: 'Эпики' }).click();
    await expect(page).toHaveURL(new RegExp(`${teamPrefix}/backlogs/epics$`));
    await page.getByRole('button', { name: `Развернуть ${epic.identifier}` }).click();
    await page.getByRole('button', { name: `Развернуть ${feature.identifier}` }).click();
    await expect
      .poll(() => outline(page))
      .toEqual([epic.identifier, `  ${feature.identifier}`, `    ${story.identifier}`]);

    // «Убрать родителя»: «Нет» at once, the notice, and the tree lets go.
    await page.goBack();
    await expect(page).toHaveURL(new RegExp(`${teamPrefix}/backlogs/stories$`));
    await page.getByRole('button', { name: `Ещё действия: ${story.identifier}` }).click();
    await page.getByRole('menuitem', { name: 'Убрать родителя' }).click();
    await expect(parentCell(page, story.identifier)).toHaveText('Нет');
    await expect(page.getByRole('alert')).toContainText(
      `У ${story.identifier} больше нет родителя.`,
    );
    await page.reload();
    await expect(parentCell(page, story.identifier)).toHaveText('Нет');
  });

  test('24.5 the level drop-down on «Бэклоги» and «Канбан» by keyboard, the address and back; the entry, h1 and «Показать канбан» read «Канбан» (US-6)', async ({
    page,
  }) => {
    await signIn(page, { returnPath: '/home' });
    const { teamPrefix, item } = await fixture(page);
    const epic = await item('EPIC', 'Вход и регистрация');
    const feature = await item('FEAT', 'Вход через SSO');
    const story = await item('US', 'Запомнить устройство');

    // «Бэклоги»: no tabs, the drop-down reads the level.
    await page.goto(`${teamPrefix}/backlogs/stories`);
    await expect(page.getByRole('heading', { level: 1, name: 'Бэклоги' })).toBeVisible();
    await expect.poll(() => outline(page)).toEqual([story.identifier]);
    await expect(page.getByRole('tab')).toHaveCount(0);
    const button = levelButton(page);
    await expect(button).toHaveAccessibleName('Уровень: Истории');
    await expect(button).toHaveAttribute('aria-haspopup', 'menu');

    // ArrowDown opens on the current level; ArrowUp and Enter choose «Фичи».
    await button.focus();
    await page.keyboard.press('ArrowDown');
    const menu = page.getByRole('menu', { name: 'Уровень: Истории' });
    await expect(menu.getByRole('menuitem')).toHaveText(['Эпики', 'Фичи', 'Истории']);
    await expect(menu.getByRole('menuitem', { name: 'Истории' })).toBeFocused();
    await expect(menu.getByRole('menuitem', { name: 'Истории' })).toHaveAttribute(
      'aria-current',
      'page',
    );
    await page.keyboard.press('ArrowUp');
    await page.keyboard.press('Enter');
    await expect(page).toHaveURL(new RegExp(`${teamPrefix}/backlogs/features$`));
    await expect.poll(() => outline(page)).toEqual([feature.identifier]);
    await expect(levelButton(page)).toHaveAccessibleName('Уровень: Фичи');
    await expect(levelButton(page)).toBeFocused();

    // Escape closes with focus back on the button; back returns to «Истории».
    await page.keyboard.press('Enter');
    await expect(page.getByRole('menu')).toBeVisible();
    await page.keyboard.press('Escape');
    await expect(page.getByRole('menu')).toHaveCount(0);
    await expect(levelButton(page)).toBeFocused();
    await page.goBack();
    await expect(page).toHaveURL(new RegExp(`${teamPrefix}/backlogs/stories$`));
    await expect(levelButton(page)).toHaveAccessibleName('Уровень: Истории');

    // The rename: «Показать канбан», then «Канбан» in the h1, the tab, the region, the entry.
    await page.getByRole('link', { name: 'Показать канбан' }).click();
    await expect(page).toHaveURL(new RegExp(`${teamPrefix}/boards/stories$`));
    await expect(page.getByRole('heading', { level: 1, name: 'Канбан' })).toBeVisible();
    await expect(page).toHaveTitle('Канбан — Mustask');
    await expect(page.getByRole('region', { name: 'Канбан' })).toContainText(story.identifier);
    const entry = mainNavigation(page).getByRole('link', { name: 'Канбан' });
    await expect(entry).toHaveAttribute('aria-current', 'page');
    await expect(main(page)).not.toContainText('Доски');
    await expect(mainNavigation(page)).not.toContainText('Доски');

    // The same drop-down on «Канбан»: Home and Enter choose «Эпики».
    await expect(levelButton(page)).toHaveAccessibleName('Уровень: Истории');
    await levelButton(page).focus();
    await page.keyboard.press('Enter');
    await expect(page.getByRole('menuitem', { name: 'Истории' })).toBeFocused();
    await page.keyboard.press('Home');
    await expect(page.getByRole('menuitem', { name: 'Эпики' })).toBeFocused();
    await page.keyboard.press('Enter');
    await expect(page).toHaveURL(new RegExp(`${teamPrefix}/boards/epics$`));
    await expect(page.getByRole('region', { name: 'Канбан' })).toContainText(epic.identifier);
    await expect(levelButton(page)).toHaveAccessibleName('Уровень: Эпики');
    await page.goBack();
    await expect(page).toHaveURL(new RegExp(`${teamPrefix}/boards/stories$`));

    // The entry opens the «Истории» board of the team; the address keeps `boards`.
    await page.goto(`${teamPrefix}/backlogs/epics`);
    await mainNavigation(page).getByRole('link', { name: 'Канбан' }).click();
    await expect(page).toHaveURL(new RegExp(`${teamPrefix}/boards/stories$`));
  });
});
