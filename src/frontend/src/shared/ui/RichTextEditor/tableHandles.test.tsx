import { act, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it } from 'vitest';
import { mountEditor, positionOf, select } from '../../../test/richTextEditor';

/** A header table 3 × 3: the header row, then two body rows. */
const TABLE_3X3 =
  '<table><tbody><tr><th><p>h1</p></th><th><p>h2</p></th><th><p>h3</p></th></tr>' +
  '<tr><td><p>a</p></td><td><p>b</p></td><td><p>c</p></td></tr>' +
  '<tr><td><p>d</p></td><td><p>e</p></td><td><p>f</p></td></tr></tbody></table>';
const PLAIN_2X2 =
  '<table><tbody><tr><td><p>a</p></td><td><p>b</p></td></tr>' +
  '<tr><td><p>c</p></td><td><p>d</p></td></tr></tbody></table>';

const ALL = [
  'Добавить строку выше',
  'Добавить строку ниже',
  'Добавить столбец слева',
  'Добавить столбец справа',
  'Удалить строку',
  'Удалить столбец',
];

function handles() {
  return within(screen.getByTestId('rt-table-handles'));
}

function shown(): string[] {
  return handles()
    .queryAllByRole('button')
    .map((button) => button.getAttribute('aria-label') ?? '');
}

async function caretIn(text: string, html: string) {
  const user = userEvent.setup();
  const mounted = await mountEditor(html);
  await user.click(mounted.body);
  select(mounted.editor(), positionOf(mounted.editor(), text));
  return { user, ...mounted };
}

function caretText(editor: ReturnType<Awaited<ReturnType<typeof caretIn>>['editor']>) {
  return editor.state.selection.$from.parent.textContent;
}

