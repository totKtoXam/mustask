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
  actions as actionsButton,
  cellText,
  findBacklogTable,
  openActions,
  outline,
  rowOf,
  rowsOf,
} from '../testBacklog';
import { catalogText, ids, mskRecord, stateId, treeDataset } from '../testFixtures';
import { chooseLevel } from '../testLevels';
import {
  holdParentWrites,
  listedParents,
  openParentDialog,
  parentField,
  parentWrites,
  pickParent,
  saveButton,
} from '../testParentDialog';
import { backlogAddress, renderBoards } from '../testRender';

// feat-001 US-5 on US-4's «Data» (`treeDataset`): «Изменить родителя…» and
// «Убрать родителя» from «Ещё действия» of a row, the parent write
// (api-contract § 3.2) with the search for every type BR-H-2 allows (feat-027
// US-6: no `levelAbove`), then the backlog read again, which since feat-027 nests
// the whole tree, expanded as far as it goes: MSK-FEAT-23 stands beneath
// MSK-FEAT-8 and MSK-US-22 beneath MSK-EPIC-1 on «Эпики». There is no «Родитель»
// column any more (feat-027 D-7): a parent shows in the nesting, in the stored
// data and in «Убрать родителя» waiting for one. US-7's backlog scenarios close
// the file; feat-027 US-6's keyboard path is in `BacklogPage.reparent.test.tsx`.

const PARENT_CHANGED_TEXT = catalogText('workitems.link.parent_changed');
const FAILED_TEXT = 'Не удалось сохранить родителя. Попробуйте ещё раз.';

afterEach(() => {
  focusManager.setFocused(undefined);
});

/** Lets the tab lose and regain focus, as the browser reports it: the backlog is read again. */
async function refocusTab() {
  await act(async () => {
    focusManager.setFocused(false);
  });
  await act(async () => {
    focusManager.setFocused(true);
  });
}

describe('US-5 Scenario: A story gets a feature as its parent', () => {
  it('MSK-US-22 under MSK-EPIC-1 is given MSK-FEAT-8 from «Истории»: the dialog, the features only, the notice, the link, focus back; the tree on «Фичи» follows', async () => {
    const api = serveBoards(treeDataset());
    renderBoards(backlogAddress('stories'));
    await findBacklogTable();
    expect(mskRecord(api.data, 22).parentId).toBe(mskRecord(api.data, 1).id);

    const dialog = await openParentDialog('MSK-US-22');
    expect(
      within(dialog).getByText('Пользовательская история MSK-US-22 «Запомнить устройство»'),
    ).toBeInTheDocument();
    const field = parentField(dialog);
    expect(field).toHaveValue('MSK-EPIC-1 Вход и регистрация');
    expect(field).toHaveFocus();
    // Every type BR-H-2 allows above a story, named as the form names them (feat-027 BR-T-14).
    expect(field).toHaveAccessibleDescription(
      'Родителем может быть: Эпик, Фича, Пользовательская история, Баг.',
    );
    expect(saveButton(dialog)).toBeDisabled();

    await userEvent.click(field);
    await screen.findByRole('option', { name: /^MSK-FEAT-8 / });
    // Epics, features, stories and bugs, the item itself and the task not among them.
    expect(listedParents()).toEqual([
      'MSK-FEAT-23 Вход по коду',
      'MSK-FEAT-21 Аудит входа',
      'MSK-EPIC-20 Отчётность',
      'MSK-FEAT-8 Вход через SSO',
      'MSK-FEAT-6 Вход по ссылке',
      'MSK-BUG-4 Ссылка истекает раньше срока',
      'MSK-US-2 Письмо со ссылкой',
      'MSK-EPIC-1 Вход и регистрация',
    ]);
    expect(api.reads('parent-candidates')[0]?.path).toBe(
      `/api/organizations/${ids.example}/projects/${ids.msk}/workitems/MSK-US-22/parent-candidates?text=`,
    );
    await userEvent.click(screen.getByRole('option', { name: /^MSK-FEAT-8 / }));
    expect(saveButton(dialog)).toBeEnabled();
    await userEvent.click(saveButton(dialog));

    await waitFor(() => expect(screen.queryByRole('dialog')).toBeNull());
    expect(await screen.findByRole('alert')).toHaveTextContent(
      'У MSK-US-22 теперь родитель MSK-FEAT-8.',
    );
    expect(screen.getByRole('alert')).toHaveClass('MuiAlert-colorSuccess');
    expect(mskRecord(api.data, 22).parentId).toBe(mskRecord(api.data, 8).id);
    await waitFor(() => expect(actionsButton('MSK-US-22')).toHaveFocus());
    expect(parentWrites(api).map((request) => request.body)).toEqual([
      { parentId: mskRecord(api.data, 8).id, seenParentId: mskRecord(api.data, 1).id },
    ]);

    await chooseLevel('Фичи');
    // Beneath MSK-FEAT-8 the feature comes first, then the story (feat-027 BR-T-6).
    await waitFor(async () =>
      expect(outline(await findBacklogTable())).toEqual([
        'MSK-FEAT-8',
        '  MSK-FEAT-23',
        '  MSK-US-22',
        'MSK-FEAT-6',
        '  MSK-US-2',
        '  MSK-BUG-4',
      ]),
    );
  });
});

