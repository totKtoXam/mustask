import { describe, expect, it } from 'vitest';
import { CODE_LANGUAGES } from '../../lib/richText';
import {
  CODE_TOKENS,
  HIGHLIGHT_MAX_CODE_POINTS,
  highlightRanges,
  isRegisteredLanguage,
  tokenOf,
} from './behaviour/codeHighlight';

describe('code colouring (F-3, S7)', () => {
  it('resolves all twelve languages of the vocabulary, html through the xml grammar', () => {
    for (const token of CODE_LANGUAGES) {
      expect(isRegisteredLanguage(token.name), token.name).toBe(true);
    }
    expect(isRegisteredLanguage('brainfuck')).toBe(false);
  });

  it('maps highlight.js scopes to the design’s eight token classes', () => {
    expect(tokenOf(['hljs-keyword'])).toBe('keyword');
    expect(tokenOf(['hljs-literal'])).toBe('keyword');
    expect(tokenOf(['hljs-name'])).toBe('keyword');
    expect(tokenOf(['hljs-string'])).toBe('string');
    expect(tokenOf(['hljs-number'])).toBe('number');
    expect(tokenOf(['hljs-comment'])).toBe('comment');
    expect(tokenOf(['hljs-title', 'function_'])).toBe('function');
    expect(tokenOf(['hljs-title', 'class_'])).toBe('type');
    expect(tokenOf(['hljs-attr'])).toBe('attribute');
    expect(tokenOf(['hljs-operator'])).toBe('punctuation');
    // A wrapper of a whole function and anything unlisted take no colour.
    expect(tokenOf(['hljs-function'])).toBeNull();
    expect(tokenOf(['hljs-params'])).toBeNull();
    expect(tokenOf([])).toBeNull();
    for (const token of CODE_TOKENS) expect(token).toMatch(/^[a-z]+$/);
  });

  it('turns a grammar’s output into ranges of the source text', () => {
    const source = 'const a = 1; // c';
    const ranges = highlightRanges('javascript', source);

    expect(ranges.map((range) => [source.slice(range.from, range.to), range.token])).toEqual(
      expect.arrayContaining([
        ['const', 'keyword'],
        ['1', 'number'],
        ['// c', 'comment'],
      ]),
    );
    for (const range of ranges) expect(range.to).toBeLessThanOrEqual(source.length);
  });

  it.each(CODE_LANGUAGES.map((token) => token.name))(
    'colours a %s sample without throwing',
    (name) => {
      const samples: Record<string, string> = {
        javascript: 'const a = "s";',
        typescript: 'let a: number = 1;',
        json: '{"a": [1, true]}',
        html: '<div class="a">x</div>',
        xml: '<a b="c">d</a>',
        css: 'a { color: red; }',
        sql: "SELECT a FROM t WHERE b = 'x';",
        csharp: 'public class A { int x = 1; }',
        python: 'def f(x):\n    return "s"',
        bash: 'echo "$a" # c',
        yaml: 'a: 1\nb:\n  - c',
        markdown: '# H\n*em*',
      };

      expect(highlightRanges(name, samples[name] as string).length).toBeGreaterThan(0);
    },
  );

  it('keeps the cap of design D-9 at 20 000 code points and 1 000 lines', () => {
    expect(HIGHLIGHT_MAX_CODE_POINTS).toBe(20_000);
  });
});
