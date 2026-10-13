import { describe, expect, it } from 'vitest';
import { render, screen } from '@testing-library/react';
import { px, theme } from '../../../test/kitTesting';
import { Circle, FieldShape, LOADING_LABEL, LoadingRegion, Shape } from './skeletonParts';

// tech-015 design § 5.12: the parts every skeleton is drawn from. The skeletons that use them
// (FieldsSkeleton, CardSkeleton, TableSkeleton) are tested in their own files; here is what
// the parts promise on their own.

function shapes(container: HTMLElement): HTMLElement[] {
  return Array.from(container.querySelectorAll('.MuiSkeleton-root'));
}

describe('tech-015 Scenario: skeletonParts (design § 5.12)', () => {
  describe('LoadingRegion', () => {
    it('is one status named «Загрузка данных», and its shapes are hidden from assistive technology', () => {
      const { container } = render(
        <LoadingRegion>
          <Shape height={10} />
          <Circle />
        </LoadingRegion>,
      );
      expect(LOADING_LABEL).toBe('Загрузка данных');
      expect(screen.getAllByRole('status')).toHaveLength(1);
      expect(screen.getByRole('status', { name: 'Загрузка данных' })).toBeInTheDocument();
      expect(screen.getByRole('status')).not.toHaveAttribute('aria-hidden');
      for (const shape of shapes(container)) {
        expect(shape.closest('[aria-hidden="true"]')).not.toBeNull();
      }
    });
  });

  describe('Shape and Circle', () => {
    it('draws a rounded shape of the given height that fills its row unless told a width', () => {
      const { container } = render(<Shape height={28} />);
      const [shape] = shapes(container);
      expect(shape).toHaveClass('MuiSkeleton-rounded');
      expect(getComputedStyle(shape as HTMLElement).height).toBe('28px');
      expect(getComputedStyle(shape as HTMLElement).width).toBe('100%');
      expect(getComputedStyle(shape as HTMLElement).maxWidth).toBe('100%');
    });

    it('takes a width in px or as a CSS size', () => {
      const { container } = render(
        <>
          <Shape height={10} width={96} />
          <Shape height={10} width="40%" />
        </>,
      );
      const [fixed, relative] = shapes(container) as [HTMLElement, HTMLElement];
      expect(getComputedStyle(fixed).width).toBe('96px');
      expect(getComputedStyle(relative).width).toBe('40%');
    });

    it('draws the person’s avatar slot as a circle of avatarInline', () => {
      const { container } = render(<Circle />);
      const [circle] = shapes(container);
      expect(circle).toHaveClass('MuiSkeleton-circular');
      expect(px(getComputedStyle(circle as HTMLElement).width)).toBe(
        theme.layout.mark.avatarInline,
      );
      expect(px(getComputedStyle(circle as HTMLElement).height)).toBe(
        theme.layout.mark.avatarInline,
      );
    });
  });

  describe('FieldShape', () => {
    it('stands for a label above a value: a label line, the value box, and the gaps — 48', () => {
      const { container } = render(<FieldShape placement="top" />);
      const [label, value] = shapes(container) as [HTMLElement, HTMLElement];
      expect(shapes(container)).toHaveLength(2);
      expect(px(getComputedStyle(value).height)).toBe(theme.layout.field.box);
      expect(getComputedStyle(label).width).toBe('96px');
    });

    it('stands for a band’s row: the label column, then the value', () => {
      const { container } = render(<FieldShape placement="start" />);
      expect(shapes(container)).toHaveLength(2);
      const row = container.firstElementChild as HTMLElement;
      expect(getComputedStyle(row).display).toBe('grid');
      expect(getComputedStyle(row).gridTemplateColumns).toContain(
        `${theme.layout.card.bandLabelWidth}px`,
      );
      expect(px(getComputedStyle(row).minHeight)).toBe(theme.layout.card.bandRow);
    });
  });
});
