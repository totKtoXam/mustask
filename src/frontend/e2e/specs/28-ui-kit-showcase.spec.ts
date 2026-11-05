import { expect, test, type Locator, type Page } from '@playwright/test';
import { signIn } from '../support/auth';
import { env } from '../support/env';
import {
  createEphemeralUserWithoutGroup,
  deleteEphemeralUser,
  userExistsById,
} from '../support/keycloakAdmin';

/**
 * 28. The UI kit's showcase (tech-015 design § 11; status.md D-5, D-6): the page at
 * `/ui-kit`, «Настройки» → «Интерфейс» → «Витрина компонентов», for the holders of
 * `dictionaries.languages.write`. It needs the stand only to sign in — the page makes no
 * request. What jsdom cannot show is proved here: a hover, a focus ring, a container
 * query, the real boxes and the real colours of the reference's measures.
 *
 * Cut by D-6: the showcase draws no table section, so there is no table scenario (the
 * design's 28.9 moves to tech-016) and the contents list 17 sections, not 18. Two
 * measures differ from the design's wording, both recorded in status.md: the contents
 * stand above the sections (a 320 px side column would leave the card demo under its 720 px
 * stacking threshold), and the column boxes are 1120, 720 and 520 px (the container query
 * reads the content box, which the layout's own 12 px gutters narrow, so 1100 and 700
 * would have given two and one columns).
 *
 * Selected by role and name, as every spec; the few `data-*` attributes read here are the
 * kit's own structural marks (`data-field`, `data-frame-part`, `data-column-grid`…), not
 * test ids. Measures are in CSS px at the reference's window, 1536 × 790, within 1 px.
 */

const SHOWCASE_PATH = '/ui-kit';
const TITLE = 'Витрина компонентов';

const SECTION_TITLES = [
  'Цвета',
  'Шрифт и размеры текста',
  'Размеры и отступы',
  'Значки',
  'Кнопки',
  'Меню',
  'Типы, состояния и люди',
  'Теги',
  'Длинный текст',
  'Заголовок страницы',
  'Вкладки карточки',
  'Поля ввода',
  'Разделы и колонки',
  'Карточка',
  'Окна',
  'Уведомления',
  'Пустые, ошибочные и загружаемые состояния',
];

/** The theme's colours as the browser computes them. */
const COLOUR = {
  accent: 'rgb(0, 120, 212)',
  fieldHover: 'rgb(200, 200, 200)',
  splitDisabledFill: 'rgb(240, 240, 240)',
  splitDisabledText: 'rgb(149, 149, 149)',
  error: 'rgb(196, 43, 28)',
  applicationBackground: 'rgb(248, 248, 248)',
  transparent: 'rgba(0, 0, 0, 0)',
} as const;

/** The reference's measures the kit's tokens carry (design § 3.3). */
const MEASURE = {
  bandLabelToText: 88,
  stateDotCentre: 64,
  adornmentGap: 19,
  textInset: 6,
  headerInset: 18,
  bandBlockPitch: 285,
  blockBFromCardLeft: 303,
  textAreaExtraInset: 2,
  textAreaFromSectionEdge: 16,
  textAreaBelowRule: 19,
  sectionGap: 32,
  panelBottom: 24,
  toolbarCentres: [24, 67, 110],
  dialogCloseCentre: 21,
  dialogExpandCentre: 55,
  tabIndicatorThick: 2,
  tabIndicatorUp: 4,
  typeBar: 10,
  headerHeight: 124,
  avatarCentre: 36,
  avatarTextStart: 54,
} as const;

test.use({ viewport: { width: 1536, height: 790 } });

const mainNavigation = (page: Page) => page.getByRole('navigation', { name: 'Основное меню' });
const settingsNavigation = (page: Page) => page.getByRole('navigation', { name: 'Меню настроек' });
const contentsOf = (page: Page) => page.getByRole('navigation', { name: 'Содержание витрины' });
const region = (page: Page, title: string) =>
  page.getByRole('region', { name: title, exact: true });

/** One field of a section: its root, found by the control it holds. */
function fieldOf(scope: Locator, page: Page, name: string): Locator {
  return scope.locator('[data-field]').filter({ has: page.getByLabel(name, { exact: true }) });
}

