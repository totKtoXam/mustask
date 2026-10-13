import { describe, expect, it } from 'vitest';
import { DESCRIPTION_MAX_MARKUP, DESCRIPTION_RAW_CEILING } from './limits';
import { normaliseRichText } from './normalise';

describe('normaliseRichText: totality and blank', () => {
  it('reduces the empty string to nothing', () => {
    expect(normaliseRichText('')).toEqual({
      html: '',
      visibleLength: 0,
      markupLength: 0,
      hasContent: false,
    });
  });

  it('keeps counts of a blank value (counted on the unblanked output)', () => {
    const result = normaliseRichText('<p></p><p> </p>');

    expect(result.html).toBe('');
    expect(result.hasContent).toBe(false);
    expect(result.markupLength).toBe('<p></p><p> </p>'.length);
    expect(result.visibleLength).toBe(2);
  });

  it('returns nothing for a frameset, which replaces the body', () => {
    expect(normaliseRichText('<frameset><frame src="x"></frameset>').html).toBe('');
  });

  it('never throws on arbitrary strings', () => {
    const samples = [
      '<',
      '<<<>>>',
      '&',
      '&#x0;',
      '</p></p></p>',
      '<p',
      '<a href="',
      '\u0000',
      '\ud83d',
      '<table><td><table><td>',
    ];
    for (const sample of samples) {
      expect(() => normaliseRichText(sample)).not.toThrow();
    }
  });

  it('replaces a lone surrogate by U+FFFD and counts it once', () => {
    const result = normaliseRichText('<p>a\ud83db</p>');

    expect(result.html).toBe('<p>a\ufffdb</p>');
    expect(result.visibleLength).toBe(3);
  });

  it('removes U+0000 from the raw string', () => {
    expect(normaliseRichText('<p>a\u0000b</p>').html).toBe('<p>ab</p>');
  });

  it('counts by code point and writes U+00A0 as an entity', () => {
    const result = normaliseRichText('<p>😀 a\u00a0b</p>');

    expect(result.html).toBe('<p>😀 a&nbsp;b</p>');
    expect(result.visibleLength).toBe(5);
    expect(result.markupLength).toBe('<p>😀 a&nbsp;b</p>'.length - 1);
  });
});

describe('normaliseRichText: marks', () => {
  it('nests all six marks and the palettes in the canonical order, whatever the input nesting', () => {
    const input =
      '<span class="rt-size-lg"><span class="rt-color-red"><mark class="rt-hl-yellow"><s><u><em><strong>' +
      '<a href="https://example.org">x</a></strong></em></u></s></mark></span></span>';

    expect(normaliseRichText(input).html).toBe(
      '<p><a href="https://example.org" rel="noopener noreferrer"><strong><em><u><s>' +
        '<mark class="rt-hl-yellow"><span class="rt-color-red"><span class="rt-size-lg">x' +
        '</span></span></mark></s></u></em></strong></a></p>',
    );
  });

  it('merges adjacent runs that share a prefix of marks', () => {
    const input = '<strong>a</strong><strong><em>b</em></strong><strong>c</strong>';

    expect(normaliseRichText(input).html).toBe('<p><strong>a<em>b</em>c</strong></p>');
  });

  it('drops the size in a heading and keeps the other marks', () => {
    expect(normaliseRichText('<h4><span class="rt-size-xl rt-color-red">a</span></h4>').html).toBe(
      '<h4><span class="rt-color-red">a</span></h4>',
    );
  });

  it('writes a code run with only the link', () => {
    expect(
      normaliseRichText('<a href="https://e.org"><strong><em><code>c</code></em></strong></a>')
        .html,
    ).toBe('<p><a href="https://e.org" rel="noopener noreferrer"><code>c</code></a></p>');
  });

  it('escapes the link address as an attribute', () => {
    expect(normaliseRichText('<a href="https://e.org/?a=1&amp;b=&quot;x&quot;">t</a>').html).toBe(
      '<p><a href="https://e.org/?a=1&amp;b=&quot;x&quot;" rel="noopener noreferrer">t</a></p>',
    );
  });
});

