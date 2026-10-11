import { expect, test, type Browser, type Page } from '@playwright/test';
import { signIn } from '../support/auth';
import {
  addTeam,
  createContext,
  createProject,
  runKey,
  runName,
  send,
  teams,
} from '../support/organizationsApi';

/**
 * 23. Backlogs and boards (mod-006 US-1..US-6) against the real backend.
 *
 * Every case creates what it needs the way specs 21 and 22 do — an
 * organization with one project through mod-002's API, whose first team is
 * named after the project and owns its root — and `afterAll` deactivates each
 * organization it created, which hides everything beneath it: nothing is ever
 * purged (BR-M1-1) and work items have no deletion before mod-005, so the
 * stand is left as it was found. Names carry a run suffix, as spec 21's do.
 *
 * Moves go through the keyboard paths — a backlog row's «Ещё действия: <ID>»
 * and the card's «Ещё действия» — which are the complete ones; the pointer
 * drags are the browser's own drag and drop and are covered by the component
 * tests (the drop onto a row is spec 26's).
 *
 * Brought in line with feat-001 (its spec § E2E): the level is chosen in the
 * drop-down «Уровень: <уровень>» that replaced the tabs (BR-H-15), and the
 * board is named «Канбан» (BR-H-16); spec 24 covers both. And with feat-027
 * (its spec § E2E; D-7): the page's `h1` is the team's name, with the one tab
 * «Бэклог» / «Канбан» under it; the backlog's rows are found by their
 * `data-identifier` — there is no ID cell — and «Порядок» is the second cell,
 * after the hierarchy column; the reorder's menu is the row's «Ещё действия: <ID>»,
 * which took over from the handle column D-7 removed.
 *
 * Nothing is injected and nothing on the session flow is stubbed
 * (`src/test/e2eSessionGuard.test.ts`). The create dialog's initial focus is
 * not asserted (tech-009: `FormDialog` under StrictMode).
 */

const created: string[] = [];

const main = (page: Page) => page.getByRole('main');

/** A signed-in API session for clean-up, closed by the caller. */
async function apiSession(browser: Browser) {
  const context = await browser.newContext();
  const page = await context.newPage();
  await signIn(page);
  return { request: page.request, close: () => context.close() };
}

/** An organization with «Пример проекта», through the API; its first team's surfaces. */
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
  return { ...context, firstTeam, prefix, teamPrefix };
}

const backlogTable = (page: Page) => page.getByRole('table', { name: 'Бэклог' });

const switcher = (page: Page) => page.getByRole('group', { name: 'Контекст работы' });

/** The columns of a backlog row that this spec reads (feat-027 design § 4.3): «Порядок» follows the hierarchy column. */
const ORDER_CELL = 1;

/**
 * The backlog's identifiers and «Порядок», top to bottom: a row is found by its
 * `data-identifier` (there is no ID cell, feat-027 D-7), its «Порядок» is a cell.
 */
async function backlogRows(page: Page): Promise<[string, string][]> {
  const rows = backlogTable(page).locator('tbody tr[data-identifier]');
  const count = await rows.count();
  const result: [string, string][] = [];
  for (let index = 0; index < count; index += 1) {
    const row = rows.nth(index);
    result.push([
      (await row.getAttribute('data-identifier')) ?? '',
      (await row.getByRole('cell').nth(ORDER_CELL).innerText()).trim(),
    ]);
  }
  return result;
}

/** A board column's list, by its header. */
const column = (page: Page, header: string) =>
  page.getByRole('list', { name: new RegExp(`^${header}, рабочих элементов: `) });

/** Creates an item of `type` titled `title` from the surface's «Новый рабочий элемент» or «Новый элемент». */
async function createFrom(page: Page, button: string, type: string, title: string) {
  await page.getByRole('button', { name: button }).first().click();
  await page.getByRole('menuitem', { name: type }).click();
  const dialog = page.getByRole('dialog', { name: 'Новый рабочий элемент' });
  await dialog.getByRole('textbox', { name: 'Заголовок' }).fill(title);
  await dialog.getByRole('button', { name: 'Создать' }).click();
  await expect(dialog).toHaveCount(0);
  return dialog;
}

