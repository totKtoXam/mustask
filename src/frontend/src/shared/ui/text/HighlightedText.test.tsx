import { describe, expect, it } from 'vitest';
import { render } from '@testing-library/react';
import { rgb, theme } from '../../../test/kitTesting';
import { HighlightedText } from './HighlightedText';
import { normaliseRanges } from './highlightRanges';

// feat-070 / feat-065 design § 1: `<mark>` pieces built from text ranges, never from markup.

function marks(container: HTMLElement): string[] {
  return Array.from(container.querySelectorAll('mark')).map((mark) => mark.textContent ?? '');
}

describe('feat-070 Scenario: HighlightedText (feat-065 design § 1)', () => {
  it('draws the text as it is without a query or ranges', () => {
    const { container } = render(<HighlightedText text="Оплата картой" />);
    expect(container.textContent).toBe('Оплата картой');
    expect(container.querySelector('mark')).toBeNull();
  });

  it.each(['', '   '])('marks nothing for the empty query %j', (query) => {
    const { container } = render(<HighlightedText text="Оплата" query={query} />);
    expect(marks(container)).toEqual([]);
  });

  it('marks nothing when the query is not in the text', () => {
    const { container } = render(<HighlightedText text="Оплата" query="склад" />);
    expect(marks(container)).toEqual([]);
    expect(container.textContent).toBe('Оплата');
  });

  it('marks every occurrence of the query, case-insensitively, keeping the text’s case', () => {
    const { container } = render(<HighlightedText text="Оплата и оПлата" query="ОПЛАТ" />);
    expect(marks(container)).toEqual(['Оплат', 'оПлат']);
    expect(container.textContent).toBe('Оплата и оПлата');
  });

  it('treats «ё» as «е» in both directions', () => {
    const first = render(<HighlightedText text="Приёмка" query="приемка" />);
    expect(marks(first.container)).toEqual(['Приёмка']);
    const second = render(<HighlightedText text="Объем" query="объём" />);
    expect(marks(second.container)).toEqual(['Объем']);
  });

  it('matches a query with regex characters as plain text', () => {
    const paren = render(<HighlightedText text="a.b (1) [x] a+b" query="(1)" />);
    expect(marks(paren.container)).toEqual(['(1)']);
    const dot = render(<HighlightedText text="a.b axb" query="a.b" />);
    expect(marks(dot.container)).toEqual(['a.b']);
    const star = render(<HighlightedText text="ab* ab" query="*" />);
    expect(marks(star.container)).toEqual(['*']);
    const bracket = render(<HighlightedText text="x[y" query="[" />);
    expect(marks(bracket.container)).toEqual(['[']);
  });

  it('marks the ranges given, several of them, and ranges win over the query', () => {
    const { container } = render(
      <HighlightedText
        text="Оплата картой и оплата счёта"
        ranges={[
          [0, 6],
          [16, 22],
        ]}
        query="счёта"
      />,
    );
    expect(marks(container)).toEqual(['Оплата', 'оплата']);
    expect(container.textContent).toBe('Оплата картой и оплата счёта');
  });

  it('never turns the text into markup', () => {
    const text = '<b>bold</b> & <img src=x onerror=alert(1)>';
    const { container } = render(<HighlightedText text={text} query="bold" />);
    expect(container.querySelector('b')).toBeNull();
    expect(container.querySelector('img')).toBeNull();
    expect(marks(container)).toEqual(['bold']);
    expect(container.textContent).toBe(text);
  });

  it('emphasises with bold and the match tint, so colour is not the only carrier', () => {
    const { container } = render(<HighlightedText text="Оплата" query="опл" />);
    const style = getComputedStyle(container.querySelector('mark') as HTMLElement);
    expect(style.backgroundColor).toBe(rgb(theme.palette.search.match));
    expect(Number(style.fontWeight)).toBe(theme.typography.fontWeightMedium);
  });

  describe('normaliseRanges', () => {
    it('clamps, drops empty and invalid ranges, sorts and merges overlaps', () => {
      expect(
        normaliseRanges(
          [
            [8, 12],
            [-3, 2],
            [5, 5],
            [9, 7],
            [1, 4],
            [20, 30],
            [11, 40],
          ],
          14,
        ),
      ).toEqual([
        [0, 4],
        [8, 14],
      ]);
    });
  });
});
