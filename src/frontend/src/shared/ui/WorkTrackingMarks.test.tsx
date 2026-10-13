import { describe, expect, it } from 'vitest';
import { render } from '@testing-library/react';
import { px, rgb, theme } from '../../test/kitTesting';
import { CATEGORY_MARKER_CLASS, CategoryMarker, TypeMark } from './WorkTrackingMarks';

// mod-015 § 2.3 and tech-015 design § 5.4: the two primitives the labels are built on.
// `TypeMark` is 20 px or, compact, 16; `CategoryMarker` is 8 px (`row`) or 10 (`field`),
// and `Removed` is a ring of `layout.frame.thick`.

describe('tech-015 Scenario: TypeMark and CategoryMarker (design § 5.4)', () => {
  describe('TypeMark', () => {
    it('is 20 px by default and 16 px compact — the tokens, not MUI’s small', () => {
      const { container, rerender } = render(<TypeMark appearance="story" />);
      const svg = () => container.querySelector('svg') as SVGElement;
      expect(px(getComputedStyle(svg()).fontSize)).toBe(theme.layout.mark.typeIcon);
      expect(svg()).not.toHaveClass('MuiSvgIcon-fontSizeSmall');
      rerender(<TypeMark appearance="story" compact />);
      expect(px(getComputedStyle(svg()).fontSize)).toBe(theme.layout.mark.typeIconCompact);
    });

    it('is aria-hidden and takes the colour override', () => {
      const { container } = render(
        <TypeMark appearance="story" colour={theme.palette.primary.dark} />,
      );
      const svg = container.querySelector('svg') as SVGElement;
      expect(svg).toHaveAttribute('aria-hidden', 'true');
      expect(getComputedStyle(svg).color).toBe(rgb(theme.palette.primary.dark));
    });
  });

  describe('CategoryMarker', () => {
    const marker = (container: HTMLElement) =>
      container.querySelector(`.${CATEGORY_MARKER_CLASS}`) as HTMLElement;

    it('is the 8 px dot by default and the 10 px dot of the band for `field`', () => {
      const { container, rerender } = render(<CategoryMarker code="InProgress" />);
      expect(px(getComputedStyle(marker(container)).width)).toBe(theme.layout.categoryDot);
      rerender(<CategoryMarker code="InProgress" size="field" />);
      expect(px(getComputedStyle(marker(container)).width)).toBe(
        theme.layout.mark.categoryDotField,
      );
      expect(px(getComputedStyle(marker(container)).height)).toBe(
        theme.layout.mark.categoryDotField,
      );
    });

    it('is a round, aria-hidden dot in the category’s colour', () => {
      const { container } = render(<CategoryMarker code="Completed" size="field" />);
      const style = getComputedStyle(marker(container));
      expect(marker(container)).toHaveAttribute('aria-hidden', 'true');
      expect(style.borderRadius).toBe('50%');
      expect(style.backgroundColor).toBe(rgb(theme.workTracking.categoryColour('Completed')));
    });

    it('draws Removed as a ring of layout.frame.thick in both sizes', () => {
      const { container } = render(<CategoryMarker code="Removed" size="field" />);
      const style = getComputedStyle(marker(container));
      expect(style.borderTopWidth).toBe(`${theme.layout.frame.thick}px`);
      expect(style.borderTopStyle).toBe('solid');
      expect(px(style.width)).toBe(theme.layout.mark.categoryDotField);
    });
  });
});