test.describe('23. backlogs and boards (mod-006)', () => {
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

  test('23.1 happy path: created on the first team’s «Истории» backlog, reordered by its menu and kept, moved on the board by the card’s menu and kept, created on the board last in «Новые» (US-1..US-6)', async ({
    page,
  }) => {
    await signIn(page, { returnPath: '/home' });
    const { project, firstTeam, teamPrefix } = await fixture(page);
    const key = project.key;

    // US-1: the empty «Истории» backlog of the first team — the team's name is the `h1`
    // (the browser tab keeps the page), «Бэклог» the one tab (feat-027 BR-T-16).
    await page.goto(`${teamPrefix}/backlogs/stories`);
    await expect(page.getByRole('heading', { level: 1, name: firstTeam.name })).toBeVisible();
    await expect(page).toHaveTitle('Бэклоги — Mustask');
    await expect(page.getByRole('button', { name: 'Уровень: Истории' })).toBeVisible();
    await expect(page.getByRole('tab')).toHaveCount(1);
    await expect(page.getByRole('tab', { name: 'Бэклог' })).toHaveAttribute(
      'aria-selected',
      'true',
    );
    await expect(main(page).getByRole('heading', { level: 2, name: 'Бэклог пуст' })).toBeVisible();

    // US-3: two stories and a bug, each last; the dialog names the team's default area.
    await page.getByRole('button', { name: 'Новый рабочий элемент' }).first().click();
    await page.getByRole('menuitem', { name: 'Пользовательская история' }).click();
    const dialog = page.getByRole('dialog', { name: 'Новый рабочий элемент' });
    await expect(dialog).toContainText(
      'Рабочий элемент будет создан в статусе «Новая» в области «Пример проекта» и встанет в конец бэклога команды «Пример проекта». Остальные поля заполняются в его форме.',
    );
    await dialog.getByRole('textbox', { name: 'Заголовок' }).fill('Вход по паролю');
    await dialog.getByRole('button', { name: 'Создать' }).click();
    await expect(page.getByText(`Рабочий элемент ${key}-US-1 создан.`)).toBeVisible();
    await expect(dialog).toHaveCount(0);
    await createFrom(page, 'Новый рабочий элемент', 'Пользовательская история', 'Выход');
    await expect(page.getByText(`Рабочий элемент ${key}-US-2 создан.`)).toBeVisible();
    await createFrom(page, 'Новый рабочий элемент', 'Баг', 'Падение при входе');
    await expect(page.getByText(`Рабочий элемент ${key}-BUG-3 создан.`)).toBeVisible();
    await expect
      .poll(() => backlogRows(page))
      .toEqual([
        [`${key}-US-1`, '1'],
        [`${key}-US-2`, '2'],
        [`${key}-BUG-3`, '3'],
      ]);

    // US-2: the last row «В начало» by its row menu (the moves sit under «Переместить»,
    // feat-027 BR-T-13); renumbered and kept.
    await page.getByRole('button', { name: `Ещё действия: ${key}-BUG-3` }).click();
    const moveMenu = page.getByRole('menu', { name: `Ещё действия: ${key}-BUG-3` });
    await expect(moveMenu.getByRole('menuitem', { name: 'Ниже' })).toBeDisabled();
    await moveMenu.getByRole('menuitem', { name: 'В начало' }).click();
    const reordered: [string, string][] = [
      [`${key}-BUG-3`, '1'],
      [`${key}-US-1`, '2'],
      [`${key}-US-2`, '3'],
    ];
    await expect.poll(() => backlogRows(page)).toEqual(reordered);
    await expect(page.getByRole('button', { name: `Ещё действия: ${key}-BUG-3` })).toBeFocused();
    await expect(page.getByRole('status').filter({ hasText: 'перемещён' })).toHaveText(
      `${key}-BUG-3 перемещён: позиция 1 из 3.`,
    );
    await page.reload();
    await expect(backlogTable(page)).toBeVisible();
    await expect.poll(() => backlogRows(page)).toEqual(reordered);

    // US-4: «Показать канбан» opens the same team's and level's board.
    await page.getByRole('link', { name: 'Показать канбан' }).click();
    await expect(page).toHaveURL(new RegExp(`${teamPrefix}/boards/stories$`));
    // The board's head is the backlog's: the team's name and the one tab «Канбан».
    await expect(page.getByRole('heading', { level: 1, name: firstTeam.name })).toBeVisible();
    await expect(page).toHaveTitle('Канбан — Mustask');
    await expect(page.getByRole('tab')).toHaveCount(1);
    await expect(page.getByRole('tab', { name: 'Канбан' })).toHaveAttribute(
      'aria-selected',
      'true',
    );
    await expect(column(page, 'Новые')).toHaveAccessibleName('Новые, рабочих элементов: 3');
    await expect(column(page, 'В работе')).toHaveAccessibleName('В работе, рабочих элементов: 0');

    // US-5: a card to «В работе» by its menu; kept after a reload, and its form says «Активна».
    await page.getByRole('button', { name: `Ещё действия: ${key}-US-1` }).click();
    const cardMenu = page.getByRole('menu', { name: 'Переместить в колонку' });
    await expect(cardMenu.getByRole('menuitem')).toHaveText([
      'В работе',
      'Разрешена',
      'Завершённые',
    ]);
    await cardMenu.getByRole('menuitem', { name: 'В работе' }).click();
    await expect(column(page, 'В работе').getByRole('listitem')).toHaveCount(1);
    await expect(column(page, 'В работе')).toContainText(`${key}-US-1`);
    await expect(page.getByRole('button', { name: `Ещё действия: ${key}-US-1` })).toBeFocused();
    await page.reload();
    await expect(column(page, 'В работе')).toContainText(`${key}-US-1`);
    await expect(column(page, 'В работе')).toContainText('Активна');
    await expect(column(page, 'Новые')).toHaveAccessibleName('Новые, рабочих элементов: 2');

    await column(page, 'В работе').getByRole('link', { name: 'Вход по паролю' }).click();
    await expect(
      page.getByRole('heading', { level: 1, name: `Пользовательская история ${key}-US-1` }),
    ).toBeVisible();
    await expect(page.getByRole('combobox', { name: 'Статус' })).toContainText('Активна');

    // US-6: «Новый элемент» on the board; the new card is the last of «Новые».
    await page.goto(`${teamPrefix}/boards/stories`);
    await expect(column(page, 'Новые')).toHaveAccessibleName('Новые, рабочих элементов: 2');
    await createFrom(page, 'Новый элемент', 'Баг', 'Сбой выгрузки');
    await expect(page.getByText(`Рабочий элемент ${key}-BUG-4 создан.`)).toBeVisible();
    await expect(column(page, 'Новые').getByRole('listitem')).toHaveCount(3);
    await expect(column(page, 'Новые').getByRole('listitem').last()).toContainText(`${key}-BUG-4`);
  });

  test('23.2 failure: an unknown level address answers «Страница не найдена», a deleted team’s backlog «Команда не найдена» (US-1, US-7)', async ({
    page,
  }) => {
    await signIn(page, { returnPath: '/home' });
    const { organization, project, prefix, teamPrefix } = await fixture(page);

    await page.goto(`${teamPrefix}/backlogs/tasks`);
    await expect(
      page.getByRole('heading', { level: 1, name: 'Страница не найдена' }),
    ).toBeVisible();
    await expect(page.getByText('Проверьте адрес или вернитесь на главную.')).toBeVisible();

    const gone = await addTeam(page.request, organization.id, project.id, runName('Команда E2E'));
    const deleted = await send(
      page.request,
      'DELETE',
      `/api/organizations/${organization.id}/projects/${project.id}/teams/${gone.id}`,
    );
    expect(deleted.status()).toBe(204);
    await page.goto(`${prefix}/teams/${gone.id}/backlogs/stories`);
    await expect(page.getByRole('heading', { level: 1, name: 'Команда не найдена' })).toBeVisible();
    await expect(page.getByRole('link', { name: 'На главную' })).toBeVisible();
  });

  test('23.3 switcher on a team-level page: another project chosen on «Бэклоги» opens the «Истории» backlog of its first team (mod-015 BR-18)', async ({
    page,
  }) => {
    await signIn(page, { returnPath: '/home' });
    const { organization, firstTeam, teamPrefix } = await fixture(page);
    // A second project; its first team comes with it, named after it, and a
    // second team is added so that the landing is the first team's, not any.
    const second = await createProject(page.request, organization.id, {
      name: 'Аналитика',
      key: runKey('A'),
    });
    await addTeam(page.request, organization.id, second.id, runName('Команда E2E'));
    const secondTeams = await teams(page.request, organization.id, second.id);
    const secondFirst = secondTeams.find((team) => team.isFirstTeam);
    if (!secondFirst) throw new Error('The second project has no first team.');

    // The `h1` is the team's name (feat-027 BR-T-16); the tab title says which page it is.
    await page.goto(`${teamPrefix}/backlogs/features`);
    await expect(page.getByRole('heading', { level: 1, name: firstTeam.name })).toBeVisible();
    await expect(page).toHaveTitle('Бэклоги — Mustask');
    await expect(switcher(page).getByRole('button', { name: /^Команда:/ })).toHaveAttribute(
      'aria-label',
      'Команда: Пример проекта',
    );

    await switcher(page)
      .getByRole('button', { name: /^Проект:/ })
      .click();
    await page.getByRole('menuitemradio', { name: 'Аналитика' }).click();

    await expect(page).toHaveURL(
      new RegExp(
        `/organizations/${organization.id}/projects/${second.id}/teams/${secondFirst.id}/backlogs/stories$`,
      ),
    );
    await expect(page.getByRole('heading', { level: 1, name: secondFirst.name })).toBeVisible();
    await expect(page.getByRole('button', { name: 'Уровень: Истории' })).toBeVisible();
    await expect(main(page).getByRole('heading', { level: 2, name: 'Бэклог пуст' })).toBeVisible();
    await expect(switcher(page).getByRole('button', { name: /^Проект:/ })).toHaveAttribute(
      'aria-label',
      'Проект: Аналитика',
    );
    await expect(switcher(page).getByRole('button', { name: /^Команда:/ })).toHaveAttribute(
      'aria-label',
      'Команда: Аналитика',
    );
  });
});