async function boxOf(
  locator: Locator,
): Promise<{ x: number; y: number; width: number; height: number }> {
  const box = await locator.boundingBox();
  if (!box) throw new Error('the element is not rendered');
  return box;
}

/** A computed property of an element, or of one of its pseudo-elements. */
async function styleOf(locator: Locator, property: string, pseudo?: string): Promise<string> {
  return locator.evaluate(
    (element, [name, pseudoElement]) =>
      getComputedStyle(element, pseudoElement ?? null).getPropertyValue(name),
    [property, pseudo] as const,
  );
}

function near(actual: number, expected: number, tolerance = 1): void {
  expect(Math.abs(actual - expected)).toBeLessThanOrEqual(tolerance + 0.01);
}

/**
 * Where an element's text starts: an input's content edge, or the first glyph of a
 * block's text (a select's value) through a range over its contents.
 */
async function textStartOf(locator: Locator): Promise<number> {
  return locator.evaluate((element) => {
    if (element instanceof HTMLInputElement || element instanceof HTMLTextAreaElement) {
      const style = getComputedStyle(element);
      return (
        element.getBoundingClientRect().left +
        parseFloat(style.borderLeftWidth) +
        parseFloat(style.paddingLeft)
      );
    }
    const range = document.createRange();
    range.selectNodeContents(element);
    return range.getBoundingClientRect().left;
  });
}

async function centreXOf(locator: Locator): Promise<number> {
  const box = await boxOf(locator);
  return box.x + box.width / 2;
}

async function openShowcase(page: Page): Promise<void> {
  await signIn(page, { returnPath: SHOWCASE_PATH });
  await expect(page.getByRole('heading', { level: 1, name: TITLE })).toBeVisible();
}

