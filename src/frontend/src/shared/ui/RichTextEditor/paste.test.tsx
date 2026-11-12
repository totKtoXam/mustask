import { act, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it } from 'vitest';
import { dropOn, mountEditor, pasteInto, positionOf, select } from '../../../test/richTextEditor';

const IMAGE = 'Изображения в описание не вставляются.';

async function empty(html = '') {
  const user = userEvent.setup();
  const mounted = await mountEditor(html);
  await user.click(mounted.body);
  return { ...mounted, user };
}

describe('paste: html is reduced to the subset (US-1, BR-RT-5)', () => {
  it('maps h1–h6, keeps bold and underline, drops colour, size and images', async () => {
    const { body, editor } = await empty();

    pasteInto(body, {
      text: 'x',
      html:
        '<h1>один</h1><h4>четыре</h4><h6>шесть</h6>' +
        '<p><b>жирный</b> <u>подчёркнутый</u> <span style="color:red;font-size:30px">цвет</span>' +
        '<img src="https://e.org/x.png"></p>',
    });

    expect(editor().getHTML()).toBe(
      '<h3>один</h3><h4>четыре</h4><h4>шесть</h4>' +
        '<p><strong>жирный</strong> <u>подчёркнутый</u> цвет</p>',
    );
  });

  it('reads marks from style, and does not take the Docs wrapper for bold', async () => {
    const { body, editor } = await empty();

    pasteInto(body, {
      text: 'x',
      html:
        '<meta charset="utf-8"><b style="font-weight:normal;" id="docs-internal-guid-1">' +
        '<p><span style="font-weight:700">жирный</span> <span style="font-style:italic">курсив</span> обычный</p></b>',
    });

    expect(editor().getHTML()).toBe('<p><strong>жирный</strong> <em>курсив</em> обычный</p>');
  });

  it('paste with Ctrl+Shift+V is plain paragraphs, with no table, link or format', async () => {
    const { body, editor, user } = await empty();
    await user.keyboard('{Control>}{Shift>}');
    act(() => {
      body.dispatchEvent(
        new KeyboardEvent('keydown', { key: 'V', ctrlKey: true, shiftKey: true, bubbles: true }),
      );
    });

    pasteInto(body, { text: 'a\tb\nc\td\ne\tf\nhttps://example.org' });

    expect(editor().getHTML()).not.toContain('<table');
    expect(editor().getHTML()).not.toContain('<a');
    await user.keyboard('{/Shift}{/Control}');
  });

  it('an IDE paste at the top level is a code block of its text, with its language when known', async () => {
    const { body, editor } = await empty();

    pasteInto(body, {
      text: 'const a = 1;\nreturn a;',
      html:
        '<div style="font-family: Consolas, monospace; white-space: pre;" class="language-javascript">' +
        '<div><span style="color:#569cd6">const</span> a = 1;</div><div>return a;</div></div>',
    });

    expect(editor().getHTML()).toBe(
      '<pre><code class="language-javascript">const a = 1;\nreturn a;</code></pre>',
    );
  });

  it('html pasted into a code block is its text', async () => {
    const { body, editor } = await empty('<pre><code>x</code></pre>');
    select(editor(), positionOf(editor(), 'x') + 1);

    pasteInto(body, { text: 'y\tz', html: '<p><b>y</b></p><p>z</p>' });

    expect(editor().getHTML()).toBe('<pre><code>xy\tz</code></pre>');
  });
});

describe('paste: plain text and the spreadsheet rule (BR-RT-17, F-7)', () => {
  it('keeps every line as a paragraph, blank lines as empty paragraphs', async () => {
    const { body, editor } = await empty();

    pasteInto(body, { text: 'один\r\n\r\nтри\nчетыре' });

    expect(editor().getHTML()).toBe('<p>один</p><p></p><p>три</p><p>четыре</p>');
  });

  it('makes a table of tab-separated lines of one shape: no header row', async () => {
    const { body, editor } = await empty();

    pasteInto(body, { text: 'а\tб\nв\tг\nд\tе\n' });

    expect(editor().getHTML()).toBe(
      '<table><tbody><tr><td><p>а</p></td><td><p>б</p></td></tr>' +
        '<tr><td><p>в</p></td><td><p>г</p></td></tr>' +
        '<tr><td><p>д</p></td><td><p>е</p></td></tr></tbody></table>',
    );
    expect(editor().getHTML()).not.toContain('<th>');
  });

  it('does not make a table of a ragged or short block', async () => {
    const { body, editor } = await empty();

    pasteInto(body, { text: 'а\tб\nв\nг\tд' });

    expect(editor().getHTML()).not.toContain('<table');
    expect(editor().getHTML()).toBe('<p>а\tб</p><p>в</p><p>г\tд</p>');
  });

  it('does not put a table inside a table cell or a list item', async () => {
    const { body, editor } = await empty(
      '<table><tbody><tr><td><p>x</p></td></tr></tbody></table><ul><li><p>y</p></li></ul>',
    );
    select(editor(), positionOf(editor(), 'x') + 1);

    pasteInto(body, { text: 'а\tб\nв\tг\nд\tе' });
    expect(
      (
        editor()
          .getHTML()
          .match(/<table/g) ?? []
      ).length,
    ).toBe(1);

    select(editor(), positionOf(editor(), 'y') + 1);
    pasteInto(body, { text: 'а\tб\nв\tг\nд\tе' });
    expect(
      (
        editor()
          .getHTML()
          .match(/<table/g) ?? []
      ).length,
    ).toBe(1);
  });
});

