import { createRef, useState } from 'react';
import { describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen } from '@testing-library/react';
import { rulesFor } from '../../../test/kitTesting';
import { InlineDateField } from './InlineDateField';

// tech-015 design § 5.7 «InlineDateField», «The date field's mask»: the browser's own
// control, a value `YYYY-MM-DD`, `min`/`max`, `onIncomplete` for a date typed in part,
// and `data-empty`, which tells the CSS when the empty mask may hide.

const input = () => screen.getByLabelText(/^Срок/) as HTMLInputElement;
const root = () => document.querySelector('[data-field]') as HTMLElement;

/** jsdom's `validity.badInput` is always false: say a date was typed only in part. */
function typedInPart(element: HTMLInputElement, partial = true) {
  Object.defineProperty(element, 'validity', {
    value: { badInput: partial },
    configurable: true,
  });
}

/** A host that keeps the date and an «incomplete» flag, as a form does. */
function Host({ initial, onIncomplete }: { initial: string; onIncomplete: () => void }) {
  const [value, setValue] = useState(initial);
  return (
    <InlineDateField label="Срок" value={value} onChange={setValue} onIncomplete={onIncomplete} />
  );
}

describe('tech-015 Scenario: InlineDateField (design § 5.7)', () => {
  it('is the browser’s own date input, named by its label', () => {
    render(<InlineDateField label="Срок" value="" onChange={() => {}} />);
    expect(input()).toHaveAttribute('type', 'date');
    expect(input()).toHaveAccessibleName('Срок');
  });

  it('shows the value and takes min and max', () => {
    render(
      <InlineDateField
        label="Срок"
        value="2026-10-12"
        onChange={() => {}}
        min="2000-01-01"
        max="2099-12-31"
      />,
    );
    expect(input()).toHaveValue('2026-10-12');
    expect(input()).toHaveAttribute('min', '2000-01-01');
    expect(input()).toHaveAttribute('max', '2099-12-31');
  });

  describe('Scenario: the value and a date typed in part', () => {
    it('reports the value as YYYY-MM-DD, and does not report the same value twice', () => {
      const onChange = vi.fn();
      const { rerender } = render(<InlineDateField label="Срок" value="" onChange={onChange} />);
      fireEvent.change(input(), { target: { value: '2026-10-12' } });
      expect(onChange).toHaveBeenCalledExactlyOnceWith('2026-10-12');
      rerender(<InlineDateField label="Срок" value="2026-10-12" onChange={onChange} />);
      fireEvent.keyUp(input());
      expect(onChange).toHaveBeenCalledTimes(1);
    });

    it('reports an empty value, then onIncomplete, when a filled date becomes partial', () => {
      const calls: string[] = [];
      // A full date being edited into a partial one: the control's value goes to ''.
      render(
        <InlineDateField
          label="Срок"
          value="2026-10-12"
          onChange={(next) => calls.push(`change:${next}`)}
          onIncomplete={() => calls.push('incomplete')}
        />,
      );
      typedInPart(input());
      fireEvent.change(input(), { target: { value: '' } });
      expect(calls).toEqual(['change:', 'incomplete']);
    });

    it('does not report a value for a partial entry that was empty already', () => {
      const onChange = vi.fn();
      const onIncomplete = vi.fn();
      render(
        <InlineDateField label="Срок" value="" onChange={onChange} onIncomplete={onIncomplete} />,
      );
      typedInPart(input());
      fireEvent.keyUp(input(), { key: '1' });
      expect(onChange).not.toHaveBeenCalled();
      expect(onIncomplete).toHaveBeenCalledTimes(1);
    });

    it('keeps the control empty after a partial edit of a filled date, not the old date back (R1-1)', () => {
      render(<Host initial="2026-10-12" onIncomplete={() => {}} />);
      typedInPart(input());
      fireEvent.change(input(), { target: { value: '' } });
      expect(input()).toHaveValue('');
    });

    it('reports the same date again when the user completes it — an «incomplete» flag can clear (R1-1)', () => {
      const onIncomplete = vi.fn();
      const reported: string[] = [];
      function Tracked() {
        const [value, setValue] = useState('2026-10-12');
        return (
          <InlineDateField
            label="Срок"
            value={value}
            onChange={(next) => {
              reported.push(next);
              setValue(next);
            }}
            onIncomplete={onIncomplete}
          />
        );
      }
      render(<Tracked />);
      typedInPart(input());
      fireEvent.change(input(), { target: { value: '' } });
      expect(reported).toEqual(['']);
      typedInPart(input(), false);
      fireEvent.change(input(), { target: { value: '2026-10-12' } });
      expect(reported).toEqual(['', '2026-10-12']);
      expect(input()).toHaveValue('2026-10-12');
      expect(onIncomplete).toHaveBeenCalledTimes(1);
    });

    it('calls onIncomplete on a key-up too — the browser may announce no change for a partial date', () => {
      const onIncomplete = vi.fn();
      render(
        <InlineDateField label="Срок" value="" onChange={() => {}} onIncomplete={onIncomplete} />,
      );
      typedInPart(input());
      fireEvent.keyUp(input(), { key: '1' });
      expect(onIncomplete).toHaveBeenCalledTimes(1);
    });

    it('calls the host’s own key-up handler as well', () => {
      const onKeyUp = vi.fn();
      render(
        <InlineDateField label="Срок" value="" onChange={() => {}} inputProps={{ onKeyUp }} />,
      );
      fireEvent.keyUp(input(), { key: '1' });
      expect(onKeyUp).toHaveBeenCalledTimes(1);
    });

    it('needs no onIncomplete: a partial date without one is simply ignored', () => {
      render(<InlineDateField label="Срок" value="" onChange={() => {}} />);
      typedInPart(input());
      expect(() => fireEvent.keyUp(input(), { key: '1' })).not.toThrow();
    });
  });

  describe('Scenario: data-empty tells the CSS when the empty mask may hide', () => {
    it('is true for an empty value with no error', () => {
      render(<InlineDateField label="Срок" value="" onChange={() => {}} />);
      expect(input()).toHaveAttribute('data-empty', 'true');
    });

    it('is false for a filled value', () => {
      render(<InlineDateField label="Срок" value="2026-10-12" onChange={() => {}} />);
      expect(input()).toHaveAttribute('data-empty', 'false');
    });

    it('is false for an empty value in error — the user must see what they typed', () => {
      render(
        <InlineDateField
          label="Срок"
          value=""
          onChange={() => {}}
          error="Введите дату полностью."
        />,
      );
      expect(input()).toHaveAttribute('data-empty', 'false');
    });

    it('follows the value: filled, then emptied', () => {
      const { rerender } = render(<InlineDateField label="Срок" value="" onChange={() => {}} />);
      rerender(<InlineDateField label="Срок" value="2026-10-12" onChange={() => {}} />);
      expect(input()).toHaveAttribute('data-empty', 'false');
      rerender(<InlineDateField label="Срок" value="" onChange={() => {}} />);
      expect(input()).toHaveAttribute('data-empty', 'true');
    });
  });

  describe('Scenario: the mask and the button hide until hover or focus (Chromium-family)', () => {
    const EDIT = '::-webkit-datetime-edit';
    const BUTTON = '::-webkit-calendar-picker-indicator';

    it('hides the empty mask at rest — kept in the layout, never display: none', () => {
      render(<InlineDateField label="Срок" value="" onChange={() => {}} />);
      const rest = rulesFor(root(), EDIT).filter(
        (rule) => rule.media === '' && !rule.selector.includes(':hover'),
      );
      expect(rest.length).toBeGreaterThan(0);
      for (const rule of rest) {
        expect(rule.selector).toContain('[data-empty="true"]:not(:focus)');
        expect(rule.declarations.opacity).toBe('0');
        expect(rule.declarations.display).toBeUndefined();
      }
    });

    it('hides the calendar button at rest and keeps it clickable', () => {
      render(<InlineDateField label="Срок" value="2026-10-12" onChange={() => {}} />);
      const rest = rulesFor(root(), BUTTON).find(
        (rule) =>
          rule.media === '' &&
          !rule.selector.includes(':hover') &&
          !rule.selector.includes('focus-within'),
      );
      expect(rest?.declarations.opacity).toBe('0');
      expect(rest?.declarations.display).toBeUndefined();
      expect(rest?.declarations['pointer-events']).toBeUndefined();
    });

    it('hides only the mask of an *empty* date: a filled date shows its text', () => {
      render(<InlineDateField label="Срок" value="2026-10-12" onChange={() => {}} />);
      // Every rule that hides the mask is tied to [data-empty="true"]; the input says "false".
      for (const rule of rulesFor(root(), EDIT).filter(
        (candidate) => candidate.declarations.opacity === '0',
      )) {
        expect(rule.selector).toContain('[data-empty="true"]');
      }
      expect(input()).toHaveAttribute('data-empty', 'false');
    });

    it('shows both on hover, the button on focus, and both with no hover', () => {
      render(<InlineDateField label="Срок" value="" onChange={() => {}} />);
      const hover = rulesFor(root(), ':hover');
      expect(
        hover.some((rule) => rule.selector.includes(EDIT) && rule.declarations.opacity === '1'),
      ).toBe(true);
      expect(
        hover.some((rule) => rule.selector.includes(BUTTON) && rule.declarations.opacity === '1'),
      ).toBe(true);
      const focus = rulesFor(root(), ':focus-within');
      expect(
        focus.some((rule) => rule.selector.includes(BUTTON) && rule.declarations.opacity === '1'),
      ).toBe(true);
      const touch = rulesFor(root(), 'input[type="date"]').filter((rule) =>
        rule.media.includes('hover: none'),
      );
      expect(touch.some((rule) => rule.selector.includes(EDIT))).toBe(true);
      expect(touch.some((rule) => rule.selector.includes(BUTTON))).toBe(true);
    });

    it('hides nothing in a framed field: a blank form shows its date control', () => {
      render(<InlineDateField label="Срок" value="" onChange={() => {}} frame="always" />);
      expect(rulesFor(root(), EDIT)).toHaveLength(0);
      expect(rulesFor(root(), BUTTON)).toHaveLength(0);
    });
  });

  it('forwards the ref to the input', () => {
    const ref = createRef<HTMLInputElement>();
    render(<InlineDateField ref={ref} label="Срок" value="" onChange={() => {}} />);
    expect(ref.current).toBe(input());
  });
});
