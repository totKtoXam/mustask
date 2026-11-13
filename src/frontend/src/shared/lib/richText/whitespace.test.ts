import { describe, expect, it } from 'vitest';
import {
  cleanText,
  codePointLength,
  isAllAsciiWhitespace,
  isUnicodeWhiteSpace,
  trimUnicodeWhiteSpace,
} from './whitespace';

describe('isUnicodeWhiteSpace (Rune.IsWhiteSpace)', () => {
  it.each([
    0x09, 0x0a, 0x0b, 0x0c, 0x0d, 0x20, 0x85, 0xa0, 0x1680, 0x2000, 0x2005, 0x200a, 0x2028, 0x2029,
    0x202f, 0x205f, 0x3000,
  ])('U+%s is white space', (codePoint) => {
    expect(isUnicodeWhiteSpace(codePoint)).toBe(true);
  });

  it.each([
    0x00, 0x08, 0x0e, 0x1c, 0x1f, 0x21, 0x84, 0x86, 0x180e, 0x200b, 0x200c, 0x200d, 0x2060, 0xfeff,
    0x61, 0x1f600,
  ])('U+%s is not white space', (codePoint) => {
    expect(isUnicodeWhiteSpace(codePoint)).toBe(false);
  });

  it('differs from the JavaScript \\s exactly where the plan says', () => {
    expect(/\s/.test('\ufeff')).toBe(true);
    expect(isUnicodeWhiteSpace(0xfeff)).toBe(false);
    expect(/\s/.test('\u0085')).toBe(false);
    expect(isUnicodeWhiteSpace(0x85)).toBe(true);
  });
});

describe('ascii whitespace and trimming', () => {
  it('knows HTML ASCII whitespace only', () => {
    expect(isAllAsciiWhitespace(' \t\n\f\r')).toBe(true);
    expect(isAllAsciiWhitespace('')).toBe(true);
    expect(isAllAsciiWhitespace(' \u00a0 ')).toBe(false);
    expect(isAllAsciiWhitespace('\u000b')).toBe(false);
  });

  it('trims with the server set', () => {
    expect(trimUnicodeWhiteSpace('\u3000 a \u0085')).toBe('a');
    expect(trimUnicodeWhiteSpace('\ufeffa')).toBe('\ufeffa');
  });
});

describe('codePointLength', () => {
  it('counts by code point', () => {
    expect(codePointLength('')).toBe(0);
    expect(codePointLength('abc')).toBe(3);
    expect(codePointLength('😀')).toBe(1);
    expect(codePointLength('👨‍👩‍👧')).toBe(5);
    expect(codePointLength('a\ud83db')).toBe(3);
    expect(codePointLength('\ude00')).toBe(1);
  });
});

describe('cleanText', () => {
  it('returns clean text as it is', () => {
    const text = 'a😀b';
    expect(cleanText(text)).toBe(text);
  });

  it('removes NUL and replaces lone surrogates', () => {
    expect(cleanText('a\u0000b')).toBe('ab');
    expect(cleanText('a\ud83db')).toBe('a\ufffdb');
    expect(cleanText('a\ude00b')).toBe('a\ufffdb');
    expect(cleanText('\ud83d')).toBe('\ufffd');
    expect(cleanText('😀\ud83d\u0000😀\ude00')).toBe('😀\ufffd😀\ufffd');
  });
});
