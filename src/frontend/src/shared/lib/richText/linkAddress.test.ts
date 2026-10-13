import { describe, expect, it } from 'vitest';
import { promptAddress, readLinkAddress } from './linkAddress';

describe('readLinkAddress (US-2 link address, the server rule)', () => {
  it.each([
    ['https://example.org', 'https://example.org'],
    ['http://example.org/a?b=1#c', 'http://example.org/a?b=1#c'],
    ['HTTPS://EXAMPLE.ORG', 'HTTPS://EXAMPLE.ORG'],
    ['mailto:a@example.org', 'mailto:a@example.org'],
    ['MAILTO:a@example.org?subject=x', 'MAILTO:a@example.org?subject=x'],
    ['http://u@h', 'http://u@h'],
    ['http://[::1]:8080/', 'http://[::1]:8080/'],
    ['  https://e.org/a\tb\n  ', 'https://e.org/ab'],
    ['\u0001 https://example.org', 'https://example.org'],
    ['https://example.org/a_(b)', 'https://example.org/a_(b)'],
  ])('keeps %j as %j', (raw, expected) => {
    expect(readLinkAddress(raw)).toBe(expected);
  });

  it.each([
    [null],
    [undefined],
    [''],
    ['   '],
    ['example.org'],
    ['/relative'],
    ['//evil.example'],
    ['#frag'],
    ['javascript:alert(1)'],
    [' JaVaScRiPt:x'],
    ['java\tscript:x'],
    ['data:text/html;base64,AAAA'],
    ['vbscript:x'],
    ['ftp://example.org'],
    ['file:///etc/passwd'],
    ['https:example.org'],
    ['https:/x'],
    ['https://'],
    ['http://:80'],
    ['http://@'],
    ['https:///x'],
    ['http://a b'],
    ['http://a<b'],
    ['http://a\\b'],
    ['mailto:'],
    ['mailto:?subject=x'],
    ['mailto:a b@example.org'],
    ['https://example.org/a<b'],
    ['https://example.org/?q=>'],
    ['mailto:a<b@example.org'],
    [':x'],
    ['1http://example.org'],
    ['ht tp://example.org'],
  ])('refuses %j', (raw) => {
    expect(readLinkAddress(raw)).toBeNull();
  });
});

describe('promptAddress (BR-RT-18, F-10)', () => {
  it('turns www. into https://', () => {
    expect(promptAddress('www.example.org')).toBe('https://www.example.org');
    expect(promptAddress('  WWW.Example.org/path  ')).toBe('https://WWW.Example.org/path');
  });

  it.each([['www.'], ['www./'], ['www.?'], ['www.#'], ['www. ']])(
    'refuses %j as an address',
    (input) => {
      expect(promptAddress(input)).toBeNull();
    },
  );

  it('passes the server rule through', () => {
    expect(promptAddress('https://example.org')).toBe('https://example.org');
    expect(promptAddress('mailto:a@example.org')).toBe('mailto:a@example.org');
    expect(promptAddress('javascript:alert(1)')).toBeNull();
    expect(promptAddress('example.org')).toBeNull();
    expect(promptAddress('')).toBeNull();
  });
});
