import { describe, expect, it } from 'vitest';
import { normaliseBody } from './normalise';
import { reducePastedHtml } from './pasteReduce';
import { tabularRows } from './spreadsheetPaste';

function reduce(html: string): string {
  const reduced = reducePastedHtml(html);
  if (!reduced) throw new Error('not reducible');
  return normaliseBody(reduced.body).html;
}

describe('reducePastedHtml: styles read into marks (US-1, BR-RT-18)', () => {
  it('reads weight, italic, underline and line-through from style, in any nesting', () => {
    expect(
      reduce(
        '<p><span style="font-weight:700">ж</span> <span style="font-style:italic">к</span> ' +
          '<span style="text-decoration:underline">п</span> ' +
          '<span style="text-decoration-line:line-through">з</span></p>',
      ),
    ).toBe('<p><strong>ж</strong> <em>к</em> <u>п</u> <s>з</s></p>');
  });

  it.each([['bold'], ['bolder'], ['600'], ['800']])('weight %s is bold', (weight) => {
    expect(reduce(`<p><span style="font-weight: ${weight}">x</span></p>`)).toBe(
      '<p><strong>x</strong></p>',
    );
  });

  it.each([['normal'], ['400'], ['500']])('weight %s is not bold', (weight) => {
    expect(reduce(`<p><span style="font-weight: ${weight}">x</span></p>`)).toBe('<p>x</p>');
  });

  it('does not take the Google Docs wrapper for bold, and keeps what is declared below it', () => {
    expect(
      reduce(
        '<meta charset="utf-8"><b style="font-weight:normal;" id="docs-internal-guid-1">' +
          '<p dir="ltr"><span style="font-weight:700">жирный</span> обычный</p></b>',
      ),
    ).toBe('<p><strong>жирный</strong> обычный</p>');
    expect(
      reduce('<strong style="font-weight:400">a<span style="font-weight:bold">b</span></strong>'),
    ).toBe('<p>a<strong>b</strong></p>');
  });

  it('keeps a real b and strong', () => {
    expect(reduce('<b>a</b><strong>b</strong>')).toBe('<p><strong>ab</strong></p>');
  });

  it('never maps a foreign colour, background or size into the palette', () => {
    expect(
      reduce(
        '<p><span style="color:red;background-color:yellow;font-size:30px">x</span></p>' +
          '<p><font color="red" size="7">y</font></p>',
      ),
    ).toBe('<p>x</p><p>y</p>');
  });

  it('takes no mark from the style of a heading, and keeps its level', () => {
    expect(reduce('<h1 style="font-weight:700">a</h1><h5 style="font-weight:700">b</h5>')).toBe(
      '<h3>a</h3><h4>b</h4>',
    );
  });
});

describe('reducePastedHtml: code from an IDE or a page (BR-RT-16)', () => {
  it('makes a VS Code style block one code block of its plain text, lines from the block children', () => {
    const html =
      '<meta charset="utf-8"><div style="color:#d4d4d4;font-family: Consolas, \'Courier New\', monospace;' +
      'font-weight: normal;white-space: pre;"><div><span style="color:#569cd6">const</span> a = 1;</div>' +
      '<div><span style="color:#c586c0">return</span> a;</div></div>';

    expect(reduce(html)).toBe('<pre><code>const a = 1;\nreturn a;</code></pre>');
  });

  it('makes a JetBrains style pre one code block, token spans and styles gone', () => {
    const html =
      '<pre style="background-color:#2b2b2b;font-family:\'JetBrains Mono\',monospace;font-size:9.8pt;">' +
      '<span style="color:#cc7832;">public </span><span style="color:#ffc66d;">run</span>()</pre>';

    expect(reduce(html)).toBe('<pre><code>public run()</code></pre>');
  });

  it('turns br into line feeds and keeps source newlines', () => {
    expect(reduce('<pre>a<br>b\nc</pre>')).toBe('<pre><code>a\nb\nc</code></pre>');
  });

  it('keeps a language class only when it is in the closed list', () => {
    expect(reduce('<pre><code class="language-python">x</code></pre>')).toBe(
      '<pre><code class="language-python">x</code></pre>',
    );
    expect(reduce('<pre class="language-sql">x</pre>')).toBe(
      '<pre><code class="language-sql">x</code></pre>',
    );
    expect(reduce('<pre><code class="language-brainfuck">x</code></pre>')).toBe(
      '<pre><code>x</code></pre>',
    );
  });

  it('leaves an inline code element as inline code', () => {
    expect(reduce('<p>a <code>b</code> c</p>')).toBe('<p>a <code>b</code> c</p>');
  });

  it('does not take white-space: pre without a monospace font for code', () => {
    expect(reduce('<div style="white-space:pre;font-family:Arial">a</div>')).toBe('<p>a</p>');
  });
});

describe('reducePastedHtml: images (D-7)', () => {
  it('reports an image and drops it', () => {
    const reduced = reducePastedHtml('<p>a<img src="https://e.org/x.png">b</p>');

    expect(reduced?.hadImage).toBe(true);
    expect(normaliseBody(reduced!.body).html).toBe('<p>ab</p>');
  });

  it('reports a data: source and nothing else as no image', () => {
    expect(
      reducePastedHtml('<p><span src="data:image/png;base64,AAAA">x</span></p>')?.hadImage,
    ).toBe(true);
    expect(reducePastedHtml('<p>a</p>')?.hadImage).toBe(false);
  });

  it('is empty for an image alone', () => {
    expect(reduce('<img src="data:image/png;base64,AAAA">')).toBe('');
  });
});

describe('tabularRows (BR-RT-17)', () => {
  it('is a table for three lines of one shape', () => {
    expect(tabularRows('a\tb\nc\td\ne\tf')).toEqual([
      ['a', 'b'],
      ['c', 'd'],
      ['e', 'f'],
    ]);
  });

  it('ignores one final empty line from a trailing line feed', () => {
    expect(tabularRows('a\tb\r\nc\td\r\ne\tf\r\n')?.length).toBe(3);
  });

  it('keeps empty cells', () => {
    expect(tabularRows('a\t\nb\tc\n\td')).toEqual([
      ['a', ''],
      ['b', 'c'],
      ['', 'd'],
    ]);
  });

  it.each([
    ['two lines', 'a\tb\nc\td'],
    ['ragged', 'a\tb\nc\nd\te'],
    ['no tab', 'a\nb\nc'],
    ['uneven tabs', 'a\tb\nc\td\te\tf\tg'],
    ['empty', ''],
  ])('is not a table: %s', (_name, text) => {
    expect(tabularRows(text)).toBeNull();
  });
});
