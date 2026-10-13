import { randomUUID } from 'node:crypto';
import {
  expect,
  test,
  type Browser,
  type Locator,
  type Page,
  type Request as ApiRequest,
} from '@playwright/test';
import { signIn } from '../support/auth';
import { env } from '../support/env';
import { createContext, runName, send } from '../support/organizationsApi';
import {
  assigneeId,
  createChildArea,
  createWorkItemIn,
  saveWorkItem,
  setParent,
  stateId,
  typeId,
  type WorkItemDto,
} from '../support/workItemsApi';

/**
 * 27. The work item card, as a dialog over the backlog and the kanban and as the
 * full page (feat-029 US-1..US-7; its spec § E2E and `design.md`) against the
 * real backend: the dialog by a click and by its address (27.1), its header,
 * band and tab at the reference's measures (27.2), the three columns and the
 * fields without frames by type (27.3), editing and saving only what was touched
 * (27.4), refresh, undo-all and the link (27.5), closing and expanding with
 * edits (27.6), and the page by its address at every width (27.7).
 *
 * Written from the spec, the stories and the design, in parallel with the code
 * (stage 5): every control is selected by the role and the accessible name that
 * `design.md` § 8 and § 9 fix, in the Russian copy written there; the one place
 * a role cannot reach is a measure, and those go through the kit's structural
 * attributes (`data-field`, `data-field-box`, `data-frame-part`,
 * `data-field-label-cell`, `data-field-adornment` — tech-015 § 5.7, `FieldFrame`),
 * each behind one helper below or named where it is used. There is no `data-testid`.
 *
 * Data is `stories/US-1`'s, built through the API: the organization with the
 * project «Мастаск» (its first team «Мастаск» owns the root area) and the team
 * «Платформа» (default area «Платформа», «Платформа / API» beneath it); the
 * feature «Вход по ссылке» with the story «Письмо со ссылкой» beneath it, and the
 * bug «Письмо не доходит», the task «Шаблон письма» and the task «Настроить SMTP»
 * (in «Мастаск»'s own area) beneath the story; the epic «Вход и регистрация», a bug
 * and a task for the fields of each type. Identifiers are the project's own
 * (`<key>-<TYPE>-<n>`, a fresh key per run), so every case reads them from the
 * creates' answers. The story's assignee is the fixture user «E2E Fixture»: it is
 * the one user the stand's directory is sure to hold (spec 22.1), where the story
 * data has «Анна Смирнова».
 *
 * Every case creates what it needs the way specs 24-26 do and `afterAll`
 * deactivates each organization it created, which hides everything beneath it:
 * nothing is ever purged (BR-M1-1) and work items have no deletion before
 * mod-005, so the stand is left as it was found.
 *
 * What the stand cannot reach is left to the component tests: the read-only
 * mode (nothing sets it, BR-C-19), a refusal that belongs to no field, a save of
 * an item that has gone (no deletion), «Неизвестный пользователь» (the directory
 * holds every id the stand uses), the resize grip and the access-key letters that
 * are not drawn (no role to look for), the exact 1 040 and 680 px thresholds of
 * the columns (the kit's `ColumnLayout` and its showcase, spec 28) — here the
 * layout is read by the card's own width at windows that stand well on each side
 * of them.
 *
 * Nothing is injected and nothing on the session flow is stubbed
 * (`src/test/e2eSessionGuard.test.ts`): the only requests held back or refused
 * are the work item's own read and save (27.4, 27.5, 27.7), never `/api/session`.
 *
 * Status at authoring (2026-10-12): type-checked, linted and listed
 * (`npx playwright test --list`), **never run** — the card's code was not written
 * yet. The kit's parts the selectors rest on were read at `tech/015-ui-kit` de7eb60
 * (fields, `SplitButton`, `IconAction`, `DialogShell`, `BoxedTabs`, `CardHeaderBar`,
 * `PropertyBand`, `Section`, `NoticeProvider`), not run either. The first run is the
 * QA session's, on a stand brought up from nothing
 * (`docs/ai-workflow/stages/05-frontend.md` § 7). Where a measure rests on a guess
 * about the kit's DOM (the backdrop's MUI class, the kit's data attributes), the
 * helper or the step that makes it says so.
 */

const created: string[] = [];

/** The accent `#0078D4` as the browser computes it. */
const ACCENT = 'rgb(0, 120, 212)';
/** The type colours of mod-015 (`tokens.ts`): `#0098C7` and `#CC293D`. */
const STORY_COLOUR = 'rgb(0, 152, 199)';
const BUG_COLOUR = 'rgb(204, 41, 61)';
/** The reference's clean Save group (design § 4.2): `#F0F0F0` with `#959595`. */
const CLEAN_FILL = 'rgb(240, 240, 240)';
const CLEAN_TEXT = 'rgb(149, 149, 149)';

const TYPE = {
  epic: 'Эпик',
  feature: 'Фича',
  story: 'Пользовательская история',
  task: 'Задача',
  bug: 'Баг',
} as const;

const CLOSE_TITLE = 'Закрыть без сохранения?';
const SWITCH_TITLE = 'Перейти без сохранения?';
const UNDO_TITLE = 'Отменить все изменения?';
const KEEP_EDITING = 'Продолжить редактирование';
const SAVED = 'Изменения сохранены.';
const DATE_BEFORE_START = 'Целевая дата не может быть раньше даты начала.';
const PARENT_CHANGED =
  'Родитель уже изменён другим участником. Данные обновлены — проверьте и повторите.';
const API_DOWN = 'Не удалось соединиться с API. Проверьте, что Mustask.Api запущен.';

/** A signed-in API session for clean-up, closed by the caller. */
async function apiSession(browser: Browser) {
  const context = await browser.newContext();
  const page = await context.newPage();
  await signIn(page);
  return { request: page.request, close: () => context.close() };
}

/**
 * The organization, the project «Мастаск», the team «Платформа» and the story's
 * data, through the API; items of a type by its key.
 */
async function fixture(page: Page) {
  const context = await createContext(page.request, {
    organization: runName('Организация E2E'),
    project: 'Мастаск',
    teams: ['Платформа'],
  });
  created.push(context.organization.id);
  const platform = context.teams.find((team) => team.name === 'Платформа');
  if (!platform) throw new Error('The project has no team «Платформа».');
  const organizationId = context.organization.id;
  const projectId = context.project.id;
  const prefix = `/organizations/${organizationId}/projects/${projectId}`;
  const teamPrefix = `${prefix}/teams/${platform.id}`;
  const types = new Map<string, string>();

  async function typeOf(key: string): Promise<string> {
    let found = types.get(key);
    if (!found) {
      found = await typeId(page.request, organizationId, key);
      types.set(key, found);
    }
    return found;
  }

  /** An item of «Платформа»: beneath `parent` when one is given (the create takes its area). */
  async function item(key: string, title: string, parent?: WorkItemDto): Promise<WorkItemDto> {
    return createWorkItemIn(page.request, organizationId, projectId, {
      typeId: await typeOf(key),
      title,
      ...(parent ? { parentId: parent.id } : { teamId: platform!.id }),
    });
  }

  /** An item in the root area, which «Мастаск» owns, then given `parent` by the parent write. */
  async function elsewhere(key: string, title: string, parent: WorkItemDto): Promise<WorkItemDto> {
    const made = await createWorkItemIn(page.request, organizationId, projectId, {
      typeId: await typeOf(key),
      title,
    });
    await setParent(page.request, organizationId, projectId, made.identifier, parent.id);
    return made;
  }

  /** What «another user» does: a save of `identifier` by the API (feat-022 § 3.2's body). */
  function save(identifier: string, body: Record<string, unknown>) {
    return saveWorkItem(page.request, organizationId, projectId, identifier, body);
  }

  /** An item's own parent write by «another user». */
  function reparent(identifier: string, parent: WorkItemDto, seen: WorkItemDto | null) {
    return setParent(
      page.request,
      organizationId,
      projectId,
      identifier,
      parent.id,
      seen?.id ?? null,
    );
  }

  /** «Платформа / API»: the area another user moves an item to. */
  function apiArea() {
    return createChildArea(
      page.request,
      organizationId,
      projectId,
      platform!.defaultArea.id,
      'API',
    );
  }

  /** The id of the state `name` of the type `key`. */
  function stateOf(key: string, name: string) {
    return stateId(page.request, organizationId, key, name);
  }

  /** US-1's story, with its parent, its children, its texts and its assignee. */
  async function seedStory() {
    const byLink = await item('FEAT', 'Вход по ссылке');
    const story = await item('US', 'Письмо со ссылкой', byLink);
    const lost = await item('BUG', 'Письмо не доходит', story);
    const template = await item('TASK', 'Шаблон письма', story);
    const smtp = await elsewhere('TASK', 'Настроить SMTP', story);
    const userId = await assigneeId(page.request, organizationId, 'e2e-fixture@');
    await save(story.identifier, {
      description: 'Пользователь получает письмо со ссылкой для входа.',
      acceptanceCriteria: 'Ссылка действует 15 минут.',
      storyPoints: { value: 5 },
      assignee: { userId },
    });
    return { byLink, story, lost, template, smtp, userId };
  }

  /** The epic, the bug and the task of US-3's types. */
  async function seedOthers() {
    const epic = await item('EPIC', 'Вход и регистрация');
    const bug = await item('BUG', 'Ссылка истекает раньше срока');
    const task = await item('TASK', 'Шаблон письма');
    await save(epic.identifier, {
      effort: { value: 20 },
      businessValue: { value: 300 },
      priority: { value: 1 },
      startDate: { value: '2026-10-01' },
    });
    await save(bug.identifier, { reproSteps: 'Открыть письмо через 20 минут' });
    await save(task.identifier, {
      priority: { value: 3 },
      activity: { value: 'Development' },
      originalEstimate: { value: 8 },
    });
    return { epic, bug, task };
  }

  return {
    ...context,
    platform,
    prefix,
    teamPrefix,
    backlog: `${teamPrefix}/backlogs/stories`,
    board: `${teamPrefix}/boards/stories`,
    workItems: `${prefix}/workitems`,
    pageOf: (identifier: string) => `${prefix}/workitems/${identifier}`,
    item,
    save,
    reparent,
    apiArea,
    stateOf,
    seedStory,
    seedOthers,
  };
}

// ---------------------------------------------------------------------------
// Where things are: roles and names of design § 8, in one place.
// ---------------------------------------------------------------------------

/** The card as a dialog: «<Тип> <ID>» names it (design § 8). */
const cardOf = (page: Page, type: string, identifier: string) =>
  page.getByRole('dialog', { name: `${type} ${identifier}`, exact: true });

/** The card as a page: the shell's `main`. */
const pageCard = (page: Page) => page.getByRole('main');

/** A confirmation above the card: a `dialog` named by its question (K-14). */
const confirmation = (page: Page, title: string) =>
  page.getByRole('dialog', { name: title, exact: true });

/** The backlog's row and the kanban's card of an item: found by what the page itself carries. */
const rowOf = (page: Page, identifier: string) =>
  page.locator(`tr[data-identifier="${identifier}"]`);
const boardCardOf = (page: Page, identifier: string) => page.locator(`[data-card="${identifier}"]`);
const rowLink = (page: Page, identifier: string, title: string) =>
  rowOf(page, identifier).getByRole('link', { name: title, exact: true });
const boardLink = (page: Page, identifier: string, title: string) =>
  boardCardOf(page, identifier).getByRole('link', { name: title, exact: true });

const titleField = (scope: Locator) =>
  scope.getByRole('textbox', { name: 'Заголовок', exact: true });
const assigneeField = (scope: Locator) =>
  scope.getByRole('combobox', { name: 'Исполнитель', exact: true });
const stateField = (scope: Locator) => scope.getByRole('combobox', { name: 'Статус', exact: true });
const areaField = (scope: Locator) => scope.getByRole('combobox', { name: 'Область', exact: true });
const textbox = (scope: Locator, name: string) => scope.getByRole('textbox', { name, exact: true });
const list = (scope: Locator, name: string) => scope.getByRole('combobox', { name, exact: true });
const date = (scope: Locator, name: string) => scope.getByLabel(name, { exact: true });
const region = (scope: Locator, name: string) => scope.getByRole('region', { name, exact: true });
const parentField = (scope: Locator) => list(region(scope, 'Связанная работа'), 'Родитель');

