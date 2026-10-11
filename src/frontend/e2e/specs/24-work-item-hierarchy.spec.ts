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
 * Brought in line with feat-027 (its spec § E2E; D-7): the backlog's tree opens
 * expanded as deep as the data go (D-6) and the rows are found by their
 * `data-identifier` and `data-tier` — there is no ID cell and no «Родитель»
 * column — the reorder and the parent change are the row's «Ещё действия»,
 * whose dialog offers every type BR-H-2 allows, and the page's `h1` is the
 * team's name with the one tab «Бэклог» / «Канбан» under it. The drop onto a
 * row, the inline add and the head's look are spec 26's.
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

  return { ...context, firstTeam, prefix, teamPrefix, item };
}

/** The cell of a backlog row that holds «Порядок» (feat-027 design § 4.3): the hierarchy column comes first. */
const ORDER_CELL = 1;

/**
 * «<ID>» for a top-level row, «  <ID>» per tier for a nested one, top to bottom:
 * a row is found by its `data-identifier`, its nesting is its `data-tier`
 * (feat-027 design § 7 — there is no ID cell and no «Родитель» column).
 */
async function outline(page: Page): Promise<string[]> {
  const rows = backlogTable(page).locator('tbody tr[data-identifier]');
  const count = await rows.count();
  const result: string[] = [];
  for (let index = 0; index < count; index += 1) {
    const row = rows.nth(index);
    const tier = Number((await row.getAttribute('data-tier')) ?? '0');
    const identifier = (await row.getAttribute('data-identifier')) ?? '';
    result.push(`${'  '.repeat(tier)}${identifier}`);
  }
  return result;
}

/** A backlog row by its identifier. */
function row(page: Page, identifier: string): Locator {
  return backlogTable(page).locator(`tbody tr[data-identifier="${identifier}"]`);
}

/** A row's «Порядок» cell: its number on a top-level row, blank on a nested one. */
function orderCell(page: Page, identifier: string): Locator {
  return row(page, identifier).getByRole('cell').nth(ORDER_CELL);
}