describe('normaliseRichText: structure', () => {
  it('clamps colspan to 1 000 columns', () => {
    const result = normaliseRichText('<table><tr><td colspan="1001">a</td></tr></table>');

    expect(result.html.match(/<td>/g)).toHaveLength(1000);
  });

  it('reads spans as trimmed non-negative integers of at most nine digits, else 1', () => {
    const wide = (span: string) =>
      normaliseRichText(`<table><tr><td colspan="${span}">a</td></tr></table>`).html.match(/<td>/g)
        ?.length;

    expect(wide(' 3 ')).toBe(3);
    expect(wide('3x')).toBe(1);
    expect(wide('-2')).toBe(1);
    expect(wide('+2')).toBe(1);
    expect(wide('')).toBe(1);
    expect(wide('2.5')).toBe(1);
    expect(wide('0')).toBe(1);
    expect(wide('0000000003')).toBe(1);
    expect(wide('000000003')).toBe(3);
  });

  it('treats a table that could not fit the markup bound as over the bound without building it', () => {
    const result = normaliseRichText(
      '<table><tr><td colspan="1000">a</td></tr>' +
        '<tr><td rowspan="1000">b</td></tr>'.repeat(70) +
        '</table>',
    );

    expect(result.markupLength).toBe(DESCRIPTION_MAX_MARKUP + 1);
  });

  it('puts a list nested deeper than three levels into the third item', () => {
    const input =
      '<ul><li>1<ul><li>2<ul><li>3<ul><li>4<ul><li>5</li></ul></li></ul></li></ul></li></ul></li></ul>';

    expect(normaliseRichText(input).html).toBe(
      '<ul><li><p>1</p><ul><li><p>2</p><ul><li><p>3</p><p>4</p><p>5</p></li></ul></li></ul></li></ul>',
    );
  });

  it('keeps a heading out of a list item and a cell (it becomes a paragraph)', () => {
    expect(
      normaliseRichText('<ul><li><h3>a</h3></li></ul><table><tr><td><h4>b</h4></td></tr></table>')
        .html,
    ).toBe('<ul><li><p>a</p></li></ul><table><tbody><tr><td><p>b</p></td></tr></tbody></table>');
  });

  it('keeps the line feeds of a code block and its language only from the first code child', () => {
    expect(
      normaliseRichText('<pre><code class="language-sql">select 1;\n\nselect 2;</code></pre>').html,
    ).toBe('<pre><code class="language-sql">select 1;\n\nselect 2;</code></pre>');
  });

  it('survives very deep nesting without recursing on it', () => {
    const depth = 1000;
    const input = '<span>'.repeat(depth) + 'deep' + '</span>'.repeat(depth);

    expect(normaliseRichText(input).html).toBe('<p>deep</p>');
  });
});

describe('normaliseRichText: size guards', () => {
  it('skips the walk above the server raw ceiling and reports the markup reason', () => {
    const result = normaliseRichText('a'.repeat(DESCRIPTION_RAW_CEILING + 1));

    expect(result.markupLength).toBe(DESCRIPTION_MAX_MARKUP + 1);
  });

  it('writes 100 000 visible characters and no more', () => {
    const result = normaliseRichText('<p>' + 'x'.repeat(100_000) + '</p>');

    expect(result.visibleLength).toBe(100_000);
    expect(result.markupLength).toBe(100_007);
  });

  it('counts whitespace it read and dropped against the markup budget', () => {
    const gap = ' '.repeat(600_000);
    const result = normaliseRichText('<p>a</p>' + gap + '<p>b</p>' + gap);

    expect(result.markupLength).toBe(DESCRIPTION_MAX_MARKUP + 1);
  });
});
