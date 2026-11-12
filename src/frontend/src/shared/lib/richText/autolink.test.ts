import { describe, expect, it } from 'vitest';
import { findAutoLinks } from './autolink';

function hrefs(text: string): string[] {
  return findAutoLinks(text).map((link) => link.href);
}

function covered(text: string): string[] {
  return findAutoLinks(text).map((link) => text.slice(link.start, link.end));
}

describe('findAutoLinks (BR-RT-18)', () => {
  it.each([
    ['https://example.org', ['https://example.org']],
    ['http://example.org/path?q=1#a', ['http://example.org/path?q=1#a']],
    ['HTTPS://EXAMPLE.ORG', ['HTTPS://EXAMPLE.ORG']],
    ['mailto:a@example.org', ['mailto:a@example.org']],
    ['www.example.org', ['https://www.example.org']],
    ['смотри https://example.org и www.test.ru', ['https://example.org', 'https://www.test.ru']],
  ])('links %j', (text, expected) => {
    expect(hrefs(text)).toEqual(expected);
  });

  it.each([
    ['example.org'],
    ['a@b.ru'],
    ['setup.py'],
    ['ftp://x.org'],
    ['http://'],
    ['www.'],
    ['https:/x'],
    ['javascript:alert(1)'],
    [''],
    ['   '],
  ])('does not link %j', (text) => {
    expect(hrefs(text)).toEqual([]);
  });

  it('keeps trailing punctuation and an unbalanced bracket out of the link', () => {
    expect(covered('(см. https://example.org).')).toEqual(['https://example.org']);
    expect(covered('https://example.org, затем')).toEqual(['https://example.org']);
    expect(covered('«https://example.org»')).toEqual([]);
    expect(covered('https://example.org!?')).toEqual(['https://example.org']);
    expect(covered('[https://example.org]')).toEqual([]);
    expect(covered('see https://example.org]')).toEqual(['https://example.org']);
  });

  it('keeps a balanced pair in the address', () => {
    expect(covered('https://example.org/a_(b)')).toEqual(['https://example.org/a_(b)']);
    expect(covered('https://example.org/a_(b).')).toEqual(['https://example.org/a_(b)']);
  });

  it('reports the position of each address in the text', () => {
    expect(findAutoLinks('a https://x.org b')).toEqual([
      { start: 2, end: 15, href: 'https://x.org' },
    ]);
  });

  it('splits on Unicode white space, a no-break space included', () => {
    expect(covered('https://a.org https://b.org')).toEqual(['https://a.org', 'https://b.org']);
  });
});