/** A row's «Ещё действия: <ID>» (feat-027 BR-T-14: on every row). */
function actionsButton(page: Page, identifier: string): Locator {
  return page.getByRole('button', { name: `Ещё действия: ${identifier}`, exact: true });
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

  test('24.3 «Эпики»: the tree opens expanded as far as the data go, an epic collapsed and expanded by keyboard, a nested row has no «Порядок» and no moves; a reorder of epics keeps the children (US-4)', async ({
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
    const expanded = [
      signIn1.identifier,
      `  ${sso.identifier}`,
      `  ${byLink.identifier}`,
      `    ${letter.identifier}`,
      `    ${expiry.identifier}`,
      reports.identifier,
    ];

    // The first state is expanded as deep as the data go (feat-027 D-6 OQ-2 (d));
    // only a row with children shows a control.
    await page.goto(`${teamPrefix}/backlogs/epics`);
    await expect(levelButton(page)).toHaveAccessibleName('Уровень: Эпики');
    await expect.poll(() => outline(page)).toEqual(expanded);
    const collapseEpic = page.getByRole('button', { name: `Свернуть ${signIn1.identifier}` });
    await expect(collapseEpic).toHaveAttribute('aria-expanded', 'true');
    await expect(
      page.getByRole('button', {
        name: new RegExp(`^(Развернуть|Свернуть) ${reports.identifier}$`),
      }),
    ).toHaveCount(0);

    // Collapsed by keyboard: only the epics, focus kept.
    await collapseEpic.focus();
    await page.keyboard.press('Enter');
    const expandEpic = page.getByRole('button', { name: `Развернуть ${signIn1.identifier}` });
    await expect(expandEpic).toBeFocused();
    await expect(expandEpic).toHaveAttribute('aria-expanded', 'false');
    await expect.poll(() => outline(page)).toEqual([signIn1.identifier, reports.identifier]);

    // Expanded again by keyboard: its features one tier in; the feature that was open stays open.
    await page.keyboard.press('Enter');
    await expect(collapseEpic).toBeFocused();
    await expect.poll(() => outline(page)).toEqual(expanded);
    await expect(row(page, sso.identifier)).toHaveAttribute('data-tier', '1');

    // «Порядок» numbers the top-level rows only; a nested row's menu has no moves.
    await expect(orderCell(page, signIn1.identifier)).toHaveText('1');
    await expect(orderCell(page, reports.identifier)).toHaveText('2');
    await expect(orderCell(page, sso.identifier)).toHaveText('');
    await actionsButton(page, sso.identifier).click();
    const nestedMenu = page.getByRole('menu', { name: `Ещё действия: ${sso.identifier}` });
    await expect(nestedMenu.getByRole('menuitem')).toHaveText([
      'Изменить родителя…',
      'Убрать родителя',
    ]);
    await page.keyboard.press('Escape');
    await expect(nestedMenu).toHaveCount(0);

    // The user folds one feature, so the state is no longer the first one: what follows can then
    // tell keeping it from forgetting it (a panel that reset to the first state on a re-read, or
    // kept the fold across a reload, would be caught by the lines below).
    await page.getByRole('button', { name: `Свернуть ${byLink.identifier}` }).click();
    await expect
      .poll(() => outline(page))
      .toEqual([
        signIn1.identifier,
        `  ${sso.identifier}`,
        `  ${byLink.identifier}`,
        reports.identifier,
      ]);

    // A reorder through the epic's row menu moves it with its shown children, and keeps them shown.
    const reread = page.waitForResponse(
      (response) =>
        response.request().method() === 'GET' &&
        new URL(response.url()).pathname.endsWith('/backlogs/epics'),
    );
    await actionsButton(page, reports.identifier).click();
    await page
      .getByRole('menu', { name: `Ещё действия: ${reports.identifier}` })
      .getByRole('menuitem', { name: 'Выше' })
      .click();
    await expect(page.getByRole('status').filter({ hasText: 'перемещён' })).toHaveText(
      `${reports.identifier} перемещён: позиция 1 из 2.`,
    );
    // Read again after the reorder: the fold is kept, the feature's two children stay hidden.
    await reread;
    await expect
      .poll(() => outline(page))
      .toEqual([
        reports.identifier,
        signIn1.identifier,
        `  ${sso.identifier}`,
        `  ${byLink.identifier}`,
      ]);
    await expect(
      page.getByRole('button', { name: `Развернуть ${byLink.identifier}` }),
    ).toHaveAttribute('aria-expanded', 'false');

    // A reload starts at the first state again: the feature is open and its children are back.
    await page.reload();
    await expect
      .poll(() => outline(page))
      .toEqual([
        reports.identifier,
        signIn1.identifier,
        `  ${sso.identifier}`,
        `  ${byLink.identifier}`,
        `    ${letter.identifier}`,
        `    ${expiry.identifier}`,
      ]);
  });

  test('24.4 «Истории»: «Изменить родителя…» to a feature, offered with every type BR-H-2 allows, the epic’s tree shows it; «Убрать родителя» (US-5)', async ({
    page,
  }) => {
    await signIn(page, { returnPath: '/home' });
    const { teamPrefix, item } = await fixture(page);
    const epic = await item('EPIC', 'Вход и регистрация');
    const feature = await item('FEAT', 'Вход через SSO', epic);
    const story = await item('US', 'Запомнить устройство');

    await page.goto(`${teamPrefix}/backlogs/stories`);
    await expect.poll(() => outline(page)).toEqual([story.identifier]);

    // The menu by keyboard: Enter opens on «Изменить родителя…», Enter chooses.
    const actions = actionsButton(page, story.identifier);
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
    // The dialog offers every type BR-H-2 allows above a story, as the form does (feat-027 BR-T-14).
    await expect(field).toHaveAccessibleDescription(
      'Родителем может быть: Эпик, Фича, Пользовательская история, Баг.',
    );
    await expect(dialog.getByRole('button', { name: 'Сохранить' })).toBeDisabled();
    // The field takes focus as the dialog opens; under StrictMode the dev
    // server's first focus is not to be relied on (tech-009 D-3), so it is
    // clicked first.
    await field.click();
    // The epic is offered beside the feature, and there is no «Нет родителя» — that is «Убрать родителя».
    await expect(
      page.getByRole('option', { name: new RegExp(`^${epic.identifier} `) }),
    ).toBeVisible();
    await expect(
      page.getByRole('option', { name: new RegExp(`^${feature.identifier} `) }),
    ).toBeVisible();
    await expect(page.getByRole('option', { name: 'Нет родителя' })).toHaveCount(0);
    await page.keyboard.type('sso');
    // The search for «sso» has answered once the epic, which does not match, has left the list.
    await expect(
      page.getByRole('option', { name: new RegExp(`^${epic.identifier} `) }),
    ).toHaveCount(0);
    await expect(
      page.getByRole('option', { name: new RegExp(`^${feature.identifier} `) }),
    ).toBeVisible();
    await page.keyboard.press('ArrowDown');
    await page.keyboard.press('Enter');
    await expect(field).toHaveValue(`${feature.identifier} Вход через SSO`);
    await dialog.getByRole('button', { name: 'Сохранить' }).click();

    // There is no «Родитель» column: the notice carries it. The feature is a level above this
    // page, so the story stays a top-level row; focus returns to «Ещё действия».
    await expect(dialog).toHaveCount(0);
    await expect(page.getByRole('alert')).toContainText(
      `У ${story.identifier} теперь родитель ${feature.identifier}.`,
    );
    await expect(actions).toBeFocused();

    // The epic's tree shows the story beneath the feature, already expanded (D-6).
    await levelButton(page).click();
    await page.getByRole('menuitem', { name: 'Эпики' }).click();
    await expect(page).toHaveURL(new RegExp(`${teamPrefix}/backlogs/epics$`));
    await expect
      .poll(() => outline(page))
      .toEqual([epic.identifier, `  ${feature.identifier}`, `    ${story.identifier}`]);

    // «Убрать родителя»: the notice, and the tree lets go.
    await page.goBack();
    await expect(page).toHaveURL(new RegExp(`${teamPrefix}/backlogs/stories$`));
    await actionsButton(page, story.identifier).click();
    await page.getByRole('menuitem', { name: 'Убрать родителя' }).click();
    await expect(page.getByRole('alert')).toContainText(
      `У ${story.identifier} больше нет родителя.`,
    );
    await page.goto(`${teamPrefix}/backlogs/epics`);
    await expect.poll(() => outline(page)).toEqual([epic.identifier, `  ${feature.identifier}`]);
  });

  test('24.5 the level drop-down on «Бэклоги» and «Канбан» by keyboard, the address and back; the team’s name is the h1 with the one tab «Бэклог» / «Канбан»; the entry and «Показать канбан» read «Канбан» (US-6)', async ({
    page,
  }) => {
    await signIn(page, { returnPath: '/home' });
    const { firstTeam, teamPrefix, item } = await fixture(page);
    const epic = await item('EPIC', 'Вход и регистрация');
    const feature = await item('FEAT', 'Вход через SSO');
    const story = await item('US', 'Запомнить устройство');

    // «Бэклоги»: the `h1` is the team's name (the browser tab names the page), «Бэклог» the one
    // tab, the drop-down reads the level and keeps «Уровень: <уровень>» as its name (BR-T-16).
    await page.goto(`${teamPrefix}/backlogs/stories`);
    await expect(page.getByRole('heading', { level: 1, name: firstTeam.name })).toBeVisible();
    await expect(page).toHaveTitle('Бэклоги — Mustask');
    await expect.poll(() => outline(page)).toEqual([story.identifier]);
    await expect(page.getByRole('tab')).toHaveCount(1);
    await expect(page.getByRole('tab', { name: 'Бэклог' })).toHaveAttribute(
      'aria-selected',
      'true',
    );
    const button = levelButton(page);
    await expect(button).toHaveAccessibleName('Уровень: Истории');
    await expect(button).toHaveText('Истории');
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

    // The rename: «Показать канбан», then «Канбан» in the tab title, the one tab, the region, the entry.
    await page.getByRole('link', { name: 'Показать канбан' }).click();
    await expect(page).toHaveURL(new RegExp(`${teamPrefix}/boards/stories$`));
    await expect(page.getByRole('heading', { level: 1, name: firstTeam.name })).toBeVisible();
    await expect(page).toHaveTitle('Канбан — Mustask');
    await expect(page.getByRole('tab')).toHaveCount(1);
    await expect(page.getByRole('tab', { name: 'Канбан' })).toHaveAttribute(
      'aria-selected',
      'true',
    );
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
