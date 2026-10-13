import { Editor, getSchema } from '@tiptap/core';
import { afterEach, describe, expect, it } from 'vitest';
import { normaliseRichText } from '../../lib/richText';
import { documentJson } from './behaviour/loadDocument';
import { EXTENSION_NAMES, createExtensions } from './schema/extensions';

/** The built-in extensions TipTap adds to every editor; not ours to choose. */
const TIPTAP_CORE = new Set([
  'clipboardTextSerializer',
  'commands',
  'delete',
  'drop',
  'editable',
  'focusEvents',
  'keymap',
  'paste',
  'tabindex',
  'textDirection',
]);

const created: Editor[] = [];

function open(html = ''): Editor {
  const extensions = createExtensions('Добавьте описание');
  const editor = new Editor({
    extensions,
    content: documentJson(getSchema(extensions), html),
  });
  created.push(editor);
  return editor;
}

afterEach(() => {
  for (const editor of created.splice(0)) editor.destroy();
});

describe('the extension list (F-11, ADR-0027 decision 7)', () => {
  it('registers exactly the planned extensions, so a dependency bump that re-adds one fails', () => {
    const names = open()
      .extensionManager.extensions.map((extension) => extension.name)
      .filter((name) => !TIPTAP_CORE.has(name))
      .sort();

    expect(names).toEqual([...EXTENSION_NAMES].sort());
  });

  it('has no blockquote, rule, task list, image or trailing node', () => {
    const { nodes, marks } = open().schema;

    for (const name of ['blockquote', 'horizontalRule', 'taskList', 'taskItem', 'image']) {
      expect(nodes[name], name).toBeUndefined();
    }
    expect(Object.keys(marks)).not.toContain('textStyle');
    expect(Object.keys(marks)).not.toContain('highlight');
  });

  it('orders the marks as the canonical nesting: link, bold, italic, underline, strike, highlight, colour, size, code', () => {
    const order = Object.keys(open().schema.marks);

    expect(order).toEqual([
      'link',
      'bold',
      'italic',
      'underline',
      'strike',
      'rtHighlight',
      'rtColor',
      'rtSize',
      'code',
    ]);
  });
});

describe('canonical form rules held by the schema', () => {
  it('takes only levels 3 and 4 of heading, and drops the size of a heading', () => {
    const editor = open('<h3><span class="rt-size-lg">a</span></h3>');

    expect(editor.getHTML()).toBe('<h3>a</h3>');
    expect(editor.can().toggleHeading({ level: 1 as never })).toBe(false);
  });

  it('lets inline code keep a link and nothing else', () => {
    const editor = open('<p><a href="https://example.org"><strong><code>c</code></strong></a></p>');

    expect(editor.getHTML()).toBe(
      '<p><a href="https://example.org" rel="noopener noreferrer"><code>c</code></a></p>',
    );
  });

  it('keeps code a mark of its own when bold is applied to a selection of it', () => {
    const editor = open('<p><code>code</code></p>');
    editor.commands.selectAll();
    editor.commands.toggleBold();

    expect(editor.getHTML()).toBe('<p><code>code</code></p>');
  });

  it('strips the other marks when code is applied to marked text', () => {
    const editor = open('<p><strong>b</strong></p>');
    editor.commands.selectAll();
    editor.commands.toggleCode();

    expect(editor.getHTML()).toBe('<p><code>b</code></p>');
  });

  it('writes the palette classes from the vocabulary, never a style', () => {
    const editor = open(
      '<p><mark class="rt-hl-yellow"><span class="rt-color-red"><span class="rt-size-lg">x</span></span></mark></p>',
    );

    const html = editor.getHTML();
    expect(html).toBe(
      '<p><mark class="rt-hl-yellow"><span class="rt-color-red"><span class="rt-size-lg">x</span></span></mark></p>',
    );
    expect(html).not.toContain('style');
  });

  it('writes a code block with its language only from the closed list', () => {
    expect(open('<pre><code class="language-sql">select 1</code></pre>').getHTML()).toBe(
      '<pre><code class="language-sql">select 1</code></pre>',
    );
    expect(open('<pre><code class="language-brainfuck">x</code></pre>').getHTML()).toBe(
      '<pre><code>x</code></pre>',
    );
  });

  it('gives a list item paragraphs and nested lists, no start, no type', () => {
    const editor = open('<ol start="5"><li>a<ul><li>b</li></ul></li></ol>');

    expect(editor.getHTML()).toBe('<ol><li><p>a</p><ul><li><p>b</p></li></ul></li></ol>');
  });

  it('keeps leading spaces, tabs and runs of spaces (preserveWhitespace: full)', () => {
    const html = '<p>  шаг</p><p>\t- a   b</p>';

    expect(open(html).getHTML()).toBe(html);
  });

  it('keeps a code block of one line feed (the string path of TipTap would drop it)', () => {
    const html = '<p>a</p><pre><code>\n</code></pre>';

    expect(open(html).getHTML()).toBe(html);
    expect(normaliseRichText(open(html).getHTML()).html).toBe(html);
  });

  it('writes cells with no span, no style and no colgroup', () => {
    const html =
      '<table><tbody><tr><th><p>h</p></th><th><p>i</p></th></tr><tr><td><p>a</p></td><td><p>b</p></td></tr></tbody></table>';

    expect(open(html).getHTML()).toBe(html);
  });

  it('refuses an address outside the rule when a link is set by command', () => {
    const editor = open('<p>text</p>');
    editor.commands.selectAll();

    expect(editor.commands.setLink({ href: 'javascript:alert(1)' })).toBe(false);
    expect(editor.commands.setLink({ href: 'https://example.org' })).toBe(true);
    expect(editor.getHTML()).toBe(
      '<p><a href="https://example.org" rel="noopener noreferrer">text</a></p>',
    );
  });
});

describe('keys that must not exist (BR-RT-20)', () => {
  it('does not bind Tab in a list item: the stock keymaps are removed', () => {
    const editor = open('<ul><li><p>a</p></li><li><p>b</p></li></ul>');
    editor.commands.setTextSelection(editor.state.doc.content.size - 3);

    const before = editor.getHTML();
    const tab = new KeyboardEvent('keydown', { key: 'Tab', bubbles: true, cancelable: true });
    editor.view.dom.dispatchEvent(tab);

    expect(editor.getHTML()).toBe(before);
    expect(tab.defaultPrevented).toBe(false);
  });
});
