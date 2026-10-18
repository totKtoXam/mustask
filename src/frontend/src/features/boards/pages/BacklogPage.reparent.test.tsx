import { afterEach, describe, expect, it } from 'vitest';
import { act, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { focusManager } from '@tanstack/react-query';
import { HttpResponse, http } from 'msw';
import { server } from '../../../test/mocks/server';
import { WORK_ITEM_PARENT_URL } from '../../workitems/testFixtures';
import { serveBoards } from '../testApi';
import {
  CELL,
  actions,
  addControl,
  announcer,
  cellOf,
  collapseOneLevel,
  expandControl,
  expandOneLevel,
  findBacklogTable,
  openActions,
  orders,
  outline,
  press,
  rowOf,
  rowsOf,
} from '../testBacklog';
import { dataTransfer, fireDrag } from '../testDrag';
import { catalogText, fullTreeDataset, mskRecord } from '../testFixtures';
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

// feat-027 US-6: any row is re-parented by keyboard — the same `fullTreeDataset`
// as US-5's drop. «Ещё действия» is on every row; its dialog offers every type
// BR-H-2 allows, so what a drop reaches the keyboard and a touch screen reach too.

const PARENT_CHANGED_TEXT = catalogText('workitems.link.parent_changed');

afterEach(() => {
  focusManager.setFocused(undefined);
});

const entriesOf = (menu: HTMLElement) =>
  within(menu)
    .getAllByRole('menuitem')
    .map((entry) => entry.textContent);

async function closeMenu() {
  await userEvent.keyboard('{Escape}');
  await waitFor(() => expect(screen.queryByRole('menu')).toBeNull());
}

describe('US-6 Scenario: Every row has the menu, on every level', () => {
  it('on «Эпики» every row, nested ones and the epics included, has «Ещё действия: <ID>» in the Tab order and no header names a menu column', async () => {
    serveBoards(fullTreeDataset());
    renderBoards(backlogAddress('epics'));
    const table = await findBacklogTable();

    expect(rowsOf(table)).toHaveLength(10);
    for (const row of rowsOf(table)) {
      const identifier = row.dataset.identifier as string;
      const button = within(row).getByRole('button', { name: `Ещё действия: ${identifier}` });
      expect(button).toHaveAttribute('aria-haspopup', 'menu');
      expect(button).not.toHaveAttribute('tabindex', '-1');
      expect(button.closest('td')).toBe(cellOf(row, CELL.title));
    }
    const headers = Array.from(table.querySelectorAll('thead th')).map(
      (header) => header.getAttribute('aria-label') ?? header.textContent,
    );
    expect(headers).not.toContain('Действия');
    expect(headers).toHaveLength(10);
  });

  it('every menu holds «Изменить родителя…» and «Убрать родителя»; the second is disabled on MSK-EPIC-1 and MSK-EPIC-20, which have no parent, and on no nested row', async () => {
    serveBoards(fullTreeDataset());
    renderBoards(backlogAddress('epics'));
    await findBacklogTable();

    for (const [identifier, hasParent] of [
      ['MSK-EPIC-1', false],
      ['MSK-EPIC-20', false],
      ['MSK-FEAT-23', true],
      ['MSK-TASK-3', true],
    ] as const) {
      const menu = await openActions(identifier);
      const change = within(menu).getByRole('menuitem', { name: 'Изменить родителя…' });
      const clear = within(menu).getByRole('menuitem', { name: 'Убрать родителя' });
      expect(change).not.toHaveAttribute('aria-disabled');
      if (hasParent) {
        expect(clear).not.toHaveAttribute('aria-disabled');
      } else {
        expect(clear).toHaveAttribute('aria-disabled', 'true');
      }
      await closeMenu();
    }
  });
});

describe('US-6 Scenario: A top-level row’s menu also reorders it', () => {
  it('on «Истории» MSK-US-22 reads «Переместить», «В начало», «Выше», «Ниже», «В конец», then both parent entries; «Ниже» and «В конец» are off; «В начало» makes it «1» with the announcement and focus on «⋮»', async () => {
    serveBoards(fullTreeDataset());
    renderBoards(backlogAddress('stories'));
    const table = await findBacklogTable();
    expect(orders(table)).toEqual(['1', '', '', '2', '3']);

    const menu = await openActions('MSK-US-22');
    expect(within(menu).getByText('Переместить')).toBeInTheDocument();
    expect(entriesOf(menu)).toEqual([
      'В начало',
      'Выше',
      'Ниже',
      'В конец',
      'Изменить родителя…',
      'Убрать родителя',
    ]);
    expect(within(menu).getByRole('menuitem', { name: 'Ниже' })).toHaveAttribute(
      'aria-disabled',
      'true',
    );
    expect(within(menu).getByRole('menuitem', { name: 'В конец' })).toHaveAttribute(
      'aria-disabled',
      'true',
    );
    await userEvent.click(within(menu).getByRole('menuitem', { name: 'В начало' }));

    expect(announcer()).toHaveTextContent('MSK-US-22 перемещён: позиция 1 из 3.');
    await waitFor(() => expect(actions('MSK-US-22')).toHaveFocus());
    expect(rowsOf(table)[0]).toHaveAttribute('data-identifier', 'MSK-US-22');
    expect(orders(table)[0]).toBe('1');
  });
});

describe('US-6 Scenario: A nested row’s menu has no moves', () => {
  it('«Ещё действия: MSK-FEAT-8» holds «Изменить родителя…» and «Убрать родителя» only', async () => {
    serveBoards(fullTreeDataset());
    renderBoards(backlogAddress('epics'));
    await findBacklogTable();

    const menu = await openActions('MSK-FEAT-8');

    expect(entriesOf(menu)).toEqual(['Изменить родителя…', 'Убрать родителя']);
    expect(within(menu).queryByText('Переместить')).toBeNull();
  });
});

describe('US-6 Scenario: The dialog offers every type BR-H-2 allows', () => {
  it('for MSK-BUG-4 the picker offers epics, features, stories and bugs, MSK-BUG-4 itself not among them, and no «Нет родителя»; MSK-FEAT-6 is preselected and «Сохранить» is off', async () => {
    serveBoards(fullTreeDataset());
    renderBoards(backlogAddress('epics'));
    await findBacklogTable();

    const dialog = await openParentDialog('MSK-BUG-4');

    expect(
      within(dialog).getByText('Баг MSK-BUG-4 «Ссылка истекает раньше срока»'),
    ).toBeInTheDocument();
    expect(parentField(dialog)).toHaveAccessibleDescription(
      'Родителем может быть: Эпик, Фича, Пользовательская история, Баг.',
    );
    expect(parentField(dialog)).toHaveValue('MSK-FEAT-6 Вход по ссылке');
    expect(saveButton(dialog)).toBeDisabled();
    await userEvent.click(parentField(dialog));
    await screen.findByRole('option', { name: /^MSK-EPIC-1 / });
    const listed = listedParents();
    expect(listed.map((line) => line.split(' ')[0]).sort()).toEqual(
      [
        'MSK-EPIC-1',
        'MSK-EPIC-20',
        'MSK-FEAT-21',
        'MSK-FEAT-23',
        'MSK-FEAT-6',
        'MSK-FEAT-8',
        'MSK-BUG-5',
        'MSK-US-2',
        'MSK-US-22',
      ].sort(),
    );
    expect(listed.some((line) => line.startsWith('MSK-BUG-4 '))).toBe(false);
    expect(listed.some((line) => line.startsWith('MSK-TASK-'))).toBe(false);
    expect(screen.queryByRole('option', { name: 'Нет родителя' })).toBeNull();
  });
});

describe('US-6 Scenario: A nested bug gets a story as its parent by keyboard', () => {
  it('Enter on «Ещё действия: MSK-BUG-4», Enter on «Изменить родителя…», the story typed, the arrows and Enter, Tab to «Сохранить» and Enter: the notice, MSK-BUG-4 beneath MSK-US-2, focus back on «⋮»', async () => {
    const api = serveBoards(fullTreeDataset());
    renderBoards(backlogAddress('epics'));
    const table = await findBacklogTable();

    actions('MSK-BUG-4').focus();
    await userEvent.keyboard('{Enter}');
    const menu = await screen.findByRole('menu', { name: 'Ещё действия: MSK-BUG-4' });
    // A nested row has no moves: the first entry is «Изменить родителя…», which takes focus.
    const change = within(menu).getByRole('menuitem', { name: 'Изменить родителя…' });
    await waitFor(() => expect(change).toHaveFocus());
    await userEvent.keyboard('{Enter}');
    const dialog = await screen.findByRole('dialog', { name: 'Изменить родителя' });
    await waitFor(() => expect(parentField(dialog)).toHaveFocus());

    await userEvent.keyboard('{Control>}a{/Control}{Backspace}со ссылкой');
    await waitFor(() => expect(listedParents()).toEqual(['MSK-US-2 Письмо со ссылкой']));
    await userEvent.keyboard('{ArrowDown}{Enter}');
    expect(parentField(dialog)).toHaveValue('MSK-US-2 Письмо со ссылкой');
    await userEvent.tab();
    expect(within(dialog).getByRole('button', { name: 'Отмена' })).toHaveFocus();
    await userEvent.tab();
    expect(saveButton(dialog)).toHaveFocus();
    await userEvent.keyboard('{Enter}');

    await waitFor(() => expect(screen.queryByRole('dialog')).toBeNull());
    expect(await screen.findByRole('alert')).toHaveTextContent(
      'У MSK-BUG-4 теперь родитель MSK-US-2.',
    );
    expect(parentWrites(api).map((request) => request.body)).toEqual([
      { parentId: mskRecord(api.data, 2).id, seenParentId: mskRecord(api.data, 6).id },
    ]);
    expect(outline(table)).toEqual([
      'MSK-EPIC-1',
      '  MSK-FEAT-8',
      '    MSK-FEAT-23',
      '  MSK-FEAT-6',
      '    MSK-US-2',
      '      MSK-BUG-4',
      '      MSK-BUG-5',
      '      MSK-TASK-3',
      '  MSK-US-22',
      'MSK-EPIC-20',
    ]);
    await waitFor(() => expect(actions('MSK-BUG-4')).toHaveFocus());
  });
});

describe('US-6 Scenario: An epic gets an epic as its parent', () => {
  it('MSK-EPIC-20 given MSK-EPIC-1: on «Эпики» MSK-EPIC-1 is «1», the only numbered row, and MSK-EPIC-20 stands beneath it before its features and stories', async () => {
    const api = serveBoards(fullTreeDataset());
    renderBoards(backlogAddress('epics'));
    const table = await findBacklogTable();

    const dialog = await openParentDialog('MSK-EPIC-20');
    expect(parentField(dialog)).toHaveAccessibleDescription('Родителем может быть: Эпик.');
    expect(parentField(dialog)).toHaveValue('');
    await pickParent(dialog, 'MSK-EPIC-1');
    await userEvent.click(saveButton(dialog));

    expect(await screen.findByRole('alert')).toHaveTextContent(
      'У MSK-EPIC-20 теперь родитель MSK-EPIC-1.',
    );
    expect(parentWrites(api).map((request) => request.body)).toEqual([
      { parentId: mskRecord(api.data, 1).id, seenParentId: null },
    ]);
    expect(outline(table)).toEqual([
      'MSK-EPIC-1',
      '  MSK-EPIC-20',
      '  MSK-FEAT-8',
      '    MSK-FEAT-23',
      '  MSK-FEAT-6',
      '    MSK-US-2',
      '      MSK-BUG-5',
      '      MSK-TASK-3',
      '    MSK-BUG-4',
      '  MSK-US-22',
    ]);
    expect(orders(table).filter((order) => order !== '')).toEqual(['1']);
  });
});

describe('US-6 Scenario: «Убрать родителя» on a nested row returns it to the top level', () => {
  it('on «Фичи» MSK-FEAT-23 beneath MSK-FEAT-8: «У MSK-FEAT-23 больше нет родителя.», then it is a top-level row «3»', async () => {
    const api = serveBoards(fullTreeDataset());
    renderBoards(backlogAddress('features'));
    const table = await findBacklogTable();
    expect(rowOf(table, 'MSK-FEAT-23')).toHaveAttribute('data-tier', '1');

    const menu = await openActions('MSK-FEAT-23');
    await userEvent.click(within(menu).getByRole('menuitem', { name: 'Убрать родителя' }));

    expect(await screen.findByRole('alert')).toHaveTextContent(
      'У MSK-FEAT-23 больше нет родителя.',
    );
    expect(parentWrites(api).map((request) => request.body)).toEqual([
      { parentId: null, seenParentId: mskRecord(api.data, 8).id },
    ]);
    expect(rowOf(table, 'MSK-FEAT-23')).toHaveAttribute('data-tier', '0');
    expect(orders(table).filter((order) => order !== '')).toEqual(['1', '2', '3']);
    expect(rowsOf(table).at(-1)).toHaveAttribute('data-identifier', 'MSK-FEAT-23');
    await waitFor(() => expect(actions('MSK-FEAT-23')).toHaveFocus());
  });
});

describe('US-6 Scenario: A refused choice stays in the dialog', () => {
  it('MSK-FEAT-8 given its descendant MSK-FEAT-23: the server’s text with the path shows under «Родитель», the dialog open', async () => {
    const api = serveBoards(fullTreeDataset());
    renderBoards(backlogAddress('epics'));
    await findBacklogTable();

    const dialog = await openParentDialog('MSK-FEAT-8');
    await pickParent(dialog, 'MSK-FEAT-23');
    await userEvent.click(saveButton(dialog));

    await waitFor(() =>
      expect(parentField(dialog)).toHaveAccessibleDescription(
        'Нельзя выбрать родителем MSK-FEAT-23: он вложен в MSK-FEAT-8 (MSK-FEAT-8 → MSK-FEAT-23).',
      ),
    );
    expect(parentField(dialog)).toHaveAttribute('aria-invalid', 'true');
    expect(screen.getByRole('dialog', { name: 'Изменить родителя' })).toBe(dialog);
    expect(mskRecord(api.data, 8).parentId).toBe(mskRecord(api.data, 1).id);
  });
});

describe('US-6 Scenario: A chain that would pass 64 items is refused (D-9)', () => {
  it('`workitems.link.depth_exceeded` shows the server’s finished text under «Родитель», the dialog stays open and nothing is written', async () => {
    const api = serveBoards(fullTreeDataset());
    const text =
      'Слишком глубокая вложенность: в цепочке родителей может быть не больше 64 элементов.';
    server.use(
      http.put(WORK_ITEM_PARENT_URL, () =>
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
      ),
    );
    renderBoards(backlogAddress('epics'));
    await findBacklogTable();

    const dialog = await openParentDialog('MSK-US-22');
    await pickParent(dialog, 'MSK-FEAT-8');
    await userEvent.click(saveButton(dialog));

    await waitFor(() => expect(parentField(dialog)).toHaveAccessibleDescription(text));
    expect(parentField(dialog)).toHaveAttribute('aria-invalid', 'true');
    expect(screen.getByRole('dialog', { name: 'Изменить родителя' })).toBe(dialog);
    expect(mskRecord(api.data, 22).parentId).toBe(mskRecord(api.data, 1).id);
  });
});

describe('US-6 Scenario: A parent changed meanwhile is refused, not overwritten', () => {
  it('the dialog opened on MSK-US-22 with MSK-EPIC-1 preselected, Анна gives it MSK-FEAT-6: picking MSK-FEAT-8 shows the code’s text under «Родитель» and MSK-FEAT-6 preselected', async () => {
    const api = serveBoards(fullTreeDataset());
    renderBoards(backlogAddress('epics'));
    await findBacklogTable();

    const dialog = await openParentDialog('MSK-US-22');
    expect(parentField(dialog)).toHaveValue('MSK-EPIC-1 Вход и регистрация');
    mskRecord(api.data, 22).parentId = mskRecord(api.data, 6).id;
    await pickParent(dialog, 'MSK-FEAT-8');
    await userEvent.click(saveButton(dialog));

    await waitFor(() =>
      expect(parentField(dialog)).toHaveAccessibleDescription(PARENT_CHANGED_TEXT),
    );
    expect(parentWrites(api).map((request) => request.body)).toEqual([
      { parentId: mskRecord(api.data, 8).id, seenParentId: mskRecord(api.data, 1).id },
    ]);
    expect(parentField(dialog)).toHaveValue('MSK-FEAT-6 Вход по ссылке');
    expect(mskRecord(api.data, 22).parentId).toBe(mskRecord(api.data, 6).id);
  });
});

describe('US-6 Scenario: On a touch screen the menu is the path', () => {
  it('every row a drag of MSK-FEAT-6 can land on is a parent the dialog offers: the keyboard and a touch screen reach every re-parenting of US-5', async () => {
    serveBoards(fullTreeDataset());
    renderBoards(backlogAddress('epics'));
    const table = await findBacklogTable();

    // The rows MSK-FEAT-6 is framed as allowed on while it is dragged.
    const transfer = dataTransfer();
    const source = rowOf(table, 'MSK-FEAT-6');
    await act(async () => fireDrag('dragStart', source, transfer));
    const reachable: string[] = [];
    for (const row of rowsOf(table)) {
      const identifier = row.dataset.identifier as string;
      await act(async () => fireDrag('dragOver', cellOf(row, CELL.title), transfer));
      if (row.getAttribute('data-drop-target') === 'allowed') {
        reachable.push(identifier);
      }
    }
    await act(async () => fireDrag('dragEnd', source, transfer));
    expect(reachable).toEqual(['MSK-FEAT-8', 'MSK-FEAT-23', 'MSK-EPIC-20']);

    const dialog = await openParentDialog('MSK-FEAT-6');
    await userEvent.click(parentField(dialog));
    await screen.findByRole('option', { name: /^MSK-EPIC-20 / });
    const offered = listedParents().map((line) => line.split(' ')[0]);
    for (const identifier of reachable) {
      expect(offered).toContain(identifier);
    }
  });
});

describe('US-6 Scenario: One write at a time', () => {
  it('while the dialog’s write is saved every row is undraggable and every «+», «Ещё действия» and both header buttons is off, and on again once the backlog is read', async () => {
    serveBoards(fullTreeDataset());
    const { release } = holdParentWrites();
    renderBoards(backlogAddress('epics'));
    const table = await findBacklogTable();
    await addControl('MSK-EPIC-1');

    const dialog = await openParentDialog('MSK-FEAT-6');
    await pickParent(dialog, 'MSK-EPIC-20');
    await userEvent.click(saveButton(dialog));

    await waitFor(() => expect(saveButton(dialog)).toBeDisabled());
    for (const row of rowsOf(table)) {
      const identifier = row.dataset.identifier as string;
      expect(rowOf(table, identifier)).toHaveAttribute('draggable', 'false');
      expect(actions(identifier)).toHaveAttribute('aria-disabled', 'true');
    }
    expect(await addControl('MSK-EPIC-1')).toHaveAttribute('aria-disabled', 'true');
    expect(expandOneLevel()).toHaveAttribute('aria-disabled', 'true');
    expect(collapseOneLevel()).toHaveAttribute('aria-disabled', 'true');

    release();
    await waitFor(() => expect(screen.queryByRole('dialog')).toBeNull());
    await waitFor(() => expect(actions('MSK-FEAT-6')).not.toHaveAttribute('aria-disabled'));
    expect(await addControl('MSK-EPIC-1')).not.toHaveAttribute('aria-disabled');
    expect(rowOf(table, 'MSK-EPIC-1')).toHaveAttribute('draggable', 'true');
  });
});

describe('US-6 Scenario: A new parent that is on the page shows the row beneath it', () => {
  it('MSK-FEAT-6 given MSK-EPIC-20, collapsed: the epic is expanded, MSK-FEAT-6 stands beneath it with its children, selected, and focus is on its «⋮»', async () => {
    serveBoards(fullTreeDataset());
    renderBoards(backlogAddress('epics'));
    const table = await findBacklogTable();
    // MSK-EPIC-20 has no child yet: it has no chevron and was never expanded.
    expect(outline(table).at(-1)).toBe('MSK-EPIC-20');

    const dialog = await openParentDialog('MSK-FEAT-6');
    await pickParent(dialog, 'MSK-EPIC-20');
    await userEvent.click(saveButton(dialog));

    await screen.findByRole('alert');
    expect(outline(table)).toEqual([
      'MSK-EPIC-1',
      '  MSK-FEAT-8',
      '    MSK-FEAT-23',
      '  MSK-US-22',
      'MSK-EPIC-20',
      '  MSK-FEAT-6',
      '    MSK-US-2',
      '      MSK-BUG-5',
      '      MSK-TASK-3',
      '    MSK-BUG-4',
    ]);
    expect(rowOf(table, 'MSK-FEAT-6')).toHaveAttribute('data-selected', 'true');
    await waitFor(() => expect(actions('MSK-FEAT-6')).toHaveFocus());
  });

  it('a new parent folded away under a collapsed ancestor (F-4): the ancestors are expanded too, the row is drawn, selected, scrolled to, and focus is on its «⋮»', async () => {
    serveBoards(fullTreeDataset());
    const scrolled: string[] = [];
    const scrollIntoView = function (this: HTMLElement) {
      scrolled.push(this.dataset.identifier ?? '');
    };
    Object.defineProperty(HTMLElement.prototype, 'scrollIntoView', {
      configurable: true,
      value: scrollIntoView,
    });
    try {
      renderBoards(backlogAddress('features'));
      const table = await findBacklogTable();
      // MSK-FEAT-8 folded away: MSK-FEAT-23 beneath it is not drawn.
      press(expandControl('MSK-FEAT-8'));
      expect(outline(table)).not.toContain('  MSK-FEAT-23');

      const dialog = await openParentDialog('MSK-FEAT-6');
      await pickParent(dialog, 'MSK-FEAT-23');
      await userEvent.click(saveButton(dialog));

      expect(await screen.findByRole('alert')).toHaveTextContent(
        'У MSK-FEAT-6 теперь родитель MSK-FEAT-23.',
      );
      // MSK-FEAT-6 stands beneath MSK-FEAT-23 beneath MSK-FEAT-8: all of them open, all drawn.
      expect(outline(table)).toEqual([
        'MSK-FEAT-8',
        '  MSK-FEAT-23',
        '    MSK-FEAT-6',
        '      MSK-US-2',
        '        MSK-BUG-5',
        '        MSK-TASK-3',
        '      MSK-BUG-4',
      ]);
      expect(expandControl('MSK-FEAT-8')).toHaveAttribute('aria-expanded', 'true');
      expect(expandControl('MSK-FEAT-23')).toHaveAttribute('aria-expanded', 'true');
      expect(rowOf(table, 'MSK-FEAT-6')).toHaveAttribute('data-selected', 'true');
      await waitFor(() => expect(actions('MSK-FEAT-6')).toHaveFocus());
      expect(scrolled).toEqual(['MSK-FEAT-6']);

      // Nothing is left pending: folding the ancestor and opening it again scrolls nowhere.
      press(expandControl('MSK-FEAT-8'));
      press(expandControl('MSK-FEAT-8'));
      expect(scrolled).toEqual(['MSK-FEAT-6']);
    } finally {
      Reflect.deleteProperty(HTMLElement.prototype, 'scrollIntoView');
    }
  });
});
