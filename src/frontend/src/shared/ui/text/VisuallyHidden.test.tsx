import { describe, expect, it } from 'vitest';
import { render, screen } from '@testing-library/react';
import { VisuallyHidden } from './VisuallyHidden';

// tech-015 design § 5.5 «VisuallyHidden»: `theme.mixins.visuallyHidden` — there for
// assistive technology, not `display: none`.

describe('tech-015 Scenario: VisuallyHidden (design § 5.5)', () => {
  it('draws its text in the DOM, clipped to a pixel, off the layout', () => {
    render(<VisuallyHidden>Подсказка для чтения</VisuallyHidden>);
    const style = getComputedStyle(screen.getByText('Подсказка для чтения'));
    expect(style.position).toBe('absolute');
    expect(style.width).toBe('1px');
    expect(style.height).toBe('1px');
    expect(style.overflow).toBe('hidden');
    expect(style.whiteSpace).toBe('nowrap');
  });

  it('is not display: none or hidden — a screen reader reads it', () => {
    render(<VisuallyHidden>Подсказка для чтения</VisuallyHidden>);
    const style = getComputedStyle(screen.getByText('Подсказка для чтения'));
    expect(style.display).not.toBe('none');
    expect(style.visibility).not.toBe('hidden');
    expect(screen.getByText('Подсказка для чтения')).toBeVisible();
  });

  it('is a span by default and any element on request', () => {
    const { container, rerender } = render(<VisuallyHidden>Текст</VisuallyHidden>);
    expect(container.firstElementChild?.tagName).toBe('SPAN');
    rerender(<VisuallyHidden component="div">Текст</VisuallyHidden>);
    expect(container.firstElementChild?.tagName).toBe('DIV');
  });

  it('takes an id, for a description or a label another control points at', () => {
    render(
      <>
        <input aria-describedby="why" aria-label="Поле" />
        <VisuallyHidden id="why">Обязательное поле</VisuallyHidden>
      </>,
    );
    expect(screen.getByRole('textbox', { name: 'Поле' })).toHaveAccessibleDescription(
      'Обязательное поле',
    );
  });
});
