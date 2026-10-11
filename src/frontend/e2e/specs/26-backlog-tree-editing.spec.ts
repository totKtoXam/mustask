import { expect, test, type Browser, type Locator, type Page } from '@playwright/test';
import { signIn } from '../support/auth';
import { createContext, runName, send } from '../support/organizationsApi';
import {
  createWorkItemIn,
  saveWorkItem,
  setParent,
  stateId,
  typeId,
  type WorkItemDto,
} from '../support/workItemsApi';

/**
 * 26. The backlog laid out and edited as the reference screen (feat-027
 * US-1..US-6, with the human's correction D-7 of 2026-10-09; its spec § E2E and
 * `design.md`) against the real backend: the page head and the eight columns,
 * the whole tree and the two header buttons, a child added inline from a row,
 * a row dragged by any point of it onto another, a parent given by keyboard — and,
 * last, the look the human compares side by side with the reference (26.6).
 *
 * Every case builds its own data through the API the way spec 24 does — an
 * organization with the project «Мастаск», whose first team «Мастаск» owns the
 * root area, and the team «Платформа»; the tree in «Платформа» (feat-027
 * US-1's data: two epics, three features, a story with a bug and a task
 * beneath it, a story directly under an epic, a bug under a feature), plus a
 * feature and a task in «Мастаск»'s own area that stand beneath a «Платформа»
 * row and so beneath none of its rows — and `afterAll` deactivates each
 * organization it created, which hides everything beneath it: nothing is ever
 * purged (BR-M1-1) and work items have no deletion before mod-005, so the
 * stand is left as it was found.
 *
 * Identifiers are the project's own (`<key>-<TYPE>-<n>`, the key a fresh one
 * per run), so the cases read them from the creates' answers. D-7 took the ID
 * column away: a row is found by its `data-identifier`, the nesting is read
 * from its `data-tier`, a cell by its column's position (design § 4.3, § 7),
 * and every control by the accessible name design § 5 fixes. The items are
 * created in the order that makes the number order the stories' order (the
 * «Фичи» backlog reads SSO, link, code with no reorder), and the children are
 * made by the create's own `parentId` (feat-027 api-contract § 3), which takes
 * the parent's area.
 *
 * The first state of the tree is expanded as deep as the data go (D-6 OQ-2
 * (d); 26.2 asserts it). The other cases bring the tree there with
 * `expandToBottom`, which does nothing when it already is.
 *
 * The drag is the browser's own (Chromium), driven by the mouse from the
 * source row's title cell (D-11: the whole row is the drag source; the dotted
 * grip, `data-grip`, which has no name — it is `aria-hidden` — is only a hint)
 * to the target row's title cell; the keyboard path of the same writes is 26.5.
 * While the pointer is over a row the page marks it: the dragged row carries
 * `data-dragging` and `data-selected`, the row under the pointer
 * `data-drop-target="allowed"` or `"refused"` (none for the item itself or its
 * current parent). 26.4 asserts those marks and the writes; the frame they
 * draw — a solid or a dashed outline — is a style the component tests own.
 *
 * 26.6 reads computed styles and boxes against `design.md` § 2 and § 4.2..§
 * 4.6 — fonts, the 36 px rows, the header, the row states, the sticky header,
 * the narrow window. They are tolerant where design.md says "about" and exact
 * where it names a value; the colours the human chose (D-10) are exact (`D10`
 * below), the surfaces (white, hover grey, selected light blue) are matched by
 * what they are.
 *
 * Left to the component tests, because the stand cannot reach it or it races:
 * the add row's pending state (a read-only field with a progress indicator)
 * and a double Enter, a create refused because the parent has gone (no item
 * can be put in «Удалена» through the API before mod-005), the look of the
 * drag's allowed and refused frames, and touch.
 *
 * Nothing is injected and nothing on the session flow is stubbed
 * (`src/test/e2eSessionGuard.test.ts`). The dialogs' initial focus is not
 * asserted (tech-009 D-3: `FormDialog` under StrictMode); «Новый рабочий
 * элемент» stays spec 23's and 24's.
 */

const created: string[] = [];

const main = (page: Page) => page.getByRole('main');
const backlogTable = (page: Page) => page.getByRole('table', { name: 'Бэклог' });
const pageTabs = (page: Page) => page.getByRole('tablist', { name: 'Вид страницы' });
const levelButton = (page: Page) => page.getByRole('button', { name: /^Уровень: / });
const teamButton = (page: Page) =>
  page.getByRole('button', { name: 'Сменить команду', exact: true });
const createButton = (page: Page) =>
  page.getByRole('button', { name: 'Новый рабочий элемент', exact: true }).first();
const expandLevel = (page: Page) =>
  page.getByRole('button', { name: 'Развернуть на один уровень', exact: true });
const collapseLevel = (page: Page) =>
  page.getByRole('button', { name: 'Свернуть на один уровень', exact: true });
const notice = (page: Page, text: string) => page.getByRole('alert').filter({ hasText: text });
const shownRows = (page: Page) => page.getByRole('status').filter({ hasText: 'Показано строк' });
const switcher = (page: Page) => page.getByRole('group', { name: 'Контекст работы' });

/** The table's columns, in the order of spec BR-T-1; 26.1 reads the headers against it. */
const HEADERS = [
  'Иерархия',
  'Порядок',
  'Тип',
  'Заголовок',
  'Статус',
  'Трудоёмкость',
  'Бизнес-ценность',
  'Вид ценности',
] as const;

/** A column's position in a row (design § 4.3); the add row has the same cells. */
const COLUMN = {
  hierarchy: 0,
  order: 1,
  type: 2,
  title: 3,
  state: 4,
  effort: 5,
  businessValue: 6,
  valueArea: 7,
} as const;
type ColumnName = keyof typeof COLUMN;

/** A signed-in API session for clean-up, closed by the caller. */
async function apiSession(browser: Browser) {
  const context = await browser.newContext();
  const page = await context.newPage();
  await signIn(page);
  return { request: page.request, close: () => context.close() };
}

/**
 * An organization with «Мастаск» (its first team «Мастаск» owns the root) and
 * the team «Платформа», through the API; items of a type by its key.
 */
async function fixture(page: Page) {
  const context = await createContext(page.request, {
    organization: runName('Организация E2E'),
    project: 'Мастаск',
    teams: ['Платформа'],
  });
  created.push(context.organization.id);
  const platform = context.teams.find((team) => team.name === 'Платформа');
  const firstTeam = context.teams.find((team) => team.isFirstTeam);
  if (!platform || !firstTeam) throw new Error('The project lacks «Платформа» or its first team.');
  const prefix = `/organizations/${context.organization.id}/projects/${context.project.id}`;
  const platformId = platform.id;
  const teamPrefix = `${prefix}/teams/${platformId}`;
  const types = new Map<string, string>();

  /** The id of the type `key` (`EPIC`, `FEAT`, `US`, `BUG`, `TASK`) in the process. */
  async function typeOf(key: string): Promise<string> {
    let found = types.get(key);
    if (!found) {
      found = await typeId(page.request, context.organization.id, key);
      types.set(key, found);
    }
    return found;
  }

  /**
   * An item of «Платформа»: beneath `parent` when one is given (the create
   * takes the parent's area), else on the team's default area.
   */
  async function item(key: string, title: string, parent?: WorkItemDto): Promise<WorkItemDto> {
    return createWorkItemIn(page.request, context.organization.id, context.project.id, {
      typeId: await typeOf(key),
      title,
      ...(parent ? { parentId: parent.id } : { teamId: platformId }),
    });
  }

  /** An item in the root area, which «Мастаск» owns, then given `parent` — in «Платформа» — by the parent write. */
  async function elsewhere(key: string, title: string, parent: WorkItemDto): Promise<WorkItemDto> {
    const made = await createWorkItemIn(page.request, context.organization.id, context.project.id, {
      typeId: await typeOf(key),
      title,
    });
    await setParent(
      page.request,
      context.organization.id,
      context.project.id,
      made.identifier,
      parent.id,
    );
    return made;
  }

  /** feat-022's save of `identifier`'s fields (each as `{ value }`), or mod-003's `stateId`. */
  function save(identifier: string, body: Record<string, unknown>) {
    return saveWorkItem(
      page.request,
      context.organization.id,
      context.project.id,
      identifier,
      body,
    );
  }

  /**
   * feat-027 US-1's data. «Эпики» then reads, fully expanded:
   * epic → (sso → byCode, byLink → (letter → (lost, template), expiry), device), reports.
   * The story `device` is «Активна» (category «В работе») and the bug `lost`
   * «Закрыта» («Завершённые»), the rest «Новая» («Новые»): the three dots of 26.1.
   */
  async function seed() {
    const epic = await item('EPIC', 'Вход и регистрация');
    const reports = await item('EPIC', 'Отчётность');
    const sso = await item('FEAT', 'Вход через SSO', epic);
    const byLink = await item('FEAT', 'Вход по ссылке', epic);
    const byCode = await item('FEAT', 'Вход по коду', sso);
    const letter = await item('US', 'Письмо со ссылкой', byLink);
    const expiry = await item('BUG', 'Ссылка истекает раньше срока', byLink);
    const lost = await item('BUG', 'Письмо не доходит', letter);
    const device = await item('US', 'Запомнить устройство', epic);
    const template = await item('TASK', 'Шаблон письма', letter);
    // Beneath «Платформа» rows, in «Мастаск»'s area: shown on no «Платформа» row.
    const audit = await elsewhere('FEAT', 'Аудит входа', epic);
    const smtp = await elsewhere('TASK', 'Настроить SMTP', letter);
    await save(epic.identifier, { effort: { value: 20 }, businessValue: { value: 300 } });
    await save(reports.identifier, { valueArea: { value: 'Architectural' } });
    await save(sso.identifier, { effort: { value: 2.5 } });
    await save(device.identifier, {
      stateId: await stateId(page.request, context.organization.id, 'US', 'Активна'),
    });
    await save(lost.identifier, {
      stateId: await stateId(page.request, context.organization.id, 'BUG', 'Закрыта'),
    });
    return {
      epic,
      reports,
      sso,
      byLink,
      byCode,
      letter,
      expiry,
      lost,
      device,
      template,
      audit,
      smtp,
    };
  }

  return { ...context, firstTeam, prefix, teamPrefix, typeOf, item, seed };
}

