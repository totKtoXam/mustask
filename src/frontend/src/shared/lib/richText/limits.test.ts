import { describe, expect, it } from 'vitest';
import {
  DESCRIPTION_MAX_MARKUP,
  DESCRIPTION_MAX_VISIBLE,
  checkDescription,
  type DescriptionVerdict,
} from './limits';
import { normaliseRichText, type NormalisedRichText } from './normalise';

function value(visibleLength: number, markupLength: number): NormalisedRichText {
  return { html: '<p>x</p>', visibleLength, markupLength, hasContent: true };
}

describe('checkDescription (BR-RT-7, US-4)', () => {
  it.each<[number, number, DescriptionVerdict]>([
    [DESCRIPTION_MAX_VISIBLE, 1000, 'ok'],
    [DESCRIPTION_MAX_VISIBLE + 1, 1000, 'too-long'],
    [10, DESCRIPTION_MAX_MARKUP, 'ok'],
    [10, DESCRIPTION_MAX_MARKUP + 1, 'markup-too-large'],
    [DESCRIPTION_MAX_VISIBLE + 1, DESCRIPTION_MAX_MARKUP + 1, 'markup-too-large'],
  ])('visible %i, markup %i is %s', (visible, markup, verdict) => {
    expect(checkDescription(value(visible, markup))).toBe(verdict);
  });

  it('refuses a blank value over a ceiling instead of treating it as empty (BR-RT-6)', () => {
    const blank = normaliseRichText('<p>' + ' '.repeat(DESCRIPTION_MAX_MARKUP) + '</p>');

    expect(blank.markupLength).toBeGreaterThan(DESCRIPTION_MAX_MARKUP);
    expect(checkDescription(blank)).toBe('markup-too-large');
  });

  it('reports a table whose grid could not fit the ceiling as over it, under the visible limit (US-4)', () => {
    // A 1 000-column row under 70 more rows is 71 000 positions of at least 16 code points each: the grid is
    // refused before it is built (plan-backend § 5.1), whatever the input size.
    const result = normaliseRichText(
      '<table><tr><td colspan="1000">a</td></tr>' + '<tr><td>b</td></tr>'.repeat(70) + '</table>',
    );

    expect(result.markupLength).toBe(DESCRIPTION_MAX_MARKUP + 1);
    expect(checkDescription(result)).toBe('markup-too-large');
  });
});
