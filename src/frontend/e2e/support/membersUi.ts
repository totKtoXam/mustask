// Specs 27-30 (mod-016): the addresses and the locators of the screens the task
// adds or narrows, in one place so a moved page (feat-028 may move the team
// page, design § 15 item 11) is one edit. Every role and name here is the
// selector contract of `plan-frontend.md` § 4.4; every string is design.md's or
// the spec's § Screens and copy, written out, not read back from the panel.
import { expect, type Locator, type Page } from '@playwright/test';

// --- addresses ---------------------------------------------------------------

const organizationPrefix = (organizationId: string) => `/organizations/${organizationId}`;
const projectPrefix = (organizationId: string, projectId: string) =>
  `${organizationPrefix(organizationId)}/projects/${projectId}`;

export const organizationGeneralAddress = (organizationId: string) =>
  `${organizationPrefix(organizationId)}/settings/general`;
export const membersAddress = (organizationId: string) =>
  `${organizationPrefix(organizationId)}/settings/members`;
export const projectsAddress = (organizationId: string) =>
  `${organizationPrefix(organizationId)}/settings/projects`;
export const deactivatedProjectsAddress = (organizationId: string) =>
  `${projectsAddress(organizationId)}/deactivated`;
export const projectGeneralAddress = (organizationId: string, projectId: string) =>
  `${projectPrefix(organizationId, projectId)}/settings/general`;
export const teamsAddress = (organizationId: string, projectId: string) =>
  `${projectPrefix(organizationId, projectId)}/settings/teams`;
/** The team page of mod-002 § 10; feat-028 may move it. */
export const teamAddress = (organizationId: string, projectId: string, teamId: string) =>
  `${teamsAddress(organizationId, projectId)}/${teamId}`;
export const areasAddress = (organizationId: string, projectId: string) =>
  `${projectPrefix(organizationId, projectId)}/settings/areas`;
export const workItemsAddress = (organizationId: string, projectId: string) =>
  `${projectPrefix(organizationId, projectId)}/workitems`;
export const workItemAddress = (organizationId: string, projectId: string, identifier: string) =>
  `${workItemsAddress(organizationId, projectId)}/${identifier}`;
export const backlogAddress = (
  organizationId: string,
  projectId: string,
  teamId: string,
  level = 'stories',
) => `${projectPrefix(organizationId, projectId)}/teams/${teamId}/backlogs/${level}`;
export const boardAddress = (
  organizationId: string,
  projectId: string,
  teamId: string,
  level = 'stories',
) => `${projectPrefix(organizationId, projectId)}/teams/${teamId}/boards/${level}`;

// --- the shell ---------------------------------------------------------------

export const main = (page: Page) => page.getByRole('main');
export const mainNavigation = (page: Page) =>
  page.getByRole('navigation', { name: 'Основное меню' });
export const settingsNavigation = (page: Page) =>
  page.getByRole('navigation', { name: 'Меню настроек' });
export const switcher = (page: Page) => page.getByRole('group', { name: 'Контекст работы' });

/** The page's `h1`. */
export const heading = (page: Page, name: string) =>
  page.getByRole('heading', { level: 1, name, exact: true });

/**
 * The page has left every loading state: no skeleton (`role="status"`
 * «Загрузка данных») is on screen. Call it after the page's `h1` and before an
 * assertion that something is *absent* (a control the role lacks), so the
 * absence is not just a page that has not drawn yet.
 */
export async function awaitLoaded(page: Page): Promise<void> {
  await expect(page.getByRole('status', { name: 'Загрузка данных' })).toHaveCount(0);
}

/** A notice: a plain `Snackbar` message, read by its text (plan § 4.4). */
export const notice = (page: Page, text: string) => page.getByText(text, { exact: true });

// --- tables ------------------------------------------------------------------

export const membersTable = (page: Page) =>
  page.getByRole('table', { name: 'Участники организации' });
export const teamMembersTable = (page: Page) =>
  page.getByRole('table', { name: 'Участники команды' });

/**
 * The row whose «Имя» cell holds `name`. Not a filter on the row's text: the
 * name of the user who added the others is also in their «Добавлен» cells.
 */
