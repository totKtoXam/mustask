import { describe, expect, it } from 'vitest';
import { render, screen } from '@testing-library/react';
import { px, rgb, theme } from '../../../test/kitTesting';
import { PersonLabel } from './PersonLabel';

// tech-015 design § 5.4 «PersonLabel»: the avatar and the name, or the thin person
// glyph (16 px in the 24 px slot) and «Не назначен» in text.secondary.

describe('tech-015 Scenario: PersonLabel (design § 5.4)', () => {
  describe('Scenario: someone is set', () => {
    it('draws the avatar and the name as text', () => {
      const { container } = render(<PersonLabel name="Иван Петров" />);
      expect(screen.getByText('Иван Петров')).toBeInTheDocument();
      expect(container.querySelector('.MuiAvatar-root')).toHaveTextContent(/^ИП$/);
      expect(container.querySelector('[data-testid="ThinPersonIcon"]')).toBeNull();
    });

    it('is named by the person’s name alone — the avatar is hidden', () => {
      const { container } = render(<PersonLabel name="Иван Петров" />);
      expect(container).toHaveTextContent(/^ИПИван Петров$/);
      expect(container.querySelector('.MuiAvatar-root')).toHaveAttribute('aria-hidden', 'true');
    });

    it('is the 24 px avatar by default and the 32 px one for `menu`', () => {
      const { container, rerender } = render(<PersonLabel name="Иван Петров" />);
      const avatar = () => container.querySelector('.MuiAvatar-root') as HTMLElement;
      expect(px(getComputedStyle(avatar()).width)).toBe(theme.layout.mark.avatarInline);
      rerender(<PersonLabel name="Иван Петров" size="menu" />);
      expect(px(getComputedStyle(avatar()).width)).toBe(theme.layout.avatarSize);
    });
  });

  describe('Scenario: nobody', () => {
    it('says «Не назначен» by default, in text.secondary', () => {
      render(<PersonLabel name={null} />);
      const text = screen.getByText('Не назначен');
      expect(getComputedStyle(text).color).toBe(rgb(theme.palette.text.secondary));
    });

    it('draws the thin person glyph at layout.mark.personGlyph, centred in the avatar’s slot', () => {
      const { container } = render(<PersonLabel name={null} />);
      const glyph = container.querySelector('[data-testid="ThinPersonIcon"]') as SVGElement;
      expect(glyph).not.toBeNull();
      expect(px(getComputedStyle(glyph).fontSize)).toBe(theme.layout.mark.personGlyph);
      const slot = glyph.parentElement as HTMLElement;
      const style = getComputedStyle(slot);
      expect(slot).toHaveAttribute('aria-hidden', 'true');
      expect(px(style.width)).toBe(theme.layout.mark.avatarInline);
      expect(px(style.height)).toBe(theme.layout.mark.avatarInline);
      expect(style.justifyContent).toBe('center');
      expect(style.alignItems).toBe('center');
      expect(style.color).toBe(rgb(theme.palette.text.secondary));
      expect(container.querySelector('.MuiAvatar-root')).toBeNull();
    });

    it('takes the host’s words for an id it cannot name', () => {
      render(<PersonLabel name={null} emptyLabel="Неизвестный пользователь" />);
      expect(screen.getByText('Неизвестный пользователь')).toBeInTheDocument();
      expect(screen.queryByText('Не назначен')).toBeNull();
    });

    it('keeps the slot as wide as the avatar so the text lines up either way', () => {
      const { container, rerender } = render(<PersonLabel name={null} size="menu" />);
      const glyph = container.querySelector('[data-testid="ThinPersonIcon"]') as SVGElement;
      expect(px(getComputedStyle(glyph.parentElement as HTMLElement).width)).toBe(
        theme.layout.avatarSize,
      );
      rerender(<PersonLabel name="Иван Петров" size="menu" />);
      expect(
        px(getComputedStyle(container.querySelector('.MuiAvatar-root') as HTMLElement).width),
      ).toBe(theme.layout.avatarSize);
    });
  });
});
