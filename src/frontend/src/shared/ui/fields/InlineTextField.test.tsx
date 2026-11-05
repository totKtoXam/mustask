import { createRef, useState } from 'react';
import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { px, rulesFor, theme, valueFontSize } from '../../../test/kitTesting';
import { InlineTextField } from './InlineTextField';

// tech-015 design § 5.7 «InlineTextField»: one line; the shared anatomy is in
// FieldFrame.shared.test.tsx. Here: the value, the three sizes, the ellipsis, the ref.

function Controlled({
  initial = '',
  onChange,
}: {
  initial?: string;
  onChange?: (v: string) => void;
}) {
  const [value, setValue] = useState(initial);
  return (
    <InlineTextField
      label="Заголовок"
      value={value}
      onChange={(next) => {
        setValue(next);
        onChange?.(next);
      }}
    />
  );
}

describe('tech-015 Scenario: InlineTextField (design § 5.7)', () => {
  it('reports the value, not the event', async () => {
    const user = userEvent.setup();
    const onChange = vi.fn();
    render(<Controlled onChange={onChange} />);
    await user.type(screen.getByRole('textbox', { name: 'Заголовок' }), 'Да');
    expect(onChange.mock.calls.map((call) => call[0])).toEqual(['Д', 'Да']);
    expect(screen.getByRole('textbox', { name: 'Заголовок' })).toHaveValue('Да');
  });

  it('is controlled: the value is the prop’s', () => {
    const { rerender } = render(
      <InlineTextField label="Заголовок" value="Раз" onChange={() => {}} />,
    );
    expect(screen.getByRole('textbox', { name: 'Заголовок' })).toHaveValue('Раз');
    rerender(<InlineTextField label="Заголовок" value="Два" onChange={() => {}} />);
    expect(screen.getByRole('textbox', { name: 'Заголовок' })).toHaveValue('Два');
  });

  it('is a single-line text input with the host’s maxLength', () => {
    render(<InlineTextField label="Код" value="" onChange={() => {}} maxLength={12} />);
    const input = screen.getByRole('textbox', { name: 'Код' });
    expect(input.tagName).toBe('INPUT');
    expect(input).toHaveAttribute('type', 'text');
    expect(input).toHaveAttribute('maxlength', '12');
  });

  describe('Scenario: sizes', () => {
    it.each([
      ['body', 14, theme.layout.field.box],
      ['table', 13, theme.layout.field.box],
      ['title', 17, theme.layout.field.titleBox],
    ] as const)('size %s sets the text at %i px in a box %i px high', (size, fontSize, height) => {
      render(<InlineTextField label="Заголовок" value="Текст" onChange={() => {}} size={size} />);
      expect(valueFontSize(document.querySelector('[data-field]') as HTMLElement)).toBe(fontSize);
      const box = document.querySelector('[data-field-box]') as HTMLElement;
      expect(px(getComputedStyle(box).height)).toBe(height);
    });

    it('is the body size by default', () => {
      render(<InlineTextField label="Заголовок" value="Текст" onChange={() => {}} />);
      expect(valueFontSize(document.querySelector('[data-field]') as HTMLElement)).toBe(14);
    });

    it('keeps the title’s 24 px line inside its 32 px box: line + 2 × padY', () => {
      expect(theme.layout.field.titleBox).toBe(24 + 2 * theme.layout.field.padY);
      expect(theme.layout.field.box).toBe(20 + 2 * theme.layout.field.padY);
    });
  });

  describe('Scenario: a value that does not fit', () => {
    it('shows an ellipsis at rest, and scrolls as an input does while the field has focus', () => {
      render(
        <InlineTextField
          label="Заголовок"
          value="Описать end-to-end сценарии использования текущей системы"
          onChange={() => {}}
        />,
      );
      const input = screen.getByRole('textbox', { name: 'Заголовок' });
      expect(getComputedStyle(input).textOverflow).toBe('ellipsis');
      const focused = rulesFor(
        document.querySelector('[data-field]') as HTMLElement,
        'input:focus',
      );
      expect(focused.some((rule) => rule.declarations['text-overflow'] === 'clip')).toBe(true);
    });
  });

  describe('Scenario: the host drives the input', () => {
    it('forwards the ref to the input, so the host can focus it', () => {
      const ref = createRef<HTMLInputElement>();
      render(<InlineTextField ref={ref} label="Заголовок" value="" onChange={() => {}} />);
      expect(ref.current).toBe(screen.getByRole('textbox', { name: 'Заголовок' }));
      ref.current?.focus();
      expect(screen.getByRole('textbox')).toHaveFocus();
    });

    it('passes the keyboard hints, handlers, aria-* and data-* to the input', async () => {
      const onKeyDown = vi.fn();
      render(
        <InlineTextField
          label="Заголовок"
          value=""
          onChange={() => {}}
          inputProps={{
            inputMode: 'search',
            autoComplete: 'off',
            enterKeyHint: 'done',
            'aria-keyshortcuts': 'Alt+T',
            'data-testid': 'title-input',
            onKeyDown,
          }}
        />,
      );
      const input = screen.getByTestId('title-input');
      expect(input).toBe(screen.getByRole('textbox', { name: 'Заголовок' }));
      expect(input).toHaveAttribute('inputmode', 'search');
      expect(input).toHaveAttribute('autocomplete', 'off');
      expect(input).toHaveAttribute('enterkeyhint', 'done');
      expect(input).toHaveAttribute('aria-keyshortcuts', 'Alt+T');
      await userEvent.type(input, '{Enter}');
      expect(onKeyDown).toHaveBeenCalled();
    });

    it('does not let the host override what the kit sets: the description and the error', () => {
      render(
        <InlineTextField
          label="Заголовок"
          value=""
          onChange={() => {}}
          error="Обязательное поле"
          inputProps={{ 'aria-describedby': 'host', 'aria-invalid': false }}
        />,
      );
      const input = screen.getByRole('textbox', { name: 'Заголовок' });
      expect(input).toHaveAttribute('aria-invalid', 'true');
      expect(input).toHaveAccessibleDescription('Обязательное поле');
    });
  });

  it('submits a form on Enter, like any input: nothing in the kit intercepts it', async () => {
    const onSubmit = vi.fn((event: { preventDefault: () => void }) => event.preventDefault());
    render(
      <form onSubmit={onSubmit}>
        <InlineTextField label="Заголовок" value="Текст" onChange={() => {}} />
        <button type="submit">Отправить</button>
      </form>,
    );
    await userEvent.type(screen.getByRole('textbox', { name: 'Заголовок' }), '{Enter}');
    expect(onSubmit).toHaveBeenCalledTimes(1);
  });
});
