import { describe, expect, it } from 'vitest';
import { render, screen } from '@testing-library/react';
import { px, rgb, theme } from '../../../test/kitTesting';
import { CATEGORY_MARKER_CLASS } from '../WorkTrackingMarks';
import { StateLabel } from './StateLabel';

// tech-015 design § 5.4 «StateLabel»: a dot and a name, never a coloured chip; the
// dot is 8 px (`row`) or 10 px (`field`); `clip` is an ellipsis with the full name in `title`.

const dot = (container: HTMLElement) =>
  container.querySelector(`.${CATEGORY_MARKER_CLASS}`) as HTMLElement;

describe('tech-015 Scenario: StateLabel (design § 5.4)', () => {
  it('draws the category’s dot aria-hidden and the state’s name as text', () => {
    const { container } = render(<StateLabel categoryCode="InProgress" name="В работе" />);
    expect(screen.getByText('В работе')).toBeInTheDocument();
    expect(dot(container)).toHaveAttribute('aria-hidden', 'true');
    expect(getComputedStyle(dot(container)).backgroundColor).toBe(
      rgb(theme.workTracking.categoryColour('InProgress')),
    );
  });

  it('is a dot and a name, not a chip: the name carries no fill of its own', () => {
    render(<StateLabel categoryCode="Completed" name="Готово" />);
    const style = getComputedStyle(screen.getByText('Готово'));
    expect(style.backgroundColor).not.toBe(rgb(theme.workTracking.categoryColour('Completed')));
    expect(screen.getByText('Готово').closest('.MuiChip-root')).toBeNull();
  });

  it('puts layout.mark.nameGap between the dot and the name', () => {
    const { container } = render(<StateLabel categoryCode="Proposed" name="Новая" />);
    const style = getComputedStyle(container.firstElementChild as HTMLElement);
    expect(style.columnGap || style.gap).toBe(`${theme.layout.mark.nameGap}px`);
  });

  it.each([
    ['row', theme.layout.categoryDot],
    ['field', theme.layout.mark.categoryDotField],
  ] as const)('size %s draws a dot of %i px', (size, expected) => {
    const { container } = render(<StateLabel categoryCode="Proposed" name="Новая" size={size} />);
    const style = getComputedStyle(dot(container));
    expect(px(style.width)).toBe(expected);
    expect(px(style.height)).toBe(expected);
  });

  it('is the row size by default', () => {
    const { container } = render(<StateLabel categoryCode="Proposed" name="Новая" />);
    expect(px(getComputedStyle(dot(container)).width)).toBe(8);
  });

  it('draws «Удалённые» as a ring of layout.frame.thick, not a filled dot', () => {
    const { container } = render(<StateLabel categoryCode="Removed" name="Удалена" size="field" />);
    const style = getComputedStyle(dot(container));
    expect(style.borderTopWidth).toBe(`${theme.layout.frame.thick}px`);
    expect(style.borderTopColor).toBe(rgb(theme.workTracking.categoryColour('Removed')));
    expect(style.backgroundColor).not.toBe(rgb(theme.workTracking.categoryColour('Removed')));
  });

  describe('Scenario: clip', () => {
    it('cuts the name to one line with an ellipsis and keeps the whole name in the title', () => {
      render(<StateLabel categoryCode="InProgress" name="В работе над согласованием" clip />);
      const name = screen.getByText('В работе над согласованием');
      expect(name).toHaveAttribute('title', 'В работе над согласованием');
      const style = getComputedStyle(name);
      expect(style.textOverflow).toBe('ellipsis');
      expect(style.whiteSpace).toBe('nowrap');
      expect(style.overflow).toBe('hidden');
    });

    it('has no title and no ellipsis when it is not clipped', () => {
      render(<StateLabel categoryCode="InProgress" name="В работе" />);
      const name = screen.getByText('В работе');
      expect(name).not.toHaveAttribute('title');
      expect(getComputedStyle(name).textOverflow).not.toBe('ellipsis');
    });
  });
});