interface Line {
  identifier: string;
  /** 0 for a row at the top, +1 per level beneath (`data-tier`, design § 7). */
  tier: number;
  order: string;
}

/** The table's item rows top to bottom: identifier, depth and «Порядок». The add row is not one. */
async function lines(page: Page): Promise<Line[]> {
  return backlogTable(page)
    .locator('tbody tr[data-identifier]')
    .evaluateAll(
      (rows, orderColumn) =>
        rows.map((tr) => {
          const cells = Array.from(
            tr.querySelectorAll(':scope > td, :scope > th'),
          ) as HTMLElement[];
          return {
            identifier: tr.getAttribute('data-identifier') ?? '',
            tier: Number(tr.getAttribute('data-tier') ?? '0'),
            order: (cells[orderColumn]?.innerText ?? '').trim(),
          };
        }),
      COLUMN.order,
    );
}

/** «<ID>» for a row at the top, two spaces more per depth. */
async function outline(page: Page): Promise<string[]> {
  return (await lines(page)).map((line) => `${'  '.repeat(line.tier)}${line.identifier}`);
}

/** The identifiers directly beneath `identifier`, top to bottom. */
function childrenOf(all: readonly Line[], identifier: string): string[] {
  const at = all.findIndex((line) => line.identifier === identifier);
  if (at < 0) return [];
  const children: string[] = [];
  for (let next = at + 1; next < all.length && all[next].tier > all[at].tier; next += 1) {
    if (all[next].tier === all[at].tier + 1) children.push(all[next].identifier);
  }
  return children;
}

/** A backlog row, by its identifier (`data-identifier`). */
function rowOf(page: Page, identifier: string): Locator {
  return backlogTable(page).locator(`tbody tr[data-identifier="${identifier}"]`);
}

/** The inline add row beneath `parent` (`data-add-row` carries the parent's identifier). */
function addRowOf(page: Page, parent: WorkItemDto): Locator {
  return backlogTable(page).locator(`tbody tr[data-add-row="${parent.identifier}"]`);
}

/** A row's cells, `td` or `th`, in column order. */
function cellsOf(row: Locator): Locator {
  return row.locator('xpath=./td | ./th');
}

function cellOf(page: Page, identifier: string, column: ColumnName): Locator {
  return cellsOf(rowOf(page, identifier)).nth(COLUMN[column]);
}

/** The type icon in a title cell: its one `svg` outside every button (the chevron and «⋮» are buttons). */
function iconIn(titleCell: Locator): Locator {
  return titleCell.locator('xpath=.//*[local-name()="svg"][not(ancestor::button)]');
}

async function box(locator: Locator) {
  const found = await locator.boundingBox();
  if (!found) throw new Error('The element is not laid out.');
  return found;
}

function centre(found: { x: number; y: number; width: number; height: number }) {
  return { x: found.x + found.width / 2, y: found.y + found.height / 2 };
}

/** `actual` is within `tolerance` px of `expected`. */
function near(actual: number, expected: number, tolerance = 1) {
  expect(Math.abs(actual - expected), `${actual} is not about ${expected}`).toBeLessThanOrEqual(
    tolerance,
  );
}

/** The table, loaded: its «Свернуть на один уровень» and its first item row stand. */
async function opened(page: Page, address: string) {
  await page.goto(address);
  await expect(backlogTable(page)).toBeVisible();
  await expect(collapseLevel(page)).toBeVisible();
  await expect(backlogTable(page).locator('tbody tr[data-identifier]').first()).toBeVisible();
}

/** «Развернуть на один уровень» until it is unavailable: every row with children is expanded. */
async function expandToBottom(page: Page) {
  const button = expandLevel(page);
  for (let step = 0; step < 8; step += 1) {
    if ((await button.getAttribute('aria-disabled')) === 'true') break;
    await button.click();
  }
  await expect(button).toHaveAttribute('aria-disabled', 'true');
}

/** Brings the given rows to the expanded state, outermost first. */
async function expandRows(page: Page, ...items: WorkItemDto[]) {
  for (const { identifier } of items) {
    const open = page.getByRole('button', { name: `Развернуть ${identifier}`, exact: true });
    const close = page.getByRole('button', { name: `Свернуть ${identifier}`, exact: true });
    await expect(open.or(close)).toBeVisible();
    if ((await open.count()) > 0) {
      await open.click();
      await expect(close).toBeVisible();
    }
  }
}

/** Tab until `target` has focus, at most `limit` presses. */
async function tabTo(page: Page, target: Locator, limit = 6) {
  for (let step = 0; step < limit; step += 1) {
    if (await target.evaluate((element) => element === document.activeElement)) break;
    await page.keyboard.press('Tab');
  }
  await expect(target).toBeFocused();
}

/**
 * The browser's own drag, up to the pointer over the target: the mouse goes
 * down on `source`'s row — in the middle of its «Заголовок» cell, or, with
 * `from: 'link'`, on its title, the link the human pressed on the stand when
 * nothing moved: the whole row is the drag source (D-11), so any point of it
 * will do, and the link is not draggable itself, or the browser would drag its
 * address; the grip is only a hint — moves over the middle of `target`'s
 * «Заголовок» cell and stays there. The caller looks at the page mid-drag and
 * then releases with `page.mouse.up()`.
 */
async function dragOver(
  page: Page,
  source: string,
  target: string,
  from: 'cell' | 'link' = 'cell',
) {
  const sourceCell =
    from === 'link'
      ? cellOf(page, source, 'title').getByRole('link', { includeHidden: true })
      : cellOf(page, source, 'title');
  const targetCell = cellOf(page, target, 'title');
  await sourceCell.scrollIntoViewIfNeeded();
  await targetCell.scrollIntoViewIfNeeded();
  const start = centre(await box(sourceCell));
  const to = centre(await box(targetCell));
  await page.mouse.move(start.x, start.y);
  await page.mouse.down();
  await page.mouse.move(start.x + 8, start.y + 8);
  await page.mouse.move(to.x, to.y, { steps: 15 });
  await page.mouse.move(to.x + 2, to.y);
}

/** The requests of the page that change something, as «METHOD path», from now on. */
function recordWrites(page: Page): string[] {
  const writes: string[] = [];
  page.on('request', (request) => {
    const { pathname } = new URL(request.url());
    if (request.method() !== 'GET' && pathname.includes('/api/organizations/')) {
      writes.push(`${request.method()} ${pathname}`);
    }
  });
  return writes;
}

/** The bodies of the creates (`POST …/workitems`) the page sends, from now on. */
function recordCreates(page: Page): Record<string, unknown>[] {
  const posts: Record<string, unknown>[] = [];
  page.on('request', (request) => {
    if (request.method() === 'POST' && new URL(request.url()).pathname.endsWith('/workitems')) {
      posts.push(request.postDataJSON() as Record<string, unknown>);
    }
  });
  return posts;
}

/** The three channels of a computed `rgb(…)` / `rgba(…)`. */
function channels(colour: string): [number, number, number] {
  const found = colour.match(/[\d.]+/g)?.map(Number);
  if (!found || found.length < 3) throw new Error(`Not a colour: ${colour}`);
  return [found[0], found[1], found[2]];
}

const isGrey = (colour: string) => {
  const [r, g, b] = channels(colour);
  return Math.max(r, g, b) - Math.min(r, g, b) <= 12;
};
/**
 * The colours the human chose for the look (status.md D-10), as a computed style
 * reads them — exact, because the two colour questions are answered: the
 * reference's type colours and «Новые» / «Завершённые» dots panel-wide, its blue
 * as the accent, its pale cross for the row «+». «В работе» and the idle level
 * button are the theme's own values.
 */
const D10 = {
  accent: 'rgb(0, 120, 212)',
  rowAdd: 'rgb(138, 136, 134)',
  dotNew: 'rgb(178, 178, 178)',
  dotInProgress: 'rgb(0, 90, 158)',
  dotClosed: 'rgb(51, 153, 51)',
  epic: 'rgb(224, 108, 0)',
  feature: 'rgb(119, 59, 147)',
  story: 'rgb(0, 152, 199)',
  bug: 'rgb(204, 41, 61)',
  task: 'rgb(164, 136, 10)',
  levelButtonIdle: 'rgb(97, 97, 97)',
  levelButtonActive: 'rgb(36, 36, 36)',
} as const;
/** Not white, and a light grey: a row's hover. */
const isLightGrey = (colour: string) =>
  isGrey(colour) && channels(colour)[0] >= 230 && colour !== 'rgb(255, 255, 255)';
/** A light blue: a selected row. */
const isLightBlue = (colour: string) => {
  const [r, g, b] = channels(colour);
  return b >= 240 && b - r >= 10 && g >= 200;
};

/** The first painted background from `locator` up through its ancestors. */
function paint(locator: Locator): Promise<string> {
  return locator.evaluate((element) => {
    for (let node: Element | null = element; node; node = node.parentElement) {
      const colour = getComputedStyle(node).backgroundColor;
      if (colour !== 'rgba(0, 0, 0, 0)' && colour !== 'transparent') return colour;
    }
    return 'rgb(255, 255, 255)';
  });
}

/** One computed style property of an element. */
function style(locator: Locator, property: string): Promise<string> {
  return locator.evaluate(
    (element, name) => getComputedStyle(element).getPropertyValue(name),
    property,
  );
}

/** The distance from a cell's left edge to its text, in px: small for a left-aligned text. */
function leftGap(cell: Locator): Promise<number> {
  return cell.evaluate((element) => {
    const range = document.createRange();
    range.selectNodeContents(element);
    return range.getBoundingClientRect().left - element.getBoundingClientRect().left;
  });
}

