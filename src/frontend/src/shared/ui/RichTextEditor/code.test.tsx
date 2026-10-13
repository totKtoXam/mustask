import { act, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { GapCursor } from '@tiptap/pm/gapcursor';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { mountEditor, positionOf, select } from '../../../test/richTextEditor';
import { CODE_LANGUAGES, normaliseRichText } from '../../lib/richText';
import { HIGHLIGHT_DELAY_MS } from './behaviour/codeHighlight';

afterEach(() => {
  vi.restoreAllMocks();
});

function stubClipboard() {
  const writeText = vi.fn().mockResolvedValue(undefined);
  Object.defineProperty(navigator, 'clipboard', { value: { writeText }, configurable: true });
  return writeText;
}

describe('code block: insert, language, copy (US-6, BR-RT-16)', () => {
  it('inserts a code block from the toolbar, and the strip shows the language', async () => {
    const user = userEvent.setup();
    const { button, editor, body } = await mountEditor('<p>код</p>');
    select(editor(), positionOf(editor(), 'код') + 1);

    await user.click(button('Блок кода'));

    expect(editor().getHTML()).toBe('<pre><code>код</code></pre>');
    await waitFor(() => expect(button('Блок кода')).toHaveAttribute('aria-pressed', 'true'));
    expect(body.querySelector('.rt-codeblock-label')).toHaveTextContent('Простой текст');
  });

  it('lists the language «Простой текст» and the twelve, and sets one; the strip follows', async () => {
    const user = userEvent.setup();
    const { button, editor, body } = await mountEditor('<pre><code>select 1</code></pre>');
    select(editor(), positionOf(editor(), 'select 1') + 1);

    await waitFor(() => expect(button('Язык кода')).toBeEnabled());
    await user.click(button('Язык кода'));
    const menu = await screen.findByRole('menu', { name: 'Язык кода' });

    const items = within(menu).getAllByRole('menuitemradio');
    expect(items).toHaveLength(13);
    expect(items[0]).toHaveTextContent('Простой текст');
    expect(items[0]).toHaveAttribute('aria-checked', 'true');
    expect(items.slice(1).map((item) => item.textContent)).toEqual([
      'JavaScript',
      'TypeScript',
      'JSON',
      'HTML',
      'XML',
      'CSS',
      'SQL',
      'C#',
      'Python',
      'Bash',
      'YAML',
      'Markdown',
    ]);
    expect(CODE_LANGUAGES).toHaveLength(12);

    await user.click(within(menu).getByRole('menuitemradio', { name: 'SQL' }));

    expect(editor().getHTML()).toBe('<pre><code class="language-sql">select 1</code></pre>');
    await waitFor(() => expect(body.querySelector('.rt-codeblock-label')).toHaveTextContent('SQL'));
  });

  it('is disabled outside a code block (language, copy)', async () => {
    const { button } = await mountEditor('<p>a</p>');

    expect(button('Язык кода')).toBeDisabled();
    expect(button('Копировать код')).toBeDisabled();
  });

  it('copies the exact text and announces «Код скопирован»; the strip is never stored', async () => {
    const user = userEvent.setup();
    const writeText = stubClipboard();
    const { button, editor, body } = await mountEditor('<pre><code>a\n\tb</code></pre>');
    select(editor(), positionOf(editor(), 'a\n\tb') + 1);

    await waitFor(() => expect(button('Копировать код')).toBeEnabled());
    await user.click(button('Копировать код'));

    await waitFor(() => expect(screen.getByRole('status')).toHaveTextContent('Код скопирован'));
    expect(writeText).toHaveBeenCalledExactlyOnceWith('a\n\tb');
    expect(body.querySelector('.rt-codeblock-strip')).not.toBeNull();
    expect(editor().getHTML()).toBe('<pre><code>a\n\tb</code></pre>');
  });

  it('says «Не удалось скопировать код.» when the clipboard refuses (G-2), from the toolbar and the strip', async () => {
    const user = userEvent.setup();
    const writeText = vi.fn().mockRejectedValue(new Error('denied'));
    Object.defineProperty(navigator, 'clipboard', { value: { writeText }, configurable: true });
    const { button, editor, body } = await mountEditor('<pre><code>x</code></pre>');
    select(editor(), positionOf(editor(), 'x') + 1);

    await waitFor(() => expect(button('Копировать код')).toBeEnabled());
    await user.click(button('Копировать код'));
    await waitFor(() =>
      expect(screen.getByRole('status')).toHaveTextContent('Не удалось скопировать код.'),
    );

    await user.click(body.querySelector('.rt-codeblock-copy') as HTMLElement);
    expect(writeText).toHaveBeenCalledTimes(2);
    expect(screen.getByRole('status')).toHaveTextContent('Не удалось скопировать код.');
  });

  it('copies from the strip for the pointer, which is not a tab stop', async () => {
    const user = userEvent.setup();
    const writeText = stubClipboard();
    const { body } = await mountEditor('<pre><code>x</code></pre>');
    const strip = body.querySelector<HTMLButtonElement>('.rt-codeblock-copy');

    expect(strip).not.toBeNull();
    expect(strip?.tabIndex).toBe(-1);
    expect(strip).toHaveAccessibleName('Копировать код');
    await user.click(strip as HTMLElement);

    expect(writeText).toHaveBeenCalledWith('x');
  });

  it('holds text only: bold, colour and the other marks are disabled in it', async () => {
    const { button, editor } = await mountEditor('<pre><code>x</code></pre>');
    select(editor(), positionOf(editor(), 'x') + 1);

    await waitFor(() => expect(button('Полужирный')).toBeDisabled());
    for (const name of [
      'Цвет текста',
      'Выделение цветом',
      'Размер шрифта',
      'Код в строке',
      'Ссылка',
    ]) {
      expect(button(name), name).toBeDisabled();
    }
  });

  it('is disabled in a table cell and a list item', async () => {
    const { button, editor } = await mountEditor(
      '<ul><li><p>пункт</p></li></ul><table><tbody><tr><td><p>ячейка</p></td></tr></tbody></table>',
    );
    for (const text of ['пункт', 'ячейка']) {
      select(editor(), positionOf(editor(), text));
      await waitFor(() => expect(button('Блок кода')).toBeDisabled());
    }
  });
});

describe('code block: keyboard (BR-RT-16)', () => {
  it('Tab inserts a tab character and Shift+Tab removes a tab, else up to four spaces', async () => {
    const user = userEvent.setup();
    const { body, editor } = await mountEditor('<pre><code>a</code></pre>');
    await user.click(body);
    select(editor(), positionOf(editor(), 'a') + 1);

    await user.keyboard('{Tab}b');
    expect(editor().getHTML()).toBe('<pre><code>a\tb</code></pre>');

    act(() => {
      editor().commands.setContent('<pre><code>      x</code></pre>');
    });
    select(editor(), positionOf(editor(), '      x') + 7);
    await user.keyboard('{Shift>}{Tab}{/Shift}');
    expect(editor().getHTML()).toBe('<pre><code>  x</code></pre>');
  });

  it('with a selection Tab indents each selected line and Shift+Tab outdents each', async () => {
    const user = userEvent.setup();
    const { body, editor } = await mountEditor('<pre><code>a\nb\nc</code></pre>');
    await user.click(body);
    const start = positionOf(editor(), 'a\nb\nc');
    select(editor(), start, start + 'a\nb\nc'.length);

    await user.keyboard('{Tab}');
    expect(editor().getHTML()).toBe('<pre><code>\ta\n\tb\n\tc</code></pre>');

    await user.keyboard('{Shift>}{Tab}{/Shift}');
    expect(editor().getHTML()).toBe('<pre><code>a\nb\nc</code></pre>');
  });

  it('Esc, then Tab leaves the block instead of indenting', async () => {
    const user = userEvent.setup();
    const { body, editor } = await mountEditor('<pre><code>a</code></pre>');
    await user.click(body);
    select(editor(), positionOf(editor(), 'a') + 1);

    await user.keyboard('{Escape}{Tab}');

    expect(editor().getHTML()).toBe('<pre><code>a</code></pre>');
    expect(body).not.toHaveFocus();
  });

  it('Enter inserts a line feed; Enter twice at the end leaves the block and removes the trailing line feeds', async () => {
    const user = userEvent.setup();
    const { body, editor } = await mountEditor('<pre><code>a</code></pre><p>после</p>');
    await user.click(body);
    select(editor(), positionOf(editor(), 'a') + 1);

    await user.keyboard('{Enter}');
    expect(editor().getHTML()).toContain('<pre><code>a\n</code></pre>');
    await user.keyboard('{Enter}');

    expect(editor().getHTML()).toBe('<pre><code>a</code></pre><p>после</p>');
    expect(editor().state.selection.$from.parent.textContent).toBe('после');
  });

  it('Ctrl+Enter leaves the block at once; ArrowDown leaves it only at the end of its text', async () => {
    const user = userEvent.setup();
    const { body, editor } = await mountEditor('<pre><code>ab</code></pre><p>после</p>');
    await user.click(body);
    select(editor(), positionOf(editor(), 'ab') + 1);

    await user.keyboard('{ArrowDown}');
    // Not at the end of the text: the default (or nothing in jsdom), the caret stays in the block.
    expect(editor().state.selection.$from.parent.type.name).toBe('codeBlock');

    select(editor(), positionOf(editor(), 'ab') + 2);
    await user.keyboard('{ArrowDown}');
    expect(editor().state.selection.$from.parent.textContent).toBe('после');

    select(editor(), positionOf(editor(), 'ab') + 1);
    await user.keyboard('{Control>}{Enter}{/Control}');
    expect(editor().state.selection.$from.parent.textContent).toBe('после');
    expect(editor().getHTML()).toBe('<pre><code>ab</code></pre><p>после</p>');
  });

  it('a final code block: leaving it puts a gap cursor and stores no paragraph until typing', async () => {
    const user = userEvent.setup();
    const { body, editor, changes, flush } = await mountEditor('<pre><code>ab</code></pre>');
    await user.click(body);
    select(editor(), positionOf(editor(), 'ab') + 2);

    await user.keyboard('{ArrowDown}');

    expect(editor().state.selection).toBeInstanceOf(GapCursor);
    expect(editor().getHTML()).toBe('<pre><code>ab</code></pre>');
    flush();
    expect(changes).toEqual([]);
  });
});

describe('gap cursor after a final block (S5)', () => {
  it('typing at the gap cursor creates the paragraph, and only then is the value changed', async () => {
    const user = userEvent.setup();
    const { body, editor, changes, flush } = await mountEditor('<pre><code>ab</code></pre>');
    await user.click(body);
    select(editor(), positionOf(editor(), 'ab') + 2);
    await user.keyboard('{ArrowDown}');
    expect(editor().state.selection).toBeInstanceOf(GapCursor);

    await user.keyboard('x');

    expect(editor().getHTML()).toBe('<pre><code>ab</code></pre><p>x</p>');
    flush();
    expect(changes.at(-1)?.html).toBe('<pre><code>ab</code></pre><p>x</p>');
  });
});

describe('code block: notices (design § Keyboard)', () => {
  it('announces the Tab hint on entering a block, and clears it on leaving', async () => {
    const user = userEvent.setup();
    const { body, editor } = await mountEditor('<p>до</p><pre><code>x</code></pre>');
    await user.click(body);
    select(editor(), positionOf(editor(), 'до') + 1);
    await waitFor(() => expect(screen.getByRole('status')).toHaveTextContent(''));

    select(editor(), positionOf(editor(), 'x') + 1);
    await waitFor(() =>
      expect(screen.getByRole('status')).toHaveTextContent(
        'Tab вставляет отступ. Esc, затем Tab — выйти из блока.',
      ),
    );

    select(editor(), positionOf(editor(), 'до') + 1);
    await waitFor(() => expect(screen.getByRole('status')).toBeEmptyDOMElement());
  });
});

describe('code colouring: decorations, never stored (F-3, design § Syntax colouring)', () => {
  it('colours a block of a closed language after the idle delay, with no change to the value', async () => {
    const { body, editor, changes, flush } = await mountEditor(
      '<pre><code class="language-javascript">const a = 1; // c</code></pre>',
    );
    const before = editor().getHTML();

    await waitFor(() => expect(body.querySelector('.rt-code-keyword')).not.toBeNull(), {
      timeout: HIGHLIGHT_DELAY_MS * 20,
    });
    expect(body.querySelector('.rt-code-number')).not.toBeNull();
    expect(body.querySelector('.rt-code-comment')).not.toBeNull();

    // Decorations are not nodes: the serialisation and the value are as without them.
    expect(editor().getHTML()).toBe(before);
    expect(normaliseRichText(editor().getHTML()).html).toBe(before);
    expect(editor().getHTML()).not.toMatch(/rt-code|hljs|span/);
    flush();
    expect(changes).toEqual([]);
  });

  it('does not colour plain text', async () => {
    const { body } = await mountEditor('<pre><code>const a = 1;</code></pre>');

    await new Promise((resolve) => setTimeout(resolve, HIGHLIGHT_DELAY_MS * 3));

    expect(body.querySelector('[class^="rt-code-"]')).toBeNull();
  });

  it('colours again after an edit, for the edited block, not on every keystroke', async () => {
    const user = userEvent.setup();
    const { body, editor } = await mountEditor(
      '<pre><code class="language-javascript">let a;</code></pre>',
    );
    await waitFor(() => expect(body.querySelector('.rt-code-keyword')).not.toBeNull());
    await user.click(body);
    select(editor(), positionOf(editor(), 'let a;') + 6);

    await user.keyboard(' return');

    await waitFor(() => {
      const keywords = Array.from(body.querySelectorAll('.rt-code-keyword')).map(
        (n) => n.textContent,
      );
      expect(keywords).toContain('return');
    });
  });
});