describe('US-5 Scenario: A feature gets an epic, replacing its old one in one write', () => {
  it('MSK-FEAT-6 mapped to MSK-EPIC-20 has that one parent and shows beneath it on «Эпики», its stories still beneath it', async () => {
    const api = serveBoards(treeDataset());
    renderBoards(backlogAddress('features'));
    await findBacklogTable();

    const dialog = await openParentDialog('MSK-FEAT-6');
    expect(parentField(dialog)).toHaveAccessibleDescription('Родителем может быть: Эпик, Фича.');
    await pickParent(dialog, 'MSK-EPIC-20');
    await userEvent.click(saveButton(dialog));
    expect(await screen.findByRole('alert')).toHaveTextContent(
      'У MSK-FEAT-6 теперь родитель MSK-EPIC-20.',
    );

    expect(mskRecord(api.data, 6).parentId).toBe(mskRecord(api.data, 20).id);
    expect(parentWrites(api)).toHaveLength(1);
    await chooseLevel('Эпики');
    await waitFor(async () =>
      expect(outline(await findBacklogTable())).toEqual([
        'MSK-EPIC-1',
        '  MSK-FEAT-8',
        '    MSK-FEAT-23',
        '  MSK-US-22',
        'MSK-EPIC-20',
        '  MSK-FEAT-6',
        '    MSK-US-2',
        '    MSK-BUG-4',
      ]),
    );
  });
});

describe('US-5 Scenario: «Убрать родителя» clears the parent', () => {
  it('MSK-FEAT-6 keeps its row and «Порядок», the notice follows the write, and «Убрать родителя» then waits for a parent; on «Эпики» it is beneath no row', async () => {
    const api = serveBoards(treeDataset());
    const { release } = holdParentWrites();
    renderBoards(backlogAddress('features'));
    const table = await findBacklogTable();

    const menu = await openActions('MSK-FEAT-6');
    await userEvent.click(within(menu).getByRole('menuitem', { name: 'Убрать родителя' }));

    // While the write is saved the table is off and the notice has not come.
    expect(actionsButton('MSK-FEAT-6')).toHaveAttribute('aria-disabled', 'true');
    expect(screen.queryByRole('alert')).toBeNull();
    release();
    expect(await screen.findByRole('alert')).toHaveTextContent('У MSK-FEAT-6 больше нет родителя.');
    expect(cellText(rowOf(table, 'MSK-FEAT-6'), CELL.order)).toBe('2');
    expect(outline(table)).toEqual([
      'MSK-FEAT-8',
      '  MSK-FEAT-23',
      'MSK-FEAT-6',
      '  MSK-US-2',
      '  MSK-BUG-4',
    ]);
    await waitFor(() => expect(actionsButton('MSK-FEAT-6')).toHaveFocus());
    expect(parentWrites(api).map((request) => request.body)).toEqual([
      { parentId: null, seenParentId: mskRecord(api.data, 1).id },
    ]);
    expect(mskRecord(api.data, 6).parentId).toBeNull();
    const again = await openActions('MSK-FEAT-6');
    expect(within(again).getByRole('menuitem', { name: 'Убрать родителя' })).toHaveAttribute(
      'aria-disabled',
      'true',
    );
    await userEvent.keyboard('{Escape}');

    await chooseLevel('Эпики');
    await waitFor(async () =>
      expect(outline(await findBacklogTable())).toEqual([
        'MSK-EPIC-1',
        '  MSK-FEAT-8',
        '    MSK-FEAT-23',
        '  MSK-US-22',
        'MSK-EPIC-20',
      ]),
    );
  });
});