const saveGroup = (scope: Locator) => scope.getByRole('group', { name: 'Сохранение', exact: true });
const saveButton = (scope: Locator) =>
  saveGroup(scope).getByRole('button', { name: 'Сохранить', exact: true });
const saveArrow = (scope: Locator) =>
  saveGroup(scope).getByRole('button', { name: 'Ещё варианты сохранения', exact: true });
const refreshButton = (scope: Locator) =>
  scope.getByRole('button', { name: 'Обновить', exact: true });
const undoButton = (scope: Locator) =>
  scope.getByRole('button', { name: 'Отменить все изменения', exact: true });
const moreButton = (scope: Locator) =>
  scope.getByRole('button', { name: 'Ещё действия', exact: true });
const expandButton = (scope: Locator) =>
  scope.getByRole('button', { name: 'Развернуть на страницу', exact: true });
const closeButton = (scope: Locator) => scope.getByRole('button', { name: 'Закрыть', exact: true });
const tabList = (scope: Locator) =>
  scope.getByRole('tablist', { name: 'Разделы рабочего элемента', exact: true });

/** A field's label, the real `<label>` the kit draws (tech-015 § 5.7). */
const labelOf = (scope: Locator, text: string) =>
  scope.locator('label').filter({ hasText: new RegExp(`^${text}$`) });
/** A section's labels, top to bottom: the kit's `<label>` of each field. */
const labelsOf = (scope: Locator, name: string) => region(scope, name).locator('label');

/** A notice of `useNotice`: found by its text, the first of what matches (a live region may repeat it). */
const notice = (page: Page, text: string) => page.getByText(text, { exact: true }).first();

/** The sections' headings in document — reading and Tab — order. */
const sectionOrder = (scope: Locator) => scope.getByRole('region').locator('h2').allInnerTexts();

const menuItem = (page: Page, name: string) => page.getByRole('menuitem', { name, exact: true });

// ---------------------------------------------------------------------------
// The address and the history.
// ---------------------------------------------------------------------------

const pathOf = (page: Page) => new URL(page.url()).pathname;
const workitemParam = (page: Page) => new URL(page.url()).searchParams.get('workitem');
/** Any dialog at all, the card's or a confirmation's: not reached by roles while another is above it. */
const anyDialog = (page: Page) => page.locator('[role="dialog"]');

// ---------------------------------------------------------------------------
// The saves.
// ---------------------------------------------------------------------------

const isSave = (request: ApiRequest, identifier: string) =>
  request.method() === 'PATCH' &&
  new URL(request.url()).pathname.endsWith(`/workitems/${identifier}`);

/**
 * «Сохранить» and its answer: the save went through and the card shows the
 * stored values with «Сохранить» disabled again. Returns the body that was sent.
 */
async function saveCard(page: Page, scope: Locator, identifier: string) {
  const sent = page.waitForRequest((request) => isSave(request, identifier));
  const answered = page.waitForResponse((response) => isSave(response.request(), identifier));
  await saveButton(scope).click();
  const body = (await sent).postDataJSON() as Record<string, unknown>;
  expect((await answered).ok()).toBe(true);
  await expect(notice(page, SAVED)).toBeVisible();
  await expect(saveButton(scope)).toBeDisabled();
  return body;
}

/** The writes a page makes to `identifier`, for a case that says nothing may be sent. */
function watchSaves(page: Page, identifier: string): string[] {
  const writes: string[] = [];
  page.on('request', (request) => {
    if (isSave(request, identifier)) writes.push(request.url());
  });
  return writes;
}

/** Picks `option` from the fixed list `name`. */
async function choose(page: Page, scope: Locator, name: string, option: string) {
  await list(scope, name).click();
  await page.getByRole('option', { name: option, exact: true }).click();
}

/** The parent field: types `text` and picks the line starting with `identifier`. */
async function pickParent(page: Page, scope: Locator, text: string, identifier: string) {
  const field = parentField(scope);
  await field.click();
  await field.fill(text);
  await page.getByRole('option', { name: new RegExp(`^${identifier} `) }).click();
}

// ---------------------------------------------------------------------------
// Measures: CSS px, from the box a browser lays out.
// ---------------------------------------------------------------------------

interface Rect {
  x: number;
  y: number;
  width: number;
  height: number;
}

async function box(locator: Locator): Promise<Rect> {
  const found = await locator.boundingBox();
  if (!found) throw new Error('The element is not laid out.');
  return found;
}

const right = (rect: Rect) => rect.x + rect.width;
const bottom = (rect: Rect) => rect.y + rect.height;
const centreX = (rect: Rect) => rect.x + rect.width / 2;
const centreY = (rect: Rect) => rect.y + rect.height / 2;

/** `actual` is within `tolerance` px of `expected`, and says what when it is not. */
function near(actual: number, expected: number, tolerance: number, what: string) {
  expect(
    Math.abs(actual - expected),
    `${what}: ${actual.toFixed(1)} is not within ${tolerance} px of ${expected}`,
  ).toBeLessThanOrEqual(tolerance);
}

/** The kit's field root, the value's box and the label cell round a control (tech-015 § 5.7). */
const fieldOf = (control: Locator) => control.locator('xpath=ancestor::*[@data-field][1]');
const boxOf = (control: Locator) => control.locator('xpath=ancestor::*[@data-field-box][1]');

/** A card's own `kit-card` width is what the columns follow; here the sections are what is measured. */
async function columnsOf(scope: Locator): Promise<'three' | 'two' | 'one' | 'other'> {
  const main = await box(region(scope, 'Описание'));
  const side = await box(region(scope, 'Планирование'));
  const extra = await box(region(scope, 'Связанная работа'));
  const sideRight = side.x > right(main) - 1;
  if (sideRight && extra.x > right(side) - 1 && Math.abs(extra.y - side.y) < 3) return 'three';
  if (sideRight && Math.abs(extra.x - side.x) < 2 && extra.y > bottom(side) - 1) return 'two';
  if (
    Math.abs(side.x - main.x) < 2 &&
    Math.abs(extra.x - main.x) < 2 &&
    side.y > bottom(main) - 1
  ) {
    return 'one';
  }
  return 'other';
}

/** Where a value's text begins: an input's content edge, or the left of the text a `div` shows. */
async function textLeft(control: Locator): Promise<number> {
  return control.evaluate((element) => {
    if (element instanceof HTMLInputElement || element instanceof HTMLTextAreaElement) {
      const style = getComputedStyle(element);
      return (
        element.getBoundingClientRect().left +
        parseFloat(style.paddingLeft) +
        parseFloat(style.borderLeftWidth)
      );
    }
    const walker = document.createTreeWalker(element, NodeFilter.SHOW_TEXT);
    let left = Number.POSITIVE_INFINITY;
    for (let node = walker.nextNode(); node; node = walker.nextNode()) {
      if (!node.textContent?.trim()) continue;
      const range = document.createRange();
      range.selectNodeContents(node);
      for (const rect of Array.from(range.getClientRects())) {
        if (rect.width > 0) left = Math.min(left, rect.left);
      }
    }
    return left;
  });
}

/** The width of the text an element shows (a tab's label, not the tab). */
async function textWidth(control: Locator): Promise<number> {
  return control.evaluate((element) => {
    const walker = document.createTreeWalker(element, NodeFilter.SHOW_TEXT);
    let left = Number.POSITIVE_INFINITY;
    let right = Number.NEGATIVE_INFINITY;
    for (let node = walker.nextNode(); node; node = walker.nextNode()) {
      if (!node.textContent?.trim()) continue;
      const range = document.createRange();
      range.selectNodeContents(node);
      for (const rect of Array.from(range.getClientRects())) {
        if (rect.width > 0) {
          left = Math.min(left, rect.left);
          right = Math.max(right, rect.right);
        }
      }
    }
    return right - left;
  });
}

/** The width of the frame a field draws round its value: the kit's inset shadow (0 at rest), and its colour. */
async function frameOf(control: Locator): Promise<{ width: number; accent: boolean }> {
  const shadow = await control.evaluate((element) => {
    const part = element.closest('[data-frame-part]');
    return part ? getComputedStyle(part).boxShadow : 'no frame part';
  });
  if (shadow === 'none') return { width: 0, accent: false };
  const width = /(\d+(?:\.\d+)?)px inset/.exec(shadow);
  if (!width) throw new Error(`Not a frame: ${shadow}`);
  return { width: Number(width[1]), accent: shadow.includes(ACCENT) };
}

/** A computed style of an element. */
const styleOf = (locator: Locator, property: string) =>
  locator.evaluate((element, name) => getComputedStyle(element).getPropertyValue(name), property);

/** An element's own fill and text colour. */
const paintOf = (locator: Locator) =>
  locator.evaluate((element) => {
    const style = getComputedStyle(element);
    return { background: style.backgroundColor, colour: style.color };
  });

/** The computed `opacity` of an input's placeholder: 0 at rest, 1 on hover and on focus (tech-015 § 5.7). */
const placeholderOpacity = (control: Locator) =>
  control.evaluate((element) => getComputedStyle(element, '::placeholder').opacity);

const isTransparent = (colour: string) =>
  colour === 'transparent' || /^rgba\(\s*\d+,\s*\d+,\s*\d+,\s*0\s*\)$/.test(colour);

/**
 * The accent-coloured line of a tab strip, thin and wide: the selected tab's indicator (the kit
 * draws it as `span[data-tab-indicator]`; MUI's own, hidden, has no box). Found by what it looks
 * like, so the measure holds whichever way the kit draws it.
 */
async function indicatorOf(strip: Locator): Promise<Rect | null> {
  return strip.evaluate((root, accent) => {
    for (const element of Array.from(root.querySelectorAll('*'))) {
      const rect = element.getBoundingClientRect();
      if (
        getComputedStyle(element).backgroundColor === accent &&
        rect.height > 0 &&
        rect.height <= 4 &&
        rect.width >= 8
      ) {
        return { x: rect.x, y: rect.y, width: rect.width, height: rect.height };
      }
    }
    return null;
  }, ACCENT);
}

/**
 * Scrolls the nearest scrolling ancestor of `locator` — the dialog's body, or
 * the page — to `top`; returns where it stands (-1: nothing scrolls).
 */
async function scrollAround(locator: Locator, top: number): Promise<number> {
  return locator.evaluate((element, target) => {
    for (let parent = element.parentElement; parent; parent = parent.parentElement) {
      const overflow = getComputedStyle(parent).overflowY;
      if (
        (overflow === 'auto' || overflow === 'scroll') &&
        parent.scrollHeight > parent.clientHeight
      ) {
        parent.scrollTop = target;
        return parent.scrollTop;
      }
    }
    const root = document.scrollingElement;
    if (!root) return -1;
    root.scrollTop = target;
    return root.scrollTop;
  }, top);
}

/** The mean brightness (0..255) of a clip of the page, decoded in the browser: no image library is needed. */
async function brightnessOf(page: Page, clip: Rect): Promise<number> {
  const png = await page.screenshot({ clip, animations: 'disabled' });
  return page.evaluate(async (base64) => {
    const bytes = Uint8Array.from(atob(base64), (character) => character.charCodeAt(0));
    const bitmap = await createImageBitmap(new Blob([bytes], { type: 'image/png' }));
    const canvas = new OffscreenCanvas(bitmap.width, bitmap.height);
    const context = canvas.getContext('2d');
    if (!context) throw new Error('No 2d context.');
    context.drawImage(bitmap, 0, 0);
    const { data } = context.getImageData(0, 0, bitmap.width, bitmap.height);
    let sum = 0;
    for (let at = 0; at < data.length; at += 4) sum += (data[at] + data[at + 1] + data[at + 2]) / 3;
    return sum / (data.length / 4);
  }, png.toString('base64'));
}

