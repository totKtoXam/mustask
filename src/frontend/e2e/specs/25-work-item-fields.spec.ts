import { expect, test, type Browser, type Locator, type Page } from '@playwright/test';
import { signIn } from '../support/auth';
import { createContext, runName, send } from '../support/organizationsApi';
import { createWorkItem, saveWorkItem, typeId, type WorkItemDto } from '../support/workItemsApi';

/**
 * 25. The work item form's fields per type (feat-022 US-1..US-6) against the
 * real backend: one case per type for the form, one for «Приоритет» on the
 * kanban card — and, since feat-027 D-7 took it off the backlog row, on no
 * column of the backlog. The spec names no E2E scenarios of its own.
 *
 * Every case creates what it needs the way spec 24 does — an organization
 * with one project through mod-002's API, its items through mod-003's create
 * — and `afterAll` deactivates each organization it created, which hides
 * everything beneath it: nothing is ever purged (BR-M1-1) and work items have
 * no deletion before mod-005, so the stand is left as it was found. Items
 * created without a team land in the project's root, which the first team
 * owns, so they are that team's backlog rows and kanban cards.
 *
 * Dates are typed into the browser's own date field (status.md D-6, OQ-2
 * (a)) as Playwright fills one: `YYYY-MM-DD`. Nothing is injected and nothing
 * on the session flow is stubbed (`src/test/e2eSessionGuard.test.ts`).
 */

const created: string[] = [];

const save = (page: Page) => page.getByRole('button', { name: 'Сохранить' });
const region = (page: Page, name: string) => page.getByRole('region', { name });
const textbox = (page: Page, name: string) => page.getByRole('textbox', { name });
const list = (page: Page, name: string) => page.getByRole('combobox', { name });
const date = (page: Page, name: string) => page.getByLabel(name, { exact: true });

/** A signed-in API session for clean-up, closed by the caller. */
async function apiSession(browser: Browser) {
  const context = await browser.newContext();
  const page = await context.newPage();
  await signIn(page);
  return { request: page.request, close: () => context.close() };
}

/** An organization with «Пример проекта», through the API; items of a type by its key. */
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
  async function item(key: string, title: string): Promise<WorkItemDto> {
    let type = types.get(key);
    if (!type) {
      type = await typeId(page.request, context.organization.id, key);
      types.set(key, type);
    }
    return createWorkItem(page.request, context.organization.id, context.project.id, {
      typeId: type,
      title,
    });
  }

  /** The form of `identifier`, opened and loaded. */
  async function open(identifier: string) {
    await page.goto(`${prefix}/workitems/${identifier}`);
    await expect(
      page.getByRole('heading', { level: 1, name: new RegExp(` ${identifier}$`) }),
    ).toBeVisible();
  }

  return { ...context, prefix, teamPrefix, item, open };
}

/**
 * «Сохранить» and its answer: the save went through and the form shows the
 * stored values with «Сохранить» disabled again. Waits for the PATCH itself,
 * not the notice, which an earlier save may still be showing.
 */
async function saveForm(page: Page) {
  const answered = page.waitForResponse(
    (response) => response.request().method() === 'PATCH' && response.url().includes('/workitems/'),
  );
  await save(page).click();
  expect((await answered).ok()).toBe(true);
  await expect(page.getByText('Изменения сохранены.')).toBeVisible();
  await expect(save(page)).toBeDisabled();
}

/** A section's controls' labels, top to bottom: MUI draws a list's label as no `label` element. */
const labelsOf = (page: Page, name: string) => region(page, name).locator('.MuiInputLabel-root');

/** Picks `option` from the fixed list `name`. */
async function choose(page: Page, name: string, option: string) {
  await list(page, name).click();
  await page.getByRole('option', { name: option, exact: true }).click();
}

/** The sections' headings in document — reading and Tab — order. */
async function sectionOrder(page: Page): Promise<string[]> {
  return page.getByRole('region').locator('h2').allInnerTexts();
}

async function box(locator: Locator) {
  const found = await locator.boundingBox();
  if (!found) throw new Error('The element is not laid out.');
  return found;
}

