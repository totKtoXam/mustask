import { act, render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type { Editor } from '@tiptap/core';
import { describe, expect, it } from 'vitest';
import { RichTextEditor } from './RichTextEditor';

async function mount(value: string, extra: { disabled?: boolean } = {}) {
  let editor: Editor | null = null;
  render(
    <RichTextEditor
      label="Описание"
      value={value}
      onChange={() => {}}
      onEditorReady={(ready) => {
        editor = ready;
      }}
      {...extra}
    />,
  );
  await screen.findByRole('textbox', { name: 'Описание' });
  const toolbar = screen.getByRole('toolbar', { name: 'Форматирование описания' });
  return {
    toolbar,
    editor: () => editor as unknown as Editor,
    button: (name: string) => within(toolbar).getByRole('button', { name }),
  };
}

/** The position inside the text node `text` (its first character), found in the document. */
function positionOf(editor: Editor, text: string): number {
  let found = -1;
  editor.state.doc.descendants((node, pos) => {
    if (found < 0 && node.isText && node.text === text) found = pos + 1;
  });
  if (found < 0) throw new Error(`No text ${text} in the document.`);
  return found;
}

const NAMES = [
  'Отменить',
  'Повторить',
  'Заголовок 3',
  'Заголовок 4',
  'Полужирный',
  'Курсив',
  'Маркированный список',
  'Нумерованный список',
  'Увеличить отступ списка',
  'Уменьшить отступ списка',
];

describe('toolbar', () => {
  it('is one toolbar with the named controls', async () => {
    const { toolbar } = await mount('<p>a</p>');

    expect(
      within(toolbar)
        .getAllByRole('button')
        .map((b) => b.getAttribute('aria-label')),
    ).toEqual(NAMES);
    expect(toolbar).toHaveAttribute('aria-orientation', 'horizontal');
  });

  it('has exactly one tab stop, on the first enabled control', async () => {
    const { toolbar, button } = await mount('<p>a</p>');

    const stops = within(toolbar)
      .getAllByRole('button')
      .filter((b) => b.tabIndex === 0);
    expect(stops).toEqual([button('Заголовок 3')]);
  });

  it('moves with the arrows over the enabled controls, wrapping, and Home and End', async () => {
    const user = userEvent.setup();
    const { button } = await mount('<p>a</p>');

    act(() => button('Заголовок 3').focus());
    await user.keyboard('{ArrowRight}');
    expect(button('Заголовок 4')).toHaveFocus();
    expect(button('Заголовок 4').tabIndex).toBe(0);
    expect(button('Заголовок 3').tabIndex).toBe(-1);

    await user.keyboard('{End}');
    // The two list-indent controls are disabled outside a list: the last enabled one is the numbered list.
    expect(button('Нумерованный список')).toHaveFocus();

    await user.keyboard('{ArrowRight}');
    expect(button('Заголовок 3')).toHaveFocus();

    await user.keyboard('{ArrowLeft}');
    expect(button('Нумерованный список')).toHaveFocus();

    await user.keyboard('{Home}');
    expect(button('Заголовок 3')).toHaveFocus();
  });

  it('remembers its last control and leaves with Tab, which it does not capture', async () => {
    const user = userEvent.setup();
    const { button } = await mount('<p>a</p>');

    act(() => button('Заголовок 3').focus());
    await user.keyboard('{ArrowRight}{ArrowRight}');
    expect(button('Полужирный')).toHaveFocus();

    await user.tab();
    expect(screen.getByRole('textbox', { name: 'Описание' })).toHaveFocus();

    await user.tab({ shift: true });
    expect(button('Полужирный')).toHaveFocus();
  });

  it('toggles bold and italic from the buttons and keeps the selection', async () => {
    const user = userEvent.setup();
    const { button, editor } = await mount('<p>слово</p>');
    act(() => {
      editor().commands.selectAll();
    });

    expect(button('Полужирный')).toHaveAttribute('aria-pressed', 'false');
    await user.click(button('Полужирный'));
    expect(editor().getHTML()).toBe('<p><strong>слово</strong></p>');
    await waitFor(() => expect(button('Полужирный')).toHaveAttribute('aria-pressed', 'true'));

    await user.click(button('Курсив'));
    expect(editor().getHTML()).toBe('<p><strong><em>слово</em></strong></p>');

    await user.click(button('Полужирный'));
    expect(editor().getHTML()).toBe('<p><em>слово</em></p>');
  });

  it('prevents the mousedown default so the body keeps its selection', async () => {
    const { button } = await mount('<p>a</p>');

    const down = new MouseEvent('mousedown', { bubbles: true, cancelable: true });
    button('Курсив').dispatchEvent(down);

    expect(down.defaultPrevented).toBe(true);
  });

  it('makes a heading of 3 or 4 exclusively and shows which one is pressed', async () => {
    const user = userEvent.setup();
    const { button, editor } = await mount('<p>тема</p>');

    await user.click(button('Заголовок 3'));
    expect(editor().getHTML()).toBe('<h3>тема</h3>');
    await waitFor(() => expect(button('Заголовок 3')).toHaveAttribute('aria-pressed', 'true'));
    expect(button('Заголовок 4')).toHaveAttribute('aria-pressed', 'false');

    await user.click(button('Заголовок 4'));
    expect(editor().getHTML()).toBe('<h4>тема</h4>');

    await user.click(button('Заголовок 4'));
    expect(editor().getHTML()).toBe('<p>тема</p>');
  });

  it('starts and ends a bullet or numbered list from the buttons', async () => {
    const user = userEvent.setup();
    const { button, editor } = await mount('<p>пункт</p>');

    await user.click(button('Маркированный список'));
    expect(editor().getHTML()).toBe('<ul><li><p>пункт</p></li></ul>');
    await waitFor(() =>
      expect(button('Маркированный список')).toHaveAttribute('aria-pressed', 'true'),
    );

    await user.click(button('Нумерованный список'));
    expect(editor().getHTML()).toBe('<ol><li><p>пункт</p></li></ol>');

    await user.click(button('Нумерованный список'));
    expect(editor().getHTML()).toBe('<p>пункт</p>');
  });

  it('indents and outdents items by buttons, three levels at most, never on a first item', async () => {
    const user = userEvent.setup();
    const { button, editor } = await mount(
      '<ul><li><p>a</p></li><li><p>b</p></li><li><p>c</p></li></ul>',
    );

    // First item: nothing to sink under, nothing to lift.
    act(() => {
      editor().commands.setTextSelection(positionOf(editor(), 'a'));
    });
    await waitFor(() => expect(button('Увеличить отступ списка')).toBeDisabled());
    expect(button('Уменьшить отступ списка')).toBeDisabled();

    // Second item: can sink; at level 1 it cannot be outdented by the button.
    act(() => {
      editor().commands.setTextSelection(positionOf(editor(), 'b'));
    });
    await waitFor(() => expect(button('Увеличить отступ списка')).toBeEnabled());
    expect(button('Уменьшить отступ списка')).toBeDisabled();

    await user.click(button('Увеличить отступ списка'));
    expect(editor().getHTML()).toBe(
      '<ul><li><p>a</p><ul><li><p>b</p></li></ul></li><li><p>c</p></li></ul>',
    );
    await waitFor(() => expect(button('Уменьшить отступ списка')).toBeEnabled());

    await user.click(button('Уменьшить отступ списка'));
    expect(editor().getHTML()).toBe('<ul><li><p>a</p></li><li><p>b</p></li><li><p>c</p></li></ul>');
  });

  it('disables the third level', async () => {
    const { button, editor } = await mount(
      '<ul><li><p>a</p><ul><li><p>b</p><ul><li><p>c</p></li><li><p>d</p></li></ul></li></ul></li></ul>',
    );
    // The caret in «d»: a third-level item that has a previous sibling.
    act(() => {
      editor().commands.setTextSelection(positionOf(editor(), 'd'));
    });

    await waitFor(() => expect(button('Уменьшить отступ списка')).toBeEnabled());
    expect(button('Увеличить отступ списка')).toBeDisabled();
  });

  it('follows the history: undo and redo are enabled only while there is a step', async () => {
    const user = userEvent.setup();
    const { button, editor } = await mount('<p>a</p>');
    expect(button('Отменить')).toBeDisabled();
    expect(button('Повторить')).toBeDisabled();

    act(() => {
      editor().commands.selectAll();
    });
    await user.click(button('Полужирный'));
    await waitFor(() => expect(button('Отменить')).toBeEnabled());

    await user.click(button('Отменить'));
    expect(editor().getHTML()).toBe('<p>a</p>');
    await waitFor(() => expect(button('Повторить')).toBeEnabled());
    await user.click(button('Повторить'));
    expect(editor().getHTML()).toBe('<p><strong>a</strong></p>');
  });

  it('puts the name and the shortcut in the tooltip, and the bare name in the label', async () => {
    const user = userEvent.setup();
    const { button } = await mount('<p>a</p>');

    await user.hover(button('Полужирный'));

    expect(await screen.findByRole('tooltip')).toHaveTextContent('Полужирный (Ctrl+B)');
    expect(button('Полужирный')).toHaveAttribute('aria-label', 'Полужирный');
  });
});