describe('US-5 Scenario: What a row’s menu offers (feat-027 BR-T-14)', () => {
  it('every top-level row of «Фичи» has «Ещё действия: <ID>» with the four moves and both parent entries; «Убрать родителя» is disabled without a parent', async () => {
    const data = treeDataset();
    mskRecord(data, 6).parentId = null;
    serveBoards(data);
    renderBoards(backlogAddress('features'));
    const table = await findBacklogTable();

    for (const row of rowsOf(table)) {
      const identifier = row.dataset.identifier as string;
      const button = within(row).getByRole('button', { name: `Ещё действия: ${identifier}` });
      expect(button).toHaveAttribute('aria-haspopup', 'menu');
      expect(button.querySelector('[data-testid="MoreVertIcon"]')).not.toBeNull();
    }

    const menu = await openActions('MSK-FEAT-8');
    expect(actionsButton('MSK-FEAT-8')).toHaveAttribute('aria-expanded', 'true');
    const entries = within(menu).getAllByRole('menuitem');
    expect(entries.map((entry) => entry.textContent)).toEqual([
      'В начало',
      'Выше',
      'Ниже',
      'В конец',
      'Изменить родителя…',
      'Убрать родителя',
    ]);
    expect(within(menu).getByText('Переместить')).toBeInTheDocument();
    expect(entries[4]?.querySelector('[data-testid="AccountTreeOutlinedIcon"]')).not.toBeNull();
    expect(entries[5]?.querySelector('[data-testid="LinkOffIcon"]')).not.toBeNull();
    expect(entries[5]).not.toHaveAttribute('aria-disabled');
    await userEvent.keyboard('{Escape}');
    await waitFor(() => expect(screen.queryByRole('menu')).toBeNull());

    const without = await openActions('MSK-FEAT-6');
    expect(within(without).getByRole('menuitem', { name: 'Убрать родителя' })).toHaveAttribute(
      'aria-disabled',
      'true',
    );
  });

  it('a nested row has «Ещё действия» too, with the two parent entries and no moves; an epic’s menu has the moves and both parent entries — an epic can have an epic as its parent', async () => {
    serveBoards(treeDataset());
    renderBoards(backlogAddress('epics'));
    const table = await findBacklogTable();

    const entriesOf = (menu: HTMLElement) =>
      within(menu)
        .getAllByRole('menuitem')
        .map((entry) => entry.textContent);
    const nested = await openActions('MSK-FEAT-6');
    expect(entriesOf(nested)).toEqual(['Изменить родителя…', 'Убрать родителя']);
    expect(within(nested).queryByText('Переместить')).toBeNull();
    await userEvent.keyboard('{Escape}');
    await waitFor(() => expect(screen.queryByRole('menu')).toBeNull());

    const epic = await openActions('MSK-EPIC-20');
    expect(entriesOf(epic)).toEqual([
      'В начало',
      'Выше',
      'Ниже',
      'В конец',
      'Изменить родителя…',
      'Убрать родителя',
    ]);
    expect(rowOf(table, 'MSK-FEAT-6')).toHaveAttribute('data-tier', '1');
  });

  it('«Изменить родителя…» on a nested feature offers the epics and the features, as on its own level; the search asks for every allowed type', async () => {
    const api = serveBoards(treeDataset());
    renderBoards(backlogAddress('epics'));
    await findBacklogTable();

    const dialog = await openParentDialog('MSK-FEAT-23');

    expect(within(dialog).getByText('Фича MSK-FEAT-23 «Вход по коду»')).toBeInTheDocument();
    expect(parentField(dialog)).toHaveValue('MSK-FEAT-8 Вход через SSO');
    expect(parentField(dialog)).toHaveAccessibleDescription('Родителем может быть: Эпик, Фича.');
    await userEvent.click(parentField(dialog));
    await screen.findByRole('option', { name: /^MSK-EPIC-1 / });
    expect(listedParents()).toEqual([
      'MSK-FEAT-21 Аудит входа',
      'MSK-EPIC-20 Отчётность',
      'MSK-FEAT-8 Вход через SSO',
      'MSK-FEAT-6 Вход по ссылке',
      'MSK-EPIC-1 Вход и регистрация',
    ]);
    expect(api.reads('parent-candidates')[0]?.path).not.toContain('levelAbove');
  });
});