export function rowOf(table: Locator, name: string): Locator {
  return table
    .getByRole('row')
    .filter({ has: table.page().locator('td:first-child, th:first-child', { hasText: name }) });
}

/** The cell of `row` under the column header `column` of `table`. */
export async function cellOf(table: Locator, row: Locator, column: string): Promise<Locator> {
  const headers = (await table.getByRole('columnheader').allInnerTexts()).map((text) =>
    text.trim(),
  );
  const index = headers.indexOf(column);
  if (index < 0) throw new Error(`The table has no column «${column}»: ${headers.join(' | ')}`);
  return row.locator(':scope > td, :scope > th').nth(index);
}

/** The «Действия: <имя>» button of a row. */
export const rowActions = (scope: Locator | Page, name: string) =>
  scope.getByRole('button', { name: `Действия: ${name}`, exact: true });

/** Every row-actions button in `scope`. */
export const anyRowActions = (scope: Locator | Page) =>
  scope.getByRole('button', { name: /^Действия: / });

/** Opens a row's menu and chooses `item`. */
export async function chooseRowAction(
  page: Page,
  scope: Locator | Page,
  name: string,
  item: string,
): Promise<void> {
  await rowActions(scope, name).click();
  await page.getByRole('menuitem', { name: item, exact: true }).click();
}

/** Today as the panel writes a date: «ДД.ММ.ГГГГ», in the browser's own time zone. */
export function todayText(page: Page): Promise<string> {
  return page.evaluate(() =>
    new Date().toLocaleDateString('ru-RU', { day: '2-digit', month: '2-digit', year: 'numeric' }),
  );
}

// --- the user search (both add dialogs, the assignee picker) --------------------

export const escapeForRegExp = (text: string) => text.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');

/** An option (a candidate line or a hint line) whose accessible name contains `text`. */
export const optionWith = (page: Page, text: string) =>
  page.getByRole('option', { name: new RegExp(escapeForRegExp(text)) });

/** The combobox of a search field: «Пользователь», «Участник организации» or «Исполнитель». */
export const userField = (scope: Locator | Page, label: string) =>
  scope.getByRole('combobox', { name: label });

/** The combobox of the «Роль» select (its name may carry the chosen value, so it is matched loosely). */
export const roleField = (scope: Locator | Page) => scope.getByRole('combobox', { name: 'Роль' });

/** Types into a user search field; the list opens by itself. */
export async function searchFor(field: Locator, text: string): Promise<void> {
  await field.click();
  await field.fill(text);
}

/**
 * Types `text` into a user search field and waits for the answer to *that*
 * search: the hint line left by an earlier search is not taken for the new
 * result. `endpoint` is the route's last segment (`member-candidates` of the
 * two add dialogs, `assignee-candidates` of the work-item form).
 */
export async function searchAnswered(
  page: Page,
  field: Locator,
  text: string,
  endpoint: 'member-candidates' | 'assignee-candidates' = 'member-candidates',
): Promise<void> {
  const answered = page.waitForResponse(
    (response) =>
      response.url().includes(`/${endpoint}`) && response.url().includes(encodeURIComponent(text)),
  );
  await searchFor(field, text);
  await answered;
}

/**
 * Searches by the user's unique token, picks them from the list and checks the
 * field shows their name. The candidate line carries the e-mail beneath the name
 * (design § 3.5), so it is found by a pattern on the display name.
 */
export async function chooseUser(
  page: Page,
  field: Locator,
  user: { token: string; displayName: string },
): Promise<void> {
  await searchFor(field, user.token);
  await optionWith(page, user.displayName).click();
  await expect(field).toHaveValue(user.displayName);
}

/** A field's refusal: the helper text, in the error colour, with `aria-invalid="true"` on the input. */
export async function expectFieldRefusal(
  scope: Locator,
  field: Locator,
  text: string,
): Promise<void> {
  await expect(scope.getByText(text, { exact: true })).toBeVisible();
  await expect(field).toHaveAttribute('aria-invalid', 'true');
}

// --- layout --------------------------------------------------------------------

export async function pageScrollsSideways(page: Page): Promise<boolean> {
  return page.evaluate(
    () => document.documentElement.scrollWidth > document.documentElement.clientWidth,
  );
}