/** A header drawn cut with an ellipsis: some element in it clips its text and says so. */
function drawnCut(header: Locator): Promise<boolean> {
  return header.evaluate((element) =>
    [element, ...Array.from(element.querySelectorAll('*'))].some(
      (node) =>
        getComputedStyle(node).textOverflow === 'ellipsis' && node.scrollWidth > node.clientWidth,
    ),
  );
}

/** A row drawn framed: a shadow, an outline or a border on the row or on one of its cells (design § 4.6). */
function framed(row: Locator): Promise<boolean> {
  return row.evaluate((tr) =>
    [tr, ...Array.from(tr.querySelectorAll(':scope > td, :scope > th'))].some((node) => {
      const found = getComputedStyle(node);
      return (
        found.boxShadow !== 'none' ||
        (found.outlineStyle !== 'none' && found.outlineWidth !== '0px') ||
        found.borderTopWidth !== '0px'
      );
    }),
  );
}

/**
 * The element that scrolls the table: its nearest ancestor with `overflow:
 * auto` or `scroll` (the card, design § 4.2). Tagged in the page so a locator
 * can address it.
 */
async function scrollerOf(table: Locator): Promise<Locator> {
  await table.evaluate((element) => {
    let node: HTMLElement | null = element.parentElement;
    while (node) {
      const { overflowX, overflowY } = getComputedStyle(node);
      if (/(auto|scroll)/.test(overflowX) || /(auto|scroll)/.test(overflowY)) break;
      node = node.parentElement;
    }
    (node ?? document.documentElement).setAttribute('data-e2e-scroller', 'true');
  });
  return table.page().locator('[data-e2e-scroller="true"]');
}

