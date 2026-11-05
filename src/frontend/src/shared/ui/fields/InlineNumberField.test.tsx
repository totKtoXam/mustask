import { createRef, useState } from 'react';
import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { InlineNumberField } from './InlineNumberField';

// tech-015 design § 5.7 «InlineNumberField»: the *text* of a number, so a half-typed
// «3,» survives; the host parses; `inputMode` picks the keyboard.

function Harness({ onChange }: { onChange?: (text: string) => void }) {
  const [value, setValue] = useState('');
  return (
    <InlineNumberField
      label="Оценка"
      inputMode="decimal"
      placeholder="Не оценено"
      value={value}
      onChange={(next) => {
        setValue(next);
        onChange?.(next);
      }}
    />
  );
}

describe('tech-015 Scenario: InlineNumberField (design § 5.7)', () => {
  it('is a text input, never type=number: «3,» and «3.» are not lost', async () => {
    const user = userEvent.setup();
    const onChange = vi.fn();
    render(<Harness onChange={onChange} />);
    const input = screen.getByRole('textbox', { name: 'Оценка' });
    expect(input).toHaveAttribute('type', 'text');
    await user.type(input, '3,');
    expect(input).toHaveValue('3,');
    expect(onChange.mock.calls.map((call) => call[0])).toEqual(['3', '3,']);
  });

  it('hands the host the text as typed — any separator, no parsing, no trailing zero removed', async () => {
    const user = userEvent.setup();
    const onChange = vi.fn();
    render(<Harness onChange={onChange} />);
    await user.type(screen.getByRole('textbox', { name: 'Оценка' }), '2.50');
    expect(onChange).toHaveBeenLastCalledWith('2.50');
    expect(typeof onChange.mock.calls[0]?.[0]).toBe('string');
  });

  it.each(['decimal', 'numeric'] as const)('asks the device for the %s keyboard', (mode) => {
    render(<InlineNumberField label="Оценка" inputMode={mode} value="" onChange={() => {}} />);
    expect(screen.getByRole('textbox', { name: 'Оценка' })).toHaveAttribute('inputmode', mode);
  });

  it('keeps the inputMode when the host passes other input props', () => {
    render(
      <InlineNumberField
        label="Оценка"
        inputMode="numeric"
        value=""
        onChange={() => {}}
        inputProps={{ inputMode: 'text', 'data-testid': 'estimate' }}
      />,
    );
    expect(screen.getByTestId('estimate')).toHaveAttribute('inputmode', 'numeric');
  });

  it('shows the placeholder attribute the E2E reads, with no hover or focus', () => {
    render(<Harness />);
    expect(screen.getByRole('textbox', { name: 'Оценка' })).toHaveAttribute(
      'placeholder',
      'Не оценено',
    );
  });

  it('says what is wrong with the host’s error, and leaves the check to the host', async () => {
    const user = userEvent.setup();
    render(
      <InlineNumberField
        label="Оценка"
        inputMode="decimal"
        value="abc"
        onChange={() => {}}
        error="Введите число."
      />,
    );
    const input = screen.getByRole('textbox', { name: 'Оценка' });
    expect(input).toHaveAttribute('aria-invalid', 'true');
    await user.type(input, 'd');
    expect(input).toHaveValue('abc');
  });

  it('forwards the ref to the input', () => {
    const ref = createRef<HTMLInputElement>();
    render(
      <InlineNumberField
        ref={ref}
        label="Оценка"
        inputMode="decimal"
        value=""
        onChange={() => {}}
      />,
    );
    expect(ref.current).toBe(screen.getByRole('textbox', { name: 'Оценка' }));
  });
});