describe('table handles (US-6, BR-RT-17, design § Tables)', () => {
  it('shows the offered handles on a middle body row, with the menu names', async () => {
    await caretIn('b', TABLE_3X3);

    await waitFor(() => expect(shown()).toEqual(ALL));
  });

  it('hides «Добавить строку выше» in the header row, as the menu does', async () => {
    await caretIn('h2', TABLE_3X3);

    await waitFor(() => expect(shown()).toEqual(ALL.filter((name) => name !== ALL[0])));
  });

  it('offers it on the first row of a table without a header row', async () => {
    await caretIn('a', PLAIN_2X2);

    await waitFor(() => expect(shown()).toEqual(ALL));
  });

  it('offers everything on the last row and column', async () => {
    await caretIn('f', TABLE_3X3);

    await waitFor(() => expect(shown()).toEqual(ALL));
  });

  it('offers no delete when one row and one column remain', async () => {
    await caretIn('x', '<table><tbody><tr><td><p>x</p></td></tr></tbody></table>');

    await waitFor(() => expect(shown()).toEqual(ALL.slice(0, 4)));
  });

  it('offers no row delete in a one-row table, and no column delete in a one-column one', async () => {
    await caretIn('x', '<table><tbody><tr><td><p>x</p></td><td><p>y</p></td></tr></tbody></table>');
    await waitFor(() => expect(shown()).toEqual(ALL.filter((name) => name !== 'Удалить строку')));
  });

  it('shows nothing outside a table, when the editor is not focused, and after blur', async () => {
    const user = userEvent.setup();
    const { body, editor } = await mountEditor('<p>до</p>' + TABLE_3X3);
    select(editor(), positionOf(editor(), 'b'));
    expect(shown()).toEqual([]);

    await user.click(body);
    select(editor(), positionOf(editor(), 'b'));
    await waitFor(() => expect(shown()).toEqual(ALL));

    select(editor(), positionOf(editor(), 'до'));
    await waitFor(() => expect(shown()).toEqual([]));

    select(editor(), positionOf(editor(), 'b'));
    await waitFor(() => expect(shown()).toEqual(ALL));
    act(() => body.blur());
    await waitFor(() => expect(shown()).toEqual([]));
  });

  it('shows nothing while the editor is disabled', async () => {
    const { editor } = await mountEditor(TABLE_3X3, { disabled: true });
    select(editor(), positionOf(editor(), 'b'));
    act(() => editor().view.dom.focus());

    expect(shown()).toEqual([]);
  });

  it('is not in the tab order and does not change the canonical html', async () => {
    const { editor, changes, flush } = await caretIn('b', TABLE_3X3);
    await waitFor(() => expect(shown()).toEqual(ALL));

    for (const button of handles().getAllByRole('button')) {
      expect(button).toHaveAttribute('tabindex', '-1');
    }
    expect(editor().getHTML()).toBe(TABLE_3X3);
    expect(editor().view.dom.querySelector('[aria-label="Удалить строку"]')).toBeNull();
    flush();
    expect(changes).toEqual([]);
  });

  it('keeps the caret in the editor on a press (mousedown is prevented)', async () => {
    const { user, body, editor } = await caretIn('b', TABLE_3X3);
    await waitFor(() => expect(shown()).toEqual(ALL));

    await user.click(handles().getByRole('button', { name: 'Добавить столбец справа' }));

    expect(body).toHaveFocus();
    expect(caretText(editor())).toBe('b');
  });

  it('adds a row below and above, the caret staying in its cell', async () => {
    const { user, editor } = await caretIn('b', TABLE_3X3);
    await waitFor(() => expect(shown()).toEqual(ALL));

    await user.click(handles().getByRole('button', { name: 'Добавить строку ниже' }));
    let html = editor().getHTML();
    expect((html.match(/<tr>/g) ?? []).length).toBe(4);
    expect(caretText(editor())).toBe('b');

    await waitFor(() => expect(shown()).toEqual(ALL));
    await user.click(handles().getByRole('button', { name: 'Добавить строку выше' }));
    html = editor().getHTML();
    expect((html.match(/<tr>/g) ?? []).length).toBe(5);
    expect((html.match(/<th>/g) ?? []).length).toBe(3);
    expect(caretText(editor())).toBe('b');
    expect(html).not.toMatch(/colspan|rowspan|style|colgroup/);
  });

  it('adds a column on each side, rectangular, the caret staying put', async () => {
    const { user, editor } = await caretIn('b', TABLE_3X3);
    await waitFor(() => expect(shown()).toEqual(ALL));

    await user.click(handles().getByRole('button', { name: 'Добавить столбец слева' }));
    await waitFor(() => expect(shown()).toEqual(ALL));
    await user.click(handles().getByRole('button', { name: 'Добавить столбец справа' }));

    const html = editor().getHTML();
    const rows = html.match(/<tr>.*?<\/tr>/g) ?? [];
    expect(rows).toHaveLength(3);
    for (const row of rows) expect((row.match(/<t[dh]>/g) ?? []).length).toBe(5);
    expect(caretText(editor())).toBe('b');
  });

  it('adds below the header row and keeps the new row a body row', async () => {
    const { user, editor } = await caretIn('h2', TABLE_3X3);
    await waitFor(() => expect(shown()).toContain('Добавить строку ниже'));

    await user.click(handles().getByRole('button', { name: 'Добавить строку ниже' }));

    const html = editor().getHTML();
    expect((html.match(/<th>/g) ?? []).length).toBe(3);
    expect((html.match(/<tr>/g) ?? []).length).toBe(4);
  });

  it('deletes a row and puts the caret in the row that took its place; the last row steps back', async () => {
    const { user, editor } = await caretIn('b', TABLE_3X3);
    await waitFor(() => expect(shown()).toEqual(ALL));

    await user.click(handles().getByRole('button', { name: 'Удалить строку' }));
    expect((editor().getHTML().match(/<tr>/g) ?? []).length).toBe(2);
    expect(caretText(editor())).toBe('e');

    await waitFor(() => expect(shown()).toEqual(ALL));
    await user.click(handles().getByRole('button', { name: 'Удалить строку' }));
    expect(caretText(editor())).toBe('h2');
    await waitFor(() => expect(shown()).not.toContain('Удалить строку'));
  });

  it('deletes a column and puts the caret in the next one; the last column steps back', async () => {
    const { user, editor } = await caretIn('b', TABLE_3X3);
    await waitFor(() => expect(shown()).toEqual(ALL));

    await user.click(handles().getByRole('button', { name: 'Удалить столбец' }));
    expect(caretText(editor())).toBe('c');
    expect((editor().getHTML().match(/<td>/g) ?? []).length).toBe(4);

    await waitFor(() => expect(shown()).toEqual(ALL));
    await user.click(handles().getByRole('button', { name: 'Удалить столбец' }));
    expect(caretText(editor())).toBe('a');
    await waitFor(() => expect(shown()).not.toContain('Удалить столбец'));
  });

  it('keeps the «…» menu, with all eight items, as the keyboard path', async () => {
    const { user, button } = await caretIn('b', TABLE_3X3);
    await waitFor(() => expect(button('Операции с таблицей')).toBeEnabled());

    await user.click(button('Операции с таблицей'));

    const menu = await screen.findByRole('menu', { name: 'Операции с таблицей' });
    expect(menu.querySelectorAll('[role^="menuitem"]')).toHaveLength(8);
  });
});