test.describe('26. the backlog tree editing (feat-027)', () => {
  test.use({ viewport: { width: 1440, height: 900 } });
  test.describe.configure({ timeout: 120_000 });

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

  test('26.1 «Эпики»: the head as the reference has it, the eight columns in its order with none of «ID», «Родитель», «Исполнитель», «Приоритет», «Действия», «Порядок» on top-level rows only, the planning values and blanks per type, the title cell, the state dot; «Канбан» has the same head (US-1)', async ({
    page,
  }) => {
    await signIn(page, { returnPath: '/home' });
    const { firstTeam, prefix, teamPrefix, seed } = await fixture(page);
    const { epic, reports, sso, byLink, byCode, letter, expiry, lost, device, template } =
      await seed();

    await opened(page, `${teamPrefix}/backlogs/epics`);
    await expandToBottom(page);
    await expect.poll(() => outline(page)).toHaveLength(10);
    const table = backlogTable(page);

    await test.step('the head: the team as the heading, its drop-down, one tab, the level at the right, the two buttons', async () => {
      const heading = page.getByRole('heading', { level: 1, name: 'Платформа', exact: true });
      await expect(heading).toBeVisible();
      await expect(page).toHaveTitle('Бэклоги — Mustask');

      const teams = teamButton(page);
      await expect(teams).toBeVisible();
      await expect(teams).toHaveAttribute('aria-haspopup', 'menu');
      const headingBox = await box(heading);
      const teamsBox = await box(teams);
      expect(teamsBox.x).toBeGreaterThanOrEqual(headingBox.x + headingBox.width - 1);
      near(centre(teamsBox).y, centre(headingBox).y, 12);

      // One tab, selected and underlined, flush with the table's left edge.
      const tabs = pageTabs(page).getByRole('tab');
      await expect(tabs).toHaveCount(1);
      const tab = pageTabs(page).getByRole('tab', { name: 'Бэклог', exact: true });
      await expect(tab).toHaveAttribute('aria-selected', 'true');
      const tabBox = await box(tab);
      near(tabBox.x, (await box(table)).x, 3);
      const indicator = page.locator('.MuiTabs-indicator');
      await expect(indicator).toBeVisible();
      const indicatorBox = await box(indicator);
      near(indicatorBox.width, tabBox.width, 3);
      near(indicatorBox.height, 2, 1);
      expect(await style(indicator, 'background-color')).toBe(D10.accent);
      // The tab row is 40 px, the label 8 px above the underline (V-2): the head stays the 132 px
      // the card's offset is built on, so the page does not scroll for it.
      near(tabBox.height, 40, 1);

      // The level drop-down: «Эпики» with a chevron, at the right end of the tab row.
      const level = levelButton(page);
      await expect(level).toHaveAccessibleName('Уровень: Эпики');
      await expect(level).toHaveText('Эпики');
      await expect(level).toHaveAttribute('aria-haspopup', 'menu');
      await expect(level.locator('svg')).toHaveCount(1);
      const levelBox = await box(level);
      expect(levelBox.x).toBeGreaterThan(tabBox.x + tabBox.width);
      expect(centre(levelBox).y).toBeGreaterThan(tabBox.y);
      expect(centre(levelBox).y).toBeLessThan(tabBox.y + tabBox.height);

      // The primary: a leading plus, no trailing chevron, blue; the secondary: grey, with an icon.
      // «Эпики» has one type, so mod-003's create opens its dialog at once and the button
      // announces no menu; that it does where the level has several types («Истории») is
      // read in the next step.
      const create = createButton(page);
      await expect(create.locator('svg')).toHaveCount(1);
      expect(await style(create, 'background-color')).toBe(D10.accent);
      const kanban = page.getByRole('link', { name: 'Показать канбан', exact: true });
      await expect(kanban).toHaveAttribute('href', new RegExp(`${teamPrefix}/boards/epics$`));
      await expect(kanban.locator('svg')).toHaveCount(1);
      const kanbanColour = await style(kanban, 'background-color');
      expect(isGrey(kanbanColour) && kanbanColour !== 'rgb(255, 255, 255)').toBe(true);

      // Top right: on the heading's row, the primary left of the secondary, which ends at the table's right edge.
      const createBox = await box(create);
      const kanbanBox = await box(kanban);
      expect(createBox.x + createBox.width).toBeLessThan(kanbanBox.x);
      near(centre(createBox).y, centre(headingBox).y, 16);
      near(centre(kanbanBox).y, centre(headingBox).y, 16);
      const tableBox = await box(table);
      near(kanbanBox.x + kanbanBox.width, tableBox.x + tableBox.width, 4);
      near(levelBox.x + levelBox.width, tableBox.x + tableBox.width, 12);

      // The team drop-down lists the project's teams, the current one marked.
      await teams.click();
      const teamMenu = page.getByRole('menu', { name: 'Команды проекта' });
      await expect(
        teamMenu.getByRole('menuitem', { name: 'Платформа', exact: true }),
      ).toHaveAttribute('aria-current', 'page');
      await expect(
        teamMenu.getByRole('menuitem', { name: firstTeam.name, exact: true }),
      ).toBeVisible();
      await page.keyboard.press('Escape');
      await expect(teamMenu).toHaveCount(0);
      await expect(teams).toBeFocused();
    });

    await test.step('the headers in the reference order, none of the columns D-7 left out, the same on every level', async () => {
      for (const level of ['epics', 'features', 'stories']) {
        if (level !== 'epics') await opened(page, `${teamPrefix}/backlogs/${level}`);
        const headers = backlogTable(page).getByRole('columnheader');
        await expect(headers).toHaveCount(HEADERS.length);
        for (const [index, name] of HEADERS.entries()) {
          await expect(headers.nth(index)).toHaveAccessibleName(name);
        }
        if (level === 'stories') {
          // Two types on this level (a story, a bug): the primary opens mod-003's picker, so it
          // announces a menu (collapsed) — and still draws its plus alone, no trailing chevron.
          const create = createButton(page);
          await expect(create).toHaveAttribute('aria-haspopup', 'menu');
          await expect(create).toHaveAttribute('aria-expanded', 'false');
          await expect(create.locator('svg')).toHaveCount(1);
        }
        for (const gone of [
          'ID',
          'Родитель',
          'Исполнитель',
          'Приоритет',
          'Действия',
          'Перемещение',
        ]) {
          await expect(
            backlogTable(page).getByRole('columnheader', { name: gone, exact: true }),
          ).toHaveCount(0);
        }
      }
      await opened(page, `${teamPrefix}/backlogs/epics`);
      await expandToBottom(page);

      // Left to right in that order; the two long labels are drawn cut, the rest whole.
      const headers = table.getByRole('columnheader');
      let previous = -Infinity;
      for (let index = 0; index < HEADERS.length; index += 1) {
        const { x } = await box(headers.nth(index));
        expect(x, HEADERS[index]).toBeGreaterThan(previous);
        previous = x;
      }
      for (const name of ['Трудоёмкость', 'Бизнес-ценность']) {
        const header = table.getByRole('columnheader', { name, exact: true });
        expect(await drawnCut(header), `${name} is drawn cut`).toBe(true);
        await expect(header).toHaveAttribute('title', name);
      }
      for (const name of ['Порядок', 'Тип', 'Заголовок', 'Статус', 'Вид ценности']) {
        expect(
          await drawnCut(table.getByRole('columnheader', { name, exact: true })),
          `${name} is drawn whole`,
        ).toBe(false);
      }

      // The hierarchy header holds the two buttons, each with its name as tooltip.
      const hierarchy = headers.first();
      await expect(hierarchy.getByRole('button')).toHaveCount(2);
      await expandLevel(page).hover();
      await expect(
        page.getByRole('tooltip').filter({ hasText: 'Развернуть на один уровень' }),
      ).toBeVisible();
      await collapseLevel(page).hover();
      await expect(
        page.getByRole('tooltip').filter({ hasText: 'Свернуть на один уровень' }),
      ).toBeVisible();
    });

    await test.step('«Порядок» on top-level rows only; «Тип» as plain text; the planning values and blanks', async () => {
      await expect(cellOf(page, epic.identifier, 'order')).toHaveText('1');
      await expect(cellOf(page, reports.identifier, 'order')).toHaveText('2');
      for (const nested of [sso, byLink, byCode, letter, expiry, lost, device, template]) {
        await expect(cellOf(page, nested.identifier, 'order'), nested.identifier).toHaveText('');
      }

      const values: [WorkItemDto, string, string, string, string][] = [
        [epic, 'Эпик', '20', '300', 'Бизнес'],
        [reports, 'Эпик', '', '', 'Архитектура'],
        [sso, 'Фича', '2,5', '', 'Бизнес'],
        [byLink, 'Фича', '', '', 'Бизнес'],
        [letter, 'Пользовательская история', '', '', 'Бизнес'],
        [lost, 'Баг', '', '', ''],
        [template, 'Задача', '', '', ''],
      ];
      for (const [item, type, effort, businessValue, valueArea] of values) {
        const who = item.identifier;
        await expect(cellOf(page, who, 'type'), `${who} type`).toHaveText(type);
        await expect(
          cellOf(page, who, 'type').locator('svg'),
          `${who} type has no icon`,
        ).toHaveCount(0);
        await expect(cellOf(page, who, 'effort'), `${who} effort`).toHaveText(effort);
        await expect(cellOf(page, who, 'businessValue'), `${who} value`).toHaveText(businessValue);
        await expect(cellOf(page, who, 'valueArea'), `${who} value area`).toHaveText(valueArea);
      }

      // The two numbers and their headers are left-aligned, as the reference draws them (D-10; the
      // review's V-7): the text starts at the cell's padding. Right-aligned in a 64 px column the
      // numbers would start 40 px in.
      expect(await leftGap(cellOf(page, epic.identifier, 'effort'))).toBeLessThan(12);
      expect(await leftGap(cellOf(page, epic.identifier, 'businessValue'))).toBeLessThan(12);
      for (const label of ['Трудоёмкость', 'Бизнес-ценность']) {
        expect(
          await leftGap(table.getByRole('columnheader', { name: label, exact: true })),
          label,
        ).toBeLessThan(12);
      }
    });

    await test.step('the title cell: the chevron, the type icon, the plain title; the identifier is not drawn', async () => {
      const link = (item: WorkItemDto) =>
        cellOf(page, item.identifier, 'title').getByRole('link', { name: item.title, exact: true });
      await expect(link(epic)).toHaveAttribute(
        'href',
        new RegExp(`/workitems/${epic.identifier}$`),
      );
      await expect(link(epic)).toHaveAttribute('title', `${epic.identifier}: ${epic.title}`);
      await expect(link(epic)).toHaveAccessibleDescription(
        `${epic.identifier}. Уровень вложенности: 1.`,
      );
      await expect(link(sso)).toHaveAccessibleDescription(
        `${sso.identifier}. Уровень вложенности: 2.`,
      );
      await expect(link(lost)).toHaveAccessibleDescription(
        `${lost.identifier}. Уровень вложенности: 4.`,
      );
      // Plain text: not underlined at rest, the colour of the text beside it, not a link blue.
      expect(await style(link(epic), 'text-decoration-line')).toBe('none');
      expect(await style(link(epic), 'color')).toBe(
        await style(cellOf(page, epic.identifier, 'type'), 'color'),
      );

      // A chevron where there are children; none, and an empty box, where there are not.
      await expect(
        cellOf(page, epic.identifier, 'title').getByRole('button', {
          name: `Свернуть ${epic.identifier}`,
          exact: true,
        }),
      ).toHaveAttribute('aria-expanded', 'true');
      await expect(
        cellOf(page, reports.identifier, 'title').getByRole('button', {
          name: /^(Развернуть|Свернуть) /,
        }),
      ).toHaveCount(0);

      // Each type has an icon of its own, 16 px, between the chevron and the title.
      const shapes = new Set<string>();
      // …in the reference's own colours (D-10): crown, trophy, two-page book, bug, clipboard.
      const colours: [WorkItemDto, string][] = [
        [epic, D10.epic],
        [sso, D10.feature],
        [letter, D10.story],
        [lost, D10.bug],
        [template, D10.task],
      ];
      for (const [item, colour] of colours) {
        const icon = iconIn(cellOf(page, item.identifier, 'title'));
        await expect(icon, item.identifier).toHaveCount(1);
        expect(await style(icon, 'color'), `${item.identifier} icon colour`).toBe(colour);
        shapes.add(await icon.evaluate((svg) => svg.innerHTML));
        const iconBox = await box(icon);
        near(iconBox.width, 16, 1);
        near(iconBox.height, 16, 1);
        const linkBox = await box(link(item));
        expect(iconBox.x + iconBox.width).toBeLessThanOrEqual(linkBox.x + 1);
      }
      expect(shapes.size, 'five types, five icons').toBe(5);
      const chevronBox = await box(
        page.getByRole('button', { name: `Свернуть ${epic.identifier}`, exact: true }),
      );
      expect(chevronBox.x + chevronBox.width).toBeLessThanOrEqual(
        (await box(iconIn(cellOf(page, epic.identifier, 'title')))).x + 1,
      );

      // One depth is one indent, the same at every depth, and the icons of a depth line up whether or not the row has a chevron.
      const iconX = async (item: WorkItemDto) =>
        (await box(iconIn(cellOf(page, item.identifier, 'title')))).x;
      const [x0, x1, x2, x3] = [
        await iconX(epic),
        await iconX(sso),
        await iconX(byCode),
        await iconX(lost),
      ];
      expect(x1 - x0).toBeGreaterThanOrEqual(12);
      expect(x1 - x0).toBeLessThanOrEqual(20);
      near(x2 - x1, x1 - x0, 1);
      near(x3 - x2, x1 - x0, 1);
      near(await iconX(reports), x0, 1);
      near(await iconX(device), x1, 1);
      near(await iconX(letter), x2, 1);

      // «⋮» stands at the title cell's right edge.
      const titleBox = await box(cellOf(page, epic.identifier, 'title'));
      const moreBox = await box(
        page.getByRole('button', { name: `Ещё действия: ${epic.identifier}`, exact: true }),
      );
      expect(titleBox.x + titleBox.width - (moreBox.x + moreBox.width)).toBeGreaterThanOrEqual(0);
      expect(titleBox.x + titleBox.width - (moreBox.x + moreBox.width)).toBeLessThanOrEqual(10);
    });

    await test.step('the state is a dot by its category and the name', async () => {
      await expect(cellOf(page, epic.identifier, 'state')).toHaveText('Новая');
      await expect(cellOf(page, device.identifier, 'state')).toHaveText('Активна');
      await expect(cellOf(page, lost.identifier, 'state')).toHaveText('Закрыта');
      const dot = (item: WorkItemDto) =>
        cellOf(page, item.identifier, 'state').locator('.WorkTrackingCategoryMarker');
      for (const item of [epic, device, lost]) await expect(dot(item)).toHaveCount(1);
      expect(await style(dot(epic), 'background-color')).toBe(D10.dotNew);
      expect(await style(dot(device), 'background-color')).toBe(D10.dotInProgress);
      expect(await style(dot(lost), 'background-color')).toBe(D10.dotClosed);
      near((await box(dot(epic))).width, 8, 1);
    });

    await test.step('«Канбан» has the same head, a grey way back and no primary button', async () => {
      await page.getByRole('link', { name: 'Показать канбан', exact: true }).click();
      await expect(page).toHaveURL(new RegExp(`${teamPrefix}/boards/epics$`));
      const heading = page.getByRole('heading', { level: 1, name: 'Платформа', exact: true });
      await expect(heading).toBeVisible();
      await expect(page).toHaveTitle('Канбан — Mustask');
      await expect(pageTabs(page).getByRole('tab')).toHaveCount(1);
      await expect(
        pageTabs(page).getByRole('tab', { name: 'Канбан', exact: true }),
      ).toHaveAttribute('aria-selected', 'true');
      await expect(levelButton(page)).toHaveAccessibleName('Уровень: Эпики');
      await expect(levelButton(page)).toHaveText('Эпики');
      await expect(
        page.getByRole('button', { name: 'Новый рабочий элемент', exact: true }),
      ).toHaveCount(0);
      const back = page.getByRole('link', { name: 'Показать бэклог', exact: true });
      await expect(back).toHaveAttribute('href', new RegExp(`${teamPrefix}/backlogs/epics$`));
      const backColour = await style(back, 'background-color');
      expect(isGrey(backColour) && backColour !== 'rgb(255, 255, 255)').toBe(true);
      const levelBox = await box(levelButton(page));
      const tabBox = await box(pageTabs(page).getByRole('tab'));
      expect(levelBox.x).toBeGreaterThan(tabBox.x + tabBox.width);
      const backBox = await box(back);
      near(levelBox.x + levelBox.width, backBox.x + backBox.width, 12);
      near(centre(backBox).y, centre(await box(heading)).y, 16);

      // The team drop-down moves to the same page of another team of the project, and the shell follows.
      await teamButton(page).click();
      await page
        .getByRole('menu', { name: 'Команды проекта' })
        .getByRole('menuitem', { name: firstTeam.name, exact: true })
        .click();
      await expect(page).toHaveURL(new RegExp(`${prefix}/teams/${firstTeam.id}/boards/epics$`));
      await expect(
        page.getByRole('heading', { level: 1, name: firstTeam.name, exact: true }),
      ).toBeVisible();
      await expect(switcher(page).getByRole('button', { name: /^Команда:/ })).toHaveAttribute(
        'aria-label',
        `Команда: ${firstTeam.name}`,
      );
    });
  });

  test('26.2 «Эпики»: opens expanded as far as the data go; «Свернуть на один уровень» three times leaves the epics and «Развернуть на один уровень» three times reaches the task, with no request; «Истории»: a bug beneath its story only (US-2, US-3)', async ({
    page,
  }) => {
    await signIn(page, { returnPath: '/home' });
    const { teamPrefix, seed } = await fixture(page);
    const {
      epic,
      reports,
      sso,
      byLink,
      byCode,
      letter,
      expiry,
      lost,
      device,
      template,
      audit,
      smtp,
    } = await seed();
    const [e, r, s, l, c, m, x, b, d, t] = [
      epic,
      reports,
      sso,
      byLink,
      byCode,
      letter,
      expiry,
      lost,
      device,
      template,
    ].map((item) => item.identifier);
    const whole = [
      e,
      `  ${s}`,
      `    ${c}`,
      `  ${l}`,
      `    ${m}`,
      `      ${b}`,
      `      ${t}`,
      `    ${x}`,
      `  ${d}`,
      r,
    ];

    // The first state: every row with a child is expanded, the other team's feature and task stand beneath no row.
    await opened(page, `${teamPrefix}/backlogs/epics`);
    await expect(levelButton(page)).toHaveAccessibleName('Уровень: Эпики');
    await expect.poll(() => outline(page)).toEqual(whole);
    expect(whole).not.toContain(audit.identifier);
    expect(whole).not.toContain(smtp.identifier);
    await expect(rowOf(page, template.identifier)).toBeVisible();
    await expect(expandLevel(page)).toHaveAttribute('aria-disabled', 'true');
    await expect(collapseLevel(page)).not.toHaveAttribute('aria-disabled', 'true');
    await page.waitForLoadState('networkidle');
    const asked: string[] = [];
    page.on('request', (request) => {
      const { pathname } = new URL(request.url());
      if (/\/backlogs\/|\/workitems/.test(pathname)) asked.push(`${request.method()} ${pathname}`);
    });

    // «Свернуть на один уровень», three times: the deepest open level closes each time.
    await collapseLevel(page).click();
    await expect
      .poll(() => outline(page))
      .toEqual([e, `  ${s}`, `    ${c}`, `  ${l}`, `    ${m}`, `    ${x}`, `  ${d}`, r]);
    await expect(shownRows(page)).toHaveText('Показано строк: 8.');
    await expect(collapseLevel(page)).toBeFocused();
    await expect(
      page.getByRole('button', { name: `Развернуть ${m}`, exact: true }),
    ).toHaveAttribute('aria-expanded', 'false');
    await expect(page.getByRole('button', { name: `Свернуть ${l}`, exact: true })).toHaveAttribute(
      'aria-expanded',
      'true',
    );

    await collapseLevel(page).click();
    await expect.poll(() => outline(page)).toEqual([e, `  ${s}`, `  ${l}`, `  ${d}`, r]);
    await expect(shownRows(page)).toHaveText('Показано строк: 5.');

    await collapseLevel(page).click();
    await expect.poll(() => outline(page)).toEqual([e, r]);
    await expect(shownRows(page)).toHaveText('Показано строк: 2.');
    await expect(collapseLevel(page)).toHaveAttribute('aria-disabled', 'true');
    await expect(collapseLevel(page)).toBeFocused();
    await expect(expandLevel(page)).not.toHaveAttribute('aria-disabled', 'true');
    await expect(cellOf(page, epic.identifier, 'order')).toHaveText('1');
    await expect(cellOf(page, reports.identifier, 'order')).toHaveText('2');

    // «Развернуть на один уровень», three times — Enter, Space, a click — down to the task.
    await expandLevel(page).focus();
    await page.keyboard.press('Enter');
    await expect.poll(() => outline(page)).toEqual([e, `  ${s}`, `  ${l}`, `  ${d}`, r]);
    await expect(shownRows(page)).toHaveText('Показано строк: 5.');
    await expect(expandLevel(page)).toBeFocused();

    await page.keyboard.press('Space');
    await expect
      .poll(() => outline(page))
      .toEqual([e, `  ${s}`, `    ${c}`, `  ${l}`, `    ${m}`, `    ${x}`, `  ${d}`, r]);
    await expect(shownRows(page)).toHaveText('Показано строк: 8.');

    await expandLevel(page).click();
    await expect.poll(() => outline(page)).toEqual(whole);
    await expect(shownRows(page)).toHaveText('Показано строк: 10.');
    await expect(rowOf(page, template.identifier)).toBeVisible();

    // Nothing left to expand: unavailable, still focusable, activating it changes nothing.
    await expect(expandLevel(page)).toHaveAttribute('aria-disabled', 'true');
    await expect(expandLevel(page)).toBeFocused();
    await page.keyboard.press('Enter');
    await expect(shownRows(page)).toHaveText('Показано строк: 10.');
    expect(await outline(page)).toEqual(whole);

    // The buttons changed only what is shown: the server was asked nothing.
    expect(asked).toEqual([]);

    // «Истории»: the bug stands beneath its story only, not at the top.
    await opened(page, `${teamPrefix}/backlogs/stories`);
    await expect(levelButton(page)).toHaveAccessibleName('Уровень: Истории');
    await expandRows(page, letter);
    await expect.poll(() => outline(page)).toEqual([m, `  ${b}`, `  ${t}`, x, d]);
    const stories = await lines(page);
    expect(stories.filter((line) => line.tier === 0).map((line) => line.order)).toEqual([
      '1',
      '2',
      '3',
    ]);
    expect(childrenOf(stories, m)).toEqual([b, t]);
    await expect(cellOf(page, lost.identifier, 'order')).toHaveText('');
    await expect(page.getByRole('button', { name: `Развернуть ${x}`, exact: true })).toHaveCount(0);
    await expect(page.getByRole('button', { name: `Свернуть ${x}`, exact: true })).toHaveCount(0);
    await expect(page.getByRole('button', { name: `Развернуть ${d}`, exact: true })).toHaveCount(0);

    // Collapsed, the bug shows nowhere: it is no top-level row of «Истории».
    await page.getByRole('button', { name: `Свернуть ${m}`, exact: true }).click();
    await expect.poll(() => outline(page)).toEqual([m, x, d]);
  });

  test('26.3 «Эпики»: «Добавить: Фича» on an epic opens the inline row at once, Escape or an empty Enter cancels it; «Добавить дочерний элемент» on a story → «Добавить: Задача», a title over 255 characters refused at the field, a title typed, Enter: one create with the parent, the task last beneath the story, selected, in the story’s area (US-4)', async ({
    page,
  }) => {
    await signIn(page, { returnPath: '/home' });
    const { prefix, teamPrefix, typeOf, seed } = await fixture(page);
    const { epic, reports, sso, byLink, letter, expiry, lost, template, device } = await seed();

    await opened(page, `${teamPrefix}/backlogs/epics`);
    await expandToBottom(page);
    await page.waitForLoadState('networkidle');
    const writes = recordWrites(page);
    const posts = recordCreates(page);
    const control = (item: WorkItemDto, name: string) =>
      rowOf(page, item.identifier).getByRole('button', { name, exact: true });

    // Each row offers the children its type takes: an epic one entry, a feature and a story a menu; a task and a bug none.
    const addFeature = control(epic, `Добавить: Фича (${epic.identifier})`);
    await expect(addFeature).toBeVisible();
    await expect(addFeature).not.toHaveAttribute('aria-haspopup', 'menu');
    await expect(control(reports, `Добавить: Фича (${reports.identifier})`)).toBeVisible();
    for (const parent of [sso, byLink, letter, device]) {
      await expect(
        control(parent, `Добавить дочерний элемент (${parent.identifier})`),
        parent.identifier,
      ).toHaveAttribute('aria-haspopup', 'menu');
    }
    for (const childless of [template, expiry, lost]) {
      await expect(
        rowOf(page, childless.identifier).getByRole('button', { name: /^Добавить/ }),
        childless.identifier,
      ).toHaveCount(0);
    }

    // An epic collapsed: «Добавить: Фича» opens the row at once, expanding the epic, directly beneath it.
    await page.getByRole('button', { name: `Свернуть ${epic.identifier}`, exact: true }).click();
    await expect.poll(() => outline(page)).toEqual([epic.identifier, reports.identifier]);
    await addFeature.click();
    await expect(page.getByRole('menu')).toHaveCount(0);
    await expect(addRowOf(page, epic)).toHaveCount(1);
    await expect(
      rowOf(page, epic.identifier).locator('xpath=./following-sibling::tr[1]'),
    ).toHaveAttribute('data-add-row', epic.identifier);
    await expect(
      page.getByRole('button', { name: `Свернуть ${epic.identifier}`, exact: true }),
    ).toHaveAttribute('aria-expanded', 'true');
    const featureField = page.getByRole('textbox', {
      name: `Заголовок: Фича (${epic.identifier})`,
      exact: true,
    });
    await expect(featureField).toBeFocused();
    await expect(featureField).toHaveAttribute('placeholder', 'Введите заголовок');
    const featureRow = cellsOf(addRowOf(page, epic));
    await expect(addRowOf(page, epic)).toHaveAttribute('data-selected', 'true');
    // Only the add row is selected: the epic's «+» did not select its row (F-8).
    await expect(rowOf(page, epic.identifier)).not.toHaveAttribute('data-selected', 'true');
    await expect(backlogTable(page).locator('tr[data-selected="true"]')).toHaveCount(1);
    await expect(featureRow.nth(COLUMN.type)).toHaveText('Фича');
    await expect(featureRow.nth(COLUMN.order)).toHaveText('');
    await expect(featureRow.nth(COLUMN.state)).toHaveText('');
    await expect(addRowOf(page, epic).getByRole('button')).toHaveCount(0);
    // One depth deeper than the parent, with the feature's icon before the field.
    const parentIcon = iconIn(cellOf(page, epic.identifier, 'title'));
    const rowIcon = iconIn(featureRow.nth(COLUMN.title));
    await expect(rowIcon).toHaveCount(1);
    const step = (await box(rowIcon)).x - (await box(parentIcon)).x;
    expect(step).toBeGreaterThanOrEqual(12);
    expect(step).toBeLessThanOrEqual(20);

    // Escape cancels with nothing sent, focus back on the control that opened it.
    await featureField.fill('Черновик фичи');
    await page.keyboard.press('Escape');
    await expect(addRowOf(page, epic)).toHaveCount(0);
    await expect(addFeature).toBeFocused();

    // An empty Enter cancels too, with nothing sent.
    await addFeature.click();
    await expect(featureField).toBeFocused();
    await page.keyboard.press('Enter');
    await expect(addRowOf(page, epic)).toHaveCount(0);
    expect(posts).toEqual([]);
    expect(writes).toEqual([]);
    await expandToBottom(page);

    // A story offers a task and a bug; «Добавить: Задача» opens the row directly beneath the story.
    const addChild = control(letter, `Добавить дочерний элемент (${letter.identifier})`);
    await addChild.click();
    const menu = page.getByRole('menu', {
      name: `Добавить дочерний элемент (${letter.identifier})`,
    });
    await expect(menu.getByRole('menuitem')).toHaveCount(2);
    await expect(menu.getByRole('menuitem', { name: 'Добавить: Баг', exact: true })).toBeVisible();
    await menu.getByRole('menuitem', { name: 'Добавить: Задача', exact: true }).click();
    await expect(addRowOf(page, letter)).toHaveCount(1);
    await expect(
      rowOf(page, letter.identifier).locator('xpath=./following-sibling::tr[1]'),
    ).toHaveAttribute('data-add-row', letter.identifier);
    await expect(cellsOf(addRowOf(page, letter)).nth(COLUMN.type)).toHaveText('Задача');
    const field = page.getByRole('textbox', {
      name: `Заголовок: Задача (${letter.identifier})`,
      exact: true,
    });
    await expect(field).toBeFocused();

    // A title over 255 characters is refused at the field, nothing sent (mod-003's check).
    await field.fill('я'.repeat(256));
    await page.keyboard.press('Enter');
    await expect(notice(page, 'Не более 255 символов.')).toBeVisible();
    await expect(field).toHaveAttribute('aria-invalid', 'true');
    expect(posts).toEqual([]);

    // Leaving the field with a title typed keeps the row; another row's «+» opens no second row.
    await field.fill('Проверить ссылку');
    await page.getByRole('heading', { level: 1 }).click();
    await expect(addRowOf(page, letter)).toHaveCount(1);
    await expect(field).toHaveValue('Проверить ссылку');
    await addFeature.click();
    await expect(backlogTable(page).locator('tbody tr[data-add-row]')).toHaveCount(1);
    await expect(field).toBeFocused();
    expect(posts).toEqual([]);

    // Enter: one create names the type, the title and the parent — and no separate parent write.
    const answered = page.waitForResponse(
      (response) =>
        response.request().method() === 'POST' &&
        new URL(response.url()).pathname.endsWith('/workitems'),
    );
    await page.keyboard.press('Enter');
    const made = (await (await answered).json()) as WorkItemDto;
    await expect(addRowOf(page, letter)).toHaveCount(0);
    await expect(page.getByText(`Рабочий элемент ${made.identifier} создан.`)).toBeVisible();
    expect(posts).toHaveLength(1);
    expect(posts[0]).toMatchObject({
      typeId: await typeOf('TASK'),
      title: 'Проверить ссылку',
      parentId: letter.id,
    });
    expect(posts[0]).not.toHaveProperty('teamId');
    expect(typeof posts[0].requestId).toBe('string');
    expect(writes.filter((write) => write.endsWith('/parent'))).toEqual([]);

    // The story shows it last among its children, selected and in view; focus is back on the «+».
    await expect
      .poll(async () => childrenOf(await lines(page), letter.identifier))
      .toEqual([lost.identifier, template.identifier, made.identifier]);
    await expect(rowOf(page, made.identifier)).toHaveAttribute('data-selected', 'true');
    await expect(rowOf(page, made.identifier)).toBeInViewport();
    await expect(addChild).toBeFocused();

    // Its form: beneath the story, in the story's area, in the initial state.
    expect(made.area.path).toBe(letter.area.path);
    await page.goto(`${prefix}/workitems/${made.identifier}`);
    await expect(
      page.getByRole('heading', { level: 1, name: `Задача ${made.identifier}` }),
    ).toBeVisible();
    await expect(
      page
        .getByRole('region', { name: 'Связанная работа' })
        .getByRole('combobox', { name: 'Родитель' }),
    ).toHaveValue(`${letter.identifier} Письмо со ссылкой`);
    await expect(page.getByRole('combobox', { name: 'Область' })).toHaveValue(made.area.path);
    await expect(page.getByRole('combobox', { name: 'Статус' })).toContainText('Новая');
  });

  test('26.4 «Эпики»: a story dragged by its «Заголовок» cell onto a feature becomes its child; a feature dropped onto a story and a feature onto its own descendant are refused with their notices and send nothing (US-5)', async ({
    page,
  }) => {
    await signIn(page, { returnPath: '/home' });
    const { teamPrefix, seed } = await fixture(page);
    const { epic, sso, byLink, byCode, letter, device } = await seed();

    await opened(page, `${teamPrefix}/backlogs/epics`);
    await expandToBottom(page);
    await expect
      .poll(async () => childrenOf(await lines(page), epic.identifier))
      .toEqual([sso.identifier, byLink.identifier, device.identifier]);
    const writes = recordWrites(page);

    // The whole row is the drag source (D-11) and its title link is not, or the browser would drag
    // the link's address; the grip is only a hint: a dotted strip in the hierarchy cell, not a
    // column, not a drag source of its own and not a tab stop.
    const grip = rowOf(page, device.identifier).locator('[data-grip]');
    await expect(rowOf(page, device.identifier)).toHaveAttribute('draggable', 'true');
    await expect(
      cellOf(page, device.identifier, 'title').getByRole('link', { includeHidden: true }),
    ).toHaveAttribute('draggable', 'false');
    await expect(cellOf(page, device.identifier, 'hierarchy').locator('[data-grip]')).toHaveCount(
      1,
    );
    await expect(grip).not.toHaveAttribute('draggable', /./);
    await expect(grip).toHaveAttribute('aria-hidden', 'true');
    await expect(grip).toHaveAttribute('tabindex', '-1');
    await expect(grip).toHaveCSS('cursor', 'grab');

    // A story onto a feature: the drag selects the dragged row and marks it; the feature under the
    // pointer is marked as an allowed target; the parent write names the target and the parent seen.
    const parentWrite = page.waitForRequest(
      (request) =>
        request.method() === 'PUT' &&
        new URL(request.url()).pathname.endsWith(`/workitems/${device.identifier}/parent`),
    );
    // The first press is on the title itself, a link: the row is dragged, not the address.
    await dragOver(page, device.identifier, sso.identifier, 'link');
    await expect(rowOf(page, device.identifier)).toHaveAttribute('data-selected', 'true');
    await expect(rowOf(page, device.identifier)).toHaveAttribute('data-dragging', 'true');
    await expect(rowOf(page, sso.identifier)).toHaveAttribute('data-drop-target', 'allowed');
    await page.mouse.up();
    expect((await parentWrite).postDataJSON()).toEqual({
      parentId: sso.id,
      seenParentId: epic.id,
    });
    await expect(
      notice(page, `У ${device.identifier} теперь родитель ${sso.identifier}.`),
    ).toBeVisible();

    // Read again: the feature is expanded with the story beneath it; the epic let go of it; no place, no other field changed.
    await expect
      .poll(async () => childrenOf(await lines(page), sso.identifier))
      .toEqual([byCode.identifier, device.identifier]);
    const afterDrop = await lines(page);
    expect(childrenOf(afterDrop, epic.identifier)).toEqual([sso.identifier, byLink.identifier]);
    expect(afterDrop.find((line) => line.identifier === device.identifier)?.order).toBe('');
    await expect(cellOf(page, device.identifier, 'state')).toHaveText('Активна');
    await expect(
      page.getByRole('button', { name: `Ещё действия: ${device.identifier}`, exact: true }),
    ).toBeFocused();
    expect(writes).toHaveLength(1);
    expect(writes[0]).toMatch(new RegExp(`^PUT .*/workitems/${device.identifier}/parent$`));
    // The drop leaves no mark behind.
    await expect(backlogTable(page).locator('tr[data-dragging], tr[data-drop-target]')).toHaveCount(
      0,
    );

    // The item itself and its current parent are no targets: neither is marked, nothing is sent.
    await dragOver(page, device.identifier, device.identifier);
    await expect(rowOf(page, device.identifier)).toHaveAttribute('data-dragging', 'true');
    await expect(rowOf(page, device.identifier)).not.toHaveAttribute('data-drop-target', /./);
    await page.mouse.up();
    await dragOver(page, device.identifier, sso.identifier);
    await expect(rowOf(page, device.identifier)).toHaveAttribute('data-dragging', 'true');
    await expect(rowOf(page, sso.identifier)).not.toHaveAttribute('data-drop-target', /./);
    await page.mouse.up();
    expect(writes).toHaveLength(1);

    // A feature onto a story is refused for its type: the story is marked refused, nothing is sent.
    await expandRows(page, byLink);
    await dragOver(page, byLink.identifier, letter.identifier);
    await expect(rowOf(page, letter.identifier)).toHaveAttribute('data-drop-target', 'refused');
    await page.mouse.up();
    await expect(
      notice(
        page,
        'Родителем может быть только рабочий элемент того же или более высокого уровня.',
      ),
    ).toBeVisible();
    expect(writes).toHaveLength(1);
    expect(childrenOf(await lines(page), epic.identifier)).toEqual([
      sso.identifier,
      byLink.identifier,
    ]);

    // A feature onto its own descendant is refused with the path: the descendant is marked refused,
    // nothing is sent.
    await dragOver(page, sso.identifier, byCode.identifier);
    await expect(rowOf(page, byCode.identifier)).toHaveAttribute('data-drop-target', 'refused');
    await page.mouse.up();
    await expect(
      notice(
        page,
        `Нельзя выбрать родителем ${byCode.identifier}: он вложен в ${sso.identifier} (${sso.identifier} → ${byCode.identifier}).`,
      ),
    ).toBeVisible();
    expect(writes).toHaveLength(1);
    expect(childrenOf(await lines(page), sso.identifier)).toEqual([
      byCode.identifier,
      device.identifier,
    ]);
    expect(childrenOf(await lines(page), epic.identifier)).toEqual([
      sso.identifier,
      byLink.identifier,
    ]);
  });

  test('26.5 «Эпики»: every row has «Ещё действия», a top-level row’s menu moves it and a nested row’s does not; a nested bug given a story as its parent by keyboard; the dialog offers every type and no «Нет родителя» (US-6)', async ({
    page,
  }) => {
    await signIn(page, { returnPath: '/home' });
    const { teamPrefix, seed } = await fixture(page);
    const { epic, reports, byLink, letter, expiry, lost, template } = await seed();

    await opened(page, `${teamPrefix}/backlogs/epics`);
    await expandToBottom(page);
    await expect
      .poll(async () => childrenOf(await lines(page), byLink.identifier))
      .toEqual([letter.identifier, expiry.identifier]);

    // Every row has the menu, on every level.
    for (const item of [epic, reports, byLink, letter, expiry, lost, template]) {
      await expect(
        rowOf(page, item.identifier).getByRole('button', {
          name: `Ещё действия: ${item.identifier}`,
          exact: true,
        }),
        item.identifier,
      ).toHaveAttribute('aria-haspopup', 'menu');
    }

    // A top-level row: the four moves under «Переместить», disabled by position, then the parent entries.
    const top = page.getByRole('button', { name: `Ещё действия: ${epic.identifier}`, exact: true });
    await top.focus();
    await page.keyboard.press('Enter');
    const topMenu = page.getByRole('menu', { name: `Ещё действия: ${epic.identifier}` });
    await expect(topMenu.getByText('Переместить', { exact: true })).toBeVisible();
    await expect(topMenu.getByRole('menuitem')).toHaveText([
      'В начало',
      'Выше',
      'Ниже',
      'В конец',
      'Изменить родителя…',
      'Убрать родителя',
    ]);
    await expect(topMenu.getByRole('menuitem', { name: 'В начало' })).toBeDisabled();
    await expect(topMenu.getByRole('menuitem', { name: 'Выше' })).toBeDisabled();
    await expect(topMenu.getByRole('menuitem', { name: 'Ниже' })).toBeEnabled();
    await expect(topMenu.getByRole('menuitem', { name: 'В конец' })).toBeEnabled();
    await expect(topMenu.getByRole('menuitem', { name: 'Изменить родителя…' })).toBeEnabled();
    await expect(topMenu.getByRole('menuitem', { name: 'Убрать родителя' })).toBeDisabled();
    await page.keyboard.press('Escape');
    await expect(topMenu).toHaveCount(0);
    await expect(top).toBeFocused();

    // The nested bug's menu, by keyboard: Tab from its title reaches «⋮»; no moves; Enter opens on the first entry.
    const actions = page.getByRole('button', {
      name: `Ещё действия: ${expiry.identifier}`,
      exact: true,
    });
    await rowOf(page, expiry.identifier)
      .getByRole('link', { name: expiry.title, exact: true })
      .focus();
    await page.keyboard.press('Tab');
    await expect(actions).toBeFocused();
    await page.keyboard.press('Enter');
    const menu = page.getByRole('menu', { name: `Ещё действия: ${expiry.identifier}` });
    await expect(menu.getByRole('menuitem')).toHaveText(['Изменить родителя…', 'Убрать родителя']);
    await expect(menu.getByText('Переместить', { exact: true })).toHaveCount(0);
    await expect(menu.getByRole('menuitem', { name: 'Изменить родителя…' })).toBeFocused();
    await expect(menu.getByRole('menuitem', { name: 'Убрать родителя' })).toBeEnabled();
    await page.keyboard.press('Enter');

    // The dialog: the bug, the helper naming every type, the current parent preselected, nothing to save yet.
    const dialog = page.getByRole('dialog', { name: 'Изменить родителя' });
    await expect(dialog).toContainText(`Баг ${expiry.identifier} «Ссылка истекает раньше срока»`);
    const field = dialog.getByRole('combobox', { name: 'Родитель' });
    const saveButton = dialog.getByRole('button', { name: 'Сохранить' });
    await expect(field).toHaveAccessibleDescription(
      'Родителем может быть: Эпик, Фича, Пользовательская история, Баг.',
    );
    await expect(field).toHaveValue(`${byLink.identifier} Вход по ссылке`);
    await expect(saveButton).toBeDisabled();

    // The story is found by its identifier — a type the old dialog did not offer — and chosen with the arrows.
    // The field takes focus as the dialog opens; under StrictMode the dev
    // server's first focus is not to be relied on (tech-009 D-3), so it is
    // clicked first.
    await field.click();
    await page.keyboard.press('ControlOrMeta+A');
    await page.keyboard.type(letter.identifier);
    await expect(
      page.getByRole('option', { name: new RegExp(`^${letter.identifier} `) }),
    ).toBeVisible();
    await expect(page.getByRole('option', { name: 'Нет родителя' })).toHaveCount(0);
    await page.keyboard.press('ArrowDown');
    await page.keyboard.press('Enter');
    await expect(field).toHaveValue(`${letter.identifier} Письмо со ссылкой`);
    await expect(saveButton).toBeEnabled();

    // Tab to «Сохранить» and Enter: one parent write, the old parent seen.
    const writes = recordWrites(page);
    const parentWrite = page.waitForRequest(
      (request) =>
        request.method() === 'PUT' &&
        new URL(request.url()).pathname.endsWith(`/workitems/${expiry.identifier}/parent`),
    );
    await tabTo(page, saveButton);
    await page.keyboard.press('Enter');
    expect((await parentWrite).postDataJSON()).toEqual({
      parentId: letter.id,
      seenParentId: byLink.id,
    });

    await expect(dialog).toHaveCount(0);
    await expect(
      notice(page, `У ${expiry.identifier} теперь родитель ${letter.identifier}.`),
    ).toBeVisible();
    expect(writes).toEqual([
      expect.stringMatching(new RegExp(`^PUT .*/workitems/${expiry.identifier}/parent$`)),
    ]);

    // Read again: the bug stands beneath the story, before its task; the feature let go of it; focus is back.
    await expect
      .poll(async () => childrenOf(await lines(page), letter.identifier))
      .toEqual([expiry.identifier, lost.identifier, template.identifier]);
    expect(childrenOf(await lines(page), byLink.identifier)).toEqual([letter.identifier]);
    await expect(actions).toBeFocused();
  });

  test('26.6 the look the reference sets: Segoe UI, 36 px rows with no lines, small grey headers with their separators, hover, selection and keyboard focus showing the row controls, a sticky header, no sideways scroll at 1 440 px and a scroll inside the card at 1 280 px (US-1, US-3, US-5; design § 2, § 4.2..§ 4.6)', async ({
    page,
  }) => {
    await signIn(page, { returnPath: '/home' });
    const { teamPrefix, seed } = await fixture(page);
    const { epic, reports, sso, byLink, byCode, letter, expiry, lost, device, template } =
      await seed();
    const everyRow = [epic, sso, byCode, byLink, letter, lost, template, expiry, device, reports];

    await opened(page, `${teamPrefix}/backlogs/epics`);
    await expandToBottom(page);
    await expect.poll(() => outline(page)).toHaveLength(everyRow.length);
    const table = backlogTable(page);
    const row = (item: WorkItemDto) => rowOf(page, item.identifier);
    const addOf = (item: WorkItemDto, name: string) =>
      row(item).getByRole('button', { name, exact: true });
    const moreOf = (item: WorkItemDto) =>
      row(item).getByRole('button', { name: `Ещё действия: ${item.identifier}`, exact: true });
    const gripOf = (item: WorkItemDto) => row(item).locator('[data-grip]');

    await test.step('the font is the system stack, Segoe UI first, with no Roboto file', async () => {
      const samples = [
        page.getByRole('heading', { level: 1 }),
        table.getByRole('columnheader', { name: 'Заголовок', exact: true }),
        cellOf(page, epic.identifier, 'type'),
        levelButton(page),
        createButton(page),
      ];
      for (const sample of samples) {
        expect(await style(sample, 'font-family')).toMatch(/^"?Segoe UI"?/);
      }
      expect(
        await page.evaluate(
          () => Array.from(document.fonts).filter((face) => /roboto/i.test(face.family)).length,
        ),
      ).toBe(0);
      // Semibold where mod-015 had 500, which Segoe UI does not have; the body text Regular.
      expect(await style(page.getByRole('heading', { level: 1 }), 'font-weight')).toBe('600');
      expect(await style(cellOf(page, epic.identifier, 'type'), 'font-weight')).toBe('400');
      expect(
        await style(
          pageTabs(page).getByRole('tab', { name: 'Бэклог', exact: true }),
          'font-weight',
        ),
      ).toBe('600');
    });

    await test.step('rows are compact, white and quiet; the header is small grey labels over one line', async () => {
      const heights = await table
        .locator('tbody tr[data-identifier]')
        .evaluateAll((rows) => rows.map((tr) => tr.getBoundingClientRect().height));
      expect(heights).toHaveLength(everyRow.length);
      for (const height of heights) near(height, 36, 2);
      for (const item of everyRow) {
        const borders = await cellsOf(row(item)).evaluateAll((cells) =>
          cells.map((cell) => getComputedStyle(cell).borderBottomWidth),
        );
        expect(new Set(borders), `${item.identifier}: no line between rows`).toEqual(
          new Set(['0px']),
        );
      }
      expect(await paint(cellOf(page, epic.identifier, 'title'))).toBe('rgb(255, 255, 255)');
      expect(await paint(cellOf(page, letter.identifier, 'title'))).toBe('rgb(255, 255, 255)');
      expect(await style(cellOf(page, epic.identifier, 'type'), 'font-size')).toBe('13px');

      const headers = table.getByRole('columnheader');
      const header = headers.nth(COLUMN.title);
      const headerBox = await box(header);
      expect(headerBox.height).toBeGreaterThanOrEqual(36);
      expect(headerBox.height).toBeLessThanOrEqual(44);
      expect(await style(header, 'font-size')).toBe('12px');
      expect(await style(header, 'border-bottom-width')).toBe('1px');
      expect(isGrey(await style(header, 'color'))).toBe(true);
      expect(channels(await style(header, 'color'))[0]).toBeGreaterThan(
        channels(await style(cellOf(page, epic.identifier, 'type'), 'color'))[0],
      );
      expect(await paint(header)).toBe('rgb(255, 255, 255)');

      // The static separators: after «Тип» … «Бизнес-ценность», none after the hierarchy column, «Порядок» or the last header.
      const separators = await headers.evaluateAll((cells) =>
        cells.map((cell) => {
          const after = getComputedStyle(cell, '::after');
          return after.content === 'none' || after.content === 'normal'
            ? null
            : `${after.width} x ${after.height}`;
        }),
      );
      expect(separators).toEqual([
        null,
        null,
        '1px x 20px',
        '1px x 20px',
        '1px x 20px',
        '1px x 20px',
        '1px x 20px',
        null,
      ]);
    });

    await test.step('the hierarchy header: two 24 px square outlined buttons 4 px apart', async () => {
      const plus = await box(expandLevel(page));
      const minus = await box(collapseLevel(page));
      for (const found of [plus, minus]) {
        near(found.width, 24, 1);
        near(found.height, 24, 1);
      }
      near(minus.x - (plus.x + plus.width), 4, 1);
      expect(await style(expandLevel(page), 'border-top-width')).toBe('1px');
      expect(await style(collapseLevel(page), 'border-top-width')).toBe('1px');
      // The tree is fully expanded, so «+» is idle and «−» active — a readable pair, not a pale «+»
      // beside a black «−» (V-3).
      expect(await style(expandLevel(page), 'border-top-color')).toBe(D10.levelButtonIdle);
      expect(await style(expandLevel(page), 'color')).toBe(D10.levelButtonIdle);
      expect(await style(collapseLevel(page), 'border-top-color')).toBe(D10.levelButtonActive);
    });

    await test.step('at rest the row’s controls are transparent but present; a hover greys the row and shows them', async () => {
      await page.mouse.move(2, 2);
      for (const control of [
        gripOf(epic),
        addOf(epic, `Добавить: Фича (${epic.identifier})`),
        moreOf(epic),
      ]) {
        await expect(control).toHaveCSS('opacity', '0');
      }
      await expect(addOf(epic, `Добавить: Фича (${epic.identifier})`)).toBeAttached();
      expect(await paint(cellOf(page, epic.identifier, 'title'))).toBe('rgb(255, 255, 255)');

      await cellOf(page, epic.identifier, 'type').hover();
      for (const control of [
        gripOf(epic),
        addOf(epic, `Добавить: Фича (${epic.identifier})`),
        moreOf(epic),
      ]) {
        await expect(control).toHaveCSS('opacity', '1');
      }
      await expect
        .poll(async () => isLightGrey(await paint(cellOf(page, epic.identifier, 'title'))))
        .toBe(true);
      // The other rows stay quiet.
      await expect(moreOf(reports)).toHaveCSS('opacity', '0');
      expect(await paint(cellOf(page, reports.identifier, 'title'))).toBe('rgb(255, 255, 255)');
    });

    await test.step('the row controls: «+» pale grey and 24 px, «⋮» 24 px, the chevron 24 px, the grip a dotted strip at the row’s left edge', async () => {
      const add = addOf(epic, `Добавить: Фича (${epic.identifier})`);
      const more = moreOf(epic);
      const chevron = page.getByRole('button', {
        name: `Свернуть ${epic.identifier}`,
        exact: true,
      });
      for (const control of [add, more, chevron]) {
        const found = await box(control);
        near(found.width, 24, 1);
        near(found.height, 24, 1);
      }
      // D-10 (design § 9, Q-2): the reference's pale cross, a little darker — grey, not D-7's orange.
      expect(await style(add, 'color')).toBe(D10.rowAdd);

      const cell = await box(cellOf(page, epic.identifier, 'hierarchy'));
      const addBox = await box(add);
      expect(addBox.x).toBeGreaterThanOrEqual(cell.x);
      expect(addBox.x + addBox.width).toBeLessThanOrEqual(cell.x + cell.width);
      const gripBox = await box(gripOf(epic));
      near(gripBox.x, cell.x, 1);
      near(gripBox.width, 16, 2);
      near(gripBox.height, cell.height, 2);
      expect(await style(gripOf(epic), 'background-image')).not.toBe('none');
      // The reference's mark (V-4): a 1 px column 20 px tall — four dots — not the strip's height.
      expect(await style(gripOf(epic), 'background-size')).toBe('1px 20px');
    });

    await test.step('a click selects one row, light blue and framed, controls shown without the pointer', async () => {
      await cellOf(page, reports.identifier, 'type').click();
      await expect(row(reports)).toHaveAttribute('data-selected', 'true');
      await page.mouse.move(2, 2);
      await expect
        .poll(async () => isLightBlue(await paint(cellOf(page, reports.identifier, 'title'))))
        .toBe(true);
      await expect(moreOf(reports)).toHaveCSS('opacity', '1');
      await expect(gripOf(reports)).toHaveCSS('opacity', '1');
      await expect(addOf(reports, `Добавить: Фича (${reports.identifier})`)).toHaveCSS(
        'opacity',
        '1',
      );
      expect(await framed(row(reports)), 'the selected row is framed').toBe(true);
      expect(await framed(row(sso)), 'a row at rest is not').toBe(false);

      // One row at a time.
      await cellOf(page, epic.identifier, 'type').click();
      await expect(row(epic)).toHaveAttribute('data-selected', 'true');
      await expect(row(reports)).not.toHaveAttribute('data-selected', 'true');
      await page.mouse.move(2, 2);
      await expect(moreOf(reports)).toHaveCSS('opacity', '0');
    });

    await test.step('by keyboard: the row’s stops are «+», the chevron, the title, «⋮»; focus shows the controls and a ring', async () => {
      await page.mouse.move(2, 2);
      await expect(moreOf(byLink)).toHaveCSS('opacity', '0');
      const add = addOf(byLink, `Добавить дочерний элемент (${byLink.identifier})`);
      await add.focus();
      await page.keyboard.press('Tab');
      await expect(
        page.getByRole('button', { name: `Свернуть ${byLink.identifier}`, exact: true }),
      ).toBeFocused();
      await page.keyboard.press('Tab');
      await expect(
        row(byLink).getByRole('link', { name: byLink.title, exact: true }),
      ).toBeFocused();
      await page.keyboard.press('Tab');
      await expect(moreOf(byLink)).toBeFocused();
      await expect(moreOf(byLink)).toHaveCSS('opacity', '1');
      await expect(add).toHaveCSS('opacity', '1');
      expect(await style(moreOf(byLink), 'outline-style')).not.toBe('none');
      await expect(moreOf(byLink)).toHaveCSS('outline-width', '2px');
      // The grip is no stop: the next one is the first control of the row beneath.
      await page.keyboard.press('Tab');
      await expect(addOf(letter, `Добавить дочерний элемент (${letter.identifier})`)).toBeFocused();
    });

    await test.step('the header stays in view while the rows scroll inside the card', async () => {
      // 460 px: the card's own floor is 240 px (design § 4.2, "at least 240") and it starts 180 px
      // down with 24 px of the page's padding beneath, so below 444 px the page scrolls for that
      // floor, not for the table — a probe at 420 px read exactly 444 whatever the row count.
      await page.setViewportSize({ width: 1440, height: 460 });
      const scroller = await scrollerOf(table);
      const room = await scroller.evaluate((element) => ({
        scroll: element.scrollHeight,
        client: element.clientHeight,
      }));
      expect(room.scroll, 'the card scrolls inside itself').toBeGreaterThan(room.client);
      await scroller.evaluate((element) => {
        element.scrollTop = element.scrollHeight;
      });
      const header = table.getByRole('columnheader', { name: 'Заголовок', exact: true });
      await expect(header).toBeInViewport();
      near((await box(header)).y, (await box(scroller)).y, 2);
      // The first row has scrolled out above the header.
      expect((await box(row(epic))).y).toBeLessThan((await box(header)).y);
      // The rows' visually hidden descriptions stay inside the card's scroll box: the page
      // itself does not scroll for the table's height (F-3, design § 4.2).
      const pageHeight = await page.evaluate(() => ({
        scroll: (document.scrollingElement as Element).scrollHeight,
        client: (document.scrollingElement as Element).clientHeight,
      }));
      expect(pageHeight.scroll, 'the page does not scroll for the table').toBeLessThanOrEqual(
        pageHeight.client,
      );
      // A row scrolled to by the browser's focus stays clear of the sticky header (F-10).
      expect(await style(scroller, 'scroll-padding-top')).toBe('40px');
    });

    await test.step('1 440 px: the table does not scroll sideways; 1 280 px: it scrolls inside the card, the page does not', async () => {
      await page.setViewportSize({ width: 1440, height: 900 });
      const scroller = await scrollerOf(table);
      await expect
        .poll(() => scroller.evaluate((element) => element.scrollWidth - element.clientWidth))
        .toBeLessThanOrEqual(1);

      await page.setViewportSize({ width: 1280, height: 900 });
      await expect
        .poll(() => scroller.evaluate((element) => element.scrollWidth - element.clientWidth))
        .toBeGreaterThan(1);
      const pageOverflow = await page.evaluate(
        () => document.documentElement.scrollWidth - document.documentElement.clientWidth,
      );
      expect(pageOverflow).toBeLessThanOrEqual(1);
      await expect(main(page)).toBeVisible();
    });
  });
});
