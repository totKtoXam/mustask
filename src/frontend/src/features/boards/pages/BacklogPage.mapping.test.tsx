import { afterEach, describe, expect, it } from 'vitest';
import { act, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { focusManager } from '@tanstack/react-query';
import { HttpResponse, http } from 'msw';
import { server } from '../../../test/mocks/server';
import { WORK_ITEM_PARENT_URL } from '../../workitems/testFixtures';
import { serveBoards, type BoardsTestApi } from '../testApi';
import {
  CELL,
  cellText,
  expandControl,
  findBacklogTable,
  handle,
  outline,
  parentOf,
  rowOf,
  rowsOf,
} from '../testBacklog';
import { catalogText, ids, mskRecord, stateId, treeDataset } from '../testFixtures';
import { chooseLevel } from '../testLevels';
import { backlogAddress, renderBoards } from '../testRender';

// feat-001 US-5 on US-4's «Data» (`treeDataset`): «Изменить родителя…» and
// «Убрать родителя» from a top-level row of «Фичи» or «Истории», the parent
// write (api-contract § 3.2) with the search one level up (§ 3.4), then the
// backlog read again. US-7's backlog scenarios close the file.

const PARENT_CHANGED_TEXT = catalogText('workitems.link.parent_changed');
const FAILED_TEXT = 'Не удалось сохранить родителя. Попробуйте ещё раз.';

afterEach(() => {
  focusManager.setFocused(undefined);
});

function parentWrites(api: BoardsTestApi) {
  return api.writes().filter((request) => request.path.endsWith('/parent'));
}

/** Lets the tab lose and regain focus, as the browser reports it: the backlog is read again. */
async function refocusTab() {
  await act(async () => {
    focusManager.setFocused(false);
  });
  await act(async () => {
    focusManager.setFocused(true);
  });
}

/** A row's «Ещё действия»; found while a menu or the dialog hides the page too. */
function actionsButton(identifier: string): HTMLElement {
  return screen.getByRole('button', { name: `Ещё действия: ${identifier}`, hidden: true });
}

async function openActions(identifier: string): Promise<HTMLElement> {
  await userEvent.click(actionsButton(identifier));
  return screen.findByRole('menu', { name: `Ещё действия: ${identifier}` });
}

async function openDialog(identifier: string): Promise<HTMLElement> {
  const menu = await openActions(identifier);
  await userEvent.click(within(menu).getByRole('menuitem', { name: 'Изменить родителя…' }));
  return screen.findByRole('dialog', { name: 'Изменить родителя' });
}

function parentField(dialog: HTMLElement): HTMLElement {
  return within(dialog).getByRole('combobox', { name: 'Родитель' });
}

/** The open list's lines, «<ID> <Заголовок>» without the state. */
function lines(): string[] {
  return within(screen.getByRole('listbox'))
    .getAllByRole('option')
    .map((option) => {
      const primary = option.querySelector('.MuiListItemText-primary');
      return (primary ?? option).textContent ?? '';
    });
}

/** Opens the dialog's «Родитель», waits for the list, and picks the line starting with `identifier`. */
async function pick(dialog: HTMLElement, identifier: string) {
  await userEvent.click(parentField(dialog));
  await userEvent.click(await screen.findByRole('option', { name: new RegExp(`^${identifier} `) }));
}

function save(dialog: HTMLElement): HTMLElement {
  return within(dialog).getByRole('button', { name: 'Сохранить' });
}

/** Holds every parent write until `release()`, then lets the in-memory API take it. */
function holdParentWrites() {
  let release: () => void = () => {};
  const held = new Promise<void>((resolve) => {
    release = resolve;
  });
  server.use(
    http.put(WORK_ITEM_PARENT_URL, async () => {
      await held;
      return undefined;
    }),
  );
  return { release };
}

describe('US-5 Scenario: A story gets a feature as its parent', () => {
  it('MSK-US-22 under MSK-EPIC-1 is given MSK-FEAT-8 from «Истории»: the dialog, the features only, the notice, the cell, focus back; the tree on «Фичи» follows', async () => {
    const api = serveBoards(treeDataset());
    renderBoards(backlogAddress('stories'));
    const table = await findBacklogTable();
    expect(parentOf(table, 'MSK-US-22')).toBe('MSK-EPIC-1');

    const dialog = await openDialog('MSK-US-22');
    expect(
      within(dialog).getByText('Пользовательская история MSK-US-22 «Запомнить устройство»'),
    ).toBeInTheDocument();
    const field = parentField(dialog);
    expect(field).toHaveValue('MSK-EPIC-1 Вход и регистрация');
    expect(field).toHaveFocus();
    expect(field).toHaveAccessibleDescription(
      'Предлагаются элементы уровня «Фичи». Родителя другого уровня можно выбрать в форме рабочего элемента.',
    );
    expect(save(dialog)).toBeDisabled();

    await userEvent.click(field);
    await screen.findByRole('option', { name: /^MSK-FEAT-8 / });
    expect(lines()).toEqual([
      'MSK-FEAT-23 Вход по коду',
      'MSK-FEAT-21 Аудит входа',
      'MSK-FEAT-8 Вход через SSO',
      'MSK-FEAT-6 Вход по ссылке',
    ]);
    expect(api.reads('parent-candidates')[0]?.path).toBe(
      `/api/organizations/${ids.example}/projects/${ids.msk}/workitems/MSK-US-22/parent-candidates?text=&levelAbove=true`,
    );
    await userEvent.click(screen.getByRole('option', { name: /^MSK-FEAT-8 / }));
    expect(save(dialog)).toBeEnabled();
    await userEvent.click(save(dialog));

    await waitFor(() => expect(screen.queryByRole('dialog')).toBeNull());
    expect(await screen.findByRole('alert')).toHaveTextContent(
      'У MSK-US-22 теперь родитель MSK-FEAT-8.',
    );
    expect(screen.getByRole('alert')).toHaveClass('MuiAlert-colorSuccess');
    expect(parentOf(table, 'MSK-US-22')).toBe('MSK-FEAT-8');
    await waitFor(() => expect(actionsButton('MSK-US-22')).toHaveFocus());
    expect(parentWrites(api).map((request) => request.body)).toEqual([
      { parentId: mskRecord(api.data, 8).id, seenParentId: mskRecord(api.data, 1).id },
    ]);

    await chooseLevel('Фичи');
    await waitFor(async () =>
      expect(outline(await findBacklogTable())).toEqual([
        'MSK-FEAT-8',
        'MSK-FEAT-6',
        'MSK-FEAT-23',
      ]),
    );
    await userEvent.click(expandControl('MSK-FEAT-8'));
    expect(outline(await findBacklogTable())).toEqual([
      'MSK-FEAT-8',
      '  MSK-US-22',
      'MSK-FEAT-6',
      'MSK-FEAT-23',
    ]);
  });
});

describe('US-5 Scenario: A feature gets an epic, replacing its old one in one write', () => {
  it('MSK-FEAT-6 mapped to MSK-EPIC-20 has that one parent and shows beneath it on «Эпики», its stories still beneath it', async () => {
    const api = serveBoards(treeDataset());
    renderBoards(backlogAddress('features'));
    await findBacklogTable();

    const dialog = await openDialog('MSK-FEAT-6');
    expect(parentField(dialog)).toHaveAccessibleDescription(
      'Предлагаются элементы уровня «Эпики». Родителя другого уровня можно выбрать в форме рабочего элемента.',
    );
    await pick(dialog, 'MSK-EPIC-20');
    await userEvent.click(save(dialog));
    expect(await screen.findByRole('alert')).toHaveTextContent(
      'У MSK-FEAT-6 теперь родитель MSK-EPIC-20.',
    );

    expect(mskRecord(api.data, 6).parentId).toBe(mskRecord(api.data, 20).id);
    expect(parentWrites(api)).toHaveLength(1);
    await chooseLevel('Эпики');
    await findBacklogTable();
    await waitFor(() => expect(expandControl('MSK-EPIC-20')).toBeInTheDocument());
    await userEvent.click(expandControl('MSK-EPIC-1'));
    await userEvent.click(expandControl('MSK-EPIC-20'));
    await userEvent.click(expandControl('MSK-FEAT-6'));
    expect(outline(await findBacklogTable())).toEqual([
      'MSK-EPIC-1',
      '  MSK-FEAT-8',
      'MSK-EPIC-20',
      '  MSK-FEAT-6',
      '    MSK-US-2',
      '    MSK-BUG-4',
    ]);
  });
});

describe('US-5 Scenario: «Убрать родителя» clears the parent', () => {
  it('MSK-FEAT-6 reads «Нет» at once, then the notice; it keeps its row and «Порядок»; on «Эпики» it is beneath no row', async () => {
    const api = serveBoards(treeDataset());
    const { release } = holdParentWrites();
    renderBoards(backlogAddress('features'));
    const table = await findBacklogTable();

    const menu = await openActions('MSK-FEAT-6');
    await userEvent.click(within(menu).getByRole('menuitem', { name: 'Убрать родителя' }));

    expect(parentOf(table, 'MSK-FEAT-6')).toBe('Нет');
    release();
    expect(await screen.findByRole('alert')).toHaveTextContent('У MSK-FEAT-6 больше нет родителя.');
    expect(parentOf(table, 'MSK-FEAT-6')).toBe('Нет');
    expect(cellText(rowOf(table, 'MSK-FEAT-6'), CELL.order)).toBe('2');
    expect(outline(table)).toEqual(['MSK-FEAT-8', 'MSK-FEAT-6', 'MSK-FEAT-23']);
    await waitFor(() => expect(actionsButton('MSK-FEAT-6')).toHaveFocus());
    expect(parentWrites(api).map((request) => request.body)).toEqual([
      { parentId: null, seenParentId: mskRecord(api.data, 1).id },
    ]);
    expect(mskRecord(api.data, 6).parentId).toBeNull();

    await chooseLevel('Эпики');
    await findBacklogTable();
    await userEvent.click(expandControl('MSK-EPIC-1'));
    expect(outline(await findBacklogTable())).toEqual([
      'MSK-EPIC-1',
      '  MSK-FEAT-8',
      'MSK-EPIC-20',
    ]);
  });
});

describe('US-5 Scenario: What a row’s menu offers', () => {
  it('«Фичи» and «Истории» top-level rows have «Ещё действия: <ID>» with both entries; «Убрать родителя» is disabled without a parent', async () => {
    const data = treeDataset();
    mskRecord(data, 23).parentId = null;
    serveBoards(data);
    renderBoards(backlogAddress('features'));
    const table = await findBacklogTable();

    expect(
      within(table)
        .getAllByRole('columnheader')
        .map((header) => header.textContent),
    ).toEqual([
      'Порядок',
      'Перемещение',
      'ID',
      'Тип',
      'Заголовок',
      'Родитель',
      'Исполнитель',
      'Статус',
      'Действия',
    ]);
    for (const row of rowsOf(table)) {
      const identifier = cellText(row, CELL.identifier);
      const button = within(row).getByRole('button', { name: `Ещё действия: ${identifier}` });
      expect(button).toHaveAttribute('aria-haspopup', 'menu');
      expect(button.querySelector('[data-testid="MoreHorizIcon"]')).not.toBeNull();
    }

    const menu = await openActions('MSK-FEAT-6');
    expect(actionsButton('MSK-FEAT-6')).toHaveAttribute('aria-expanded', 'true');
    const entries = within(menu).getAllByRole('menuitem');
    expect(entries.map((entry) => entry.textContent)).toEqual([
      'Изменить родителя…',
      'Убрать родителя',
    ]);
    expect(entries[0]?.querySelector('[data-testid="AccountTreeOutlinedIcon"]')).not.toBeNull();
    expect(entries[1]?.querySelector('[data-testid="LinkOffIcon"]')).not.toBeNull();
    expect(entries[1]).not.toHaveAttribute('aria-disabled');
    await userEvent.keyboard('{Escape}');

    const without = await openActions('MSK-FEAT-23');
    expect(within(without).getByRole('menuitem', { name: 'Убрать родителя' })).toHaveAttribute(
      'aria-disabled',
      'true',
    );
  });

  it('«Эпики» rows have no «Ещё действия» and no «Действия» column; nested rows have none either', async () => {
    serveBoards(treeDataset());
    renderBoards(backlogAddress('epics'));
    const table = await findBacklogTable();
    await userEvent.click(expandControl('MSK-EPIC-1'));

    expect(within(table).queryAllByRole('button', { name: /^Ещё действия/ })).toEqual([]);
    expect(within(table).getAllByRole('columnheader')).toHaveLength(8);

    await chooseLevel('Фичи');
    const features = await findBacklogTable();
    await waitFor(() =>
      expect(outline(features)).toEqual(['MSK-FEAT-8', 'MSK-FEAT-6', 'MSK-FEAT-23']),
    );
    await userEvent.click(expandControl('MSK-FEAT-6'));
    for (const nested of ['MSK-US-2', 'MSK-BUG-4']) {
      expect(
        within(rowOf(features, nested)).queryByRole('button', { name: /^Ещё действия/ }),
      ).toBeNull();
      expect(cellText(rowOf(features, nested), CELL.actions)).toBe('');
    }
  });
});

describe('US-5 Scenario: «Сохранить» waits for a different parent', () => {
  it('on MSK-FEAT-6 with MSK-EPIC-1 preselected «Сохранить» is disabled until another epic is picked; «Отмена» closes with nothing sent', async () => {
    const api = serveBoards(treeDataset());
    renderBoards(backlogAddress('features'));
    await findBacklogTable();

    const dialog = await openDialog('MSK-FEAT-6');
    expect(parentField(dialog)).toHaveValue('MSK-EPIC-1 Вход и регистрация');
    expect(save(dialog)).toBeDisabled();
    await pick(dialog, 'MSK-EPIC-1');
    expect(save(dialog)).toBeDisabled();
    await pick(dialog, 'MSK-EPIC-20');
    expect(save(dialog)).toBeEnabled();

    await userEvent.click(within(dialog).getByRole('button', { name: 'Отмена' }));
    await waitFor(() => expect(screen.queryByRole('dialog')).toBeNull());
    expect(parentWrites(api)).toEqual([]);
    await waitFor(() => expect(actionsButton('MSK-FEAT-6')).toHaveFocus());
  });
});

describe('US-5 Scenario: The mapping changes no order and no other field', () => {
  it('MSK-US-2 mapped to MSK-FEAT-8 keeps its place, Статус, Исполнитель and Область; under MSK-FEAT-8 it stands in the «Истории» order', async () => {
    const data = treeDataset();
    const us2 = mskRecord(data, 2);
    us2.assigneeId = ids.anna;
    us2.stateId = stateId('US', 'Активна');
    mskRecord(data, 22).parentId = mskRecord(data, 8).id;
    const api = serveBoards(data);
    renderBoards(backlogAddress('stories'));
    const table = await findBacklogTable();
    const before = api.order(ids.platformTeam, 'stories');

    const dialog = await openDialog('MSK-US-2');
    await pick(dialog, 'MSK-FEAT-8');
    await userEvent.click(save(dialog));
    expect(await screen.findByRole('alert')).toHaveTextContent(
      'У MSK-US-2 теперь родитель MSK-FEAT-8.',
    );

    expect(api.order(ids.platformTeam, 'stories')).toEqual(before);
    expect(outline(table)).toEqual(before);
    const stored = mskRecord(api.data, 2);
    expect([stored.stateId, stored.assigneeId, stored.areaId]).toEqual([
      stateId('US', 'Активна'),
      ids.anna,
      ids.platform,
    ]);

    await chooseLevel('Эпики');
    await findBacklogTable();
    await userEvent.click(expandControl('MSK-EPIC-1'));
    await userEvent.click(expandControl('MSK-FEAT-8'));
    expect(outline(await findBacklogTable())).toEqual([
      'MSK-EPIC-1',
      '  MSK-FEAT-8',
      '    MSK-US-2',
      '    MSK-US-22',
      '  MSK-FEAT-6',
      'MSK-EPIC-20',
    ]);
  });
});

describe('US-5 Scenario: A parent changed meanwhile refuses the mapping', () => {
  it('Борис maps MSK-US-2 to MSK-FEAT-23 after Анна mapped it to MSK-FEAT-8: the code’s text under «Родитель», the backlog read again, MSK-FEAT-8 preselected', async () => {
    const api = serveBoards(treeDataset());
    renderBoards(backlogAddress('stories'));
    const table = await findBacklogTable();
    expect(parentOf(table, 'MSK-US-2')).toBe('MSK-FEAT-6');

    const dialog = await openDialog('MSK-US-2');
    await pick(dialog, 'MSK-FEAT-23');
    // Анна's write, taken as the server would.
    mskRecord(api.data, 2).parentId = mskRecord(api.data, 8).id;
    const reads = api.reads('/backlogs/stories').length;
    await userEvent.click(save(dialog));

    await waitFor(() =>
      expect(parentField(dialog)).toHaveAccessibleDescription(PARENT_CHANGED_TEXT),
    );
    expect(parentField(dialog)).toHaveAttribute('aria-invalid', 'true');
    expect(screen.getByRole('dialog', { name: 'Изменить родителя' })).toBe(dialog);
    expect(api.reads('/backlogs/stories').length).toBeGreaterThan(reads);
    expect(parentOf(table, 'MSK-US-2')).toBe('MSK-FEAT-8');
    expect(parentField(dialog)).toHaveValue('MSK-FEAT-8 Вход через SSO');
    expect(save(dialog)).toBeDisabled();
    expect(mskRecord(api.data, 2).parentId).toBe(mskRecord(api.data, 8).id);
    expect(within(dialog).queryByRole('alert')).toBeNull();
  });

  /** Opens the dialog on MSK-US-2 (MSK-FEAT-6), then Анна's MSK-FEAT-8 lands and the tab is read again. */
  async function dialogUnderARead(api: BoardsTestApi, table: HTMLElement) {
    const dialog = await openDialog('MSK-US-2');
    expect(parentField(dialog)).toHaveValue('MSK-FEAT-6 Вход по ссылке');
    mskRecord(api.data, 2).parentId = mskRecord(api.data, 8).id;
    const reads = api.reads('/backlogs/stories').length;
    await refocusTab();
    await waitFor(() => expect(api.reads('/backlogs/stories').length).toBeGreaterThan(reads));
    await waitFor(() => expect(parentOf(table, 'MSK-US-2')).toBe('MSK-FEAT-8'));
    return dialog;
  }

  it('a re-read under the open dialog changes nothing in it: MSK-FEAT-6 still shown, «Сохранить» off until another parent is picked', async () => {
    const api = serveBoards(treeDataset());
    renderBoards(backlogAddress('stories'));
    const table = await findBacklogTable();

    const dialog = await dialogUnderARead(api, table);

    expect(parentField(dialog)).toHaveValue('MSK-FEAT-6 Вход по ссылке');
    expect(save(dialog)).toBeDisabled();
    await pick(dialog, 'MSK-FEAT-6');
    expect(save(dialog)).toBeDisabled();
    await pick(dialog, 'MSK-FEAT-23');
    expect(save(dialog)).toBeEnabled();
    expect(parentWrites(api)).toEqual([]);
  });

  it('a parent picked after that re-read is sent with MSK-FEAT-6 as seen: refused, read again, MSK-FEAT-8 preselected; the retry is decided on MSK-FEAT-8', async () => {
    const api = serveBoards(treeDataset());
    renderBoards(backlogAddress('stories'));
    const table = await findBacklogTable();
    const dialog = await dialogUnderARead(api, table);

    await pick(dialog, 'MSK-FEAT-23');
    const reads = api.reads('/backlogs/stories').length;
    await userEvent.click(save(dialog));

    await waitFor(() => expect(parentWrites(api)).toHaveLength(1));
    expect(parentWrites(api).map((request) => request.body)).toEqual([
      { parentId: mskRecord(api.data, 23).id, seenParentId: mskRecord(api.data, 6).id },
    ]);
    await waitFor(() =>
      expect(parentField(dialog)).toHaveAccessibleDescription(PARENT_CHANGED_TEXT),
    );
    expect(mskRecord(api.data, 2).parentId).toBe(mskRecord(api.data, 8).id);
    expect(api.reads('/backlogs/stories').length).toBeGreaterThan(reads);
    expect(parentField(dialog)).toHaveValue('MSK-FEAT-8 Вход через SSO');
    expect(save(dialog)).toBeDisabled();

    await pick(dialog, 'MSK-FEAT-23');
    await userEvent.click(save(dialog));
    await waitFor(() => expect(screen.queryByRole('dialog')).toBeNull());
    expect(parentWrites(api).map((request) => request.body)[1]).toEqual({
      parentId: mskRecord(api.data, 23).id,
      seenParentId: mskRecord(api.data, 8).id,
    });
    expect(parentOf(table, 'MSK-US-2')).toBe('MSK-FEAT-23');
  });

  it('a save with no answer keeps the parent the dialog showed: Анна’s MSK-FEAT-8, read after it, is refused on the retry, not overwritten', async () => {
    const api = serveBoards(treeDataset());
    // Борис's write is lost; Анна's lands before the read after it.
    server.use(
      http.put(
        WORK_ITEM_PARENT_URL,
        () => {
          mskRecord(api.data, 2).parentId = mskRecord(api.data, 8).id;
          return HttpResponse.error();
        },
        { once: true },
      ),
    );
    renderBoards(backlogAddress('stories'));
    const table = await findBacklogTable();

    const dialog = await openDialog('MSK-US-2');
    await pick(dialog, 'MSK-FEAT-23');
    await userEvent.click(save(dialog));
    expect(await within(dialog).findByRole('alert')).toHaveTextContent(FAILED_TEXT);
    await waitFor(() => expect(parentOf(table, 'MSK-US-2')).toBe('MSK-FEAT-8'));
    expect(parentField(dialog)).toHaveValue('MSK-FEAT-23 Вход по коду');

    await userEvent.click(save(dialog));
    await waitFor(() => expect(parentWrites(api)).toHaveLength(1));
    expect(parentWrites(api).map((request) => request.body)).toEqual([
      { parentId: mskRecord(api.data, 23).id, seenParentId: mskRecord(api.data, 6).id },
    ]);
    await waitFor(() =>
      expect(parentField(dialog)).toHaveAccessibleDescription(PARENT_CHANGED_TEXT),
    );
    expect(mskRecord(api.data, 2).parentId).toBe(mskRecord(api.data, 8).id);
    expect(parentField(dialog)).toHaveValue('MSK-FEAT-8 Вход через SSO');
  });
});

describe('US-5 Scenario: A refused clear puts the cell back', () => {
  it('`workitems.link.parent_changed` on «Убрать родителя»: the backlog read again shows the stored parent, and the code’s text is the error notice', async () => {
    const api = serveBoards(treeDataset());
    renderBoards(backlogAddress('features'));
    const table = await findBacklogTable();
    // Another user gave MSK-FEAT-6 MSK-EPIC-20 meanwhile.
    mskRecord(api.data, 6).parentId = mskRecord(api.data, 20).id;

    const menu = await openActions('MSK-FEAT-6');
    await userEvent.click(within(menu).getByRole('menuitem', { name: 'Убрать родителя' }));

    const alert = await screen.findByRole('alert');
    expect(alert).toHaveTextContent(PARENT_CHANGED_TEXT);
    expect(alert).toHaveClass('MuiAlert-colorError');
    expect(parentOf(table, 'MSK-FEAT-6')).toBe('MSK-EPIC-20');
    expect(mskRecord(api.data, 6).parentId).toBe(mskRecord(api.data, 20).id);
  });

  it('a re-read under the open menu keeps the parent it opened on: «Убрать родителя» sends MSK-EPIC-1 as seen and is refused, MSK-EPIC-20 kept', async () => {
    const api = serveBoards(treeDataset());
    renderBoards(backlogAddress('features'));
    const table = await findBacklogTable();
    expect(parentOf(table, 'MSK-FEAT-6')).toBe('MSK-EPIC-1');

    const menu = await openActions('MSK-FEAT-6');
    // Another user gives MSK-FEAT-6 MSK-EPIC-20 while the menu is open, and the tab is read again.
    mskRecord(api.data, 6).parentId = mskRecord(api.data, 20).id;
    await refocusTab();
    await waitFor(() => expect(parentOf(table, 'MSK-FEAT-6')).toBe('MSK-EPIC-20'));
    await userEvent.click(within(menu).getByRole('menuitem', { name: 'Убрать родителя' }));

    const alert = await screen.findByRole('alert');
    expect(alert).toHaveTextContent(PARENT_CHANGED_TEXT);
    expect(alert).toHaveClass('MuiAlert-colorError');
    expect(parentWrites(api).map((request) => request.body)).toEqual([
      { parentId: null, seenParentId: mskRecord(api.data, 1).id },
    ]);
    expect(parentOf(table, 'MSK-FEAT-6')).toBe('MSK-EPIC-20');
    expect(mskRecord(api.data, 6).parentId).toBe(mskRecord(api.data, 20).id);
  });

  it('with no answer at all «Не удалось сохранить родителя. Попробуйте ещё раз.» is shown and the cell is back', async () => {
    serveBoards(treeDataset());
    server.use(http.put(WORK_ITEM_PARENT_URL, () => HttpResponse.error()));
    renderBoards(backlogAddress('features'));
    const table = await findBacklogTable();

    const menu = await openActions('MSK-FEAT-6');
    await userEvent.click(within(menu).getByRole('menuitem', { name: 'Убрать родителя' }));

    expect(await screen.findByRole('alert')).toHaveTextContent(FAILED_TEXT);
    await waitFor(() => expect(parentOf(table, 'MSK-FEAT-6')).toBe('MSK-EPIC-1'));
  });

  it('in the dialog, no answer is its `Alert` with the same text, the dialog open and the choice kept', async () => {
    serveBoards(treeDataset());
    server.use(http.put(WORK_ITEM_PARENT_URL, () => HttpResponse.error()));
    renderBoards(backlogAddress('features'));
    await findBacklogTable();

    const dialog = await openDialog('MSK-FEAT-6');
    await pick(dialog, 'MSK-EPIC-20');
    await userEvent.click(save(dialog));

    expect(await within(dialog).findByRole('alert')).toHaveTextContent(FAILED_TEXT);
    expect(parentField(dialog)).toHaveValue('MSK-EPIC-20 Отчётность');
    expect(parentField(dialog)).not.toHaveAttribute('aria-invalid', 'true');
    expect(save(dialog)).toBeEnabled();
  });
});

describe('US-5 Scenario: One write at a time', () => {
  it('while «Убрать родителя» is saved every handle and «Ещё действия» is off, and on again once the backlog is read', async () => {
    serveBoards(treeDataset());
    const { release } = holdParentWrites();
    renderBoards(backlogAddress('features'));
    const table = await findBacklogTable();

    const menu = await openActions('MSK-FEAT-6');
    await userEvent.click(within(menu).getByRole('menuitem', { name: 'Убрать родителя' }));

    for (const identifier of ['MSK-FEAT-8', 'MSK-FEAT-6', 'MSK-FEAT-23']) {
      expect(handle(identifier)).toHaveAttribute('aria-disabled', 'true');
      expect(handle(identifier)).toHaveAttribute('draggable', 'false');
      expect(actionsButton(identifier)).toHaveAttribute('aria-disabled', 'true');
    }
    await userEvent.click(actionsButton('MSK-FEAT-8'));
    expect(screen.queryByRole('menu')).toBeNull();

    release();
    await screen.findByRole('alert');
    for (const identifier of ['MSK-FEAT-8', 'MSK-FEAT-6', 'MSK-FEAT-23']) {
      expect(handle(identifier)).not.toHaveAttribute('aria-disabled');
      expect(actionsButton(identifier)).not.toHaveAttribute('aria-disabled');
    }
    expect(outline(table)).toEqual(['MSK-FEAT-8', 'MSK-FEAT-6', 'MSK-FEAT-23']);
  });

  it('while the dialog saves it is busy — every control off — and so is the table behind it', async () => {
    serveBoards(treeDataset());
    const { release } = holdParentWrites();
    renderBoards(backlogAddress('features'));
    await findBacklogTable();

    const dialog = await openDialog('MSK-FEAT-6');
    await pick(dialog, 'MSK-EPIC-20');
    await userEvent.click(save(dialog));

    await waitFor(() => expect(save(dialog)).toBeDisabled());
    expect(within(dialog).getByRole('button', { name: 'Отмена' })).toBeDisabled();
    expect(within(dialog).getByRole('button', { name: 'Закрыть' })).toBeDisabled();
    expect(parentField(dialog)).toBeDisabled();
    expect(save(dialog).querySelector('.MuiCircularProgress-root')).not.toBeNull();
    expect(handle('MSK-FEAT-8')).toHaveAttribute('aria-disabled', 'true');
    expect(actionsButton('MSK-FEAT-8')).toHaveAttribute('aria-disabled', 'true');
    await userEvent.keyboard('{Escape}');
    expect(screen.getByRole('dialog', { name: 'Изменить родителя' })).toBeInTheDocument();

    release();
    await waitFor(() => expect(screen.queryByRole('dialog')).toBeNull());
    expect(handle('MSK-FEAT-8')).not.toHaveAttribute('aria-disabled');
  });
});

describe('US-5 Scenario: The menu is the complete path by keyboard', () => {
  it('Enter on «Ещё действия: MSK-US-2», Enter on «Изменить родителя…», «sso», the arrows and Enter, Tab to «Сохранить» and Enter: MSK-FEAT-8, focus back', async () => {
    const api = serveBoards(treeDataset());
    renderBoards(backlogAddress('stories'));
    const table = await findBacklogTable();

    actionsButton('MSK-US-2').focus();
    await userEvent.keyboard('{Enter}');
    const menu = await screen.findByRole('menu', { name: 'Ещё действия: MSK-US-2' });
    await waitFor(() =>
      expect(within(menu).getByRole('menuitem', { name: 'Изменить родителя…' })).toHaveFocus(),
    );
    await userEvent.keyboard('{Enter}');
    const dialog = await screen.findByRole('dialog', { name: 'Изменить родителя' });
    await waitFor(() => expect(parentField(dialog)).toHaveFocus());

    await userEvent.keyboard('{Control>}a{/Control}{Backspace}sso');
    await waitFor(() => expect(lines()).toEqual(['MSK-FEAT-8 Вход через SSO']));
    await userEvent.keyboard('{ArrowDown}{Enter}');
    expect(parentField(dialog)).toHaveValue('MSK-FEAT-8 Вход через SSO');
    await userEvent.tab();
    expect(within(dialog).getByRole('button', { name: 'Отмена' })).toHaveFocus();
    await userEvent.tab();
    expect(save(dialog)).toHaveFocus();
    await userEvent.keyboard('{Enter}');

    await waitFor(() => expect(screen.queryByRole('dialog')).toBeNull());
    expect(parentOf(table, 'MSK-US-2')).toBe('MSK-FEAT-8');
    expect(mskRecord(api.data, 2).parentId).toBe(mskRecord(api.data, 8).id);
    await waitFor(() => expect(actionsButton('MSK-US-2')).toHaveFocus());
  });

  it('ArrowDown on «Ещё действия» opens the menu too; Escape closes it, focus back', async () => {
    serveBoards(treeDataset());
    renderBoards(backlogAddress('stories'));
    await findBacklogTable();

    actionsButton('MSK-US-2').focus();
    await userEvent.keyboard('{ArrowDown}');
    expect(await screen.findByRole('menu', { name: 'Ещё действия: MSK-US-2' })).toBeInTheDocument();
    await userEvent.keyboard('{Escape}');
    await waitFor(() => expect(screen.queryByRole('menu')).toBeNull());
    expect(actionsButton('MSK-US-2')).toHaveFocus();
  });
});

describe('US-7 Scenario: The mapping replaces the link to a deleted parent', () => {
  it('MSK-EPIC-1 in «Удалена», MSK-FEAT-6 reading «Нет»: the dialog starts empty, MSK-EPIC-20 is accepted with `seenParentId: null`', async () => {
    const data = treeDataset();
    mskRecord(data, 1).stateId = stateId('EPIC', 'Удалена');
    const api = serveBoards(data);
    renderBoards(backlogAddress('features'));
    const table = await findBacklogTable();
    expect(parentOf(table, 'MSK-FEAT-6')).toBe('Нет');

    const actions = await openActions('MSK-FEAT-6');
    expect(within(actions).getByRole('menuitem', { name: 'Убрать родителя' })).toHaveAttribute(
      'aria-disabled',
      'true',
    );
    await userEvent.click(within(actions).getByRole('menuitem', { name: 'Изменить родителя…' }));
    const dialog = await screen.findByRole('dialog', { name: 'Изменить родителя' });
    expect(parentField(dialog)).toHaveValue('');
    expect(save(dialog)).toBeDisabled();

    await userEvent.click(parentField(dialog));
    await screen.findByRole('option', { name: /^MSK-EPIC-20 / });
    // A deleted item is never offered (US-7 Scenario: A deleted item is not offered as a parent).
    expect(lines()).toEqual(['MSK-EPIC-20 Отчётность']);
    await userEvent.click(screen.getByRole('option', { name: /^MSK-EPIC-20 / }));
    await userEvent.click(save(dialog));

    expect(await screen.findByRole('alert')).toHaveTextContent(
      'У MSK-FEAT-6 теперь родитель MSK-EPIC-20.',
    );
    expect(parentWrites(api).map((request) => request.body)).toEqual([
      { parentId: mskRecord(api.data, 20).id, seenParentId: null },
    ]);
    expect(mskRecord(api.data, 6).parentId).toBe(mskRecord(api.data, 20).id);
    expect(parentOf(table, 'MSK-FEAT-6')).toBe('MSK-EPIC-20');
  });

  it('a parent deleted after it was picked is refused with «Удалённый рабочий элемент нельзя связывать.» under «Родитель»', async () => {
    const api = serveBoards(treeDataset());
    renderBoards(backlogAddress('features'));
    await findBacklogTable();

    const dialog = await openDialog('MSK-FEAT-23');
    await pick(dialog, 'MSK-EPIC-20');
    mskRecord(api.data, 20).stateId = stateId('EPIC', 'Удалена');
    await userEvent.click(save(dialog));

    await waitFor(() =>
      expect(parentField(dialog)).toHaveAccessibleDescription(
        'Удалённый рабочий элемент нельзя связывать.',
      ),
    );
    expect(mskRecord(api.data, 23).parentId).toBe(mskRecord(api.data, 8).id);
  });
});
