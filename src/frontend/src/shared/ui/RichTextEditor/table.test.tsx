import { act, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { GapCursor } from '@tiptap/pm/gapcursor';
import { describe, expect, it } from 'vitest';
import { mountEditor, positionOf, select } from '../../../test/richTextEditor';

const TABLE_2X2 =
  '<table><tbody><tr><th><p>h1</p></th><th><p>h2</p></th></tr>' +
  '<tr><td><p>a</p></td><td><p>b</p></td></tr></tbody></table>';

async function openOps(
  user: ReturnType<typeof userEvent.setup>,
  button: (name: string) => HTMLElement,
) {
  await user.click(button('Операции с таблицей'));
  return screen.findByRole('menu', { name: 'Операции с таблицей' });
}

describe('table: insert and operations (US-6, BR-RT-17)', () => {
  it('inserts a 3 × 3 table with a header row, the caret in the first cell', async () => {
    const user = userEvent.setup();
    const { button, editor } = await mountEditor('<p>до</p>');
    select(editor(), positionOf(editor(), 'до') + 2);

    await user.click(button('Таблица'));

    const html = editor().getHTML();
    expect(html).toContain(
      '<table><tbody><tr><th><p></p></th><th><p></p></th><th><p></p></th></tr>',
    );
    expect((html.match(/<tr>/g) ?? []).length).toBe(3);
    expect((html.match(/<td>/g) ?? []).length).toBe(6);
    const { $from } = editor().state.selection;
    expect($from.node($from.depth - 1).type.name).toBe('tableHeader');
    expect(html).not.toMatch(/colspan|rowspan|style|colgroup/);
  });

  it('lists the eight operations in order, and nothing for merge, split or width', async () => {
    const user = userEvent.setup();
    const { button, editor } = await mountEditor(TABLE_2X2);
    select(editor(), positionOf(editor(), 'a'));

    await waitFor(() => expect(button('Операции с таблицей')).toBeEnabled());
    const menu = await openOps(user, button);

    const items = Array.from(menu.querySelectorAll('[role^="menuitem"]'));
    expect(items.map((item) => item.textContent)).toEqual([
      'Добавить строку выше',
      'Добавить строку ниже',
      'Добавить столбец слева',
      'Добавить столбец справа',
      'Удалить строку',
      'Удалить столбец',
      'Строка заголовка',
      'Удалить таблицу',
    ]);
  });

  it('adds rows and columns on each side, keeping the table rectangular and spanless', async () => {
    const user = userEvent.setup();
    const { button, editor } = await mountEditor(TABLE_2X2);
    select(editor(), positionOf(editor(), 'a'));

    await waitFor(() => expect(button('Операции с таблицей')).toBeEnabled());
    let menu = await openOps(user, button);
    await user.click(within(menu).getByRole('menuitem', { name: 'Добавить строку ниже' }));
    select(editor(), positionOf(editor(), 'a'));
    await waitFor(() => expect(button('Операции с таблицей')).toBeEnabled());
    menu = await openOps(user, button);
    await user.click(within(menu).getByRole('menuitem', { name: 'Добавить столбец справа' }));

    const html = editor().getHTML();
    expect((html.match(/<tr>/g) ?? []).length).toBe(3);
    expect((html.match(/<td>/g) ?? []).length + (html.match(/<th>/g) ?? []).length).toBe(9);
    expect(html).not.toMatch(/colspan|rowspan/);
  });

  it('disables «Добавить строку выше» in the header row, and the deletes at one row or one column', async () => {
    const user = userEvent.setup();
    const { button, editor } = await mountEditor(
      '<table><tbody><tr><td><p>x</p></td></tr></tbody></table>',
    );
    select(editor(), positionOf(editor(), 'x'));

    await waitFor(() => expect(button('Операции с таблицей')).toBeEnabled());
    const menu = await openOps(user, button);
    expect(within(menu).getByRole('menuitem', { name: 'Удалить строку' })).toHaveAttribute(
      'aria-disabled',
      'true',
    );
    expect(within(menu).getByRole('menuitem', { name: 'Удалить столбец' })).toHaveAttribute(
      'aria-disabled',
      'true',
    );
    expect(
      within(menu).getByRole('menuitem', { name: 'Добавить строку выше' }),
    ).not.toHaveAttribute('aria-disabled');
  });

  it('disables «Добавить строку выше» when the caret is in the header row', async () => {
    const user = userEvent.setup();
    const { button, editor } = await mountEditor(TABLE_2X2);
    select(editor(), positionOf(editor(), 'h1'));

    await waitFor(() => expect(button('Операции с таблицей')).toBeEnabled());
    const menu = await openOps(user, button);

    expect(within(menu).getByRole('menuitem', { name: 'Добавить строку выше' })).toHaveAttribute(
      'aria-disabled',
      'true',
    );
    expect(
      within(menu).getByRole('menuitemcheckbox', { name: 'Строка заголовка' }),
    ).toHaveAttribute('aria-checked', 'true');
  });

  it('toggles the header row, deletes a row, a column and the table', async () => {
    const user = userEvent.setup();
    const { button, editor } = await mountEditor(TABLE_2X2);
    select(editor(), positionOf(editor(), 'a'));

    await waitFor(() => expect(button('Операции с таблицей')).toBeEnabled());
    let menu = await openOps(user, button);
    await user.click(within(menu).getByRole('menuitemcheckbox', { name: 'Строка заголовка' }));
    expect(editor().getHTML()).not.toContain('<th>');

    select(editor(), positionOf(editor(), 'a'));
    await waitFor(() => expect(button('Операции с таблицей')).toBeEnabled());
    menu = await openOps(user, button);
    await user.click(within(menu).getByRole('menuitem', { name: 'Удалить столбец' }));
    expect((editor().getHTML().match(/<td>/g) ?? []).length).toBe(2);

    select(editor(), positionOf(editor(), 'b'));
    await waitFor(() => expect(button('Операции с таблицей')).toBeEnabled());
    menu = await openOps(user, button);
    await user.click(within(menu).getByRole('menuitem', { name: 'Удалить строку' }));
    expect((editor().getHTML().match(/<tr>/g) ?? []).length).toBe(1);

    select(editor(), positionOf(editor(), 'h2'));
    await waitFor(() => expect(button('Операции с таблицей')).toBeEnabled());
    menu = await openOps(user, button);
    await user.click(within(menu).getByRole('menuitem', { name: 'Удалить таблицу' }));
    expect(editor().getHTML()).not.toContain('<table');
  });

  it('disables «Таблица» in a table, a list item and a code block, and the operations outside a table', async () => {
    const { button, editor } = await mountEditor(
      '<p>a</p>' + TABLE_2X2 + '<ul><li><p>пункт</p></li></ul><pre><code>код</code></pre>',
    );
    expect(button('Операции с таблицей')).toBeDisabled();

    for (const text of ['b', 'пункт', 'код']) {
      select(editor(), positionOf(editor(), text));
      await waitFor(() => expect(button('Таблица')).toBeDisabled());
    }
    select(editor(), positionOf(editor(), 'h1'));
    await waitFor(() => expect(button('Таблица')).toBeDisabled());
    expect(button('Операции с таблицей')).toBeEnabled();
  });

  it('one undo takes the whole insertion back', async () => {
    const user = userEvent.setup();
    const { button, editor } = await mountEditor('<p>до</p>');
    select(editor(), positionOf(editor(), 'до') + 2);
    await user.click(button('Таблица'));

    await user.click(button('Отменить'));

    expect(editor().getHTML()).toBe('<p>до</p>');
  });
});

describe('table: keyboard (US-6, BR-RT-17)', () => {
  it('Tab and Shift+Tab move between cells, and never add a row', async () => {
    const user = userEvent.setup();
    const { body, editor } = await mountEditor(TABLE_2X2);
    await user.click(body);
    select(editor(), positionOf(editor(), 'h1'));

    await user.keyboard('{Tab}');
    expect(editor().state.selection.$from.parent.textContent).toBe('h2');
    await user.keyboard('{Tab}');
    expect(editor().state.selection.$from.parent.textContent).toBe('a');
    await user.keyboard('{Shift>}{Tab}{/Shift}');
    expect(editor().state.selection.$from.parent.textContent).toBe('h2');
    expect(editor().getHTML()).toBe(TABLE_2X2);
  });

  it('Tab in the last cell leaves the editor and adds no row', async () => {
    const user = userEvent.setup();
    const { body, editor } = await mountEditor(TABLE_2X2 + '<p>после</p>');
    await user.click(body);
    select(editor(), positionOf(editor(), 'b'));

    await user.tab();

    expect(editor().getHTML()).toBe(TABLE_2X2 + '<p>после</p>');
    expect(body).not.toHaveFocus();
  });

  it('Esc, then Tab leaves from a middle cell; any other key in between disarms', async () => {
    const user = userEvent.setup();
    const { body, editor } = await mountEditor(TABLE_2X2);
    await user.click(body);
    select(editor(), positionOf(editor(), 'h1'));

    await user.keyboard('{Escape}{Tab}');
    expect(body).not.toHaveFocus();
    expect(editor().state.selection.$from.parent.textContent).toBe('h1');

    act(() => body.focus());
    select(editor(), positionOf(editor(), 'h1'));
    await user.keyboard('{Escape}');
    await user.keyboard('{ArrowRight}');
    await user.keyboard('{Tab}');
    expect(editor().state.selection.$from.parent.textContent).not.toBe('h1');
  });

  it('Esc, then Shift+Tab leaves backward', async () => {
    const user = userEvent.setup();
    const { body, editor, toolbar } = await mountEditor(TABLE_2X2);
    await user.click(body);
    select(editor(), positionOf(editor(), 'a'));

    await user.keyboard('{Escape}{Shift>}{Tab}{/Shift}');

    expect(body).not.toHaveFocus();
    // Backward, the previous tab stop is the toolbar's one: its first enabled control.
    expect(toolbar).toContainElement(document.activeElement as HTMLElement);
  });

  it('Ctrl+Enter puts the caret after the table; a final table gets a gap cursor and no document change', async () => {
    const user = userEvent.setup();
    const { body, editor, changes, flush } = await mountEditor(TABLE_2X2);
    await user.click(body);
    select(editor(), positionOf(editor(), 'a'));
    let docChanges = 0;
    editor().on('update', () => {
      docChanges++;
    });

    await user.keyboard('{Control>}{Enter}{/Control}');

    expect(editor().state.selection).toBeInstanceOf(GapCursor);
    expect(editor().getHTML()).toBe(TABLE_2X2);
    flush();
    expect(docChanges).toBe(0);
    expect(changes).toEqual([]);
  });

  it('Ctrl+Enter before another block puts the caret at its start', async () => {
    const user = userEvent.setup();
    const { body, editor } = await mountEditor(TABLE_2X2 + '<p>после</p>');
    await user.click(body);
    select(editor(), positionOf(editor(), 'a'));

    await user.keyboard('{Control>}{Enter}{/Control}');

    expect(editor().state.selection.$from.parent.textContent).toBe('после');
    expect(editor().state.selection.$from.parentOffset).toBe(0);
  });

  it('moving through an untouched final table dispatches no document change', async () => {
    const user = userEvent.setup();
    const { body, editor, changes, flush, button } = await mountEditor(TABLE_2X2);
    let docChanges = 0;
    editor().on('update', () => {
      docChanges++;
    });

    await user.click(body);
    select(editor(), positionOf(editor(), 'h1'));
    await user.keyboard('{Tab}{Tab}{Tab}{Shift>}{Tab}{/Shift}');
    await user.keyboard('{Control>}{Enter}{/Control}');
    await user.keyboard('{ArrowUp}');
    flush();

    expect(docChanges).toBe(0);
    expect(changes).toEqual([]);
    expect(button('Отменить')).toBeDisabled();
  });
});
