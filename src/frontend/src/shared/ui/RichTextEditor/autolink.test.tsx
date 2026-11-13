import { act } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it } from 'vitest';
import { mountEditor, pasteInto, positionOf, select } from '../../../test/richTextEditor';

const LINK = (href: string, text: string) =>
  `<a href="${href}" rel="noopener noreferrer">${text}</a>`;

describe('typed auto-link (BR-RT-18)', () => {
  it('links an address when a space ends it, and the first undo removes only the link', async () => {
    const user = userEvent.setup();
    const { body, editor } = await mountEditor('');
    await user.click(body);

    await user.keyboard('см. https://example.org ');

    expect(editor().getHTML()).toBe(
      `<p>см. ${LINK('https://example.org', 'https://example.org')} </p>`,
    );

    await user.keyboard('{Control>}z{/Control}');
    expect(editor().getHTML()).toBe('<p>см. https://example.org </p>');

    await user.keyboard('{Control>}z{/Control}');
    expect(editor().getHTML()).not.toContain('https://example.org');
  });

  it('keeps trailing punctuation and an unbalanced bracket out of the link', async () => {
    const user = userEvent.setup();
    const { body, editor } = await mountEditor('');
    await user.click(body);

    await user.keyboard('(см. https://example.org). ');

    expect(editor().getHTML()).toBe(
      `<p>(см. ${LINK('https://example.org', 'https://example.org')}). </p>`,
    );
  });

  it('links on Enter too, in the line that was ended', async () => {
    const user = userEvent.setup();
    const { body, editor } = await mountEditor('');
    await user.click(body);

    await user.keyboard('www.example.org{Enter}');

    expect(editor().getHTML()).toBe(
      `<p>${LINK('https://www.example.org', 'www.example.org')}</p><p></p>`,
    );
  });

  it('does not link what is not an address: a bare domain, a bare e-mail, a file name', async () => {
    const user = userEvent.setup();
    const { body, editor } = await mountEditor('');
    await user.click(body);

    await user.keyboard('example.org a@b.ru setup.py ftp://x.org ');

    expect(editor().getHTML()).not.toContain('<a');
  });

  it('does not link inside inline code or an existing link', async () => {
    const user = userEvent.setup();
    const { body, editor } = await mountEditor('<p><code>x</code></p>');
    await user.click(body);
    select(editor(), positionOf(editor(), 'x') + 1);

    await user.keyboard('https://example.org ');

    expect(editor().getHTML()).not.toContain('<a');
  });

  it('does not link inside a code block', async () => {
    const user = userEvent.setup();
    const { body, editor } = await mountEditor('<pre><code>x</code></pre>');
    await user.click(body);
    select(editor(), positionOf(editor(), 'x') + 1);

    await user.keyboard('https://example.org ');

    expect(editor().getHTML()).not.toContain('<a');
  });

  it('does not link a legacy value on open, and opening is not a change', async () => {
    const { editor, changes, flush } = await mountEditor(
      '<p>https://example.org и www.test.ru</p>',
    );
    act(() => {
      editor().commands.focus();
    });
    flush();

    expect(editor().getHTML()).toBe('<p>https://example.org и www.test.ru</p>');
    expect(changes).toEqual([]);
  });
});

describe('pasted auto-link (BR-RT-18, F-6)', () => {
  it('links addresses in plain text and the first undo removes only the links', async () => {
    const user = userEvent.setup();
    const { body, editor } = await mountEditor('');
    await user.click(body);

    pasteInto(body, { text: 'см. https://example.org и www.test.ru' });

    expect(editor().getHTML()).toBe(
      `<p>см. ${LINK('https://example.org', 'https://example.org')} и ${LINK('https://www.test.ru', 'www.test.ru')}</p>`,
    );

    await user.keyboard('{Control>}z{/Control}');
    expect(editor().getHTML()).toBe('<p>см. https://example.org и www.test.ru</p>');
  });

  it('links a selection over a single pasted address and keeps its text', async () => {
    const user = userEvent.setup();
    const { body, editor } = await mountEditor('<p>документация</p>');
    await user.click(body);
    select(editor(), 1, 1 + 'документация'.length);

    pasteInto(body, { text: 'https://example.org/docs' });

    expect(editor().getHTML()).toBe(`<p>${LINK('https://example.org/docs', 'документация')}</p>`);
  });

  it('adds no link on a plain paste (Ctrl+Shift+V)', async () => {
    const user = userEvent.setup();
    const { body, editor } = await mountEditor('');
    await user.click(body);

    act(() => {
      body.dispatchEvent(
        new KeyboardEvent('keydown', { key: 'V', ctrlKey: true, shiftKey: true, bubbles: true }),
      );
    });
    pasteInto(body, { text: 'https://example.org' });

    expect(editor().getHTML()).toBe('<p>https://example.org</p>');
  });

  it('leaves a paste with html to the paste pipeline', async () => {
    const user = userEvent.setup();
    const { body, editor } = await mountEditor('');
    await user.click(body);

    pasteInto(body, { text: 'https://example.org', html: '<p>https://example.org</p>' });

    expect(editor().getHTML()).not.toContain('<a');
  });
});
