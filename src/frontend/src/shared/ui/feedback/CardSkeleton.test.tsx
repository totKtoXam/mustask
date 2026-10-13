import { describe, expect, it } from 'vitest';
import { render, screen } from '@testing-library/react';
import { px, theme } from '../../../test/kitTesting';
import { CardSkeleton } from './CardSkeleton';

// tech-015 design § 5.12 «Skeletons»: the card while it loads — header, band and three generic
// columns — in one `role="status"` «Загрузка данных»; the page passes a hidden heading so it has
// its `h1`, the dialog passes none and is named by `DialogShell`'s `label`.

function shapes(container: HTMLElement): HTMLElement[] {
  return Array.from(container.querySelectorAll('.MuiSkeleton-root'));
}

describe('tech-015 Scenario: CardSkeleton (design § 5.12)', () => {
  describe('Scenario: one status, every shape hidden', () => {
    it('is exactly one status named «Загрузка данных»', () => {
      render(<CardSkeleton />);
      expect(screen.getAllByRole('status')).toHaveLength(1);
      expect(screen.getByRole('status', { name: 'Загрузка данных' })).toBeInTheDocument();
    });

    it('hides all of its shapes, and reads nothing but its name', () => {
      const { container } = render(<CardSkeleton />);
      const all = shapes(container);
      expect(all.length).toBeGreaterThan(10);
      for (const shape of all) {
        expect(shape.closest('[aria-hidden="true"]')).not.toBeNull();
      }
      expect(screen.getByRole('status')).toHaveTextContent('');
    });
  });

  describe('Scenario: the heading', () => {
    it('draws no heading by default: the dialog is named by its own label', () => {
      render(<CardSkeleton />);
      expect(screen.queryByRole('heading')).toBeNull();
    });

    it.each([1, 2] as const)('draws a visually hidden h%s of the given text', (level) => {
      render(<CardSkeleton heading={{ text: 'Рабочий элемент MSK-US-2', level }} />);
      const heading = screen.getByRole('heading', { level, name: 'Рабочий элемент MSK-US-2' });
      expect(heading.tagName).toBe(`H${level}`);
      // Hidden by the standard technique, not `display: none`: a screen reader still reads it.
      expect(getComputedStyle(heading).display).not.toBe('none');
      expect(getComputedStyle(heading).position).toBe('absolute');
      expect(px(getComputedStyle(heading).width)).toBe(1);
    });

    it('keeps the heading outside the live region, so only «Загрузка данных» is a status', () => {
      render(<CardSkeleton heading={{ text: 'Рабочий элемент MSK-US-2', level: 1 }} />);
      expect(screen.getByRole('status')).not.toContainElement(screen.getByRole('heading'));
      expect(screen.getAllByRole('status')).toHaveLength(1);
    });
  });

  describe('Scenario: the header mirrors CardHeaderBar', () => {
    it('is 124 high: 4 + three rows of 32 + two gaps of 8 + 8 below, with the content at 18', () => {
      const { container } = render(<CardSkeleton />);
      const header = container.querySelector('[data-card-skeleton-header]') as HTMLElement;
      const bar = header.firstElementChild as HTMLElement;
      const content = header.children[1] as HTMLElement;
      const style = getComputedStyle(content);
      const { card } = theme.layout;
      expect(px(getComputedStyle(bar).width)).toBe(card.typeBar);
      expect(card.typeBar + px(style.paddingLeft)).toBe(card.headerInset);
      expect(px(style.paddingRight)).toBe(card.rightInset);
      expect(px(style.paddingTop)).toBe(card.headerPadTop);
      expect(px(style.paddingBottom)).toBe(card.headerPadBottom);
      expect(px(style.rowGap)).toBe(card.headerRowGap);
      expect(content.children).toHaveLength(3);
      for (const row of Array.from(content.children)) {
        expect(px(getComputedStyle(row).height)).toBe(theme.density.controlHeight);
      }
    });

    it('draws a 220 × 16 line, a 60 % × 24 title, a circle, a 160 × 16 line and a 112 × 32 block', () => {
      const { container } = render(<CardSkeleton />);
      const header = container.querySelector('[data-card-skeleton-header]') as HTMLElement;
      const sizes = shapes(header).map((shape) => {
        const style = getComputedStyle(shape);
        return [style.width, style.height];
      });
      expect(sizes).toEqual([
        ['220px', '16px'],
        ['60%', '24px'],
        [`${theme.layout.mark.avatarInline}px`, `${theme.layout.mark.avatarInline}px`],
        ['160px', '16px'],
        ['112px', '32px'],
      ]);
      expect(shapes(header)[2]).toHaveClass('MuiSkeleton-circular');
    });
  });

  describe('Scenario: the band mirrors PropertyBand', () => {
    it('is the application background, at least bandMinHeight, with two label-and-value rows and a 160 × 16 stamp', () => {
      const { container } = render(<CardSkeleton />);
      const band = container.querySelector('[data-card-skeleton-band]') as HTMLElement;
      const { card } = theme.layout;
      expect(px(getComputedStyle(band).minHeight)).toBe(card.bandMinHeight);
      expect(px(getComputedStyle(band).paddingTop)).toBe(card.bandPaddingTop);
      expect(px(getComputedStyle(band).paddingLeft)).toBe(card.headerInset);
      const block = band.firstElementChild as HTMLElement;
      expect(block.children).toHaveLength(2);
      expect(px(getComputedStyle(block).rowGap)).toBe(card.bandRowGap);
      const stamp = shapes(band).at(-1) as HTMLElement;
      expect([getComputedStyle(stamp).width, getComputedStyle(stamp).height]).toEqual([
        '160px',
        '16px',
      ]);
    });
  });

  describe('Scenario: the body is three generic columns', () => {
    it('is a ColumnLayout of main, side and extra, each with a 24 high head', () => {
      const { container } = render(<CardSkeleton />);
      const areas = Array.from(container.querySelectorAll('[data-area]')) as HTMLElement[];
      expect(areas.map((area) => area.dataset.area)).toEqual(['main', 'side', 'extra']);
      for (const area of areas) {
        const head = area.querySelector('.MuiSkeleton-root') as HTMLElement;
        expect(getComputedStyle(head).height).toBe('24px');
      }
    });

    it('has six lines in the first column, three fields in the second and two in the third', () => {
      const { container } = render(<CardSkeleton />);
      const [main, side, extra] = Array.from(container.querySelectorAll('[data-area]')) as [
        HTMLElement,
        HTMLElement,
        HTMLElement,
      ];
      // A head, then the content: lines are one shape each, a field is a label and a value.
      expect(shapes(main)).toHaveLength(1 + 6);
      expect(shapes(side)).toHaveLength(1 + 3 * 2);
      expect(shapes(extra)).toHaveLength(1 + 2 * 2);
    });

    it('draws the fields at their own height: label 16 + gap + value 28 = 46 before the trailing gap', () => {
      const { container } = render(<CardSkeleton />);
      const side = container.querySelector('[data-area="side"]') as HTMLElement;
      const values = shapes(side).filter((shape) => getComputedStyle(shape).height === '28px');
      expect(values).toHaveLength(3);
      expect(16 + theme.layout.field.gap + theme.layout.field.box).toBe(46);
    });
  });
});