describe('US-5 Scenario: «Сохранить» waits for a different parent', () => {
  it('on MSK-FEAT-6 with MSK-EPIC-1 preselected «Сохранить» is disabled until another epic is picked; «Отмена» closes with nothing sent', async () => {
    const api = serveBoards(treeDataset());
    renderBoards(backlogAddress('features'));
    await findBacklogTable();

    const dialog = await openParentDialog('MSK-FEAT-6');
    expect(parentField(dialog)).toHaveValue('MSK-EPIC-1 Вход и регистрация');
    expect(saveButton(dialog)).toBeDisabled();
    await pickParent(dialog, 'MSK-EPIC-1');
    expect(saveButton(dialog)).toBeDisabled();
    await pickParent(dialog, 'MSK-EPIC-20');
    expect(saveButton(dialog)).toBeEnabled();

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

    const dialog = await openParentDialog('MSK-US-2');
    await pickParent(dialog, 'MSK-FEAT-8');
    await userEvent.click(saveButton(dialog));
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
    await waitFor(async () =>
      expect(outline(await findBacklogTable())).toEqual([
        'MSK-EPIC-1',
        '  MSK-FEAT-8',
        '    MSK-FEAT-23',
        '    MSK-US-2',
        '    MSK-US-22',
        '  MSK-FEAT-6',
        '    MSK-BUG-4',
        'MSK-EPIC-20',
      ]),
    );
  });
});

