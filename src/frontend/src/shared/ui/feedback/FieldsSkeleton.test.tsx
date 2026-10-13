import { describe, expect, it } from 'vitest';
import { render, screen } from '@testing-library/react';
import { px, theme } from '../../../test/kitTesting';
import { FieldsSkeleton } from './FieldsSkeleton';

// tech-015 design § 5.12 «Skeletons»: `count` fields of a field's own size (label 16, value 28,
// so 48), in one `role="status"` «Загрузка данных» with every shape hidden.

function shapes(container: HTMLElement): HTMLElement[] {
  return Array.from(container.querySelectorAll('.MuiSkeleton-root'));
}

describe('tech-015 Scenario: FieldsSkeleton (design § 5.12)', () => {
  it('is one status named «Загрузка данных»', () => {
    render(<FieldsSkeleton count={3} />);
    expect(screen.getAllByRole('status')).toHaveLength(1);
    expect(screen.getByRole('status', { name: 'Загрузка данных' })).toBeInTheDocument();
  });

  it('hides every shape from assistive technology, outside the status itself', () => {
    const { container } = render(<FieldsSkeleton count={2} />);
    const status = screen.getByRole('status');
    expect(status).not.toHaveAttribute('aria-hidden');
    for (const shape of shapes(container)) {
      expect(shape.closest('[aria-hidden="true"]')).not.toBeNull();
    }
    expect(status).toHaveTextContent('');
  });

  it('draws a label shape and a value shape for each of `count` fields', () => {
    const { container } = render(<FieldsSkeleton count={4} />);
    expect(shapes(container)).toHaveLength(8);
  });

  it('draws nothing but the empty status for none', () => {
    const { container } = render(<FieldsSkeleton count={0} />);
    expect(shapes(container)).toHaveLength(0);
    expect(screen.getByRole('status', { name: 'Загрузка данных' })).toBeInTheDocument();
  });

  describe('Scenario: label above the value mirrors a 48 px field', () => {
    it('is a 16 px label line, the gap, a 28 px value box and the gap', () => {
      const { container } = render(<FieldsSkeleton count={1} />);
      const [label, value] = shapes(container) as [HTMLElement, HTMLElement];
      const { field } = theme.layout;
      expect(px(getComputedStyle(value).height)).toBe(field.box);
      const line = label.parentElement as HTMLElement;
      expect(px(getComputedStyle(line).height)).toBe(16);
      expect(px(getComputedStyle(line).marginBottom)).toBe(field.gap);
      expect(px(getComputedStyle(value.parentElement as HTMLElement).marginBottom)).toBe(field.gap);
      expect(16 + field.gap + field.box + field.gap).toBe(48);
    });

    it('fills its container’s width with the value', () => {
      const { container } = render(<FieldsSkeleton count={1} />);
      const value = shapes(container)[1] as HTMLElement;
      expect(getComputedStyle(value).width).toBe('100%');
    });
  });

  describe('Scenario: label at the left mirrors the band’s rows', () => {
    it('is a row of 32 with the label column before the value, rows 4 apart', () => {
      const { container } = render(<FieldsSkeleton count={2} labelPlacement="start" />);
      const [label] = shapes(container) as [HTMLElement];
      const row = label.parentElement as HTMLElement;
      expect(px(getComputedStyle(row).minHeight)).toBe(theme.layout.card.bandRow);
      expect(getComputedStyle(row).gridTemplateColumns).toBe(
        `${theme.layout.card.bandLabelWidth}px minmax(0, 1fr)`,
      );
      const stack = row.parentElement as HTMLElement;
      expect(px(getComputedStyle(stack).rowGap)).toBe(theme.layout.card.bandRowGap);
      expect(stack.children).toHaveLength(2);
    });
  });
});
