import { describe, expect, it } from 'vitest';
import { countText, percentText } from './figures';

describe('mod-007 D-10 B: the outcome figures', () => {
  it.each([
    [63, '63 %'],
    [62.6, '63 %'],
    [0, '0 %'],
    [null, '—'],
  ])('writes the percent %s as %s', (value, text) => {
    expect(percentText(value)).toBe(text);
  });

  it.each([
    [6, '6'],
    [6.3, '6,3'],
    [0.5, '0,5'],
    [null, '—'],
  ])('writes the count %s with a decimal comma as %s', (value, text) => {
    expect(countText(value)).toBe(text);
  });
});
