import { useState } from 'react';
import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { px, rgb, theme } from '../../../test/kitTesting';
import { PersonField, type PersonFieldProps } from './PersonField';

// tech-015 design § 5.7 «PersonField»: an assignee picker whose start slot is 24 px
// wide — a UserAvatar once someone is set, the thin person glyph (16 px) while nobody is.

const PEOPLE = ['Иван Петров', 'Мария Иванова'];

function Harness({
  initial = null,
  onChange,
  ...rest
}: Partial<PersonFieldProps<string>> & { initial?: string | null }) {
  const [value, setValue] = useState<string | null>(initial);
  return (
    <PersonField<string>
      label="Исполнитель"
      labelPlacement="hidden"
      emptyText="Не назначен"
      value={value}
      options={PEOPLE}
      getOptionLabel={(name) => name}
      onChange={(next) => {
        setValue(next);
        onChange?.(next);
      }}
      {...rest}
    />
  );
}

const input = () => screen.getByRole('combobox', { name: 'Исполнитель' }) as HTMLInputElement;
const slot = () => document.querySelector('[data-field-adornment]') as HTMLElement;

describe('tech-015 Scenario: PersonField (design § 5.7)', () => {
  describe('Scenario: nobody is assigned', () => {
    it('draws the thin person glyph at personGlyph, text.secondary, centred in the avatar’s slot', () => {
      render(<Harness />);
      const glyph = slot().querySelector('[data-testid="ThinPersonIcon"]') as SVGElement;
      expect(glyph).not.toBeNull();
      const cell = glyph.parentElement as HTMLElement;
      const style = getComputedStyle(cell);
      expect(cell).toHaveAttribute('aria-hidden', 'true');
      expect(px(style.width)).toBe(theme.layout.mark.avatarInline);
      expect(px(style.height)).toBe(theme.layout.mark.avatarInline);
      expect(style.justifyContent).toBe('center');
      expect(style.color).toBe(rgb(theme.palette.text.secondary));
      expect(px(getComputedStyle(glyph).fontSize)).toBe(theme.layout.mark.personGlyph);
      expect(slot().querySelector('.MuiAvatar-root')).toBeNull();
    });

    it('reads the host’s emptyText at rest, in text.secondary — and the input is empty', () => {
      render(<Harness />);
      const text = document.querySelector('[data-empty-text]') as HTMLElement;
      expect(text).toHaveTextContent('Не назначен');
      expect(getComputedStyle(text).color).toBe(rgb(theme.palette.text.secondary));
      expect(input()).toHaveValue('');
    });
  });

  describe('Scenario: someone is assigned', () => {
    it('draws a 24 px UserAvatar with the initials, and no glyph', () => {
      render(<Harness initial="Иван Петров" />);
      const avatar = slot().querySelector('.MuiAvatar-root') as HTMLElement;
      expect(avatar).toHaveTextContent(/^ИП$/);
      expect(avatar).toHaveAttribute('aria-hidden', 'true');
      expect(px(getComputedStyle(avatar).width)).toBe(theme.layout.mark.avatarInline);
      expect(slot().querySelector('[data-testid="ThinPersonIcon"]')).toBeNull();
      expect(document.querySelector('[data-empty-text]')).toBeNull();
      expect(input()).toHaveValue('Иван Петров');
    });

    it('keeps the name as the control’s value, not as its name', () => {
      render(<Harness initial="Иван Петров" />);
      expect(input()).toHaveAccessibleName('Исполнитель');
    });
  });

  describe('Scenario: the slot and the text', () => {
    it('stands the slot inside the box, in either placement, before the text', () => {
      const { unmount } = render(<Harness />);
      const box = document.querySelector('[data-field-box]') as HTMLElement;
      expect(box).toContainElement(slot());
      expect(slot().getAttribute('data-frame-part')).toBeNull();
      unmount();
      render(<Harness labelPlacement="start" />);
      expect(document.querySelector('[data-field-box]')).toContainElement(slot());
      expect(document.querySelector('[data-frame-part="lead"]')).toBeNull();
    });

    it('puts the name’s text 36 from the box’s edge: textInset 6 + the 24 px slot + nameGap 6', () => {
      render(<Harness />);
      const { field, mark } = theme.layout;
      const box = getComputedStyle(document.querySelector('[data-field-box]') as HTMLElement);
      const adornment = getComputedStyle(slot());
      expect(px(box.paddingLeft) + mark.avatarInline + px(adornment.marginRight)).toBe(36);
      expect(field.textInset + mark.avatarInline + mark.nameGap).toBe(36);
    });

    it('puts it at 54 from the card’s edge in the header: headerInset 18 + 36', () => {
      expect(theme.layout.card.headerInset + 36).toBe(54);
      expect(
        theme.layout.card.headerInset +
          theme.layout.field.textInset +
          theme.layout.mark.avatarInline / 2,
      ).toBe(36);
    });
  });

  describe('Scenario: choosing', () => {
    it('shows the avatar once a person is chosen, and the glyph again when none is', async () => {
      const user = userEvent.setup();
      const onChange = vi.fn();
      render(<Harness onChange={onChange} clearable />);
      await user.click(input());
      await user.click(await screen.findByRole('option', { name: 'Мария Иванова' }));
      expect(onChange).toHaveBeenCalledWith('Мария Иванова');
      expect(slot().querySelector('.MuiAvatar-root')).toHaveTextContent(/^МИ$/);
      await user.click(document.querySelector('.MuiAutocomplete-clearIndicator') as HTMLElement);
      expect(onChange).toHaveBeenLastCalledWith(null);
      expect(slot().querySelector('[data-testid="ThinPersonIcon"]')).not.toBeNull();
    });
  });
});