/** Focus is on the dialog's own frame and in no field (design § 4.1). */
function focusOnFrame(page: Page) {
  return page.evaluate(() => {
    const active = document.activeElement;
    const inDialog = Boolean(
      active?.closest('[role="dialog"]') ?? active?.querySelector('[role="dialog"]'),
    );
    const editable =
      active instanceof HTMLInputElement ||
      active instanceof HTMLTextAreaElement ||
      active instanceof HTMLSelectElement ||
      active?.getAttribute('role') === 'combobox';
    return { inDialog, editable };
  });
}

test.describe('27. the work item card (feat-029)', () => {
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

  test('27.1 a title opens the dialog over the backlog and the kanban, the address and the history follow, a modified click keeps the page, a related link switches it, an unknown item answers its frame (US-1)', async ({
    page,
    context,
  }) => {
    test.setTimeout(240_000);
    await signIn(page, { returnPath: '/home' });
    const fx = await fixture(page);
    const { story, byLink } = await fx.seedStory();
    const card = cardOf(page, TYPE.story, story.identifier);
    const link = rowLink(page, story.identifier, 'Письмо со ссылкой');

    await test.step('a backlog title is a link to the full page, and a plain click opens the card over the backlog', async () => {
      await page.goto(fx.backlog);
      await expect(link).toBeVisible();
      await expect(link).toHaveAttribute('href', new RegExp(`${fx.pageOf(story.identifier)}$`));

      await link.click();
      await expect(card).toBeVisible();
      await expect.poll(() => workitemParam(page)).toBe(story.identifier);
      expect(pathOf(page)).toBe(fx.backlog);
      // The page behind stays drawn, and the card reads the item.
      await expect(rowOf(page, story.identifier)).toBeVisible();
      await expect(titleField(card)).toHaveValue('Письмо со ссылкой');
      // Focus is on the dialog's frame, in no field.
      await expect.poll(() => focusOnFrame(page)).toEqual({ inDialog: true, editable: false });
    });

    await test.step('Esc, «Закрыть» and Back close it; focus returns to the title', async () => {
      await page.keyboard.press('Escape');
      await expect(card).toHaveCount(0);
      expect(workitemParam(page)).toBeNull();
      expect(pathOf(page)).toBe(fx.backlog);
      await expect(link).toBeFocused();

      await link.click();
      await expect(card).toBeVisible();
      await closeButton(card).click();
      await expect(card).toHaveCount(0);
      expect(workitemParam(page)).toBeNull();
      await expect(link).toBeFocused();

      await link.click();
      await expect(card).toBeVisible();
      await page.goBack();
      await expect(card).toHaveCount(0);
      expect(workitemParam(page)).toBeNull();
      expect(pathOf(page)).toBe(fx.backlog);
      await expect(rowOf(page, story.identifier)).toBeVisible();
    });

    await test.step('the address opens the dialog; closing replaces the address, so Back leaves the backlog', async () => {
      await page.goto('/home');
      await expect(page.getByRole('heading', { level: 1, name: 'Главная' })).toBeVisible();
      await page.goto(`${fx.backlog}?workitem=${story.identifier}`);
      await expect(card).toBeVisible();
      await expect(rowOf(page, story.identifier)).toBeVisible();
      await closeButton(card).click();
      await expect(card).toHaveCount(0);
      expect(workitemParam(page)).toBeNull();
      expect(pathOf(page)).toBe(fx.backlog);
      await page.goBack();
      await expect.poll(() => pathOf(page)).toBe('/home');
    });

    await test.step('a link to a related item switches the dialog, Back shows the first again', async () => {
      await page.goto(`${fx.backlog}?workitem=${story.identifier}`);
      await expect(card).toBeVisible();
      const parent = card.getByRole('link', { name: `Открыть ${byLink.identifier}`, exact: true });
      await expect(parent).toHaveAttribute('href', new RegExp(`${fx.pageOf(byLink.identifier)}$`));
      await parent.click();
      const featureCard = cardOf(page, TYPE.feature, byLink.identifier);
      await expect(featureCard).toBeVisible();
      await expect(card).toHaveCount(0);
      await expect.poll(() => workitemParam(page)).toBe(byLink.identifier);
      await page.goBack();
      await expect(card).toBeVisible();
      await expect.poll(() => workitemParam(page)).toBe(story.identifier);
    });

    await test.step('after such a switch, Esc closes a dialog the address opened at once, and Back leaves the backlog (R1-1)', async () => {
      await page.goto('/home');
      await expect(page.getByRole('heading', { level: 1, name: 'Главная' })).toBeVisible();
      await page.goto(`${fx.backlog}?workitem=${story.identifier}`);
      await expect(card).toBeVisible();
      await card.getByRole('link', { name: `Открыть ${byLink.identifier}`, exact: true }).click();
      await expect(cardOf(page, TYPE.feature, byLink.identifier)).toBeVisible();
      await page.keyboard.press('Escape');
      await expect(anyDialog(page)).toHaveCount(0);
      await expect.poll(() => workitemParam(page)).toBeNull();
      expect(pathOf(page)).toBe(fx.backlog);
      await page.goBack();
      await expect.poll(() => pathOf(page)).toBe('/home');
    });

    await test.step('another level of the same page drops the dialog', async () => {
      await page.goto(`${fx.teamPrefix}/backlogs/features`);
      await page.getByRole('button', { name: /^Уровень: / }).click();
      await page.getByRole('menuitem', { name: 'Истории' }).click();
      await expect.poll(() => pathOf(page)).toBe(fx.backlog);
      await link.click();
      await expect(card).toBeVisible();
      // Two entries back: the card's and the level's, to the first page.
      await page.evaluate(() => window.history.go(-2));
      await expect.poll(() => pathOf(page)).toBe(`${fx.teamPrefix}/backlogs/features`);
      await expect(anyDialog(page)).toHaveCount(0);
      expect(workitemParam(page)).toBeNull();
    });

    await test.step('a modified click or the middle button follows the link to the full page', async () => {
      await page.goto(fx.backlog);
      await expect(link).toBeVisible();
      for (const click of [{ modifiers: ['Control' as const] }, { button: 'middle' as const }]) {
        const opened = context.waitForEvent('page');
        await link.click(click);
        const tab = await opened;
        await expect(tab).toHaveURL(new RegExp(`${fx.pageOf(story.identifier)}$`));
        await tab.close();
        await expect(anyDialog(page)).toHaveCount(0);
        expect(workitemParam(page)).toBeNull();
      }
    });

    await test.step('the kanban card’s title does the same', async () => {
      await page.goto(fx.board);
      const boardLinkOfStory = boardLink(page, story.identifier, 'Письмо со ссылкой');
      await expect(boardLinkOfStory).toHaveAttribute(
        'href',
        new RegExp(`${fx.pageOf(story.identifier)}$`),
      );
      await boardLinkOfStory.click();
      await expect(card).toBeVisible();
      await expect.poll(() => workitemParam(page)).toBe(story.identifier);
      expect(pathOf(page)).toBe(fx.board);
      await expect(boardCardOf(page, story.identifier)).toBeVisible();
      await page.keyboard.press('Escape');
      await expect(card).toHaveCount(0);
      expect(workitemParam(page)).toBeNull();
      await expect(boardLinkOfStory).toBeFocused();
    });

    await test.step('failure: an item that is not there answers its frame in the dialog, which keeps the page’s tab', async () => {
      const missing = `${fx.project.key}-US-999`;
      await page.goto(`${fx.backlog}?workitem=${missing}`);
      const gone = page.getByRole('dialog', { name: `Рабочий элемент ${missing}`, exact: true });
      await expect(gone).toBeVisible();
      await expect(
        gone.getByRole('heading', { level: 2, name: 'Рабочий элемент не найден' }),
      ).toBeVisible();
      await expect(gone.getByText('Проверьте адрес или закройте окно.')).toBeVisible();
      await expect(page).toHaveTitle('Бэклоги — Mustask');
      // The shell's icon and the frame's action are both «Закрыть»; the action comes later in the DOM.
      await expect(closeButton(gone)).toHaveCount(2);
      await closeButton(gone).last().click();
      await expect(gone).toHaveCount(0);
      expect(workitemParam(page)).toBeNull();
      expect(pathOf(page)).toBe(fx.backlog);
    });

    await test.step('the page behind shows what the card changed once it is closed', async () => {
      await page.goto(fx.backlog);
      await link.click();
      await expect(card).toBeVisible();
      await titleField(card).fill('Письмо со ссылкой для входа');
      await saveCard(page, card, story.identifier);
      await expect(card).toBeVisible();
      // «Сохранить» is disabled again and gave its focus up: the card takes it to the title, so
      // Esc closes the saved dialog at once (R1-6).
      await expect(titleField(card)).toBeFocused();
      await page.keyboard.press('Escape');
      await expect(card).toHaveCount(0);
      await expect(rowLink(page, story.identifier, 'Письмо со ссылкой для входа')).toBeVisible();
    });

    await test.step('on a window narrower than 1 024 px the dialog is the whole window, square', async () => {
      await page.setViewportSize({ width: 1000, height: 800 });
      await page.goto(`${fx.backlog}?workitem=${story.identifier}`);
      await expect(card).toBeVisible();
      const paper = await box(card);
      near(paper.x, 0, 1, 'the dialog’s left');
      near(paper.y, 0, 1, 'the dialog’s top');
      near(paper.width, 1000, 1, 'the dialog’s width');
      near(paper.height, 800, 1, 'the dialog’s height');
      expect(await styleOf(card, 'border-top-left-radius')).toBe('0px');
    });
  });

  test.describe('27.2 at 1 536 × 790, pixel ratio 1.25', () => {
    test.use({ viewport: { width: 1536, height: 790 }, deviceScaleFactor: 1.25 });

    test('27.2 the dialog’s bar, three header rows, band, tab and body stand at the reference’s measures; the backdrop is transparent and the page keeps its brightness; no place kept is drawn (US-2, BR-C-22)', async ({
      page,
    }, testInfo) => {
      test.setTimeout(240_000);
      await signIn(page, { returnPath: '/home' });
      const fx = await fixture(page);
      const { story } = await fx.seedStory();
      const bug = await fx.item('BUG', 'Ссылка истекает раньше срока');
      const card = cardOf(page, TYPE.story, story.identifier);
      expect(await page.evaluate(() => window.devicePixelRatio)).toBe(1.25);

      await page.goto(fx.backlog);
      await expect(rowOf(page, story.identifier)).toBeVisible();
      await page.mouse.move(0, 0);
      const strip = { x: 0, y: 0, width: 1536, height: 30 };
      const before = await brightnessOf(page, strip);
      await rowLink(page, story.identifier, 'Письмо со ссылкой').click();
      await expect(card).toBeVisible();
      await expect(titleField(card)).toHaveValue('Письмо со ссылкой');
      await page.mouse.move(0, 0);

      const d = await box(card);

      await test.step('the dialog is 90 % of the window, centred, with a shadow and no dimming', async () => {
        near(d.width, 1382, 2, 'dialog width');
        near(d.height, 711, 2, 'dialog height');
        near(centreX(d), 768, 2, 'dialog centre x');
        near(centreY(d), 395, 2, 'dialog centre y');
        expect(await styleOf(card, 'box-shadow')).not.toBe('none');

        // `DialogShell size="card"` is a MUI `Dialog` with an `invisible` backdrop (tech-015
        // § 5.10): the backdrop is looked for by MUI's own class, the one guess about its DOM.
        const backdrop = await card.evaluate((paper) => {
          const root = paper.closest('.MuiDialog-root');
          const element = root?.querySelector('.MuiBackdrop-root');
          return element ? getComputedStyle(element).backgroundColor : null;
        });
        expect(
          backdrop,
          'the backdrop is drawn so that a click outside still closes',
        ).not.toBeNull();
        expect(isTransparent(backdrop ?? ''), `the backdrop’s fill is ${backdrop}`).toBe(true);
        const after = await brightnessOf(page, strip);
        expect(after, 'the page behind keeps its brightness').toBeGreaterThan(before * 0.93);
      });

      await test.step('the type’s bar, the three rows of the header and the two dialog icons', async () => {
        // The kit's `TypeStripe` is an `aria-hidden` span with no role: it is found by its own
        // attribute (tech-015 § 5.9).
        const stripe = card.locator('[data-type-stripe="card"]');
        const bar = await box(stripe);
        near(bar.width, 10, 1, 'the type bar’s width');
        near(bar.height, 124, 2, 'the type bar is as high as the header');
        near(bar.x, d.x, 1, 'the bar stands at the dialog’s left edge');
        near(bar.y, d.y, 1, 'the bar starts at the dialog’s top');
        expect(await styleOf(stripe, 'background-color')).toBe(STORY_COLOUR);

        const typeLine = await box(
          card.getByRole('heading', {
            level: 2,
            name: `${TYPE.story} ${story.identifier}`,
            exact: true,
          }),
        );
        near(centreY(typeLine) - d.y, 20, 3, 'the type line’s centre');
        const identifier = await box(card.getByText(story.identifier, { exact: true }));
        near(identifier.x - d.x, 18, 2, 'the identifier stands at the header’s inset');
        near(centreY(identifier) - d.y, 60, 3, 'the title row’s centre');
        const title = await box(titleField(card));
        near(centreY(title) - d.y, 60, 3, 'the title’s centre');
        const gap = title.x - right(identifier);
        expect(gap, 'the title follows the identifier after a gap').toBeGreaterThan(14);
        expect(gap).toBeLessThan(28);

        const assignee = assigneeField(card);
        near(centreY(await box(assignee)) - d.y, 100, 3, 'the action row’s centre');
        near((await textLeft(assignee)) - d.x, 54, 2, 'the assignee’s name');
        await expect(assignee).toHaveValue('E2E Fixture');

        const closeBox = await box(closeButton(card));
        const expandBox = await box(expandButton(card));
        near(right(d) - centreX(closeBox), 21, 2, '✕ from the dialog’s right edge');
        near(right(d) - centreX(expandBox), 55, 2, '⤢ from the dialog’s right edge');
        near(centreY(closeBox) - d.y, 20, 2, '✕ on row 1’s centre line');
        near(closeBox.width, 32, 1, '✕’s box');
      });

      await test.step('the action row: the Save group, «Обновить», «Отменить все изменения» and «Ещё действия»', async () => {
        const group = await box(saveGroup(card));
        const refresh = await box(refreshButton(card));
        const undo = await box(undoButton(card));
        const more = await box(moreButton(card));
        near(group.height, 32, 1, 'the Save group’s height');
        near(centreY(group) - d.y, 100, 2, 'the Save group’s centre');
        near(right(d) - centreX(refresh), 110, 2, '«Обновить» from the right edge');
        near(right(d) - centreX(undo), 67, 2, '«Отменить все изменения» from the right edge');
        near(right(d) - centreX(more), 24, 2, '«Ещё действия» from the right edge');
        near(refresh.x - right(group), 11, 2, 'the gap between the Save group and «Обновить»');
        near(undo.x - right(refresh), 11, 2, 'the gap between two icon buttons');
        near(refresh.width, 32, 1, 'an icon button’s box');

        // Clean: the reference's pale grey, not MUI's darker disabled one.
        await expect(saveButton(card)).toBeDisabled();
        await expect(undoButton(card)).toBeDisabled();
        for (const part of [saveButton(card), saveArrow(card)]) {
          expect(await paintOf(part)).toEqual({ background: CLEAN_FILL, colour: CLEAN_TEXT });
        }
      });

      await test.step('the grey band: «Статус», «Область», the stamp and the one tab', async () => {
        const statusLabel = await box(labelOf(card, 'Статус'));
        const areaLabel = await box(labelOf(card, 'Область'));
        near(statusLabel.x - d.x, 18, 2, '«Статус»’s label from the dialog’s left edge');
        near(areaLabel.x - d.x, 303, 2, '«Область»’s label from the dialog’s left edge');
        near(centreY(statusLabel) - d.y, 146, 3, 'the band’s first row');

        const status = stateField(card);
        const area = areaField(card);
        await expect(status).toContainText('Новая');
        await expect(area).toHaveValue('Платформа');
        const statusText = await textLeft(status);
        const areaText = await textLeft(area);
        near(statusText - statusLabel.x, 88, 2, '«Новая» from its label’s left edge');
        near(areaText - areaLabel.x, 88, 2, '«Платформа» from its label’s left edge');
        // The state's dot stands in the label column, 19 before the name: the label cell's
        // adornment wrapper (the kit's `leadSx` pads it) holds the 10 px marker, which is measured.
        const dot = await box(
          fieldOf(status)
            .locator('[data-field-label-cell] [data-field-adornment]')
            .locator('xpath=./*[1]'),
        );
        near(dot.width, 10, 1, 'the dot’s size');
        near(centreX(dot) - statusLabel.x, 64, 2, 'the dot’s centre from its label’s edge');
        near(statusText - right(dot), 19, 2, 'the dot’s gap to the name');

        const stamp = card.getByText(/^Изменён: \d{2}\.\d{2}\.\d{4} \d{2}:\d{2}$/);
        await expect(stamp).toBeVisible();
        near(right(d) - right(await box(stamp)), 8, 2, 'the stamp’s right edge');
        await expect(card.getByText(/Обновил/)).toHaveCount(0);

        const tabs = tabList(card);
        await expect(card.getByRole('tab')).toHaveCount(1);
        const tab = card.getByRole('tab', { name: 'Детали', exact: true });
        await expect(tab).toHaveAttribute('aria-selected', 'true');
        await expect(card.getByRole('tabpanel', { name: 'Детали', exact: true })).toBeVisible();
        const strip36 = await box(tabs);
        near(strip36.height, 36, 1, 'the tab strip’s height');
        near(right(d) - right(strip36), 8, 2, 'the tab strip’s right edge');
        near(
          bottom(strip36) - (d.y + 124),
          76,
          2,
          'the strip stands on the band’s lower edge, 76 below the header',
        );

        const indicator = await indicatorOf(tabs);
        expect(indicator, 'the selected tab’s indicator is drawn').not.toBeNull();
        near(indicator!.height, 2, 0.5, 'the indicator’s thickness');
        near(
          bottom(strip36) - bottom(indicator!),
          4,
          1.5,
          'the indicator’s foot above the strip’s bottom',
        );
        near(indicator!.width, await textWidth(tab), 2, 'the indicator is as wide as the label');
      });

      await test.step('the body: three columns, «Описание» 20 below its rule, the next head 102 below it', async () => {
        const main = await box(region(card, 'Описание'));
        const side = await box(region(card, 'Планирование'));
        const extra = await box(region(card, 'Связанная работа'));
        near(main.x - d.x, 12, 1, 'the body’s left gutter');
        near(side.width, 320, 1, 'the second column');
        near(extra.width, 320, 1, 'the third column');
        near(side.x - right(main), 16, 1, 'the gap between the first and second column');
        near(extra.x - right(side), 16, 1, 'the gap between the second and third column');
        expect(main.width, 'the first column takes the rest').toBeGreaterThan(640);

        const head = await box(region(card, 'Описание').getByRole('heading', { level: 2 }));
        const ruleTop = bottom(head) + 12;
        const text = textbox(card, 'Описание');
        near((await box(boxOf(text))).y - ruleTop, 20, 2, '«Описание»’s text box below its rule');
        near((await textLeft(text)) - main.x, 16, 2, 'the text from the column’s edge');
        // The head of the section names the text: no second, drawn label.
        const hiddenLabel = await box(labelOf(region(card, 'Описание'), 'Описание'));
        expect(hiddenLabel.width, 'a text has no drawn label of its own').toBeLessThanOrEqual(1);
        const nextHead = await box(
          region(card, 'Критерии приёмки').getByRole('heading', { level: 2 }),
        );
        near(
          nextHead.y - ruleTop,
          102,
          2,
          'the next head below the rule (the cap tops 30 and 107, KD-12)',
        );
      });

      await test.step('nothing of the places kept is drawn', async () => {
        for (const word of [
          'Причина',
          'Итерация',
          'Следить',
          'Обсуждение',
          'Развёртывание',
          'Разработка',
          'Добавить связь',
          'История',
          'Связи',
          'Вложения',
        ]) {
          await expect(card.getByText(word, { exact: true }), word).toHaveCount(0);
        }
        await expect(card.getByRole('tab')).toHaveCount(1);
      });

      await test.step('the header and the band stay while the body scrolls; «Сохранить» alone is filled', async () => {
        const description = textbox(card, 'Описание');
        await description.fill(
          Array.from({ length: 60 }, (_, line) => `Строка описания ${line + 1}`).join('\n'),
        );
        await expect(saveButton(card)).toBeEnabled();
        expect((await paintOf(saveButton(card))).background).toBe(ACCENT);
        for (const other of [refreshButton(card), moreButton(card)]) {
          expect(isTransparent((await paintOf(other)).background)).toBe(true);
        }
        await expect(undoButton(card)).toBeEnabled();

        const head = card.getByRole('heading', {
          level: 2,
          name: `${TYPE.story} ${story.identifier}`,
        });
        const headBefore = await box(head);
        const bandBefore = await box(labelOf(card, 'Статус'));
        const textBefore = await box(description);
        const scrolled = await scrollAround(description, 300);
        expect(scrolled, 'the body scrolls on its own, inside the dialog').toBeGreaterThan(0);
        const headAfter = await box(head);
        const bandAfter = await box(labelOf(card, 'Статус'));
        near(headAfter.y, headBefore.y, 1, 'the header stays');
        near(bandAfter.y, bandBefore.y, 1, 'the band stays');
        expect(Math.abs((await box(description)).y - textBefore.y)).toBeGreaterThan(100);

        await undoButton(card).click();
        await confirmation(page, UNDO_TITLE)
          .getByRole('button', { name: 'Отменить изменения', exact: true })
          .click();
        await expect(saveButton(card)).toBeDisabled();
      });

      await test.step('the story’s side-by-side, and a bug’s bar, icon and line', async () => {
        await page.mouse.move(0, 0);
        await testInfo.attach('card-user-story-1536x790-ratio-1.25.png', {
          body: await page.screenshot({ animations: 'disabled' }),
          contentType: 'image/png',
        });
        await page.keyboard.press('Escape');
        await expect(card).toHaveCount(0);

        await page.goto(`${fx.backlog}?workitem=${bug.identifier}`);
        const bugCard = cardOf(page, TYPE.bug, bug.identifier);
        await expect(bugCard).toBeVisible();
        const bugStripe = bugCard.locator('[data-type-stripe="card"]');
        expect(await styleOf(bugStripe, 'background-color')).toBe(BUG_COLOUR);
        near((await box(bugStripe)).width, 10, 1, 'the bug’s bar');
        await expect(bugCard.getByText('Не назначен', { exact: true })).toBeVisible();
        await expect(
          bugCard.getByRole('heading', {
            level: 2,
            name: `${TYPE.bug} ${bug.identifier}`,
            exact: true,
          }),
        ).toBeVisible();
        await page.mouse.move(0, 0);
        await testInfo.attach('card-bug-1536x790-ratio-1.25.png', {
          body: await page.screenshot({ animations: 'disabled' }),
          contentType: 'image/png',
        });
      });
    });
  });

  test('27.3 the three columns of a story, a bug, a task and an epic by their positions, labels above values, frames and hints on hover and focus, the fold at 1 100, 800 and 640 px (US-3)', async ({
    page,
  }) => {
    test.setTimeout(240_000);
    await signIn(page, { returnPath: '/home' });
    const fx = await fixture(page);
    const { story, byLink, lost, template, smtp } = await fx.seedStory();
    const { epic, bug, task } = await fx.seedOthers();
    const open = async (type: string, identifier: string) => {
      await page.goto(`${fx.backlog}?workitem=${identifier}`);
      const card = cardOf(page, type, identifier);
      await expect(card).toBeVisible();
      await expect(titleField(card)).not.toHaveValue('');
      return card;
    };

    await test.step('a user story: «Описание» and «Критерии приёмки», then «Планирование» and «Классификация», then «Связанная работа»', async () => {
      const card = await open(TYPE.story, story.identifier);
      expect(await sectionOrder(card)).toEqual([
        'Описание',
        'Критерии приёмки',
        'Планирование',
        'Классификация',
        'Связанная работа',
      ]);
      expect(await columnsOf(card)).toBe('three');
      await expect(textbox(card, 'Описание')).toHaveValue(
        'Пользователь получает письмо со ссылкой для входа.',
      );
      await expect(textbox(card, 'Критерии приёмки')).toHaveValue('Ссылка действует 15 минут.');
      await expect(labelsOf(card, 'Планирование')).toHaveText([
        'Оценка в пунктах',
        'Приоритет',
        'Риск',
      ]);
      await expect(labelsOf(card, 'Классификация')).toHaveText(['Вид ценности']);
      await expect(textbox(card, 'Оценка в пунктах')).toHaveValue('5');
      await expect(list(card, 'Приоритет')).toHaveText('2');
      await expect(list(card, 'Вид ценности')).toHaveText('Бизнес');

      // The columns' widths and the sections' rhythm: 320 and 16, 41 + three fields of 48, 32 between sections.
      const main = await box(region(card, 'Описание'));
      const side = await box(region(card, 'Планирование'));
      const extra = await box(region(card, 'Связанная работа'));
      const classification = await box(region(card, 'Классификация'));
      near(side.width, 320, 1, 'the second column');
      near(extra.width, 320, 1, 'the third column');
      near(side.x - right(main), 16, 1, 'the first gap');
      near(extra.x - right(side), 16, 1, 'the second gap');
      near(side.height, 185, 3, '«Планирование»: its head’s 41 and three fields of 48');
      near(classification.y - bottom(side), 32, 2, 'the sections’ gap');
      // No frame round a section.
      expect(await styleOf(region(card, 'Планирование'), 'box-shadow')).toBe('none');
      expect(await styleOf(region(card, 'Планирование'), 'border-top-width')).toBe('0px');

      // «Связанная работа»: the parent picker, the open link, the children; no «Добавить связь» (D-3).
      const related = region(card, 'Связанная работа');
      await expect(parentField(card)).toHaveValue(`${byLink.identifier} Вход по ссылке`);
      await expect(
        related.getByRole('link', { name: `Открыть ${byLink.identifier}` }),
      ).toBeVisible();
      await expect(
        related.getByRole('heading', { level: 3, name: 'Дочерние элементы (3)' }),
      ).toBeVisible();
      const children = related.getByRole('list', { name: 'Дочерние элементы (3)' });
      for (const child of [lost, template, smtp])
        await expect(children).toContainText(child.identifier);
      await expect(card.getByText('Добавить связь')).toHaveCount(0);
      await expect(card.getByRole('button', { name: /Добавить связь/ })).toHaveCount(0);
      // Nothing stands above it where the reference has the deployment and development blocks.
      await expect(card.getByText('Развёртывание', { exact: true })).toHaveCount(0);
      await expect(card.getByText('Разработка', { exact: true })).toHaveCount(0);
    });

    await test.step('a field: label above its value, no frame at rest, 1 px on hover, 2 px accent on focus, the hint on the label’s line', async () => {
      const card = await open(TYPE.story, story.identifier);
      await page.mouse.move(0, 0);
      const planning = region(card, 'Планирование');
      const points = textbox(card, 'Оценка в пунктах');
      const priority = list(card, 'Приоритет');
      const risk = list(card, 'Риск');

      // The label sits above the value: label at the section's inset 8, the text 6 further in.
      const planningBox = await box(planning);
      const label = await box(labelOf(planning, 'Приоритет'));
      const priorityBox = await box(boxOf(priority));
      near(label.x - planningBox.x, 8, 2, 'a label’s inset');
      expect(bottom(label)).toBeLessThanOrEqual(priorityBox.y + 1);
      near((await textLeft(priority)) - planningBox.x, 14, 2, 'a value’s text');
      // Fields stand 48 apart, empty or filled.
      near(priorityBox.y - (await box(boxOf(points))).y, 48, 2, 'the pitch of two fields');
      near((await box(boxOf(risk))).y - priorityBox.y, 48, 2, 'the pitch of two fields');
      // An empty optional list shows its `emptyLabel` while nothing is chosen: US-3 and design
      // § 4.6 say «blank at rest», the kit's `InlineSelectField` pins the label
      // (`InlineSelectField.test.tsx`) and the kit wins (tech-015 D-5). An empty number, date and
      // text are blank (below, the epic).
      await expect(risk).toHaveText('Не указан');

      // No frame at rest; a 1 px frame on hover; a 2 px accent one on focus; the box never moves.
      const atRest = await box(boxOf(points));
      expect(await frameOf(points)).toEqual({ width: 0, accent: false });
      await points.hover();
      const hovered = await frameOf(points);
      expect(hovered.width).toBe(1);
      expect(hovered.accent).toBe(false);
      near((await box(boxOf(points))).height, atRest.height, 0.5, 'the box on hover');
      await page.mouse.move(0, 0);
      await points.focus();
      expect(await frameOf(points)).toEqual({ width: 2, accent: true });
      const focused = await box(boxOf(points));
      near(focused.height, atRest.height, 0.5, 'the box on focus');
      near(focused.width, atRest.width, 0.5, 'the box on focus');
      expect(await frameOf(risk), 'the frame is only on the field in focus').toEqual({
        width: 0,
        accent: false,
      });
      await risk.hover();
      expect((await frameOf(risk)).width).toBe(1);
      await page.mouse.move(0, 0);

      // The hint: in the description always, drawn only on focus, on the label's own line, right-aligned.
      await expect(priority).toHaveAccessibleDescription('1 — самый высокий');
      const hint = planning.getByText('1 — самый высокий', { exact: true });
      await expect(hint).toBeHidden();
      const riskBefore = await box(risk);
      await priority.focus();
      await expect(hint).toBeVisible();
      const hintBox = await box(hint);
      near(centreY(hintBox), centreY(label), 3, 'the hint stands on the label’s line');
      near(right(hintBox), right(planningBox) - 8, 3, 'the hint is right-aligned');
      near((await box(risk)).y, riskBefore.y, 0.5, 'no field below moves');
      // The hint goes with the focus.
      await points.focus();
      await expect(hint).toBeHidden();
      await page.mouse.move(0, 0);
    });

    await test.step('a bug: three texts, then «Планирование» and «Трудозатраты (часы)»', async () => {
      const card = await open(TYPE.bug, bug.identifier);
      expect(await sectionOrder(card)).toEqual([
        'Описание',
        'Шаги воспроизведения',
        'Сведения о системе',
        'Планирование',
        'Трудозатраты (часы)',
        'Связанная работа',
      ]);
      expect(await columnsOf(card)).toBe('three');
      await expect(labelsOf(card, 'Планирование')).toHaveText([
        'Оценка в пунктах',
        'Приоритет',
        'Серьёзность',
        'Вид работ',
      ]);
      await expect(labelsOf(card, 'Трудозатраты (часы)')).toHaveText([
        'Исходная оценка',
        'Оставшаяся работа',
        'Выполненная работа',
      ]);
      await expect(list(card, 'Серьёзность')).toHaveText('3 — Средняя');
      await expect(textbox(card, 'Шаги воспроизведения')).toHaveValue(
        'Открыть письмо через 20 минут',
      );
      await expect(textbox(card, 'Сведения о системе')).toHaveValue('');
      await expect(card.getByText('Классификация', { exact: true })).toHaveCount(0);
    });

    await test.step('a task: «Планирование» with «Вид работ», «Трудозатраты (часы)» with the estimate', async () => {
      const card = await open(TYPE.task, task.identifier);
      expect(await sectionOrder(card)).toEqual([
        'Описание',
        'Планирование',
        'Трудозатраты (часы)',
        'Связанная работа',
      ]);
      await expect(labelsOf(card, 'Планирование')).toHaveText(['Приоритет', 'Вид работ']);
      await expect(labelsOf(card, 'Трудозатраты (часы)')).toHaveText([
        'Исходная оценка',
        'Оставшаяся работа',
        'Выполненная работа',
      ]);
      await expect(list(card, 'Приоритет')).toHaveText('3');
      await expect(list(card, 'Вид работ')).toHaveText('Разработка');
      await expect(textbox(card, 'Исходная оценка')).toHaveValue('8');
      await expect(textbox(card, 'Оставшаяся работа')).toHaveValue('');
    });

    await test.step('an epic: the seven planning fields, an empty text and an empty number blank until hover and focus', async () => {
      const card = await open(TYPE.epic, epic.identifier);
      expect(await sectionOrder(card)).toEqual([
        'Описание',
        'Планирование',
        'Классификация',
        'Связанная работа',
      ]);
      await expect(labelsOf(card, 'Планирование')).toHaveText([
        'Приоритет',
        'Риск',
        'Трудоёмкость',
        'Бизнес-ценность',
        'Критичность по времени',
        'Дата начала',
        'Целевая дата',
      ]);
      await expect(textbox(card, 'Трудоёмкость')).toHaveValue('20');
      await expect(textbox(card, 'Бизнес-ценность')).toHaveValue('300');
      await expect(list(card, 'Приоритет')).toHaveText('1');
      await expect(date(card, 'Дата начала')).toHaveValue('2026-10-01');

      // An empty number: blank at rest, its placeholder (always the attribute) shown on hover and focus.
      const criticality = textbox(card, 'Критичность по времени');
      await expect(criticality).toHaveValue('');
      await expect(criticality).toHaveAttribute('placeholder', 'Не задано');
      await page.mouse.move(0, 0);
      expect(await placeholderOpacity(criticality)).toBe('0');
      await criticality.hover();
      expect(await placeholderOpacity(criticality)).toBe('1');
      await page.mouse.move(0, 0);

      // An empty text: its hint and placeholder show on focus; the head names it.
      const description = textbox(card, 'Описание');
      await expect(description).toHaveValue('');
      await expect(description).toHaveAttribute('placeholder', 'Добавьте описание');
      expect(await placeholderOpacity(description)).toBe('0');
      const hint = region(card, 'Описание').getByText('До 100 000 символов', { exact: true });
      await expect(description).toHaveAccessibleDescription('До 100 000 символов');
      await expect(hint).toBeHidden();
      await description.focus();
      expect(await placeholderOpacity(description)).toBe('1');
      await expect(hint).toBeVisible();
      expect((await box(hint)).y, 'the hint stands under the text').toBeGreaterThan(
        (await box(description)).y,
      );
    });

    await test.step('the columns fold by the card’s own width: three, two at 1 100 and at 800, one at 640', async () => {
      const card = await open(TYPE.story, story.identifier);
      await expect.poll(() => columnsOf(card)).toBe('three');

      await page.setViewportSize({ width: 1100, height: 900 });
      await expect.poll(() => columnsOf(card)).toBe('two');
      const wide = await box(card);
      near(wide.width, 990, 2, 'the dialog at 1 100 is 90 % of the window');

      await page.setViewportSize({ width: 800, height: 800 });
      await expect.poll(() => columnsOf(card)).toBe('two');
      const full = await box(card);
      near(full.width, 800, 1, 'under 1 024 the dialog is the window');
      near(full.height, 800, 1, 'under 1 024 the dialog is the window');
      const side = await box(region(card, 'Планирование'));
      const extra = await box(region(card, 'Связанная работа'));
      const classification = await box(region(card, 'Классификация'));
      near(extra.y - bottom(classification), 32, 2, '«Связанная работа» stands under the sections');
      near(extra.x, side.x, 1, 'the two columns’ right one');

      await page.setViewportSize({ width: 640, height: 800 });
      await expect.poll(() => columnsOf(card)).toBe('one');
      // One column: a field takes at most 480, the texts take the column.
      expect((await box(list(card, 'Приоритет'))).width).toBeLessThanOrEqual(481);
      expect((await box(textbox(card, 'Описание'))).width).toBeGreaterThan(500);
      await expect
        .poll(() =>
          page.evaluate(
            () => document.documentElement.scrollWidth <= document.documentElement.clientWidth,
          ),
        )
        .toBe(true);
    });
  });

  test('27.4 only the touched fields are sent; «Сохранить и закрыть»; Enter in the title saves; empty title and a bad number are refused in the browser; a date and a parent are refused by the server and keep the other edits; a save in flight is inert (US-4)', async ({
    page,
  }) => {
    test.setTimeout(240_000);
    await signIn(page, { returnPath: '/home' });
    const fx = await fixture(page);
    const { story, byLink, userId } = await fx.seedStory();
    const { epic } = await fx.seedOthers();
    const platformApi = await fx.apiArea();
    const sso = await fx.item('FEAT', 'Вход через SSO');
    const card = cardOf(page, TYPE.story, story.identifier);
    const description = textbox(card, 'Описание');
    const open = async () => {
      await page.goto(`${fx.backlog}?workitem=${story.identifier}`);
      await expect(card).toBeVisible();
      await expect(titleField(card)).not.toHaveValue('');
    };

    await test.step('nothing changed, nothing to save; a value changed and changed back is no change', async () => {
      await open();
      await expect(saveButton(card)).toBeDisabled();
      await expect(undoButton(card)).toBeDisabled();
      await choose(page, card, 'Приоритет', '1');
      await expect(saveButton(card)).toBeEnabled();
      await expect(undoButton(card)).toBeEnabled();
      await choose(page, card, 'Приоритет', '2');
      await expect(saveButton(card)).toBeDisabled();
      await expect(undoButton(card)).toBeDisabled();
    });

    await test.step('only the touched fields are sent: another user’s change to «Область» is not overwritten', async () => {
      await fx.save(story.identifier, { areaId: platformApi.id });
      await titleField(card).fill('Письмо со ссылкой для входа');
      await choose(page, card, 'Приоритет', '1');
      const body = await saveCard(page, card, story.identifier);
      expect(body).toEqual({ title: 'Письмо со ссылкой для входа', priority: { value: 1 } });
      // The card shows what the server answered, «Область» included; the notice has the dialog stay.
      await expect(areaField(card)).toHaveValue('Платформа / API');
      await expect(titleField(card)).toHaveValue('Письмо со ссылкой для входа');
      await expect(undoButton(card)).toBeDisabled();
      await expect(card.getByText(/^Изменён: \d{2}\.\d{2}\.\d{4} \d{2}:\d{2}$/)).toBeVisible();
      await expect(card).toBeVisible();
    });

    await test.step('Enter in the title saves, as «Сохранить» does', async () => {
      await titleField(card).fill('Письмо со ссылкой для входа в систему');
      const answered = page.waitForResponse((response) =>
        isSave(response.request(), story.identifier),
      );
      await titleField(card).press('Enter');
      expect((await answered).ok()).toBe(true);
      await expect(saveButton(card)).toBeDisabled();
    });

    await test.step('«Сохранить и закрыть» saves, and only when it is accepted does the dialog close', async () => {
      await titleField(card).fill('Письмо со ссылкой для входа');
      await saveArrow(card).click();
      const answered = page.waitForResponse((response) =>
        isSave(response.request(), story.identifier),
      );
      await menuItem(page, 'Сохранить и закрыть').click();
      expect((await answered).ok()).toBe(true);
      await expect(card).toHaveCount(0);
      expect(workitemParam(page)).toBeNull();
      await open();
      await expect(titleField(card)).toHaveValue('Письмо со ссылкой для входа');
    });

    await test.step('failure: an empty title and a text that is not a number never leave the browser', async () => {
      const writes = watchSaves(page, story.identifier);
      await titleField(card).fill('');
      await saveButton(card).click();
      await expect(titleField(card)).toHaveAttribute('aria-invalid', 'true');
      await expect(titleField(card)).toHaveAccessibleDescription('Введите заголовок.');
      await expect(card.getByText('Введите заголовок.').first()).toBeVisible();
      await expect(titleField(card)).toBeFocused();

      await titleField(card).fill('Письмо со ссылкой для входа');
      await textbox(card, 'Оценка в пунктах').fill('пять');
      await saveButton(card).click();
      await expect(textbox(card, 'Оценка в пунктах')).toHaveAccessibleDescription('Введите число.');
      await expect(textbox(card, 'Оценка в пунктах')).toBeFocused();
      expect(writes).toEqual([]);
      // Editing a field clears its error; the number is put right.
      await textbox(card, 'Оценка в пунктах').fill('5');
      await expect(textbox(card, 'Оценка в пунктах')).not.toHaveAttribute('aria-invalid', 'true');
      await page.reload();
      await expect(card).toBeVisible();
    });

    await test.step('failure: a target date before the start is refused under «Целевая дата», the other edits stay, editing clears the error', async () => {
      await page.goto(`${fx.backlog}?workitem=${epic.identifier}`);
      const epicCard = cardOf(page, TYPE.epic, epic.identifier);
      await expect(epicCard).toBeVisible();
      await expect(date(epicCard, 'Дата начала')).toHaveValue('2026-10-01');
      await titleField(epicCard).fill('Вход, регистрация и выход');
      await date(epicCard, 'Целевая дата').fill('2026-09-30');
      await saveButton(epicCard).click();
      await expect(date(epicCard, 'Целевая дата')).toHaveAccessibleDescription(DATE_BEFORE_START);
      await expect(date(epicCard, 'Целевая дата')).toHaveAttribute('aria-invalid', 'true');
      await expect(date(epicCard, 'Целевая дата')).toBeFocused();
      await expect(titleField(epicCard)).toHaveValue('Вход, регистрация и выход');
      await expect(saveButton(epicCard)).toBeEnabled();
      await date(epicCard, 'Целевая дата').fill('2026-12-31');
      await expect(date(epicCard, 'Целевая дата')).not.toHaveAttribute('aria-invalid', 'true');
      const body = await saveCard(page, epicCard, epic.identifier);
      expect(Object.keys(body).sort()).toEqual(['targetDate', 'title']);
      await expect(date(epicCard, 'Целевая дата')).toHaveValue('2026-12-31');
    });

    await test.step('«Родитель» stays feat-001’s picker: a parent replaced and put back is sent with the one the user saw', async () => {
      await open();
      await pickParent(page, card, sso.identifier, sso.identifier);
      await expect(parentField(card)).toHaveValue(`${sso.identifier} Вход через SSO`);
      const replaced = await saveCard(page, card, story.identifier);
      expect(replaced).toEqual({ parent: { parentId: sso.id, seenParentId: byLink.id } });
      await expect(
        region(card, 'Связанная работа').getByRole('link', { name: `Открыть ${sso.identifier}` }),
      ).toBeVisible();
      await pickParent(page, card, 'ссылк', byLink.identifier);
      const back = await saveCard(page, card, story.identifier);
      expect(back).toEqual({ parent: { parentId: byLink.id, seenParentId: sso.id } });
      await expect(parentField(card)).toHaveValue(`${byLink.identifier} Вход по ссылке`);
    });

    await test.step('failure: the parent changed meanwhile — the card reads again, the parent is the stored one, «Описание» keeps the user’s text', async () => {
      await open();
      await fx.reparent(story.identifier, sso, byLink);
      await description.fill('Ссылка действует 15 минут, затем письмо устаревает.');
      await parentField(card).click();
      await page.getByRole('option', { name: 'Нет родителя', exact: true }).click();
      await expect(parentField(card)).toHaveValue('Нет родителя');
      await saveButton(card).click();
      await expect(parentField(card)).toHaveValue(`${sso.identifier} Вход через SSO`);
      await expect(parentField(card)).toHaveAccessibleDescription(PARENT_CHANGED);
      await expect(description).toHaveValue('Ссылка действует 15 минут, затем письмо устаревает.');
      await expect(saveButton(card)).toBeEnabled();
      // The retry, made on the fresh read, sends «Описание» alone.
      const body = await saveCard(page, card, story.identifier);
      expect(body).toEqual({ description: 'Ссылка действует 15 минут, затем письмо устаревает.' });
    });

    await test.step('the assignee and the state are fields like the rest; «Не назначен» sends a null user', async () => {
      await open();
      await choose(page, card, 'Статус', 'Активна');
      await expect(stateField(card)).toContainText('Активна');
      await expect(saveButton(card)).toBeEnabled();
      await assigneeField(card).click();
      await page.getByRole('option', { name: 'Не назначен', exact: true }).click();
      const cleared = await saveCard(page, card, story.identifier);
      expect(cleared).toEqual({
        stateId: await fx.stateOf('US', 'Активна'),
        assignee: { userId: null },
      });
      await expect(stateField(card)).toContainText('Активна');
      await expect(card.getByText('Не назначен', { exact: true })).toBeVisible();

      await assigneeField(card).click();
      await assigneeField(card).fill('e2e-fixture@');
      await page.getByRole('option', { name: /E2E Fixture/ }).click();
      const again = await saveCard(page, card, story.identifier);
      expect(again).toEqual({ assignee: { userId } });
      await expect(assigneeField(card)).toHaveValue('E2E Fixture');
    });

    await test.step('while it saves, «Сохранить» keeps focus and does nothing, the fields are busy and the dialog cannot be dismissed', async () => {
      await open();
      await description.fill('Описание, которое сохраняется');
      let release: () => void = () => undefined;
      const held = new Promise<void>((resolve) => {
        release = resolve;
      });
      const isItem = (url: URL) =>
        url.origin === new URL(env().apiUrl).origin &&
        url.pathname.endsWith(`/workitems/${story.identifier}`);
      await page.route(isItem, async (route) => {
        if (route.request().method() === 'PATCH') await held;
        await route.continue();
      });
      try {
        const sent = page.waitForRequest((request) => isSave(request, story.identifier));
        await saveButton(card).click();
        await sent;
        await expect(saveButton(card)).toHaveAttribute('aria-disabled', 'true');
        await expect(saveButton(card)).toBeFocused();
        await expect(titleField(card)).toHaveAttribute('aria-busy', 'true');
        await expect(refreshButton(card)).toHaveAttribute('aria-disabled', 'true');
        await expect(undoButton(card)).toHaveAttribute('aria-disabled', 'true');
        await page.keyboard.press('Escape');
        // The icon is inert, not disabled: a click lands and does nothing (Playwright waits for
        // "enabled" on an `aria-disabled` control, so the click is forced).
        await closeButton(card).click({ force: true });
        await page.mouse.click(8, 8);
        await expect(card).toBeVisible();
        await expect(confirmation(page, CLOSE_TITLE)).toHaveCount(0);
      } finally {
        release();
        await page.unroute(isItem);
      }
      await expect(notice(page, SAVED)).toBeVisible();
      await expect(saveButton(card)).toBeDisabled();
    });
  });

  test('27.5 «Обновить» shows what others changed and keeps what the user typed, and a failed read says why; «Отменить все изменения» asks first; «Копировать ссылку» copies the full page’s address; «Ещё действия» holds only that and has the menus’ keys; the backlog behind reads again (US-5)', async ({
    page,
  }) => {
    test.setTimeout(240_000);
    await signIn(page, { returnPath: '/home' });
    const fx = await fixture(page);
    const { story } = await fx.seedStory();
    const platformApi = await fx.apiArea();
    const card = cardOf(page, TYPE.story, story.identifier);
    const description = textbox(card, 'Описание');
    const open = async () => {
      await page.goto(`${fx.backlog}?workitem=${story.identifier}`);
      await expect(card).toBeVisible();
      await expect(titleField(card)).not.toHaveValue('');
    };

    await test.step('«Обновить» shows what another user changed', async () => {
      await open();
      await fx.save(story.identifier, { areaId: platformApi.id, priority: { value: 1 } });
      await expect(areaField(card)).toHaveValue('Платформа');
      await refreshButton(card).click();
      await expect(areaField(card)).toHaveValue('Платформа / API');
      await expect(list(card, 'Приоритет')).toHaveText('1');
      await expect(card.getByText(/^Изменён: \d{2}\.\d{2}\.\d{4} \d{2}:\d{2}$/)).toBeVisible();
      await expect(notice(page, 'Данные обновлены.')).toBeVisible();
      await expect(saveButton(card)).toBeDisabled();
    });

    await test.step('«Обновить» keeps what the user typed; a save then sends only that', async () => {
      await description.fill('Моё описание');
      await fx.save(story.identifier, { description: 'Чужое описание', priority: { value: 3 } });
      await refreshButton(card).click();
      await expect(description).toHaveValue('Моё описание');
      await expect(list(card, 'Приоритет')).toHaveText('3');
      await expect(saveButton(card)).toBeEnabled();
      await expect(
        notice(page, 'Данные обновлены. Несохранённые изменения остались на месте.'),
      ).toBeVisible();
      expect(await saveCard(page, card, story.identifier)).toEqual({ description: 'Моё описание' });
    });

    await test.step('failure: a refresh that cannot read leaves the card as it was and says why', async () => {
      const isRead = (url: URL) =>
        url.origin === new URL(env().apiUrl).origin &&
        url.pathname.endsWith(`/workitems/${story.identifier}`);
      await description.fill('Описание, которое не должно пропасть');
      await page.route(isRead, (route) =>
        route.request().method() === 'GET' ? route.abort('connectionrefused') : route.continue(),
      );
      try {
        await refreshButton(card).click();
        await expect(card.getByRole('alert').filter({ hasText: API_DOWN })).toBeVisible();
        await expect(description).toHaveValue('Описание, которое не должно пропасть');
        await expect(saveButton(card)).toBeEnabled();
        await expect(card).toBeVisible();
      } finally {
        await page.unroute(isRead);
      }
      // The next read clears the alert.
      await refreshButton(card).click();
      await expect(card.getByRole('alert').filter({ hasText: API_DOWN })).toHaveCount(0);
      await expect(description).toHaveValue('Описание, которое не должно пропасть');
    });

    await test.step('«Отменить все изменения» asks first: keep editing, then undo', async () => {
      await open();
      const stored = {
        title: await titleField(card).inputValue(),
        description: await description.inputValue(),
        priority: (await list(card, 'Приоритет').innerText()).trim(),
      };
      await titleField(card).fill('Другой заголовок');
      await choose(page, card, 'Приоритет', '4');
      await description.fill('Другое описание');
      await undoButton(card).click();
      const ask = confirmation(page, UNDO_TITLE);
      await expect(ask).toBeVisible();
      await expect(ask).toContainText(
        `Поля вернутся к сохранённым значениям. Несохранённые изменения рабочего элемента ${story.identifier} будут потеряны.`,
      );
      await expect(ask.getByRole('button', { name: KEEP_EDITING, exact: true })).toBeFocused();
      await ask.getByRole('button', { name: KEEP_EDITING, exact: true }).click();
      await expect(ask).toHaveCount(0);
      await expect(titleField(card)).toHaveValue('Другой заголовок');
      await expect(saveButton(card)).toBeEnabled();

      await undoButton(card).click();
      await ask.getByRole('button', { name: 'Отменить изменения', exact: true }).click();
      await expect(ask).toHaveCount(0);
      await expect(titleField(card)).toHaveValue(stored.title);
      await expect(description).toHaveValue(stored.description);
      await expect(list(card, 'Приоритет')).toHaveText(stored.priority);
      await expect(saveButton(card)).toBeDisabled();
      await expect(undoButton(card)).toBeDisabled();
      await expect(notice(page, 'Изменения отменены.')).toBeVisible();
    });

    await test.step('«Ещё действия» holds «Копировать ссылку» only; it puts the full page’s address on the clipboard', async () => {
      await open();
      const origin = new URL(page.url()).origin;
      await page.context().grantPermissions(['clipboard-read', 'clipboard-write'], { origin });
      await moreButton(card).click();
      await expect(
        page.getByRole('menu', { name: 'Ещё действия', exact: true }).getByRole('menuitem'),
      ).toHaveText(['Копировать ссылку']);
      await menuItem(page, 'Копировать ссылку').click();
      await expect(notice(page, 'Ссылка скопирована.')).toBeVisible();
      expect(await page.evaluate(() => navigator.clipboard.readText())).toBe(
        `${origin}${fx.pageOf(story.identifier)}`,
      );

      // The browser has no clipboard to give.
      await page.evaluate(() =>
        Object.defineProperty(navigator, 'clipboard', { value: undefined, configurable: true }),
      );
      await moreButton(card).click();
      await menuItem(page, 'Копировать ссылку').click();
      await expect(notice(page, 'Не удалось скопировать ссылку.')).toBeVisible();
    });

    await test.step('the menu has the menus’ keys: Enter, Space and ArrowDown open it, Esc closes the menu and not the dialog', async () => {
      await open();
      const more = moreButton(card);
      for (const key of ['Enter', 'Space', 'ArrowDown']) {
        await more.focus();
        await page.keyboard.press(key);
        const menu = page.getByRole('menu', { name: 'Ещё действия', exact: true });
        await expect(menu, key).toBeVisible();
        await expect(menuItem(page, 'Копировать ссылку'), key).toBeFocused();
        await page.keyboard.press('Escape');
        await expect(menu, key).toHaveCount(0);
        await expect(more, key).toBeFocused();
        await expect(card).toBeVisible();
      }
    });

    await test.step('the backlog behind reads again: its row shows the title another user gave', async () => {
      await page.goto(fx.backlog);
      await rowLink(page, story.identifier, 'Письмо со ссылкой').click();
      await expect(card).toBeVisible();
      await fx.save(story.identifier, { title: 'Письмо для входа по ссылке' });
      await refreshButton(card).click();
      await expect(titleField(card)).toHaveValue('Письмо для входа по ссылке');
      await page.keyboard.press('Escape');
      await expect(card).toHaveCount(0);
      await expect(rowLink(page, story.identifier, 'Письмо для входа по ссылке')).toBeVisible();
    });
  });

  test('27.6 every way to close asks when an edit is unsaved and Esc belongs to what is open; Back is not guarded; a switch to a related item asks too; «Развернуть на страницу» brings the edits to the page (US-6)', async ({
    page,
  }) => {
    test.setTimeout(240_000);
    await signIn(page, { returnPath: '/home' });
    const fx = await fixture(page);
    const { story, byLink } = await fx.seedStory();
    const card = cardOf(page, TYPE.story, story.identifier);
    const link = rowLink(page, story.identifier, 'Письмо со ссылкой');
    const description = textbox(card, 'Описание');
    const stored = 'Пользователь получает письмо со ссылкой для входа.';
    const writes = watchSaves(page, story.identifier);
    const openFromBacklog = async () => {
      await link.click();
      await expect(card).toBeVisible();
      await expect(description).toHaveValue(/./);
    };
    await page.goto(fx.backlog);
    await expect(link).toBeVisible();

    await test.step('Esc, «Закрыть» and a click outside each ask «Закрыть без сохранения?» and leave the card with its edit', async () => {
      await openFromBacklog();
      await description.fill('Новое описание');
      const closers: [string, () => Promise<void>][] = [
        ['Esc', () => page.keyboard.press('Escape')],
        ['«Закрыть»', () => closeButton(card).click()],
        ['a click outside the dialog', () => page.mouse.click(8, 8)],
      ];
      for (const [how, close] of closers) {
        await close();
        const ask = confirmation(page, CLOSE_TITLE);
        await expect(ask, how).toBeVisible();
        await expect(ask).toContainText(
          `Несохранённые изменения рабочего элемента ${story.identifier} будут потеряны.`,
        );
        await expect(ask.getByRole('button', { name: KEEP_EDITING, exact: true })).toBeFocused();
        await expect(
          ask.getByRole('button', { name: 'Закрыть без сохранения', exact: true }),
        ).toBeVisible();
        // The card stays behind the question, its edit kept.
        await expect(anyDialog(page), how).toHaveCount(2);
        if (how === 'Esc') {
          // Esc is the question's cancel: it closes the question, and focus is back in the card.
          await page.keyboard.press('Escape');
          await expect(ask).toHaveCount(0);
          await expect(description).toBeFocused();
        } else {
          await ask.getByRole('button', { name: KEEP_EDITING, exact: true }).click();
          await expect(ask).toHaveCount(0);
        }
        await expect(card).toBeVisible();
        await expect(description).toHaveValue('Новое описание');
      }
    });

    await test.step('«Закрыть без сохранения» closes both, drops the parameter, returns focus to the title and sends nothing', async () => {
      await closeButton(card).click();
      await confirmation(page, CLOSE_TITLE)
        .getByRole('button', { name: 'Закрыть без сохранения', exact: true })
        .click();
      await expect(anyDialog(page)).toHaveCount(0);
      expect(workitemParam(page)).toBeNull();
      expect(pathOf(page)).toBe(fx.backlog);
      await expect(link).toBeFocused();
      expect(writes).toEqual([]);
      await openFromBacklog();
      await expect(description).toHaveValue(stored);
    });

    await test.step('nothing unsaved, nothing asked: a value changed and changed back, and a saved card, close at once', async () => {
      await description.fill('Другое');
      await description.fill(stored);
      await expect(saveButton(card)).toBeDisabled();
      await page.keyboard.press('Escape');
      await expect(anyDialog(page)).toHaveCount(0);

      await openFromBacklog();
      await description.fill('Сохранённое описание');
      await saveCard(page, card, story.identifier);
      // The saved card keeps the user's place in the title (see 27.1): Esc closes it at once.
      await expect(titleField(card)).toBeFocused();
      await page.keyboard.press('Escape');
      await expect(anyDialog(page)).toHaveCount(0);
      await expect(confirmation(page, CLOSE_TITLE)).toHaveCount(0);
    });

    await test.step('Esc belongs to what is open: a list closes, not the dialog, and the edit stays', async () => {
      await openFromBacklog();
      await description.fill('Ещё одно описание');
      await stateField(card).click();
      await expect(page.getByRole('listbox')).toBeVisible();
      await page.keyboard.press('Escape');
      await expect(page.getByRole('listbox')).toHaveCount(0);
      await expect(card).toBeVisible();
      await expect(confirmation(page, CLOSE_TITLE)).toHaveCount(0);
      await expect(description).toHaveValue('Ещё одно описание');

      await assigneeField(card).click();
      await assigneeField(card).fill('e2e-fixture@');
      await expect(page.getByRole('listbox')).toBeVisible();
      await page.keyboard.press('Escape');
      await expect(page.getByRole('listbox')).toHaveCount(0);
      await expect(card).toBeVisible();
      await expect(confirmation(page, CLOSE_TITLE)).toHaveCount(0);
      await expect(description).toHaveValue('Ещё одно описание');
    });

    await test.step('Back is not guarded: the dialog closes with no question, as every page does', async () => {
      await page.goBack();
      await expect(anyDialog(page)).toHaveCount(0);
      expect(workitemParam(page)).toBeNull();
      expect(pathOf(page)).toBe(fx.backlog);
    });

    await test.step('a switch to a related item asks in its own words; keeping the edit stays, switching drops it', async () => {
      await openFromBacklog();
      await description.fill('Описание перед переходом');
      const parent = card.getByRole('link', { name: `Открыть ${byLink.identifier}`, exact: true });
      await parent.click();
      const ask = confirmation(page, SWITCH_TITLE);
      await expect(ask).toBeVisible();
      await expect(ask).toContainText(
        `Несохранённые изменения рабочего элемента ${story.identifier} будут потеряны.`,
      );
      await expect(ask.getByRole('button', { name: KEEP_EDITING, exact: true })).toBeVisible();
      await ask.getByRole('button', { name: KEEP_EDITING, exact: true }).click();
      await expect(ask).toHaveCount(0);
      await expect(description).toHaveValue('Описание перед переходом');
      expect(workitemParam(page)).toBe(story.identifier);

      await parent.click();
      await confirmation(page, SWITCH_TITLE)
        .getByRole('button', { name: 'Перейти без сохранения', exact: true })
        .click();
      const featureCard = cardOf(page, TYPE.feature, byLink.identifier);
      await expect(featureCard).toBeVisible();
      await expect.poll(() => workitemParam(page)).toBe(byLink.identifier);
      await expect(anyDialog(page)).toHaveCount(1);
      // Nothing was edited on the feature: it closes at once, back over both entries to the backlog.
      await page.keyboard.press('Escape');
      await expect(anyDialog(page)).toHaveCount(0);
      expect(workitemParam(page)).toBeNull();
      expect(pathOf(page)).toBe(fx.backlog);
      await expect(link).toBeFocused();
    });

    await test.step('«Развернуть на страницу» takes the edits to the page with no question; Back shows the dialog without them', async () => {
      await openFromBacklog();
      await titleField(card).fill('Письмо со ссылкой для входа');
      await choose(page, card, 'Приоритет', '1');
      await expandButton(card).click();

      const pageCardOfStory = pageCard(page);
      await expect.poll(() => pathOf(page)).toBe(fx.pageOf(story.identifier));
      await expect(confirmation(page, CLOSE_TITLE)).toHaveCount(0);
      await expect(
        page.getByRole('heading', {
          level: 1,
          name: `${TYPE.story} ${story.identifier}`,
          exact: true,
        }),
      ).toBeVisible();
      await expect(titleField(pageCardOfStory)).toHaveValue('Письмо со ссылкой для входа');
      await expect(list(pageCardOfStory, 'Приоритет')).toHaveText('1');
      await expect(saveButton(pageCardOfStory)).toBeEnabled();
      await expect(undoButton(pageCardOfStory)).toBeEnabled();
      // The page has neither control the dialog has.
      await expect(expandButton(pageCardOfStory)).toHaveCount(0);
      await expect(closeButton(pageCardOfStory)).toHaveCount(0);

      await page.goBack();
      await expect(card).toBeVisible();
      await expect.poll(() => workitemParam(page)).toBe(story.identifier);
      expect(pathOf(page)).toBe(fx.backlog);
      await expect(titleField(card)).toHaveValue('Письмо со ссылкой');
      await expect(saveButton(card)).toBeDisabled();
    });

    await test.step('edits do not travel to a changed item: the page shows the edited text and the stored priority; a save sends the text alone', async () => {
      await description.fill('Описание из окна');
      await fx.save(story.identifier, { priority: { value: 3 } });
      await expandButton(card).click();
      const pageCardOfStory = pageCard(page);
      await expect.poll(() => pathOf(page)).toBe(fx.pageOf(story.identifier));
      await expect(textbox(pageCardOfStory, 'Описание')).toHaveValue('Описание из окна');
      await expect(list(pageCardOfStory, 'Приоритет')).toHaveText('3');
      await expect(saveButton(pageCardOfStory)).toBeEnabled();
      expect(await saveCard(page, pageCardOfStory, story.identifier)).toEqual({
        description: 'Описание из окна',
      });

      // The trail of the page leads to the project's list.
      await page
        .getByRole('navigation', { name: 'Путь к странице' })
        .getByRole('link', { name: 'Рабочие элементы' })
        .click();
      await expect.poll(() => pathOf(page)).toBe(fx.workItems);
    });
  });

  test('27.7 the page by its address is the same card: its `h1`, trail and title, three columns, two and one by the card’s own width, the header and the band scroll with the page; the frames, the loading skeleton and a failed read answer (US-7)', async ({
    page,
  }) => {
    test.setTimeout(240_000);
    await signIn(page, { returnPath: '/home' });
    const fx = await fixture(page);
    const { story } = await fx.seedStory();
    const main = pageCard(page);
    const heading = page.getByRole('heading', {
      level: 1,
      name: `${TYPE.story} ${story.identifier}`,
      exact: true,
    });
    const navigation = page.getByRole('navigation', { name: 'Основное меню' });

    await test.step('the page is the card: one `h1`, the tab title, the trail, no dialog’s two icons, three columns', async () => {
      await page.goto(fx.pageOf(story.identifier));
      await expect(heading).toBeVisible();
      await expect(page.getByRole('heading', { level: 1 })).toHaveCount(1);
      await expect(page).toHaveTitle(`${TYPE.story} ${story.identifier} — Mustask`);
      await expect(
        page
          .getByRole('navigation', { name: 'Путь к странице' })
          .getByRole('link', { name: 'Рабочие элементы' }),
      ).toBeVisible();
      await expect(navigation.getByRole('link', { name: 'Рабочие элементы' })).toHaveAttribute(
        'aria-current',
        'true',
      );
      await expect(titleField(main)).toHaveValue('Письмо со ссылкой');
      await expect(expandButton(main)).toHaveCount(0);
      await expect(closeButton(main)).toHaveCount(0);
      await expect(tabList(main).getByRole('tab', { name: 'Детали', exact: true })).toHaveAttribute(
        'aria-selected',
        'true',
      );
      expect(await sectionOrder(main)).toEqual([
        'Описание',
        'Критерии приёмки',
        'Планирование',
        'Классификация',
        'Связанная работа',
      ]);
      // 1 440 with the navigation expanded: the body is about 1 128, 456 / 320 / 320.
      expect(await columnsOf(main)).toBe('three');
      const first = await box(region(main, 'Описание'));
      const second = await box(region(main, 'Планирование'));
      const third = await box(region(main, 'Связанная работа'));
      near(second.width, 320, 1, 'the second column');
      near(third.width, 320, 1, 'the third column');
      near(first.width, 456, 8, 'the first column at 1 440 with the navigation expanded');
    });

    await test.step('the page scrolls, not the card: the header scrolls away with it', async () => {
      await page.setViewportSize({ width: 1440, height: 420 });
      const before = await box(heading);
      const scrolled = await scrollAround(heading, 120);
      expect(scrolled, 'the page scrolls').toBeGreaterThan(0);
      near(before.y - (await box(heading)).y, scrolled, 2, 'the header moves with the page');
      await scrollAround(heading, 0);
      await page.setViewportSize({ width: 1440, height: 900 });
    });

    await test.step('three columns again with the navigation collapsed to the rail', async () => {
      await page.getByRole('button', { name: 'Свернуть меню' }).click();
      await expect(page.getByRole('button', { name: 'Развернуть меню' })).toBeVisible();
      await expect.poll(() => columnsOf(main)).toBe('three');
      await page.getByRole('button', { name: 'Развернуть меню' }).click();
      await expect(page.getByRole('button', { name: 'Свернуть меню' })).toBeVisible();
      await expect.poll(() => columnsOf(main)).toBe('three');
    });

    await test.step('two columns at 1 000 with the navigation as an overlay, one at 640, and the page never scrolls sideways', async () => {
      const noSidewaysScroll = () =>
        page.evaluate(
          () => document.documentElement.scrollWidth <= document.documentElement.clientWidth,
        );
      await page.setViewportSize({ width: 1000, height: 900 });
      await expect.poll(() => columnsOf(main)).toBe('two');
      await expect.poll(noSidewaysScroll).toBe(true);

      await page.setViewportSize({ width: 640, height: 900 });
      await expect.poll(() => columnsOf(main)).toBe('one');
      await expect.poll(noSidewaysScroll).toBe(true);
    });

    await test.step('at 640 the card is whole: the assignee on its own line, the buttons under it, the band’s blocks stacked, the stamp and the tab below', async () => {
      const assignee = await box(assigneeField(main));
      const group = await box(saveGroup(main));
      const refresh = await box(refreshButton(main));
      const more = await box(moreButton(main));
      expect(bottom(assignee), 'the assignee stands above the buttons').toBeLessThanOrEqual(
        group.y + 1,
      );
      near(centreY(refresh), centreY(group), 2, 'the buttons stand on one line');
      expect(more.x).toBeGreaterThan(group.x);
      expect(right(more)).toBeLessThanOrEqual(640);

      const statusLabel = await box(labelOf(main, 'Статус'));
      const areaLabel = await box(labelOf(main, 'Область'));
      near(areaLabel.x, statusLabel.x, 2, 'the band’s blocks stand in one column');
      expect(areaLabel.y).toBeGreaterThan(statusLabel.y + 20);
      const stamp = await box(main.getByText(/^Изменён: /));
      expect(stamp.y, 'the stamp stands under the blocks').toBeGreaterThan(areaLabel.y);
      expect((await box(tabList(main))).y, 'the tab strip stands under the stamp').toBeGreaterThan(
        stamp.y,
      );
      // Every control is there and named.
      for (const control of [
        titleField(main),
        assigneeField(main),
        saveButton(main),
        saveArrow(main),
        refreshButton(main),
        undoButton(main),
        moreButton(main),
        stateField(main),
        areaField(main),
      ]) {
        await expect(control).toBeVisible();
      }
      await page.setViewportSize({ width: 1440, height: 900 });
    });

    await test.step('from the list a title opens the full page, never the dialog', async () => {
      await page.goto(fx.workItems);
      const table = page.getByRole('table', { name: 'Рабочие элементы' });
      await table
        .getByRole('row')
        .filter({ hasText: story.identifier })
        .getByRole('link', { name: 'Письмо со ссылкой', exact: true })
        .click();
      await expect.poll(() => pathOf(page)).toBe(fx.pageOf(story.identifier));
      await expect(heading).toBeVisible();
      await expect(anyDialog(page)).toHaveCount(0);
    });

    await test.step('failure: an item, a project and an organization that are not there answer their own frames', async () => {
      await page.goto(fx.pageOf(`${fx.project.key}-US-999`));
      await expect(
        page.getByRole('heading', { level: 1, name: 'Рабочий элемент не найден' }),
      ).toBeVisible();
      await expect(page.getByText('Проверьте адрес или вернитесь на главную.')).toBeVisible();
      await expect(page.getByRole('link', { name: 'На главную' })).toBeVisible();
      await expect(navigation.getByRole('link', { name: 'Рабочие элементы' })).toHaveAttribute(
        'aria-current',
        'true',
      );

      await page.goto(
        `/organizations/${fx.organization.id}/projects/${randomUUID()}/workitems/${story.identifier}`,
      );
      await expect(page.getByRole('heading', { level: 1, name: 'Проект не найден' })).toBeVisible();
      await page.goto(
        `/organizations/${randomUUID()}/projects/${randomUUID()}/workitems/${story.identifier}`,
      );
      await expect(
        page.getByRole('heading', { level: 1, name: 'Организация не найдена' }),
      ).toBeVisible();
    });

    await test.step('loading: the frame and a skeleton at once, announced once, with the page’s own `h1`', async () => {
      const isRead = (url: URL) =>
        url.origin === new URL(env().apiUrl).origin &&
        url.pathname.endsWith(`/workitems/${story.identifier}`);
      let release: () => void = () => undefined;
      const held = new Promise<void>((resolve) => {
        release = resolve;
      });
      await page.route(isRead, async (route) => {
        if (route.request().method() === 'GET') await held;
        await route.continue();
      });
      try {
        await page.goto(fx.pageOf(story.identifier));
        const loading = page.getByRole('status', { name: 'Загрузка данных' });
        await expect(loading).toBeVisible();
        await expect(loading).toHaveCount(1);
        await expect(
          page.getByRole('heading', {
            level: 1,
            name: `Рабочий элемент ${story.identifier}`,
            exact: true,
          }),
        ).toBeVisible();
      } finally {
        release();
        await page.unroute(isRead);
      }
      await expect(heading).toBeVisible();
      await expect(page.getByRole('status', { name: 'Загрузка данных' })).toHaveCount(0);
    });

    await test.step('failure: a read that fails shows the banner with «Повторить», and the retry loads the card', async () => {
      const isRead = (url: URL) =>
        url.origin === new URL(env().apiUrl).origin &&
        url.pathname.endsWith(`/workitems/${story.identifier}`);
      await page.route(isRead, (route) => route.abort('connectionrefused'));
      try {
        await page.goto(fx.pageOf(story.identifier));
        const banner = page.getByRole('alert').filter({ hasText: 'Не удалось соединиться с API' });
        await expect(banner).toBeVisible();
        await expect(banner).toContainText('Рабочий элемент');
        await expect(banner.getByRole('button', { name: 'Повторить', exact: true })).toBeVisible();
      } finally {
        await page.unroute(isRead);
      }
      await page.getByRole('alert').getByRole('button', { name: 'Повторить', exact: true }).click();
      await expect(heading).toBeVisible();
      await expect(titleField(main)).toHaveValue('Письмо со ссылкой');
    });
  });
});