test.describe('25. the work item form’s fields per type (feat-022)', () => {
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

  test('25.1 «Эпик»: two columns at 1440 px, one at 1279 px, no sideways scroll at 640 px; planning and classification saved and kept; a target date before the start refused (US-1, US-2, US-5)', async ({
    page,
  }) => {
    await signIn(page, { returnPath: '/home' });
    const { item, open } = await fixture(page);
    const epic = await item('EPIC', 'Вход и регистрация');
    await open(epic.identifier);

    // US-5: a new epic's defaults; every other field empty.
    await expect(list(page, 'Приоритет')).toHaveText('2');
    await expect(list(page, 'Приоритет')).toHaveAccessibleDescription('1 — самый высокий');
    await expect(list(page, 'Вид ценности')).toHaveText('Бизнес');
    await expect(list(page, 'Риск')).toHaveText('Не указан');
    await expect(textbox(page, 'Трудоёмкость')).toHaveValue('');
    await expect(textbox(page, 'Трудоёмкость')).toHaveAttribute('placeholder', 'Не оценено');
    await expect(date(page, 'Дата начала')).toHaveValue('');

    // US-1: «Планирование» stands to the right of «Описание».
    expect(await sectionOrder(page)).toEqual([
      'Описание',
      'Планирование',
      'Классификация',
      'Связанная работа',
    ]);
    const description = await box(region(page, 'Описание'));
    const planning = await box(region(page, 'Планирование'));
    expect(planning.x).toBeGreaterThan(description.x + description.width - 1);

    // US-2: the seven values in one save, kept after a reload.
    await choose(page, 'Риск', '1 — Высокий');
    await textbox(page, 'Трудоёмкость').fill('13,5');
    await textbox(page, 'Бизнес-ценность').fill('40');
    await textbox(page, 'Критичность по времени').fill('8');
    await date(page, 'Дата начала').fill('2026-11-01');
    await date(page, 'Целевая дата').fill('2027-03-31');
    await choose(page, 'Вид ценности', 'Архитектура');
    await saveForm(page);
    await page.reload();
    await expect(list(page, 'Риск')).toHaveText('1 — Высокий');
    await expect(textbox(page, 'Трудоёмкость')).toHaveValue('13,5');
    await expect(textbox(page, 'Бизнес-ценность')).toHaveValue('40');
    await expect(textbox(page, 'Критичность по времени')).toHaveValue('8');
    await expect(date(page, 'Дата начала')).toHaveValue('2026-11-01');
    await expect(date(page, 'Целевая дата')).toHaveValue('2027-03-31');
    await expect(list(page, 'Вид ценности')).toHaveText('Архитектура');

    // Failure: a start after the stored target is refused under «Целевая дата».
    await date(page, 'Дата начала').fill('2027-04-01');
    await save(page).click();
    await expect(date(page, 'Целевая дата')).toHaveAccessibleDescription(
      'Целевая дата не может быть раньше даты начала.',
    );
    await page.reload();
    await expect(date(page, 'Дата начала')).toHaveValue('2026-11-01');

    // US-1: below 1280 px one column, the left one's sections first.
    // The permanent navigation animates its width when the viewport crosses a
    // breakpoint (mod-015 design § 2.6), and the page moves with it for about
    // 225 ms: the boxes are read once the two sections stand on one left edge.
    await page.setViewportSize({ width: 1279, height: 900 });
    await expect
      .poll(async () =>
        Math.abs(
          (await box(region(page, 'Планирование'))).x - (await box(region(page, 'Описание'))).x,
        ),
      )
      .toBeLessThan(2);
    const folded = await box(region(page, 'Описание'));
    const below = await box(region(page, 'Планирование'));
    expect(below.y).toBeGreaterThan(folded.y + folded.height - 1);
    expect((await box(list(page, 'Приоритет'))).width).toBeLessThanOrEqual(480);

    // US-1: at 640 px the page never scrolls sideways (once the navigation has settled).
    await page.setViewportSize({ width: 640, height: 900 });
    await expect(list(page, 'Вид ценности')).toBeVisible();
    await expect
      .poll(() =>
        page.evaluate(
          () => document.documentElement.scrollWidth <= document.documentElement.clientWidth,
        ),
      )
      .toBe(true);
  });

  test('25.2 «Фича»: the epic’s fields; «Риск» emptied to «Не указан»; a number emptied and 0 kept (US-2)', async ({
    page,
  }) => {
    await signIn(page, { returnPath: '/home' });
    const { item, open } = await fixture(page);
    const feature = await item('FEAT', 'Вход по ссылке');
    await open(feature.identifier);

    await expect(labelsOf(page, 'Планирование')).toHaveText([
      'Приоритет',
      'Риск',
      'Трудоёмкость',
      'Бизнес-ценность',
      'Критичность по времени',
      'Дата начала',
      'Целевая дата',
    ]);

    await choose(page, 'Риск', '2 — Средний');
    await textbox(page, 'Трудоёмкость').fill('5');
    await saveForm(page);

    await list(page, 'Риск').click();
    await expect(page.getByRole('option')).toHaveText([
      'Не указан',
      '1 — Высокий',
      '2 — Средний',
      '3 — Низкий',
    ]);
    await page.getByRole('option', { name: 'Не указан' }).click();
    await textbox(page, 'Трудоёмкость').fill('');
    await saveForm(page);
    await page.reload();
    await expect(list(page, 'Риск')).toHaveText('Не указан');
    await expect(textbox(page, 'Трудоёмкость')).toHaveValue('');

    await textbox(page, 'Трудоёмкость').fill('0');
    await saveForm(page);
    await page.reload();
    await expect(textbox(page, 'Трудоёмкость')).toHaveValue('0');
  });

  test('25.3 «Пользовательская история»: «Критерии приёмки» under «Описание»; points, risk and criteria in one save; text that is not a number never leaves the browser (US-1, US-2, US-4)', async ({
    page,
  }) => {
    await signIn(page, { returnPath: '/home' });
    const { item, open } = await fixture(page);
    const story = await item('US', 'Письмо со ссылкой');
    await open(story.identifier);

    expect(await sectionOrder(page)).toEqual([
      'Описание',
      'Критерии приёмки',
      'Планирование',
      'Классификация',
      'Связанная работа',
    ]);
    await expect(textbox(page, 'Критерии приёмки')).toHaveAttribute(
      'placeholder',
      'Опишите, по каким признакам история считается готовой',
    );
    await expect(textbox(page, 'Трудоёмкость')).toHaveCount(0);

    // Failure: «пять» is refused in the browser and nothing is stored.
    await textbox(page, 'Оценка в пунктах').fill('пять');
    await save(page).click();
    await expect(textbox(page, 'Оценка в пунктах')).toHaveAccessibleDescription('Введите число.');

    await textbox(page, 'Оценка в пунктах').fill('5');
    await choose(page, 'Риск', '2 — Средний');
    await textbox(page, 'Критерии приёмки').fill('<b>важно</b>\nшаг 2');
    await saveForm(page);
    await page.reload();
    await expect(textbox(page, 'Оценка в пунктах')).toHaveValue('5');
    await expect(list(page, 'Риск')).toHaveText('2 — Средний');
    await expect(textbox(page, 'Критерии приёмки')).toHaveValue('<b>важно</b>\nшаг 2');
  });

  test('25.4 «Задача»: «Трудозатраты (часы)» kept with «,»; hours beyond two decimals refused, never rounded (US-3)', async ({
    page,
  }) => {
    await signIn(page, { returnPath: '/home' });
    const { item, open } = await fixture(page);
    const task = await item('TASK', 'Шаблон письма');
    await open(task.identifier);

    expect(await sectionOrder(page)).toEqual([
      'Описание',
      'Планирование',
      'Трудозатраты (часы)',
      'Связанная работа',
    ]);
    await expect(textbox(page, 'Оставшаяся работа')).toHaveAttribute('placeholder', 'Не оценено');

    // Failure: «1,333» hours.
    await textbox(page, 'Исходная оценка').fill('1,333');
    await save(page).click();
    await expect(textbox(page, 'Исходная оценка')).toHaveAccessibleDescription(
      'Не более двух знаков после запятой.',
    );

    await choose(page, 'Вид работ', 'Разработка');
    await textbox(page, 'Исходная оценка').fill('8');
    await textbox(page, 'Оставшаяся работа').fill('5,5');
    await textbox(page, 'Выполненная работа').fill('2.5');
    await saveForm(page);
    await page.reload();
    await expect(list(page, 'Вид работ')).toHaveText('Разработка');
    await expect(textbox(page, 'Исходная оценка')).toHaveValue('8');
    await expect(textbox(page, 'Оставшаяся работа')).toHaveValue('5,5');
    await expect(textbox(page, 'Выполненная работа')).toHaveValue('2,5');
  });

  test('25.5 «Баг»: repro steps and system info under «Описание»; «Серьёзность» «3 — Средняя» by default; points, severity and hours saved together (US-3, US-4, US-5)', async ({
    page,
  }) => {
    await signIn(page, { returnPath: '/home' });
    const { item, open } = await fixture(page);
    const bug = await item('BUG', 'Ссылка истекает раньше срока');
    await open(bug.identifier);

    expect(await sectionOrder(page)).toEqual([
      'Описание',
      'Шаги воспроизведения',
      'Сведения о системе',
      'Планирование',
      'Трудозатраты (часы)',
      'Связанная работа',
    ]);
    await expect(labelsOf(page, 'Планирование')).toHaveText([
      'Оценка в пунктах',
      'Приоритет',
      'Серьёзность',
      'Вид работ',
    ]);
    await expect(list(page, 'Серьёзность')).toHaveText('3 — Средняя');
    await expect(textbox(page, 'Шаги воспроизведения')).toHaveValue('');
    await expect(textbox(page, 'Сведения о системе')).toHaveValue('');

    await textbox(page, 'Шаги воспроизведения').fill('Открыть ссылку через сутки');
    await textbox(page, 'Сведения о системе').fill('Chrome 140, Windows 11');
    await textbox(page, 'Оценка в пунктах').fill('1');
    await choose(page, 'Приоритет', '1');
    await choose(page, 'Серьёзность', '1 — Критическая');
    await textbox(page, 'Оставшаяся работа').fill('3');
    await saveForm(page);
    await page.reload();
    await expect(textbox(page, 'Шаги воспроизведения')).toHaveValue('Открыть ссылку через сутки');
    await expect(textbox(page, 'Сведения о системе')).toHaveValue('Chrome 140, Windows 11');
    await expect(textbox(page, 'Оценка в пунктах')).toHaveValue('1');
    await expect(list(page, 'Приоритет')).toHaveText('1');
    await expect(list(page, 'Серьёзность')).toHaveText('1 — Критическая');
    await expect(textbox(page, 'Оставшаяся работа')).toHaveValue('3');
  });

  test('25.6 «Приоритет» on the kanban card, changed on the form, and no column of it on the backlog (US-6)', async ({
    page,
  }) => {
    await signIn(page, { returnPath: '/home' });
    const { organization, project, teamPrefix, item, open } = await fixture(page);
    const story = await item('US', 'Вход по ссылке из письма');
    const bug = await item('BUG', 'Падение при входе');
    await saveWorkItem(page.request, organization.id, project.id, bug.identifier, {
      priority: { value: 3 },
    });

    // On the form: the story's «Приоритет» 1.
    await open(story.identifier);
    await choose(page, 'Приоритет', '1');
    await saveForm(page);

    // The backlog: «Приоритет» is no column of it any more (feat-027 BR-T-1, D-7) — the rows
    // stand, the headers hold none of it; it stays on the card and the form.
    await page.goto(`${teamPrefix}/backlogs/stories`);
    const table = page.getByRole('table', { name: 'Бэклог' });
    // `allInnerTexts` does not wait: read the headers once the table has drawn them.
    await expect(table.getByRole('columnheader', { name: 'Статус' })).toBeVisible();
    const headers = await table.getByRole('columnheader').allInnerTexts();
    expect(headers).not.toContain('Приоритет');
    await expect(table.getByRole('columnheader', { name: 'Приоритет' })).toHaveCount(0);
    await expect(table.locator(`tbody tr[data-identifier="${story.identifier}"]`)).toHaveCount(1);
    await expect(table.locator(`tbody tr[data-identifier="${bug.identifier}"]`)).toHaveCount(1);

    // The kanban: the card's third line.
    await page.goto(`${teamPrefix}/boards/stories`);
    const board = page.getByRole('region', { name: 'Канбан' });
    await expect(board.getByRole('listitem').filter({ hasText: story.identifier })).toContainText(
      'Приоритет: 1',
    );
    await expect(board.getByRole('listitem').filter({ hasText: bug.identifier })).toContainText(
      'Приоритет: 3',
    );

    // Changed on the form again, read again on the card.
    await open(story.identifier);
    await choose(page, 'Приоритет', '4');
    await saveForm(page);
    await page.goto(`${teamPrefix}/boards/stories`);
    await expect(board.getByRole('listitem').filter({ hasText: story.identifier })).toContainText(
      'Приоритет: 4',
    );
  });
});
