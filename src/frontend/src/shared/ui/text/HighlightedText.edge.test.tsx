import { describe, expect, it } from 'vitest';
import { render } from '@testing-library/react';
import { HighlightedText } from './HighlightedText';
import { normaliseRanges, rangesOfQuery } from './highlightRanges';

// feat-070 QA: edges of the highlight maths and of the rendering.

function marks(container: HTMLElement): string[] {
  return Array.from(container.querySelectorAll('mark')).map((mark) => mark.textContent ?? '');
}

describe('feat-070 QA: HighlightedText edges', () => {
  it('merges overlapping and touching ranges into one mark', () => {
    const { container } = render(
      <HighlightedText
        text="abcdefghij"
        ranges={[
          [0, 4],
          [2, 6],
          [6, 8],
        ]}
      />,
    );
    expect(marks(container)).toEqual(['abcdefgh']);
    expect(container.textContent).toBe('abcdefghij');
  });

  it('clamps ranges past the end of the text and accepts unsorted input', () => {
    const { container } = render(
      <HighlightedText
        text="abcdef"
        ranges={[
          [4, 99],
          [0, 1],
        ]}
      />,
    );
    expect(marks(container)).toEqual(['a', 'ef']);
    expect(container.textContent).toBe('abcdef');
  });

  it('marks nothing for an empty ranges list, even with a query', () => {
    const { container } = render(<HighlightedText text="Оплата" ranges={[]} query="опл" />);
    expect(marks(container)).toEqual([]);
    expect(container.textContent).toBe('Оплата');
  });

  it('renders an empty text without crashing', () => {
    const { container } = render(<HighlightedText text="" query="a" />);
    expect(container.textContent).toBe('');
  });

  it('drops NaN ranges', () => {
    expect(
      normaliseRanges(
        [
          [Number.NaN, 3],
          [1, Number.NaN],
        ],
        10,
      ),
    ).toEqual([]);
  });

  it('trims the query and finds non-overlapping occurrences only', () => {
    expect(rangesOfQuery('aaaa', ' aa ')).toEqual([
      [0, 2],
      [2, 4],
    ]);
    expect(rangesOfQuery('aaa', 'aa')).toEqual([[0, 2]]);
  });

  it('keeps offsets aligned for letters whose lower case is longer', () => {
    const { container } = render(<HighlightedText text="İstanbul İ" query="stan" />);
    expect(marks(container)).toEqual(['stan']);
    expect(container.textContent).toBe('İstanbul İ');
  });

  it('matches Latin case-insensitively too', () => {
    const { container } = render(<HighlightedText text="Payment PAYMENT" query="payment" />);
    expect(marks(container)).toEqual(['Payment', 'PAYMENT']);
  });
});