describe('US-5 Scenario: A parent changed meanwhile refuses the mapping', () => {
  it('Борис maps MSK-US-2 to MSK-FEAT-23 after Анна mapped it to MSK-FEAT-8: the code’s text under «Родитель», the backlog read again, MSK-FEAT-8 preselected', async () => {
    const api = serveBoards(treeDataset());
    renderBoards(backlogAddress('stories'));
    await findBacklogTable();
    expect(mskRecord(api.data, 2).parentId).toBe(mskRecord(api.data, 6).id);

    const dialog = await openParentDialog('MSK-US-2');
    await pickParent(dialog, 'MSK-FEAT-23');
    // Анна's write, taken as the server would.
    mskRecord(api.data, 2).parentId = mskRecord(api.data, 8).id;
    const reads = api.reads('/backlogs/stories').length;
    await userEvent.click(saveButton(dialog));

    await waitFor(() =>
      expect(parentField(dialog)).toHaveAccessibleDescription(PARENT_CHANGED_TEXT),
    );
    expect(parentField(dialog)).toHaveAttribute('aria-invalid', 'true');
    expect(screen.getByRole('dialog', { name: 'Изменить родителя' })).toBe(dialog);
    expect(api.reads('/backlogs/stories').length).toBeGreaterThan(reads);
    expect(parentField(dialog)).toHaveValue('MSK-FEAT-8 Вход через SSO');
    expect(saveButton(dialog)).toBeDisabled();
    expect(mskRecord(api.data, 2).parentId).toBe(mskRecord(api.data, 8).id);
    expect(within(dialog).queryByRole('alert')).toBeNull();
  });

  /** Opens the dialog on MSK-US-2 (MSK-FEAT-6), then Анна's MSK-FEAT-8 lands and the tab is read again. */
  async function dialogUnderARead(api: BoardsTestApi) {
    const dialog = await openParentDialog('MSK-US-2');
    expect(parentField(dialog)).toHaveValue('MSK-FEAT-6 Вход по ссылке');
    mskRecord(api.data, 2).parentId = mskRecord(api.data, 8).id;
    const reads = api.reads('/backlogs/stories').length;
    await refocusTab();
    await waitFor(() => expect(api.reads('/backlogs/stories').length).toBeGreaterThan(reads));
    return dialog;
  }

  it('a re-read under the open dialog changes nothing in it: MSK-FEAT-6 still shown, «Сохранить» off until another parent is picked', async () => {
    const api = serveBoards(treeDataset());
    renderBoards(backlogAddress('stories'));
    await findBacklogTable();

    const dialog = await dialogUnderARead(api);

    expect(parentField(dialog)).toHaveValue('MSK-FEAT-6 Вход по ссылке');
    expect(saveButton(dialog)).toBeDisabled();
    await pickParent(dialog, 'MSK-FEAT-6');
    expect(saveButton(dialog)).toBeDisabled();
    await pickParent(dialog, 'MSK-FEAT-23');
    expect(saveButton(dialog)).toBeEnabled();
    expect(parentWrites(api)).toEqual([]);
  });

  it('a parent picked after that re-read is sent with MSK-FEAT-6 as seen: refused, read again, MSK-FEAT-8 preselected; the retry is decided on MSK-FEAT-8', async () => {
    const api = serveBoards(treeDataset());
    renderBoards(backlogAddress('stories'));
    await findBacklogTable();
    const dialog = await dialogUnderARead(api);

    await pickParent(dialog, 'MSK-FEAT-23');
    const reads = api.reads('/backlogs/stories').length;
    await userEvent.click(saveButton(dialog));

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
    expect(saveButton(dialog)).toBeDisabled();

    await pickParent(dialog, 'MSK-FEAT-23');
    await userEvent.click(saveButton(dialog));
    await waitFor(() => expect(screen.queryByRole('dialog')).toBeNull());
    expect(parentWrites(api).map((request) => request.body)[1]).toEqual({
      parentId: mskRecord(api.data, 23).id,
      seenParentId: mskRecord(api.data, 8).id,
    });
    expect(mskRecord(api.data, 2).parentId).toBe(mskRecord(api.data, 23).id);
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
    await findBacklogTable();

    const dialog = await openParentDialog('MSK-US-2');
    await pickParent(dialog, 'MSK-FEAT-23');
    await userEvent.click(saveButton(dialog));
    expect(await within(dialog).findByRole('alert')).toHaveTextContent(FAILED_TEXT);
    expect(mskRecord(api.data, 2).parentId).toBe(mskRecord(api.data, 8).id);
    expect(parentField(dialog)).toHaveValue('MSK-FEAT-23 Вход по коду');

    await userEvent.click(saveButton(dialog));
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

describe('US-5 Scenario: A refused clear changes nothing', () => {
  it('`workitems.link.parent_changed` on «Убрать родителя»: the backlog is read again, the stored parent stays, and the code’s text is the error notice', async () => {
    const api = serveBoards(treeDataset());
    renderBoards(backlogAddress('features'));
    await findBacklogTable();
    // Another user gave MSK-FEAT-6 MSK-EPIC-20 meanwhile.
    mskRecord(api.data, 6).parentId = mskRecord(api.data, 20).id;

    const menu = await openActions('MSK-FEAT-6');
    await userEvent.click(within(menu).getByRole('menuitem', { name: 'Убрать родителя' }));

    const alert = await screen.findByRole('alert');
    expect(alert).toHaveTextContent(PARENT_CHANGED_TEXT);
    expect(alert).toHaveClass('MuiAlert-colorError');
    expect(mskRecord(api.data, 6).parentId).toBe(mskRecord(api.data, 20).id);
  });

  it('a re-read under the open menu keeps the parent it opened on: «Убрать родителя» sends MSK-EPIC-1 as seen and is refused, MSK-EPIC-20 kept', async () => {
    const api = serveBoards(treeDataset());
    renderBoards(backlogAddress('features'));
    await findBacklogTable();

    const menu = await openActions('MSK-FEAT-6');
    // Another user gives MSK-FEAT-6 MSK-EPIC-20 while the menu is open, and the tab is read again.
    mskRecord(api.data, 6).parentId = mskRecord(api.data, 20).id;
    const reads = api.reads('/backlogs/features').length;
    await refocusTab();
    await waitFor(() => expect(api.reads('/backlogs/features').length).toBeGreaterThan(reads));
    await userEvent.click(within(menu).getByRole('menuitem', { name: 'Убрать родителя' }));

    const alert = await screen.findByRole('alert');
    expect(alert).toHaveTextContent(PARENT_CHANGED_TEXT);
    expect(alert).toHaveClass('MuiAlert-colorError');
    expect(parentWrites(api).map((request) => request.body)).toEqual([
      { parentId: null, seenParentId: mskRecord(api.data, 1).id },
    ]);
    expect(mskRecord(api.data, 6).parentId).toBe(mskRecord(api.data, 20).id);
  });

  it('with no answer at all «Не удалось сохранить родителя. Попробуйте ещё раз.» is shown and the parent stays', async () => {
    const api = serveBoards(treeDataset());
    server.use(http.put(WORK_ITEM_PARENT_URL, () => HttpResponse.error()));
    renderBoards(backlogAddress('features'));
    await findBacklogTable();

    const menu = await openActions('MSK-FEAT-6');
    await userEvent.click(within(menu).getByRole('menuitem', { name: 'Убрать родителя' }));

    expect(await screen.findByRole('alert')).toHaveTextContent(FAILED_TEXT);
    expect(mskRecord(api.data, 6).parentId).toBe(mskRecord(api.data, 1).id);
    await waitFor(() => expect(actionsButton('MSK-FEAT-6')).not.toHaveAttribute('aria-disabled'));
  });

  it('in the dialog, no answer is its `Alert` with the same text, the dialog open and the choice kept', async () => {
    serveBoards(treeDataset());
    server.use(http.put(WORK_ITEM_PARENT_URL, () => HttpResponse.error()));
    renderBoards(backlogAddress('features'));
    await findBacklogTable();

    const dialog = await openParentDialog('MSK-FEAT-6');
    await pickParent(dialog, 'MSK-EPIC-20');
    await userEvent.click(saveButton(dialog));

    expect(await within(dialog).findByRole('alert')).toHaveTextContent(FAILED_TEXT);
    expect(parentField(dialog)).toHaveValue('MSK-EPIC-20 Отчётность');
    expect(parentField(dialog)).not.toHaveAttribute('aria-invalid', 'true');
    expect(saveButton(dialog)).toBeEnabled();
  });
});

describe('US-5 Scenario: One write at a time', () => {
  it('while «Убрать родителя» is saved every row is undraggable and every «Ещё действия» is off, and on again once the backlog is read', async () => {
    serveBoards(treeDataset());
    const { release } = holdParentWrites();
    renderBoards(backlogAddress('features'));
    const table = await findBacklogTable();

    const menu = await openActions('MSK-FEAT-6');
    await userEvent.click(within(menu).getByRole('menuitem', { name: 'Убрать родителя' }));

    for (const identifier of ['MSK-FEAT-8', 'MSK-FEAT-6']) {
      expect(rowOf(table, identifier)).toHaveAttribute('draggable', 'false');
      expect(actionsButton(identifier)).toHaveAttribute('aria-disabled', 'true');
    }
    await userEvent.click(actionsButton('MSK-FEAT-8'));
    expect(screen.queryByRole('menu')).toBeNull();

    release();
    await screen.findByRole('alert');
    for (const identifier of ['MSK-FEAT-8', 'MSK-FEAT-6']) {
      expect(rowOf(table, identifier)).toHaveAttribute('draggable', 'true');
      expect(actionsButton(identifier)).not.toHaveAttribute('aria-disabled');
    }
    expect(outline(table)).toEqual([
      'MSK-FEAT-8',
      '  MSK-FEAT-23',
      'MSK-FEAT-6',
      '  MSK-US-2',
      '  MSK-BUG-4',
    ]);
  });

  it('while the dialog saves it is busy — every control off — and so is the table behind it', async () => {
    serveBoards(treeDataset());
    const { release } = holdParentWrites();
    renderBoards(backlogAddress('features'));
    const table = await findBacklogTable();

    const dialog = await openParentDialog('MSK-FEAT-6');
    await pickParent(dialog, 'MSK-EPIC-20');
    await userEvent.click(saveButton(dialog));

    await waitFor(() => expect(saveButton(dialog)).toBeDisabled());
    expect(within(dialog).getByRole('button', { name: 'Отмена' })).toBeDisabled();
    expect(within(dialog).getByRole('button', { name: 'Закрыть' })).toBeDisabled();
    expect(parentField(dialog)).toBeDisabled();
    expect(saveButton(dialog).querySelector('.MuiCircularProgress-root')).not.toBeNull();
    expect(rowOf(table, 'MSK-FEAT-8')).toHaveAttribute('draggable', 'false');
    expect(actionsButton('MSK-FEAT-8')).toHaveAttribute('aria-disabled', 'true');
    await userEvent.keyboard('{Escape}');
    expect(screen.getByRole('dialog', { name: 'Изменить родителя' })).toBeInTheDocument();

    release();
    await waitFor(() => expect(screen.queryByRole('dialog')).toBeNull());
    expect(actionsButton('MSK-FEAT-8')).not.toHaveAttribute('aria-disabled');
  });
});

describe('US-5 Scenario: The menu is the complete path by keyboard', () => {
  it('Enter on «Ещё действия: MSK-US-2», Enter on «Изменить родителя…», «sso», the arrows and Enter, Tab to «Сохранить» and Enter: MSK-FEAT-8, focus back', async () => {
    const api = serveBoards(treeDataset());
    renderBoards(backlogAddress('stories'));
    await findBacklogTable();

    actionsButton('MSK-US-2').focus();
    await userEvent.keyboard('{Enter}');
    const menu = await screen.findByRole('menu', { name: 'Ещё действия: MSK-US-2' });
    // The first entry is «В начало», disabled on the first row: focus lands on the first one that is on.
    const change = within(menu).getByRole('menuitem', { name: 'Изменить родителя…' });
    await waitFor(() => expect(menu.contains(document.activeElement)).toBe(true));
    change.focus();
    await userEvent.keyboard('{Enter}');
    const dialog = await screen.findByRole('dialog', { name: 'Изменить родителя' });
    await waitFor(() => expect(parentField(dialog)).toHaveFocus());

    await userEvent.keyboard('{Control>}a{/Control}{Backspace}sso');
    await waitFor(() => expect(listedParents()).toEqual(['MSK-FEAT-8 Вход через SSO']));
    await userEvent.keyboard('{ArrowDown}{Enter}');
    expect(parentField(dialog)).toHaveValue('MSK-FEAT-8 Вход через SSO');
    await userEvent.tab();
    expect(within(dialog).getByRole('button', { name: 'Отмена' })).toHaveFocus();
    await userEvent.tab();
    expect(saveButton(dialog)).toHaveFocus();
    await userEvent.keyboard('{Enter}');

    await waitFor(() => expect(screen.queryByRole('dialog')).toBeNull());
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
    await findBacklogTable();

    const actions = await openActions('MSK-FEAT-6');
    expect(within(actions).getByRole('menuitem', { name: 'Убрать родителя' })).toHaveAttribute(
      'aria-disabled',
      'true',
    );
    await userEvent.click(within(actions).getByRole('menuitem', { name: 'Изменить родителя…' }));
    const dialog = await screen.findByRole('dialog', { name: 'Изменить родителя' });
    expect(parentField(dialog)).toHaveValue('');
    expect(saveButton(dialog)).toBeDisabled();

    await userEvent.click(parentField(dialog));
    await screen.findByRole('option', { name: /^MSK-EPIC-20 / });
    // A deleted item is never offered (US-7 Scenario: A deleted item is not offered as a parent):
    // MSK-EPIC-1 is not among the epics and features, nor is MSK-FEAT-6 itself.
    expect(listedParents()).toEqual([
      'MSK-FEAT-23 Вход по коду',
      'MSK-FEAT-21 Аудит входа',
      'MSK-EPIC-20 Отчётность',
      'MSK-FEAT-8 Вход через SSO',
    ]);
    await userEvent.click(screen.getByRole('option', { name: /^MSK-EPIC-20 / }));
    await userEvent.click(saveButton(dialog));

    expect(await screen.findByRole('alert')).toHaveTextContent(
      'У MSK-FEAT-6 теперь родитель MSK-EPIC-20.',
    );
    expect(parentWrites(api).map((request) => request.body)).toEqual([
      { parentId: mskRecord(api.data, 20).id, seenParentId: null },
    ]);
    expect(mskRecord(api.data, 6).parentId).toBe(mskRecord(api.data, 20).id);
  });

  it('a parent deleted after it was picked is refused with «Удалённый рабочий элемент нельзя связывать.» under «Родитель»', async () => {
    const api = serveBoards(treeDataset());
    renderBoards(backlogAddress('features'));
    await findBacklogTable();

    const dialog = await openParentDialog('MSK-FEAT-6');
    await pickParent(dialog, 'MSK-EPIC-20');
    mskRecord(api.data, 20).stateId = stateId('EPIC', 'Удалена');
    await userEvent.click(saveButton(dialog));

    await waitFor(() =>
      expect(parentField(dialog)).toHaveAccessibleDescription(
        'Удалённый рабочий элемент нельзя связывать.',
      ),
    );
    expect(mskRecord(api.data, 6).parentId).toBe(mskRecord(api.data, 1).id);
  });
});
