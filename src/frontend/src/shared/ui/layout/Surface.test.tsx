import { createRef } from 'react';
import { describe, expect, it } from 'vitest';
import { render, screen } from '@testing-library/react';
import { rgb, theme } from '../../../test/kitTesting';
import { Surface } from './Surface';

// tech-015 design § 5.8 «Surface»: a card with the soft glow, a flat square band, three
// paddings, and nothing of MUI's elevation or outline.

function surface() {
  return screen.getByTestId('surface');
}

describe('tech-015 Scenario: Surface (design § 5.8)', () => {
  describe('Scenario: the card is white with the reference’s soft glow', () => {
    it('is the paper colour with theme.cardShadow, the theme’s corner and no outline', () => {
      render(
        <Surface variant="card" data-testid="surface">
          Содержимое
        </Surface>,
      );
      const style = getComputedStyle(surface());
      expect(style.backgroundColor).toBe(rgb(theme.palette.background.paper));
      // jsdom drops the space after a comma; the shadow itself is the token's.
      expect(style.boxShadow.replace(/\s+/g, '')).toBe(theme.cardShadow.replace(/\s+/g, ''));
      expect(style.borderRadius).toBe(`${theme.shape.borderRadius}px`);
      expect(surface()).not.toHaveClass('MuiPaper-outlined');
      expect(surface()).toHaveClass('MuiPaper-elevation0');
    });
  });

  describe('Scenario: the band is the application background, flat and square', () => {
    it('has no shadow and no corner', () => {
      render(<Surface variant="band" data-testid="surface" />);
      const style = getComputedStyle(surface());
      expect(style.backgroundColor).toBe(rgb(theme.palette.background.default));
      expect(style.boxShadow).toBe('none');
      expect(parseFloat(style.borderRadius)).toBe(0);
    });
  });

  describe('Scenario: three paddings', () => {
    it('is no padding by default: the content’s own', () => {
      render(<Surface variant="card" data-testid="surface" />);
      expect(getComputedStyle(surface()).paddingTop).toBe('0px');
    });

    it('dense is the filter band’s 12 × 16', () => {
      render(<Surface variant="card" padding="dense" data-testid="surface" />);
      const style = getComputedStyle(surface());
      expect([
        style.paddingTop,
        style.paddingRight,
        style.paddingBottom,
        style.paddingLeft,
      ]).toEqual(['12px', '16px', '12px', '16px']);
    });

    it('form is the form column’s 24 on every side', () => {
      render(<Surface variant="card" padding="form" data-testid="surface" />);
      const style = getComputedStyle(surface());
      expect([
        style.paddingTop,
        style.paddingRight,
        style.paddingBottom,
        style.paddingLeft,
      ]).toEqual(['24px', '24px', '24px', '24px']);
    });
  });

  describe('Scenario: the element it draws', () => {
    it('is a div by default', () => {
      render(<Surface variant="card" data-testid="surface" />);
      expect(surface().tagName).toBe('DIV');
    });

    it('draws the element it is told to (a form, a section) and forwards its attributes and ref', () => {
      const ref = createRef<HTMLDivElement>();
      render(
        <Surface
          variant="card"
          component="form"
          aria-label="Фильтр"
          data-testid="surface"
          ref={ref}
        />,
      );
      expect(surface().tagName).toBe('FORM');
      expect(screen.getByRole('form', { name: 'Фильтр' })).toBe(surface());
      expect(ref.current).toBe(surface());
    });
  });

  describe('Scenario: a surface is never the card container', () => {
    it('declares no container — only a shell does, so the header and the band have one parent to read', () => {
      render(<Surface variant="card" data-testid="surface" />);
      expect(getComputedStyle(surface()).containerType).toBe('');
    });
  });
});
