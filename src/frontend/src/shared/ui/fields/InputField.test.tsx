import { createRef } from 'react';
import { describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen } from '@testing-library/react';
import { theme } from '../../../test/kitTesting';
import { InputField } from './InputField';

// tech-015 design § 5.7: the shared body of the four fields that are an `InputBase` — text,
// text area, number and date. Each kind's own file tests what only it has; this file is the
// body they share, asked directly: what the control is told, and what the host is told.

const input = () => screen.getByLabelText(/^Поле/) as HTMLInputElement;

describe('tech-015 Scenario: InputField (design § 5.7)', () => {
  it('is a text input named by its label, with the value it is given', () => {
    render(<InputField label="Поле" value="Письмо" />);
    expect(input()).toHaveAttribute('type', 'text');
    expect(input()).toHaveValue('Письмо');
    expect(input()).toHaveAccessibleName('Поле');
  });

  it('reports the value, not the event, and the input itself to onInputEvent', () => {
    const onChange = vi.fn();
    const onInputEvent = vi.fn();
    render(<InputField label="Поле" value="" onChange={onChange} onInputEvent={onInputEvent} />);
    fireEvent.change(input(), { target: { value: 'Да' } });
    expect(onChange).toHaveBeenCalledExactlyOnceWith('Да');
    expect(onInputEvent).toHaveBeenCalledExactlyOnceWith(input());
  });

  it('tells onInputEvent on a key-up too, and still calls the host’s own key-up handler', () => {
    const onInputEvent = vi.fn();
    const onKeyUp = vi.fn();
    render(
      <InputField label="Поле" value="" onInputEvent={onInputEvent} inputProps={{ onKeyUp }} />,
    );
    fireEvent.keyUp(input(), { key: 'a' });
    expect(onInputEvent).toHaveBeenCalledExactlyOnceWith(input());
    expect(onKeyUp).toHaveBeenCalledTimes(1);
  });

  it('needs neither handler', () => {
    render(<InputField label="Поле" value="" />);
    expect(() => {
      fireEvent.change(input(), { target: { value: 'x' } });
      fireEvent.keyUp(input(), { key: 'x' });
    }).not.toThrow();
  });

  it('passes maxLength, min and max to the input', () => {
    render(
      <InputField
        label="Поле"
        value=""
        type="date"
        maxLength={10}
        min="2000-01-01"
        max="2099-12-31"
      />,
    );
    expect(input()).toHaveAttribute('maxlength', '10');
    expect(input()).toHaveAttribute('min', '2000-01-01');
    expect(input()).toHaveAttribute('max', '2099-12-31');
  });

  it('is a text area, measured by MUI’s autosize, when it is multiline', () => {
    expect(theme.layout.field.textMinRows).toBe(3);
    const { container, rerender } = render(<InputField label="Поле" value="" multiline />);
    expect(input().tagName).toBe('TEXTAREA');
    expect(container.querySelector('textarea[aria-hidden="true"]')).not.toBeNull();
    rerender(<InputField label="Поле" value="" multiline minRows={5} />);
    expect(input().tagName).toBe('TEXTAREA');
  });

  it('sets data-empty on a date only: true while it is empty and has no error', () => {
    const { rerender } = render(<InputField label="Поле" value="" type="date" />);
    expect(input()).toHaveAttribute('data-empty', 'true');
    rerender(<InputField label="Поле" value="" type="date" error="Введите дату полностью." />);
    expect(input()).toHaveAttribute('data-empty', 'false');
    rerender(<InputField label="Поле" value="" />);
    expect(input()).not.toHaveAttribute('data-empty');
  });

  it('wires the description, invalidity, requirement and busy state to the control', () => {
    render(<InputField label="Поле" value="" hint="Подсказка" required busy />);
    expect(input()).toHaveAttribute('aria-required', 'true');
    expect(input()).toHaveAttribute('aria-busy', 'true');
    expect(input()).toHaveAttribute('aria-readonly', 'true');
    expect(input()).not.toHaveAttribute('required');
    expect(input()).toHaveAccessibleDescription('Подсказка');
  });

  it('forwards the ref to the input', () => {
    const ref = createRef<HTMLInputElement | HTMLTextAreaElement>();
    render(<InputField ref={ref} label="Поле" value="" />);
    expect(ref.current).toBe(input());
  });
});