describe('paste into a cell or a list item (plan § 7.5)', () => {
  it('turns a pasted table and code block into paragraphs in a cell', async () => {
    const { body, editor } = await empty(
      '<table><tbody><tr><td><p>x</p></td></tr></tbody></table>',
    );
    select(editor(), positionOf(editor(), 'x') + 1);

    pasteInto(body, {
      text: 'a',
      html:
        '<table><tr><td>р1</td><td>р2</td></tr></table><pre><code>к1\nк2</code></pre>' +
        '<ul><li>п1</li></ul><h3>з</h3>',
    });

    const html = editor().getHTML();
    expect((html.match(/<table/g) ?? []).length).toBe(1);
    expect(html).not.toContain('<pre');
    expect(html).not.toContain('<ul');
    for (const text of ['р1', 'р2', 'к1', 'к2', 'п1', 'з']) expect(html).toContain(text);
  });

  it('turns a pasted table and code block into paragraphs in a list item, keeping a list', async () => {
    const { body, editor } = await empty('<ul><li><p>x</p></li></ul>');
    select(editor(), positionOf(editor(), 'x') + 1);

    pasteInto(body, {
      text: 'a',
      html: '<table><tr><td>р1</td></tr></table><pre><code>к1</code></pre>',
    });

    const html = editor().getHTML();
    expect(html).not.toContain('<table');
    expect(html).not.toContain('<pre');
    expect(html).toContain('р1');
    expect(html).toContain('к1');
    expect(html.startsWith('<ul><li>')).toBe(true);
  });
});

describe('images and files (D-7, US-1)', () => {
  it('drops an image of rich content, keeps the text, and tells the member', async () => {
    const { body, editor } = await empty();

    pasteInto(body, { text: 'a', html: '<p>до<img src="https://e.org/x.png">после</p>' });

    expect(editor().getHTML()).toBe('<p>дополсле</p>'.replace('полсле', 'после'));
    await waitFor(() => expect(screen.getByRole('status')).toHaveTextContent(IMAGE));
  });

  it('tells the member also when the image was the only thing pasted', async () => {
    const { body, editor } = await empty('<p>до</p>');

    pasteInto(body, { text: '', html: '<img src="data:image/png;base64,AAAA">' });

    expect(editor().getHTML()).toBe('<p>до</p>');
    await waitFor(() => expect(screen.getByRole('status')).toHaveTextContent(IMAGE));
  });

  it('does not insert a file, prevents the browser from navigating to it, and tells the member', async () => {
    const { body, editor } = await empty('<p>до</p>');

    const event = pasteInto(body, { files: [{ name: 'a.png', type: 'image/png' }] });

    expect(event.defaultPrevented).toBe(true);
    expect(editor().getHTML()).toBe('<p>до</p>');
    await waitFor(() => expect(screen.getByRole('status')).toHaveTextContent(IMAGE));
  });

  it('refuses a dropped file the same way', async () => {
    const { body, editor } = await empty('<p>до</p>');

    const event = dropOn(body, { files: [{ name: 'a.png', type: 'image/png' }] });

    expect(event.defaultPrevented).toBe(true);
    expect(editor().getHTML()).toBe('<p>до</p>');
    await waitFor(() => expect(screen.getByRole('status')).toHaveTextContent(IMAGE));
  });

  it('announces a second drop again: the region is emptied and filled once more', async () => {
    const { body } = await empty('<p>до</p>');
    const region = screen.getByRole('status');
    const seen: string[] = [];
    const observer = new MutationObserver(() => seen.push(region.textContent ?? ''));
    observer.observe(region, { childList: true, characterData: true, subtree: true });

    dropOn(body, { files: [{ name: 'a.png', type: 'image/png' }] });
    await waitFor(() => expect(region).toHaveTextContent(IMAGE));
    seen.length = 0;

    dropOn(body, { files: [{ name: 'b.png', type: 'image/png' }] });
    await waitFor(() => expect(seen).toContain(''));
    await waitFor(() => expect(region).toHaveTextContent(IMAGE));
    observer.disconnect();
  });

  it('clears the notice on the next edit', async () => {
    const { body, editor, user } = await empty('<p>до</p>');
    dropOn(body, { files: [{ name: 'a.png', type: 'image/png' }] });
    await waitFor(() => expect(screen.getByRole('status')).toHaveTextContent(IMAGE));

    select(editor(), positionOf(editor(), 'до') + 2);
    await user.keyboard('!');

    await waitFor(() => expect(screen.getByRole('status')).toBeEmptyDOMElement());
  });
});