test.describe('28. the UI kit showcase', () => {
  test('28.1 the settings lead to the page, whose contents list the 17 sections, each leading to its region', async ({
    page,
  }) => {
    await signIn(page, { returnPath: '/home' });
    await mainNavigation(page).getByRole('link', { name: 'Настройки' }).click();
    await settingsNavigation(page).getByRole('link', { name: TITLE }).click();

    await expect(page).toHaveURL(new RegExp(`${SHOWCASE_PATH}$`));
    await expect(page.getByRole('heading', { level: 1, name: TITLE })).toBeVisible();
    await expect(page).toHaveTitle(`${TITLE} — Mustask`);
    await expect(settingsNavigation(page).getByText('Интерфейс', { exact: true })).toBeVisible();

    const contents = contentsOf(page);
    await expect(contents.getByRole('link')).toHaveText(SECTION_TITLES);
    for (const title of SECTION_TITLES) {
      await contents.getByRole('link', { name: title, exact: true }).click();
      await expect(region(page, title)).toBeInViewport();
    }
  });

  test('28.2 a reader without the code gets «Доступ ограничен» and has no «Интерфейс» entry', async ({
    page,
  }) => {
    const { keycloakUrl, realm } = env();
    const reader = await createEphemeralUserWithoutGroup(keycloakUrl, realm);
    try {
      await signIn(page, { username: reader.username, returnPath: SHOWCASE_PATH });
      await expect(page.getByText('Доступ ограничен')).toBeVisible();
      await expect(page.getByRole('heading', { name: TITLE })).toHaveCount(0);

      // The reader holds `dictionaries.languages.read` only: the settings show «Языки» and
      // no «Интерфейс».
      await page.goto('/dictionaries/languages');
      await expect(page.getByRole('heading', { level: 1, name: 'Языки' })).toBeVisible();
      await expect(settingsNavigation(page).getByText('Интерфейс', { exact: true })).toHaveCount(0);
      await expect(settingsNavigation(page).getByRole('link', { name: TITLE })).toHaveCount(0);
    } finally {
      await deleteEphemeralUser(keycloakUrl, realm, reader.id);
      expect(await userExistsById(keycloakUrl, realm, reader.id)).toBe(false);
    }
  });

  test('28.3 the fields: one size in every state, the frames, the hint, the placeholder and the date mask', async ({
    page,
  }) => {
    await openShowcase(page);
    const fields = region(page, 'Поля ввода');
    await fields.scrollIntoViewIfNeeded();

    // «Тема» and «Оценка в пунктах»: the box does not change size at rest, hover and focus.
    for (const name of ['Тема', 'Оценка в пунктах']) {
      const field = fieldOf(fields, page, name);
      const input = fields.getByRole('textbox', { name, exact: true });
      const frame = field.locator('[data-frame-part]').first();

      const rest = await boxOf(frame);
      expect(await styleOf(frame, 'box-shadow')).toBe('none');

      await frame.hover();
      const hovered = await boxOf(frame);
      expect(await styleOf(frame, 'box-shadow')).toContain(COLOUR.fieldHover);

      await input.focus();
      await page.mouse.move(0, 0);
      const focused = await boxOf(frame);
      const focusShadow = await styleOf(frame, 'box-shadow');
      expect(focusShadow).toContain(COLOUR.accent);
      expect(focusShadow).toContain('2px');

      for (const box of [hovered, focused]) {
        near(box.width, rest.width, 0);
        near(box.height, rest.height, 0);
      }
    }

    // The hint appears while the field is focused, and is its description all the time.
    const points = fields.getByRole('textbox', { name: 'Оценка в пунктах', exact: true });
    const hint = fieldOf(fields, page, 'Оценка в пунктах').locator('[data-field-hint]');
    await expect(points).toHaveAccessibleDescription(/От 0 до 9 999/);
    await points.blur();
    await expect(hint).toBeHidden();
    await points.focus();
    await expect(hint).toBeVisible();

    // The error, the read-only field and the select.
    const due = fields.getByRole('textbox', { name: 'Срок', exact: true });
    await expect(due).toHaveAttribute('aria-invalid', 'true');
    await expect(due).toHaveAccessibleDescription(/Введите дату полностью\./);
    await expect(fields.getByRole('textbox', { name: 'Автор', exact: true })).toHaveAttribute(
      'aria-readonly',
      'true',
    );
    const status = fields.getByRole('combobox', { name: 'Статус', exact: true });
    const statusField = fieldOf(fields, page, 'Статус');
    const caret = statusField.locator('svg[data-testid="ThinChevronDownIcon"]');
    // The caret is drawn with the frame — on hover, and while the menu is open, when the menu
    // has taken the focus away and only the field's `data-open` keeps it — and not at rest.
    await page.mouse.move(0, 0);
    expect(await styleOf(caret, 'opacity')).toBe('0');
    await statusField.hover();
    expect(await styleOf(caret, 'opacity')).toBe('1');
    await page.mouse.move(0, 0);
    await status.focus();
    await page.keyboard.press('Enter');
    await expect(page.getByRole('listbox')).toBeVisible();
    await expect(statusField).toHaveAttribute('data-open', 'true');
    expect(await styleOf(caret, 'opacity')).toBe('1');
    await page.keyboard.press('Escape');
    await expect(page.getByRole('listbox')).toBeHidden();
    await expect(statusField).not.toHaveAttribute('data-open', /.*/);

    // The empty comment has its placeholder attribute with no hover and no focus; the text
    // is transparent at rest and visible on hover and on focus.
    const comment = fields.getByRole('textbox', { name: 'Комментарий', exact: true });
    await page.mouse.move(0, 0);
    await comment.evaluate((element) => (element as HTMLElement).blur());
    await expect(comment).toHaveAttribute('placeholder', 'Добавьте комментарий');
    expect(await styleOf(comment, 'opacity', '::placeholder')).toBe('0');
    await fieldOf(fields, page, 'Комментарий').hover();
    expect(await styleOf(comment, 'opacity', '::placeholder')).toBe('1');
    await comment.focus();
    await page.mouse.move(0, 0);
    expect(await styleOf(comment, 'opacity', '::placeholder')).toBe('1');

    // The empty date hides its mask and its button at rest, and the box keeps its size.
    const target = fields.getByLabel('Целевая дата', { exact: true });
    const targetFrame = fieldOf(fields, page, 'Целевая дата').locator('[data-frame-part]').first();
    await target.evaluate((element) => (element as HTMLElement).blur());
    await page.mouse.move(0, 0);
    const dateRest = await boxOf(targetFrame);
    expect(await styleOf(target, 'opacity', '::-webkit-datetime-edit')).toBe('0');
    expect(await styleOf(target, 'opacity', '::-webkit-calendar-picker-indicator')).toBe('0');
    await targetFrame.hover();
    expect(await styleOf(target, 'opacity', '::-webkit-datetime-edit')).toBe('1');
    expect(await styleOf(target, 'opacity', '::-webkit-calendar-picker-indicator')).toBe('1');
    const dateHovered = await boxOf(targetFrame);
    near(dateHovered.width, dateRest.width, 0);
    near(dateHovered.height, dateRest.height, 0);

    // A filled date shows its text at rest.
    const start = fields.getByLabel('Дата начала', { exact: true });
    await page.mouse.move(0, 0);
    expect(await styleOf(start, 'opacity', '::-webkit-datetime-edit')).toBe('1');

    // A date cleared in part stays cleared — the control does not snap back to the old date —
    // and completing it again gives a value again (review R1-1).
    await start.focus();
    await page.keyboard.press('Backspace');
    await expect(start).toHaveValue('');
    await start.fill('2026-10-05');
    await expect(start).toHaveValue('2026-10-05');
  });

  test('28.4 buttons and menus: busy keeps focus, the split button’s clean greys, the menu’s keys', async ({
    page,
  }) => {
    await openShowcase(page);

    const buttons = region(page, 'Кнопки');
    const busy = buttons.getByRole('button', { name: 'Основная: занято', exact: true });
    await busy.focus();
    await busy.click();
    await expect(busy).toBeFocused();
    await expect(busy).toHaveAttribute('aria-disabled', 'true');

    const disabledGroup = buttons.getByRole('group', { name: 'Сохранение: отключено' });
    for (const part of [
      disabledGroup.getByRole('button', { name: 'Сохранить', exact: true }),
      disabledGroup.getByRole('button', { name: 'Ещё варианты: отключено', exact: true }),
    ]) {
      expect(await styleOf(part, 'background-color')).toBe(COLOUR.splitDisabledFill);
      expect(await styleOf(part, 'color')).toBe(COLOUR.splitDisabledText);
    }
    const enabledMain = buttons
      .getByRole('group', { name: 'Сохранение: в покое' })
      .getByRole('button', { name: 'Сохранить', exact: true });
    expect(await styleOf(enabledMain, 'background-color')).toBe(COLOUR.accent);

    const menus = region(page, 'Меню');
    const more = menus.getByRole('button', { name: 'Ещё действия', exact: true });
    const menu = page.getByRole('menu', { name: 'Ещё действия' });
    for (const key of ['Enter', 'Space', 'ArrowDown']) {
      await more.focus();
      await page.keyboard.press(key);
      await expect(menu).toBeVisible();
      if (key === 'Enter') {
        expect(await styleOf(menu.getByRole('menuitem', { name: 'Удалить' }), 'color')).toBe(
          COLOUR.error,
        );
      }
      await page.keyboard.press('Escape');
      await expect(menu).toBeHidden();
      await expect(more).toBeFocused();
    }
  });

  test('28.5 the tabs: arrows, Enter, the linked panel and an indicator 2 thick, label-wide, 4 up', async ({
    page,
  }) => {
    await openShowcase(page);
    const tabsRegion = region(page, 'Вкладки карточки');
    const strip = tabsRegion.getByRole('tablist', { name: 'Разделы демонстрационной карточки' });

    const first = strip.getByRole('tab', { name: /^Связи/ });
    await first.focus();
    await page.keyboard.press('ArrowRight');
    const second = strip.getByRole('tab', { name: /^История/ });
    await expect(second).toBeFocused();
    await page.keyboard.press('Enter');
    await expect(second).toHaveAttribute('aria-selected', 'true');
    await expect(tabsRegion.getByRole('tabpanel')).toHaveAccessibleName(/^История/);

    const indicator = second.locator('[data-tab-indicator]');
    const indicatorBox = await boxOf(indicator);
    const stripBox = await boxOf(strip);
    const labelBox = await boxOf(second.locator('[data-tab-label]'));
    near(indicatorBox.height, MEASURE.tabIndicatorThick);
    near(indicatorBox.width, labelBox.width);
    near(
      stripBox.y + stripBox.height - (indicatorBox.y + indicatorBox.height),
      MEASURE.tabIndicatorUp,
    );
  });

  test('28.6 the windows: the card’s window draws no dimming, its guard, the busy window, the icons’ places', async ({
    page,
  }) => {
    await openShowcase(page);
    const windows = region(page, 'Окна');
    const cardWindow = page.getByRole('dialog', { name: 'Пользовательская история MSK-US-2' });
    const rootOf = (dialog: Locator) => page.locator('.MuiDialog-root').filter({ has: dialog });

    // The card’s window: no dimming, a shadow, its header and band stay, its icons.
    const opener = windows.getByRole('button', { name: 'Открыть окно карточки', exact: true });
    await opener.click();
    await expect(cardWindow).toBeVisible();
    const backdrop = rootOf(cardWindow).locator('.MuiBackdrop-root');
    expect(await styleOf(backdrop, 'background-color')).toBe(COLOUR.transparent);
    expect(await styleOf(cardWindow, 'box-shadow')).not.toBe('none');

    const windowBox = await boxOf(cardWindow);
    near(
      windowBox.x +
        windowBox.width -
        (await centreXOf(cardWindow.getByRole('button', { name: 'Закрыть', exact: true }))),
      MEASURE.dialogCloseCentre,
    );
    near(
      windowBox.x +
        windowBox.width -
        (await centreXOf(
          cardWindow.getByRole('button', { name: 'Развернуть на весь экран', exact: true }),
        )),
      MEASURE.dialogExpandCentre,
    );

    const header = cardWindow.locator('[data-dialog-area="header"]');
    const headerTop = (await boxOf(header)).y;
    await cardWindow.locator('[data-dialog-area="panel"]').evaluate((element) => {
      element.scrollTop = element.scrollHeight;
    });
    expect(
      await cardWindow.locator('[data-dialog-area="panel"]').evaluate((e) => e.scrollTop),
    ).toBeGreaterThan(0);
    near((await boxOf(header)).y, headerTop, 0);
    await expect(cardWindow.locator('[data-dialog-area="band"]')).toBeInViewport();

    // Escape closes it and focus returns to the button.
    await page.keyboard.press('Escape');
    await expect(cardWindow).toBeHidden();
    await expect(opener).toBeFocused();

    // A click outside reaches the close path.
    await opener.click();
    await expect(cardWindow).toBeVisible();
    await page.mouse.click(4, 4);
    await expect(cardWindow).toBeHidden();

    // The guard: an edit makes Escape open the confirmation, which dims; a second Escape
    // closes only the confirmation.
    await windows
      .getByRole('button', { name: 'Открыть окно с защитой от потери изменений' })
      .click();
    await expect(cardWindow).toBeVisible();
    await cardWindow
      .getByRole('textbox', { name: 'Заголовок', exact: true })
      .fill('Письмо со ссылкой!');
    await page.keyboard.press('Escape');
    const confirmation = page.getByRole('dialog', { name: 'Закрыть без сохранения?' });
    await expect(confirmation).toBeVisible();
    expect(
      await styleOf(rootOf(confirmation).locator('.MuiBackdrop-root'), 'background-color'),
    ).not.toBe(COLOUR.transparent);
    await page.keyboard.press('Escape');
    await expect(confirmation).toBeHidden();
    await expect(cardWindow).toBeVisible();
    await cardWindow.getByRole('button', { name: 'Закрыть', exact: true }).click();
    await confirmation.getByRole('button', { name: 'Закрыть без сохранения' }).click();
    await expect(cardWindow).toBeHidden();

    // The busy window ignores Escape and the outside click.
    await windows.getByRole('button', { name: 'Открыть занятое окно' }).click();
    await expect(cardWindow).toBeVisible();
    await page.keyboard.press('Escape');
    await page.mouse.click(4, 4);
    await expect(cardWindow).toBeVisible();
  });

  test('28.7 the columns follow the block’s width, and the sections keep 32 px between them', async ({
    page,
  }) => {
    await openShowcase(page);
    const sections = region(page, 'Разделы и колонки');
    const gridOf = (width: number) =>
      sections
        .getByRole('group', { name: `Блок ${width}`, exact: true })
        .locator('[data-column-grid]');

    for (const [width, columns] of [
      [1120, 3],
      [720, 2],
      [520, 1],
    ] as const) {
      await gridOf(width).scrollIntoViewIfNeeded();
      expect((await styleOf(gridOf(width), 'grid-template-columns')).split(' ')).toHaveLength(
        columns,
      );
    }

    // One column: two consecutive sections of the main area are 32 apart, and the last
    // section stands the panel's 24 above its bottom.
    const narrow = sections.getByRole('group', { name: 'Блок 520', exact: true });
    const main = await boxOf(narrow.getByRole('region', { name: 'Основное', exact: true }));
    const details = await boxOf(narrow.getByRole('region', { name: 'Детали', exact: true }));
    near(details.y - (main.y + main.height), MEASURE.sectionGap);
    const lastNarrow = await boxOf(narrow.getByRole('region', { name: 'Связи', exact: true }));
    const panelNarrow = await boxOf(narrow.locator('[data-column-layout]'));
    near(
      panelNarrow.y + panelNarrow.height - (lastNarrow.y + lastNarrow.height),
      MEASURE.panelBottom,
    );

    // Two columns: `side` and `extra` are 32 apart.
    const wide = sections.getByRole('group', { name: 'Блок 720', exact: true });
    const side = await boxOf(wide.getByRole('region', { name: 'Свойства', exact: true }));
    const extra = await boxOf(wide.getByRole('region', { name: 'Связи', exact: true }));
    near(extra.y - (side.y + side.height), MEASURE.sectionGap);
  });

  test('28.8 a notice stands bottom left, a second replaces it, and it is gone after 6 seconds', async ({
    page,
  }) => {
    await openShowcase(page);
    const notices = region(page, 'Уведомления');
    await notices.getByRole('button', { name: 'Показать уведомление', exact: true }).click();
    const first = page.getByText('Изменения сохранены.');
    await expect(first).toBeVisible();
    const box = await boxOf(first);
    expect(box.x).toBeLessThan(768);
    expect(box.y).toBeGreaterThan(395);

    await notices.getByRole('button', { name: 'Показать успех', exact: true }).click();
    await expect(page.getByText('Рабочий элемент MSK-US-24 создан.')).toBeVisible();
    await expect(first).toBeHidden();

    await page.waitForTimeout(3000);
    await expect(page.getByText('Рабочий элемент MSK-US-24 создан.')).toBeVisible();
    await expect(page.getByText('Рабочий элемент MSK-US-24 создан.')).toBeHidden({ timeout: 8000 });
  });

  test('28.10 at 640 × 800 the page does not scroll sideways and every section is reachable', async ({
    page,
  }) => {
    await page.setViewportSize({ width: 640, height: 800 });
    await openShowcase(page);

    const scrollsSideways = () =>
      page.evaluate(
        () => document.documentElement.scrollWidth > document.documentElement.clientWidth + 1,
      );
    expect(await scrollsSideways()).toBe(false);
    for (const title of SECTION_TITLES) {
      await contentsOf(page).getByRole('link', { name: title, exact: true }).click();
      await expect(region(page, title)).toBeInViewport();
    }
    expect(await scrollsSideways()).toBe(false);
  });

  test('28.11 the card’s band and texts line up by the reference’s measures', async ({ page }) => {
    await openShowcase(page);
    const card = region(page, 'Карточка');
    await card.scrollIntoViewIfNeeded();
    const panel = card.locator('[data-panel-shell]').first();
    const panelBox = await boxOf(panel);

    // «Статус» and «Область»: the text starts 88 from the label, the dot's centre is 64 and its
    // right edge 19 before the text, and the hover frame starts 6 before the dot.
    const statusField = card
      .locator('[data-field]')
      .filter({ has: page.getByRole('combobox', { name: 'Статус', exact: true }) });
    const areaField = fieldOf(card, page, 'Область');
    const statusLabelX = (await boxOf(statusField.locator('label').first())).x;
    const areaLabelX = (await boxOf(areaField.locator('label').first())).x;
    const statusTextX = await textStartOf(
      card.getByRole('combobox', { name: 'Статус', exact: true }),
    );
    const areaTextX = await textStartOf(card.getByLabel('Область', { exact: true }));
    near(statusTextX - statusLabelX, MEASURE.bandLabelToText);
    near(areaTextX - areaLabelX, MEASURE.bandLabelToText);
    near(statusTextX, areaTextX);

    const dot = await boxOf(statusField.locator('.WorkTrackingCategoryMarker'));
    near(dot.x + dot.width / 2 - statusLabelX, MEASURE.stateDotCentre);
    near(statusTextX - (dot.x + dot.width), MEASURE.adornmentGap);
    await statusField.locator('[data-frame-part]').first().hover();
    near(
      (await boxOf(statusField.locator('[data-frame-part="lead"]'))).x,
      dot.x - MEASURE.textInset,
    );

    // Block A’s label stands `headerInset` from the card’s left edge, block B’s 303.
    near(statusLabelX - panelBox.x, MEASURE.headerInset);
    const priorityField = card
      .locator('[data-field]')
      .filter({ has: page.getByRole('combobox', { name: 'Приоритет', exact: true }) });
    near(
      (await boxOf(priorityField.locator('label').first())).x - panelBox.x,
      MEASURE.blockBFromCardLeft,
    );
    near(
      (await boxOf(priorityField.locator('label').first())).x - statusLabelX,
      MEASURE.bandBlockPitch,
    );

    // The multi-line «Описание» of the fields section starts its text 2 px further in than a
    // single-line value of the same section.
    const fields = region(page, 'Поля ввода');
    await fields.scrollIntoViewIfNeeded();
    const topic = fields.getByRole('textbox', { name: 'Тема', exact: true });
    const description = fields.getByRole('textbox', { name: 'Описание', exact: true });
    const topicInset = (await textStartOf(topic)) - (await boxOf(fieldOf(fields, page, 'Тема'))).x;
    const descriptionInset =
      (await textStartOf(description)) - (await boxOf(fieldOf(fields, page, 'Описание'))).x;
    near(descriptionInset - topicInset, MEASURE.textAreaExtraInset);

    // The text under its head in the card: its box 19 below the section’s rule, its text 16
    // from the section’s left edge, no label of its own — and still named «Описание».
    await card.scrollIntoViewIfNeeded();
    const section = card.getByRole('region', { name: 'Описание', exact: true });
    const head = section.getByRole('heading', { level: 3, name: 'Описание' });
    const headBox = await boxOf(head.locator('xpath=..'));
    const text = section.getByRole('textbox', { name: 'Описание', exact: true });
    near((await boxOf(text)).y - (headBox.y + headBox.height), MEASURE.textAreaBelowRule, 2);
    near((await textStartOf(text)) - (await boxOf(section)).x, MEASURE.textAreaFromSectionEdge);
    await expect(section.locator('label')).toHaveCount(0);
  });

  test('28.12 the card’s header: the toolbar’s centres, the type bar, the band, the assignee', async ({
    page,
  }) => {
    await openShowcase(page);
    const card = region(page, 'Карточка');
    await card.scrollIntoViewIfNeeded();
    const panelBox = await boxOf(card.locator('[data-panel-shell]').first());
    const right = panelBox.x + panelBox.width;

    const toolbar = [
      card.getByRole('button', { name: 'Ещё действия', exact: true }),
      card.getByRole('button', { name: 'Отменить изменения', exact: true }),
      card.getByRole('button', { name: 'Обновить', exact: true }),
    ];
    for (const [index, button] of toolbar.entries()) {
      near(right - (await centreXOf(button)), MEASURE.toolbarCentres[index] ?? 0);
    }

    const header = await boxOf(card.locator('[data-card-header]').first());
    const stripe = await boxOf(card.locator('[data-type-stripe="card"]').first());
    near(stripe.width, MEASURE.typeBar);
    near(stripe.height, MEASURE.headerHeight);
    near(header.height, MEASURE.headerHeight);
    expect(await styleOf(card.locator('[data-property-band]').first(), 'background-color')).toBe(
      COLOUR.applicationBackground,
    );

    // The assigned person’s avatar is centred 36 from the card’s left edge and its text
    // starts 54; the empty person’s glyph is centred on the same line in its own field.
    const assignee = card.getByLabel('Исполнитель', { exact: true });
    const meta = card.locator('[data-card-meta]').first();
    near(
      (await centreXOf(meta.locator('.MuiAvatar-root').first())) - panelBox.x,
      MEASURE.avatarCentre,
    );
    near((await textStartOf(assignee)) - panelBox.x, MEASURE.avatarTextStart);

    const fields = region(page, 'Поля ввода');
    const assigned = fieldOf(fields, page, 'Исполнитель');
    const empty = fieldOf(fields, page, 'Наблюдатель');
    const assignedCentre =
      (await centreXOf(assigned.locator('.MuiAvatar-root').first())) - (await boxOf(assigned)).x;
    const emptyCentre =
      (await centreXOf(empty.locator('[data-person-empty]').first())) - (await boxOf(empty)).x;
    near(emptyCentre, assignedCentre);
  });
});
