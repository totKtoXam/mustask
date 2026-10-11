import { afterEach, describe, expect, it } from 'vitest';
import { act, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { focusManager } from '@tanstack/react-query';
import { HttpResponse, http } from 'msw';
import { theme } from '../../../app/theme';
import { server } from '../../../test/mocks/server';
import {
  PROCESS_URL,
  WORK_ITEMS_URL,
  WORK_ITEM_PARENT_URL,
  process,
  processType,
} from '../../workitems/testFixtures';
import { serveBoards, type BoardsTestApi } from '../testApi';
import {
  CELL,
  actions,
  addControl,
  addRowOf,
  cellOf,
  cellText,
  collapseOneLevel,
  expandControl,
  expandOneLevel,
  findBacklogTable,
  outline,
  rowOf,
} from '../testBacklog';
import { catalogText, fullTreeDataset, ids, mskRecord, stateId } from '../testFixtures';
import { backlogAddress, renderBoards } from '../testRender';

// feat-027 US-4: a child added inline, in the tree, from the row's «+» (BR-T-9,
// BR-T-10; design § 4.5, § 4.8). US-1's data (`fullTreeDataset`); MSK-FEAT-6's
// area is «Платформа / API», every other «Платформа» item's «Платформа».

afterEach(() => {
  focusManager.setFocused(undefined);
});

const TITLE = 'Повторная отправка письма';

function creates(api: BoardsTestApi) {
  return api.writes().filter((request) => request.path.endsWith('/workitems'));
}

/** Holds every create until `release()`, then lets the in-memory API take it. */
function holdCreates() {
  let release: () => void = () => {};
  const held = new Promise<void>((resolve) => {
    release = resolve;
  });
  server.use(
    http.post(WORK_ITEMS_URL, async () => {
      await held;
      return undefined;
    }),
  );
  return { release };
}

/** Types a title into the add row's field: one paste, not a key at a time. */
async function typeTitle(input: HTMLElement, text: string) {
  await userEvent.click(input);
  await userEvent.paste(text);
}

/** The add row's field for a type under a parent, once it stands there. */
function field(type: string, parent: string): Promise<HTMLElement> {
  return screen.findByRole('textbox', { name: `Заголовок: ${type} (${parent})` });
}

/** Opens a two-entry «+» (a feature's or a story's) and chooses an entry. */
async function chooseEntry(parent: string, entry: string) {
  await userEvent.click(await addControl(parent));
  const menu = await screen.findByRole('menu', { name: `Добавить дочерний элемент (${parent})` });
  await userEvent.click(within(menu).getByRole('menuitem', { name: entry }));
}

/** On «Фичи»: a story added to MSK-FEAT-6, its field open and focused. */
async function openStoryRow() {
  await chooseEntry('MSK-FEAT-6', 'Добавить: Пользовательская история');
  const input = await field('Пользовательская история', 'MSK-FEAT-6');
  await waitFor(() => expect(input).toHaveFocus());
  return input;
}

describe('US-4 Scenario Outline: Each row offers the children its type takes', () => {
  it('MSK-EPIC-1 has «Добавить: Фича (MSK-EPIC-1)», which adds at once, with no menu behind it', async () => {
    serveBoards(fullTreeDataset());
    renderBoards(backlogAddress('epics'));
    await findBacklogTable();

    const control = await addControl('MSK-EPIC-1');

    expect(control).toHaveAccessibleName('Добавить: Фича (MSK-EPIC-1)');
    expect(control).not.toHaveAttribute('aria-haspopup');
    expect(control.querySelector('[data-testid="ThinAddIcon"]')).not.toBeNull();
  });

  it.each([
    ['MSK-FEAT-6', ['Добавить: Пользовательская история', 'Добавить: Баг']],
    ['MSK-US-2', ['Добавить: Задача', 'Добавить: Баг']],
  ])('%s has «Добавить дочерний элемент (%s)», a menu of %j', async (identifier, entries) => {
    serveBoards(fullTreeDataset());
    renderBoards(backlogAddress('epics'));
    await findBacklogTable();

    const control = await addControl(identifier);
    expect(control).toHaveAccessibleName(`Добавить дочерний элемент (${identifier})`);
    expect(control).toHaveAttribute('aria-haspopup', 'menu');
    expect(control).toHaveAttribute('aria-expanded', 'false');
    await userEvent.click(control);

    const menu = await screen.findByRole('menu', {
      name: `Добавить дочерний элемент (${identifier})`,
    });
    expect(control).toHaveAttribute('aria-expanded', 'true');
    expect(
      within(menu)
        .getAllByRole('menuitem')
        .map((entry) => entry.textContent),
    ).toEqual(entries);
  });

  it('the menu’s entries carry their types’ icons', async () => {
    serveBoards(fullTreeDataset());
    renderBoards(backlogAddress('epics'));
    await findBacklogTable();

    await userEvent.click(await addControl('MSK-FEAT-6'));

    const menu = await screen.findByRole('menu');
    const [story, bug] = within(menu).getAllByRole('menuitem');
    expect(story?.querySelector('[data-testid="StoryIcon"]')).not.toBeNull();
    expect(bug?.querySelector('[data-testid="BugReportIcon"]')).not.toBeNull();
  });

  it.each(['MSK-BUG-4', 'MSK-TASK-3'])('%s has none (D-6 OQ-6)', async (identifier) => {
    serveBoards(fullTreeDataset());
    renderBoards(backlogAddress('epics'));
    const table = await findBacklogTable();
    // The others show theirs once the process is read: wait for one, then look.
    await addControl('MSK-EPIC-1');

    expect(
      within(rowOf(table, identifier)).queryByRole('button', { name: /^Добавить/ }),
    ).toBeNull();
  });
});

describe('US-4 Scenario: The row’s «+» is grey and shows on hover, focus and selection', () => {
  it('is a small grey «+» (D-10: the reference’s pale cross, not D-7’s orange) in the hierarchy column, transparent at rest, shown on selection, and still a tab stop while transparent', async () => {
    serveBoards(fullTreeDataset());
    renderBoards(backlogAddress('epics'));
    const table = await findBacklogTable();
    const control = await addControl('MSK-EPIC-1');
    const row = rowOf(table, 'MSK-EPIC-1');

    expect(cellOf(row, CELL.hierarchy)).toContainElement(control);
    expect(getComputedStyle(control).color).toBe(
      // rgb of `shell.addAction`
      'rgb(138, 136, 134)',
    );
    expect(theme.palette.shell.addAction).toBe('#8A8886');
    expect(getComputedStyle(control).width).toBe('24px');
    expect(getComputedStyle(control).opacity).toBe('0');
    expect(control).toHaveAttribute('tabindex', '0');

    await userEvent.click(cellOf(row, CELL.type));
    expect(getComputedStyle(control).opacity).toBe('1');
  });

  it('nested rows offer theirs too, on every level', async () => {
    serveBoards(fullTreeDataset());
    renderBoards(backlogAddress('epics'));
    const table = await findBacklogTable();
    await addControl('MSK-EPIC-1');

    for (const identifier of ['MSK-FEAT-6', 'MSK-US-2', 'MSK-FEAT-8']) {
      expect(
        within(rowOf(table, identifier)).getByRole('button', { name: /^Добавить/ }),
      ).toBeInTheDocument();
    }
  });
});

describe('US-4 Scenario: A story is added to a feature', () => {
  it('opens an editable row directly beneath MSK-FEAT-6, one depth deeper, selected, its field focused; Enter saves, the row closes, MSK-US-24 stands last beneath MSK-FEAT-6, selected, and focus returns to the «+»', async () => {
    const api = serveBoards(fullTreeDataset());
    const { release } = holdCreates();
    renderBoards(backlogAddress('features'));
    const table = await findBacklogTable();
    // MSK-FEAT-6 starts expanded; collapse it: the add row opens it.
    await userEvent.click(expandControl('MSK-FEAT-6'));
    expect(expandControl('MSK-FEAT-6')).toHaveAttribute('aria-expanded', 'false');

    await chooseEntry('MSK-FEAT-6', 'Добавить: Пользовательская история');

    // The row stands beneath MSK-FEAT-6, which is expanded to show it.
    expect(expandControl('MSK-FEAT-6')).toHaveAttribute('aria-expanded', 'true');
    const add = addRowOf(table);
    expect(add).not.toBeNull();
    expect(add).toHaveAttribute('data-add-row', 'MSK-FEAT-6');
    expect(add).toHaveAttribute('data-tier', '1');
    expect(add).toHaveAttribute('data-selected', 'true');
    expect(add?.previousElementSibling).toBe(rowOf(table, 'MSK-FEAT-6'));
    expect(cellText(add as HTMLElement, CELL.type)).toBe('Пользовательская история');
    expect(cellText(add as HTMLElement, CELL.order)).toBe('');
    expect(
      cellOf(add as HTMLElement, CELL.title).querySelector('[data-testid="StoryIcon"]'),
    ).not.toBeNull();
    const input = await field('Пользовательская история', 'MSK-FEAT-6');
    await waitFor(() => expect(input).toHaveFocus());
    expect(input).toHaveAttribute('placeholder', 'Введите заголовок');
    expect(add?.querySelector('[data-grip]')).toBeNull();
    expect(within(add as HTMLElement).queryByRole('button')).toBeNull();
    // Never a drag source (D-11): neither the row nor anything round the field is draggable.
    expect(add).not.toHaveAttribute('draggable');
    expect(input.closest('[draggable="true"]')).toBeNull();

    await typeTitle(input, TITLE);
    await userEvent.keyboard('{Enter}');

    // Pending: the field is read-only with a progress indicator.
    await waitFor(() => expect(input).toHaveAttribute('aria-busy', 'true'));
    expect(input).toHaveAttribute('readonly');
    expect(add?.querySelector('.MuiCircularProgress-root')).not.toBeNull();
    release();

    expect(await screen.findByText('Рабочий элемент MSK-US-24 создан.')).toBeInTheDocument();
    await waitFor(() => expect(addRowOf(table)).toBeNull());
    expect(outline(table)).toEqual([
      'MSK-FEAT-8',
      '  MSK-FEAT-23',
      'MSK-FEAT-6',
      '  MSK-US-2',
      '    MSK-BUG-5',
      '    MSK-TASK-3',
      '  MSK-BUG-4',
      '  MSK-US-24',
    ]);
    expect(rowOf(table, 'MSK-US-24')).toHaveAttribute('data-selected', 'true');
    expect(titleOf(table, 'MSK-US-24')).toBe(TITLE);
    await waitFor(() =>
      expect(
        screen.getByRole('button', {
          name: 'Добавить дочерний элемент (MSK-FEAT-6)',
          hidden: true,
        }),
      ).toHaveFocus(),
    );
    expect(creates(api)).toHaveLength(1);
  });

  it('the child takes the parent’s area, state and defaults, comes last on «Истории», and moves the parent’s «Изменён»', async () => {
    const api = serveBoards(fullTreeDataset());
    renderBoards(backlogAddress('features'));
    await findBacklogTable();
    const parentChanged = mskRecord(api.data, 6).changedAt;

    const input = await openStoryRow();
    await typeTitle(input, TITLE);
    await userEvent.keyboard('{Enter}');
    await screen.findByText('Рабочий элемент MSK-US-24 создан.');

    const created = api.item(ids.msk, 24);
    expect(created.parentId).toBe(mskRecord(api.data, 6).id);
    expect(created.areaId).toBe(ids.api);
    expect(created.stateId).toBe(stateId('US', 'Новая'));
    expect(created.assigneeId).toBeNull();
    expect(created.fields.priority).toBe(2);
    expect(api.order(ids.platformTeam, 'stories').at(-1)).toBe('MSK-US-24');
    expect(mskRecord(api.data, 6).changedAt).not.toBe(parentChanged);
  });

  it('carries its parent in one create request and sends no separate parent write', async () => {
    const api = serveBoards(fullTreeDataset());
    renderBoards(backlogAddress('features'));
    await findBacklogTable();

    const input = await openStoryRow();
    await typeTitle(input, TITLE);
    await userEvent.keyboard('{Enter}');
    await screen.findByText('Рабочий элемент MSK-US-24 создан.');

    expect(api.writes()).toHaveLength(1);
    const [post] = creates(api);
    expect(post?.body).toEqual({
      requestId: expect.stringMatching(/^[0-9a-f-]{36}$/),
      typeId: processType('US').id,
      title: TITLE,
      parentId: mskRecord(api.data, 6).id,
    });
    expect(
      api.requests.some((request) => request.path.endsWith('/parent') && request.method === 'PUT'),
    ).toBe(false);
  });
});

describe('US-4 Scenario: An epic gets a feature at once', () => {
  it('«Добавить: Фича (MSK-EPIC-1)» opens the inline row for «Фича» beneath MSK-EPIC-1 with no menu first', async () => {
    serveBoards(fullTreeDataset());
    renderBoards(backlogAddress('epics'));
    const table = await findBacklogTable();

    await userEvent.click(await addControl('MSK-EPIC-1'));

    expect(screen.queryByRole('menu')).toBeNull();
    const add = addRowOf(table);
    expect(add).toHaveAttribute('data-add-row', 'MSK-EPIC-1');
    expect(add?.previousElementSibling).toBe(rowOf(table, 'MSK-EPIC-1'));
    const input = await field('Фича', 'MSK-EPIC-1');
    await waitFor(() => expect(input).toHaveFocus());
    expect(cellText(add as HTMLElement, CELL.type)).toBe('Фича');
  });
});

describe('US-4 Scenario: Escape or an empty title cancels', () => {
  it('Escape closes the row with nothing sent and focus back on the «+»; Enter on an empty field closes it with no error; leaving it empty closes it', async () => {
    const api = serveBoards(fullTreeDataset());
    renderBoards(backlogAddress('features'));
    const table = await findBacklogTable();

    const input = await openStoryRow();
    await typeTitle(input, 'Повторная отправка');
    await userEvent.keyboard('{Escape}');
    expect(addRowOf(table)).toBeNull();
    await waitFor(() =>
      expect(
        screen.getByRole('button', { name: 'Добавить дочерний элемент (MSK-FEAT-6)' }),
      ).toHaveFocus(),
    );

    await openStoryRow();
    await userEvent.keyboard('{Enter}');
    expect(addRowOf(table)).toBeNull();
    expect(screen.queryByRole('alert')).toBeNull();

    await openStoryRow();
    await userEvent.tab();
    await waitFor(() => expect(addRowOf(table)).toBeNull());

    expect(creates(api)).toEqual([]);
  });
});

describe('US-4 Scenario: Leaving the field with a title typed keeps the row', () => {
  it('stays open with its text, nothing sent; another row’s «+» opens no second row and returns focus to the field', async () => {
    const api = serveBoards(fullTreeDataset());
    renderBoards(backlogAddress('features'));
    const table = await findBacklogTable();
    const input = await openStoryRow();
    await typeTitle(input, 'Повторная отправка');

    await userEvent.click(screen.getByRole('heading', { level: 1 }));

    expect(addRowOf(table)).not.toBeNull();
    expect(input).toHaveValue('Повторная отправка');
    await userEvent.click(await addControl('MSK-FEAT-8'));

    expect(table.querySelectorAll('tbody > tr[data-add-row]')).toHaveLength(1);
    expect(addRowOf(table)).toHaveAttribute('data-add-row', 'MSK-FEAT-6');
    expect(input).toHaveFocus();
    expect(creates(api)).toEqual([]);
  });

  it('an empty open row is closed first by another «+», which then opens its own', async () => {
    serveBoards(fullTreeDataset());
    renderBoards(backlogAddress('features'));
    const table = await findBacklogTable();
    await openStoryRow();

    await userEvent.click(await addControl('MSK-FEAT-8'));
    await userEvent.click(
      within(await screen.findByRole('menu')).getByRole('menuitem', { name: 'Добавить: Баг' }),
    );

    await waitFor(() => expect(addRowOf(table)).toHaveAttribute('data-add-row', 'MSK-FEAT-8'));
    expect(table.querySelectorAll('tbody > tr[data-add-row]')).toHaveLength(1);
  });
});

/** A beat for the field's `setTimeout(…, 0)` focus to fire, if the row were to ask for it. */
async function settle() {
  await act(async () => {
    await new Promise((resolve) => setTimeout(resolve, 50));
  });
}

/** On «Эпики»: a task being added beneath the nested MSK-US-2, with `TITLE` typed. */
async function openTaskRow() {
  await chooseEntry('MSK-US-2', 'Добавить: Задача');
  const input = await field('Задача', 'MSK-US-2');
  await waitFor(() => expect(input).toHaveFocus());
  await typeTitle(input, TITLE);
  return input;
}

describe('US-4 Scenario: An ancestor collapsed over the add row folds it away and gives it back whole (F-7)', () => {
  it('collapsing MSK-FEAT-6 hides the row, expanding it brings it back with the title typed, and focus stays on the chevron', async () => {
    serveBoards(fullTreeDataset());
    renderBoards(backlogAddress('epics'));
    const table = await findBacklogTable();
    const input = await openTaskRow();

    await userEvent.click(expandControl('MSK-FEAT-6'));
    expect(addRowOf(table)).toBeNull();
    expect(input).not.toBeInTheDocument();

    await userEvent.click(expandControl('MSK-FEAT-6'));
    const back = await field('Задача', 'MSK-US-2');
    expect(back).toHaveValue(TITLE);
    expect(addRowOf(table)).toHaveAttribute('data-add-row', 'MSK-US-2');
    // The row was not opened again: it does not take the focus off the control the user pressed.
    await settle();
    expect(expandControl('MSK-FEAT-6')).toHaveFocus();
    expect(back).not.toHaveFocus();
  });

  it('the same two header buttons: «Свернуть на один уровень» twice folds it away, «Развернуть на один уровень» brings it back with its title', async () => {
    serveBoards(fullTreeDataset());
    renderBoards(backlogAddress('epics'));
    const table = await findBacklogTable();
    await openTaskRow();

    await userEvent.click(collapseOneLevel());
    await userEvent.click(collapseOneLevel());
    expect(addRowOf(table)).toBeNull();
    await userEvent.click(expandOneLevel());
    await userEvent.click(expandOneLevel());

    expect(await field('Задача', 'MSK-US-2')).toHaveValue(TITLE);
    await settle();
    expect(expandOneLevel()).toHaveFocus();
  });

  it('the resend after a lost answer keeps its requestId through the fold: one item', async () => {
    const api = serveBoards(fullTreeDataset());
    api.loseNextAnswer('/workitems');
    renderBoards(backlogAddress('epics'));
    const table = await findBacklogTable();
    await openTaskRow();
    await userEvent.keyboard('{Enter}');
    await within(addRowOf(table) as HTMLElement).findByRole('alert');
    expect(creates(api)).toHaveLength(1);

    await userEvent.click(expandControl('MSK-FEAT-6'));
    await userEvent.click(expandControl('MSK-FEAT-6'));
    const back = await field('Задача', 'MSK-US-2');
    expect(back).toHaveValue(TITLE);
    await userEvent.click(back);
    await userEvent.keyboard('{Enter}');

    await screen.findByText(/Рабочий элемент MSK-TASK-\d+ создан\./);
    const bodies = creates(api).map((request) => request.body as { requestId: string });
    expect(bodies).toHaveLength(2);
    expect(bodies[1]?.requestId).toBe(bodies[0]?.requestId);
    expect(api.data.items.filter((record) => record.title === TITLE)).toHaveLength(1);
  });

  it('another row’s «+» while the folded-away row holds text draws the row again, with its title and focus, and opens no second row', async () => {
    const api = serveBoards(fullTreeDataset());
    renderBoards(backlogAddress('epics'));
    const table = await findBacklogTable();
    await openTaskRow();
    await userEvent.click(expandControl('MSK-FEAT-6'));
    expect(addRowOf(table)).toBeNull();

    await userEvent.click(await addControl('MSK-EPIC-20'));

    const back = await field('Задача', 'MSK-US-2');
    await waitFor(() => expect(back).toHaveFocus());
    expect(back).toHaveValue(TITLE);
    expect(table.querySelectorAll('tbody > tr[data-add-row]')).toHaveLength(1);
    expect(expandControl('MSK-FEAT-6')).toHaveAttribute('aria-expanded', 'true');
    expect(creates(api)).toEqual([]);
  });

  it('a refusal that arrives while the row is folded away draws it again, at the field, the title kept', async () => {
    const api = serveBoards(fullTreeDataset());
    const { release } = holdCreates();
    renderBoards(backlogAddress('epics'));
    const table = await findBacklogTable();
    await openTaskRow();
    await userEvent.keyboard('{Enter}');
    await userEvent.click(expandControl('MSK-FEAT-6'));
    expect(expandControl('MSK-FEAT-6')).toHaveAttribute('aria-expanded', 'false');
    mskRecord(api.data, 2).stateId = stateId('US', 'Удалена');

    release();

    const alert = await within(table).findByRole('alert');
    expect(alert).toHaveTextContent('Удалённый рабочий элемент нельзя связывать.');
    expect(expandControl('MSK-FEAT-6')).toHaveAttribute('aria-expanded', 'true');
    const input = await field('Задача', 'MSK-US-2');
    expect(input).toHaveValue(TITLE);
    expect(input).toHaveAttribute('aria-invalid', 'true');
    expect(addRowOf(table)).not.toBeNull();
  });
});

describe('US-4 Scenario: Only the add row is selected after a single-entry «+» (F-8)', () => {
  it('«Добавить: Фича (MSK-EPIC-1)» leaves MSK-EPIC-1 unselected: the add row is the one selected row', async () => {
    serveBoards(fullTreeDataset());
    renderBoards(backlogAddress('epics'));
    const table = await findBacklogTable();
    await userEvent.click(rowOf(table, 'MSK-EPIC-1'));
    expect(rowOf(table, 'MSK-EPIC-1')).toHaveAttribute('data-selected', 'true');

    await userEvent.click(await addControl('MSK-EPIC-1'));

    await field('Фича', 'MSK-EPIC-1');
    expect(rowOf(table, 'MSK-EPIC-1')).not.toHaveAttribute('data-selected');
    const selected = Array.from(table.querySelectorAll('tbody > tr[data-selected="true"]')).map(
      (row) =>
        row.hasAttribute('data-add-row') ? 'the add row' : row.getAttribute('data-identifier'),
    );
    expect(selected).toEqual(['the add row']);
  });

  it('a «+» that opens a menu does not select its row either; the entry chosen leaves only the add row selected', async () => {
    serveBoards(fullTreeDataset());
    renderBoards(backlogAddress('epics'));
    const table = await findBacklogTable();

    await userEvent.click(await addControl('MSK-FEAT-6'));
    expect(rowOf(table, 'MSK-FEAT-6')).not.toHaveAttribute('data-selected');
    await userEvent.click(
      within(await screen.findByRole('menu')).getByRole('menuitem', { name: 'Добавить: Баг' }),
    );

    await field('Баг', 'MSK-FEAT-6');
    const selected = Array.from(table.querySelectorAll('tbody > tr[data-selected="true"]')).map(
      (row) =>
        row.hasAttribute('data-add-row') ? 'the add row' : row.getAttribute('data-identifier'),
    );
    expect(selected).toEqual(['the add row']);
  });
});

describe('US-4 Scenario: A task is added to a nested story on «Эпики»', () => {
  it('MSK-US-2 is expanded and the new task stands last beneath it, after MSK-BUG-5 and MSK-TASK-3', async () => {
    const api = serveBoards(fullTreeDataset());
    renderBoards(backlogAddress('epics'));
    const table = await findBacklogTable();

    await chooseEntry('MSK-US-2', 'Добавить: Задача');
    const input = await field('Задача', 'MSK-US-2');
    await typeTitle(input, 'Проверить ссылку');
    await userEvent.keyboard('{Enter}');

    await screen.findByText('Рабочий элемент MSK-TASK-24 создан.');
    await waitFor(() =>
      expect(outline(table)).toEqual([
        'MSK-EPIC-1',
        '  MSK-FEAT-8',
        '    MSK-FEAT-23',
        '  MSK-FEAT-6',
        '    MSK-US-2',
        '      MSK-BUG-5',
        '      MSK-TASK-3',
        '      MSK-TASK-24',
        '    MSK-BUG-4',
        '  MSK-US-22',
        'MSK-EPIC-20',
      ]),
    );
    expect(api.item(ids.msk, 24).parentId).toBe(mskRecord(api.data, 2).id);
  });
});

describe('US-4 Scenario: The type is recognised by its appearance, not its name', () => {
  it('with the type of appearance `feature` named «Возможность», MSK-EPIC-1’s control reads «Добавить: Возможность (MSK-EPIC-1)» and creates an item of that type', async () => {
    const api = serveBoards(fullTreeDataset());
    server.use(
      http.get(PROCESS_URL, () =>
        HttpResponse.json({
          ...process,
          workItemTypes: process.workItemTypes.map((type) =>
            type.appearance === 'feature' ? { ...type, name: 'Возможность' } : type,
          ),
        }),
      ),
    );
    renderBoards(backlogAddress('epics'));
    await findBacklogTable();

    const control = await screen.findByRole('button', {
      name: 'Добавить: Возможность (MSK-EPIC-1)',
      hidden: true,
    });
    await userEvent.click(control);
    const input = await field('Возможность', 'MSK-EPIC-1');
    await typeTitle(input, 'Единый вход');
    await userEvent.keyboard('{Enter}');
    await screen.findByText('Рабочий элемент MSK-FEAT-24 создан.');

    expect((creates(api)[0]?.body as { typeId: string }).typeId).toBe(processType('FEAT').id);
  });
});

describe('US-4 Scenario: A double Enter creates one child', () => {
  it('Enter twice in a row sends one create', async () => {
    const api = serveBoards(fullTreeDataset());
    const { release } = holdCreates();
    renderBoards(backlogAddress('features'));
    await findBacklogTable();
    const input = await openStoryRow();
    await typeTitle(input, TITLE);

    await userEvent.keyboard('{Enter}{Enter}');
    release();

    await screen.findByText('Рабочий элемент MSK-US-24 создан.');
    expect(creates(api)).toHaveLength(1);
    expect(api.data.items.filter((record) => record.title === TITLE)).toHaveLength(1);
  });

  it('a resend after a lost answer carries the same requestId and creates one item', async () => {
    const api = serveBoards(fullTreeDataset());
    // The server makes the item, and the answer is lost: the case the requestId exists for.
    api.loseNextAnswer('/workitems');
    renderBoards(backlogAddress('features'));
    const table = await findBacklogTable();
    const input = await openStoryRow();
    await typeTitle(input, TITLE);

    await userEvent.keyboard('{Enter}');
    // No answer: the refusal shows at the field, the title kept, focus in the field.
    const alert = await within(addRowOf(table) as HTMLElement).findByRole('alert');
    expect(alert.textContent).not.toBe('');
    expect(input).toHaveAttribute('aria-invalid', 'true');
    expect(input).toHaveValue(TITLE);
    // The first create reached the server and made the item.
    expect(creates(api)).toHaveLength(1);
    expect(api.data.items.filter((record) => record.title === TITLE)).toHaveLength(1);
    await userEvent.keyboard('{Enter}');

    // The resend is the replay of the first: the same item, named in the notice.
    await screen.findByText('Рабочий элемент MSK-US-24 создан.');
    const bodies = creates(api).map((request) => request.body as { requestId: string });
    expect(bodies).toHaveLength(2);
    expect(bodies[1]?.requestId).toBe(bodies[0]?.requestId);
    expect(api.data.items.filter((record) => record.title === TITLE)).toHaveLength(1);
  });

  it('a new opening of the row makes a requestId of its own', async () => {
    const api = serveBoards(fullTreeDataset());
    api.loseNextAnswer('/workitems');
    api.loseNextAnswer('/workitems');
    renderBoards(backlogAddress('features'));
    await findBacklogTable();
    let input = await openStoryRow();
    await typeTitle(input, TITLE);
    await userEvent.keyboard('{Enter}');
    await waitFor(() => expect(input).toHaveAttribute('aria-invalid', 'true'));
    await userEvent.keyboard('{Escape}');
    await waitFor(() => expect(screen.queryByRole('textbox')).toBeNull());

    input = await openStoryRow();
    await typeTitle(input, 'Другой заголовок');
    await userEvent.keyboard('{Enter}');
    await waitFor(() => expect(creates(api)).toHaveLength(2));

    const [first, second] = creates(api).map(
      (request) => (request.body as { requestId: string }).requestId,
    );
    expect(second).not.toBe(first);
  });
});

describe('US-4 Scenario: A parent gone meanwhile refuses the create, at the field', () => {
  it('«Удалённый рабочий элемент нельзя связывать.» shows under the field, invalid, the title kept, nothing created; Escape reads the backlog again and MSK-FEAT-6 is gone', async () => {
    const api = serveBoards(fullTreeDataset());
    renderBoards(backlogAddress('features'));
    const table = await findBacklogTable();
    const input = await openStoryRow();
    mskRecord(api.data, 6).stateId = stateId('FEAT', 'Удалена');
    await typeTitle(input, TITLE);

    await userEvent.keyboard('{Enter}');

    const alert = await within(addRowOf(table) as HTMLElement).findByRole('alert');
    expect(alert).toHaveTextContent(catalogText('workitems.link.item_deleted'));
    expect(alert).toHaveTextContent('Удалённый рабочий элемент нельзя связывать.');
    expect(input).toHaveAttribute('aria-invalid', 'true');
    expect(input).toHaveValue(TITLE);
    await waitFor(() => expect(input).toHaveFocus());
    expect(api.data.items.filter((record) => record.title === TITLE)).toEqual([]);

    const reads = api.reads('/backlogs/features').length;
    await userEvent.keyboard('{Escape}');

    await waitFor(() => expect(api.reads('/backlogs/features').length).toBeGreaterThan(reads));
    await waitFor(() => expect(outline(table)).toEqual(['MSK-FEAT-8', '  MSK-FEAT-23']));
    expect(addRowOf(table)).toBeNull();
  });

  it('`workitems.link.parent_not_found` for a parent that no longer exists shows its text at the field too', async () => {
    const api = serveBoards(fullTreeDataset());
    renderBoards(backlogAddress('features'));
    const table = await findBacklogTable();
    const input = await openStoryRow();
    const index = api.data.items.indexOf(mskRecord(api.data, 6));
    api.data.items.splice(index, 1);
    await typeTitle(input, TITLE);

    await userEvent.keyboard('{Enter}');

    const alert = await within(addRowOf(table) as HTMLElement).findByRole('alert');
    expect(alert).toHaveTextContent(catalogText('workitems.link.parent_not_found'));
  });

  it('`workitems.link.depth_exceeded` (D-9) shows the server’s finished text at the field, invalid, the title kept', async () => {
    const api = serveBoards(fullTreeDataset());
    const text =
      'Слишком глубокая вложенность: в цепочке родителей может быть не больше 64 элементов.';
    server.use(
      http.post(
        WORK_ITEMS_URL,
        () =>
          HttpResponse.json(
            {
              status: 409,
              title: 'workitems.link.depth_exceeded',
              detail: text,
              errorCode: 'workitems.link.depth_exceeded',
              params: { limit: { value: 64, type: 'number' } },
            },
            { status: 409, headers: { 'Content-Type': 'application/problem+json' } },
          ),
        { once: true },
      ),
    );
    renderBoards(backlogAddress('features'));
    const table = await findBacklogTable();
    const input = await openStoryRow();
    await typeTitle(input, TITLE);

    await userEvent.keyboard('{Enter}');

    const alert = await within(addRowOf(table) as HTMLElement).findByRole('alert');
    expect(alert).toHaveTextContent(text);
    expect(input).toHaveAttribute('aria-invalid', 'true');
    expect(input).toHaveValue(TITLE);
    expect(api.data.items.filter((record) => record.title === TITLE)).toHaveLength(0);
  });
});

describe('US-4 Scenario: mod-003’s title check holds', () => {
  it('a title of 256 characters shows «Не более 255 символов.» under the field and sends nothing', async () => {
    const api = serveBoards(fullTreeDataset());
    renderBoards(backlogAddress('features'));
    const table = await findBacklogTable();
    const input = await openStoryRow();

    await userEvent.click(input);
    await userEvent.paste('а'.repeat(256));
    await userEvent.keyboard('{Enter}');

    const alert = await within(addRowOf(table) as HTMLElement).findByRole('alert');
    expect(alert).toHaveTextContent('Не более 255 символов.');
    expect(input).toHaveAttribute('aria-invalid', 'true');
    expect(creates(api)).toEqual([]);

    // Typing again takes the text off.
    await userEvent.keyboard('{Backspace}');
    expect(within(addRowOf(table) as HTMLElement).queryByRole('alert')).toBeNull();
  });
});

describe('US-4 Scenario: One write at a time', () => {
  it('while a create is saved every row is undraggable and every row «+», «Ещё действия» and both header buttons is off, until the backlog is read again', async () => {
    serveBoards(fullTreeDataset());
    const { release } = holdCreates();
    renderBoards(backlogAddress('features'));
    const table = await findBacklogTable();
    const input = await openStoryRow();
    await typeTitle(input, TITLE);
    await userEvent.keyboard('{Enter}');
    await waitFor(() => expect(input).toHaveAttribute('aria-busy', 'true'));

    for (const identifier of ['MSK-FEAT-8', 'MSK-FEAT-6']) {
      expect(rowOf(table, identifier)).toHaveAttribute('draggable', 'false');
      expect(actions(identifier)).toHaveAttribute('aria-disabled', 'true');
      expect(await addControl(identifier)).toHaveAttribute('aria-disabled', 'true');
    }
    expect(expandOneLevel()).toHaveAttribute('aria-disabled', 'true');
    expect(collapseOneLevel()).toHaveAttribute('aria-disabled', 'true');
    // A second Enter is ignored, and a menu does not open.
    await userEvent.click(actions('MSK-FEAT-8'));
    expect(screen.queryByRole('menu')).toBeNull();

    release();
    await screen.findByText('Рабочий элемент MSK-US-24 создан.');
    await waitFor(() => expect(actions('MSK-FEAT-8')).not.toHaveAttribute('aria-disabled'));
    expect(rowOf(table, 'MSK-FEAT-8')).toHaveAttribute('draggable', 'true');
    expect(collapseOneLevel()).not.toHaveAttribute('aria-disabled');
  });
});

describe('US-4 Scenario: A parent write is not part of the create', () => {
  it('a drop-free create leaves the parent’s own parent as it was', async () => {
    const api = serveBoards(fullTreeDataset());
    server.use(
      http.put(WORK_ITEM_PARENT_URL, () => {
        throw new Error('The create must not write a parent separately.');
      }),
    );
    renderBoards(backlogAddress('features'));
    await findBacklogTable();
    const input = await openStoryRow();
    await typeTitle(input, TITLE);
    await userEvent.keyboard('{Enter}');
    await screen.findByText('Рабочий элемент MSK-US-24 создан.');

    expect(mskRecord(api.data, 6).parentId).toBe(mskRecord(api.data, 1).id);
  });
});

/** The title link's text of a row. */
function titleOf(table: HTMLElement, identifier: string): string {
  return within(cellOf(rowOf(table, identifier), CELL.title)).getByRole('link', { hidden: true })
    .textContent as string;
}
