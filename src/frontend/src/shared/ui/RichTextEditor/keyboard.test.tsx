import { act } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it } from 'vitest';
import { mountEditor, positionOf, select } from '../../../test/richTextEditor';

const LIST3 =
  '<ul><li><p>a</p><ul><li><p>b</p><ul><li><p>c</p></li><li><p>d</p></li></ul></li></ul></li></ul>';

describe('keys of the body (US-1, BR-RT-20)', () => {
  it('Tab leaves the body from a paragraph and from a list item: it never indents', async () => {
    const user = userEvent.setup();
    const { body, editor } = await mountEditor('<ul><li><p>a</p></li><li><p>b</p></li></ul>');
    await user.click(body);
    select(editor(), positionOf(editor(), 'b') + 1);
    const before = editor().getHTML();

    await user.tab();

    expect(editor().getHTML()).toBe(before);
    expect(body).not.toHaveFocus();
  });

  it('Ctrl+] sinks a list item and Ctrl+[ lifts it', async () => {
    const user = userEvent.setup();
    const { body, editor } = await mountEditor('<ul><li><p>a</p></li><li><p>b</p></li></ul>');
    await user.click(body);
    select(editor(), positionOf(editor(), 'b') + 1);

    await user.keyboard('{Control>}]{/Control}');
    expect(editor().getHTML()).toBe('<ul><li><p>a</p><ul><li><p>b</p></li></ul></li></ul>');

    await user.keyboard('{Control>}[[{/Control}');
    expect(editor().getHTML()).toBe('<ul><li><p>a</p></li><li><p>b</p></li></ul>');
  });

  it('never goes past three levels with Ctrl+], and never lifts out of the list with Ctrl+[', async () => {
    const user = userEvent.setup();
    const { body, editor } = await mountEditor(LIST3);
    await user.click(body);
    select(editor(), positionOf(editor(), 'd') + 1);

    await user.keyboard('{Control>}]{/Control}');
    expect(editor().getHTML()).toBe(LIST3);

    act(() => {
      editor().commands.setContent('<ul><li><p>x</p></li></ul>');
    });
    select(editor(), positionOf(editor(), 'x') + 1);
    await user.keyboard('{Control>}[[{/Control}');
    expect(editor().getHTML()).toBe('<ul><li><p>x</p></li></ul>');
  });

  it('Esc arms exactly one keystroke: Esc, a letter, then Tab is an ordinary Tab', async () => {
    const user = userEvent.setup();
    const { body, editor } = await mountEditor('<pre><code>ab</code></pre>');
    await user.click(body);
    select(editor(), positionOf(editor(), 'ab') + 2);

    await user.keyboard('{Escape}');
    await user.keyboard('x');
    await user.keyboard('{Tab}');

    expect(editor().getHTML()).toBe('<pre><code>abx\t</code></pre>');
  });

  it('a lone modifier does not disarm: Esc, then Shift+Tab', async () => {
    const user = userEvent.setup();
    const { body, editor } = await mountEditor('<pre><code>\tab</code></pre>');
    await user.click(body);
    select(editor(), positionOf(editor(), '\tab') + 3);

    await user.keyboard('{Escape}{Shift>}{Tab}{/Shift}');

    expect(editor().getHTML()).toBe('<pre><code>\tab</code></pre>');
  });

  it('Ctrl+Z and Ctrl+Y undo and redo', async () => {
    const user = userEvent.setup();
    const { body, editor } = await mountEditor('<p>a</p>');
    await user.click(body);
    select(editor(), positionOf(editor(), 'a') + 1);

    await user.keyboard('b');
    expect(editor().getHTML()).toBe('<p>ab</p>');
    await user.keyboard('{Control>}z{/Control}');
    expect(editor().getHTML()).toBe('<p>a</p>');
    await user.keyboard('{Control>}y{/Control}');
    expect(editor().getHTML()).toBe('<p>ab</p>');
  });

  it('Ctrl+B and Ctrl+I toggle the marks', async () => {
    const user = userEvent.setup();
    const { body, editor } = await mountEditor('<p>слово</p>');
    await user.click(body);
    act(() => {
      editor().commands.selectAll();
    });

    await user.keyboard('{Control>}b{/Control}{Control>}i{/Control}');

    expect(editor().getHTML()).toBe('<p><strong><em>слово</em></strong></p>');
  });
});
