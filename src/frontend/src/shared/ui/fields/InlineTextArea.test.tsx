import { createRef, useState } from 'react';
import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { px, theme, valueFontSize } from '../../../test/kitTesting';
import { InlineTextArea } from './InlineTextArea';

// tech-015 design § 5.7 «InlineTextArea»: a multi-line text whose text starts at
// `textAreaInset`; with a hidden label it is a text under a head of its own name.

const root = () => document.querySelector('[data-field]') as HTMLElement;
const box = () => root().querySelector('[data-field-box]') as HTMLElement;

describe('tech-015 Scenario: InlineTextArea (design § 5.7)', () => {
  it('is a textarea named by its label, and reports the value', async () => {
    const user = userEvent.setup();
    const onChange = vi.fn();
    function Harness() {
      const [value, setValue] = useState('');
      return (
        <InlineTextArea
          label="Критерии приёмки"
          value={value}
          onChange={(next) => {
            setValue(next);
            onChange(next);
          }}
        />
      );
    }
    render(<Harness />);
    const area = screen.getByRole('textbox', { name: 'Критерии приёмки' });
    expect(area.tagName).toBe('TEXTAREA');
    await user.type(area, 'Раз{Enter}Два');
    expect(onChange).toHaveBeenLastCalledWith('Раз\nДва');
    expect(area).toHaveValue('Раз\nДва');
  });

  it('is the body size only, and its type is the body’s', () => {
    // @ts-expect-error — a text area has no `size`: the title and table sizes are one line's.
    render(<InlineTextArea label="Описание" value="Текст" onChange={() => {}} size="title" />);
    expect(valueFontSize(root())).toBe(14);
  });

  it('keeps `textMinRows` as the least rows by default', () => {
    expect(theme.layout.field.textMinRows).toBe(3);
    render(<InlineTextArea label="Описание" value="" onChange={() => {}} />);
    const area = screen.getByRole('textbox', { name: 'Описание' });
    // MUI's autosize reads `minRows`; the visible area keeps one `rows` of its own.
    expect(area).toBeInTheDocument();
    expect(box()).toContainElement(area);
  });

  it('takes the host’s minRows', () => {
    const { container } = render(
      <InlineTextArea label="Описание" value="" onChange={() => {}} minRows={6} />,
    );
    expect(container.querySelector('textarea[aria-hidden="true"]')).not.toBeNull();
  });

  describe('Scenario: its box', () => {
    it('begins at the top: the text stands `padY` below the box’s edge and `textAreaInset` in', () => {
      render(<InlineTextArea label="Описание" value="Текст" onChange={() => {}} />);
      const style = getComputedStyle(box());
      expect(style.alignItems).toBe('flex-start');
      expect(px(style.paddingTop)).toBe(theme.layout.field.padY);
      expect(px(style.paddingBottom)).toBe(theme.layout.field.padY);
      expect(px(style.paddingLeft)).toBe(theme.layout.field.textAreaInset);
      expect(px(style.minHeight)).toBe(theme.layout.field.box);
    });

    it('is a little further in than a single line: `textAreaInset` 8 against `textInset` 6', () => {
      expect(theme.layout.field.textAreaInset).toBeGreaterThan(theme.layout.field.textInset);
      expect(theme.layout.field.textAreaInset).toBe(8);
    });
  });

  describe('Scenario: a text under a head of its own name — labelPlacement="hidden"', () => {
    it('draws no second label: the one label element is hidden and names the textbox', () => {
      render(
        <InlineTextArea
          label="Описание"
          labelPlacement="hidden"
          value="Текст"
          onChange={() => {}}
        />,
      );
      const labels = root().querySelectorAll('label');
      expect(labels).toHaveLength(1);
      expect(getComputedStyle(labels[0] as HTMLElement).position).toBe('absolute');
      expect(screen.getByRole('textbox', { name: 'Описание' })).toBeInTheDocument();
      expect(root().querySelector('[data-field-label-row]')).toBeNull();
    });

    it('leaves `textAreaTop` above the box, where a top label’s line would have been', () => {
      render(
        <InlineTextArea
          label="Описание"
          labelPlacement="hidden"
          value="Текст"
          onChange={() => {}}
        />,
      );
      expect(px(getComputedStyle(root()).paddingTop)).toBe(theme.layout.field.textAreaTop);
      expect(theme.layout.field.textAreaTop).toBe(15);
    });

    it('makes its block `textAreaTop` + the box + `gap`', () => {
      render(
        <InlineTextArea
          label="Описание"
          labelPlacement="hidden"
          value="Текст"
          onChange={() => {}}
        />,
      );
      expect(px(getComputedStyle(root()).paddingTop)).toBe(15);
      expect(px(getComputedStyle(box()).minHeight)).toBe(theme.layout.field.box);
      expect(px(getComputedStyle(box()).marginBottom)).toBe(theme.layout.field.gap);
    });

    it('puts the first line’s cap top 30 below the head’s rule, with the section’s rule and body top', () => {
      const { field, section, frame } = theme.layout;
      // rule + bodyTop + textAreaTop + padY + the cap's offset in a 20 px line.
      expect(frame.thin + section.bodyTop + field.textAreaTop + field.padY + 6).toBe(30);
    });

    it('draws a top label above the box, and leaves no `textAreaTop`, in every other case', () => {
      render(<InlineTextArea label="Критерии" value="" onChange={() => {}} />);
      expect(parseFloat(getComputedStyle(root()).paddingTop || '0')).toBe(0);
      expect(root().querySelector('[data-field-label-row]')).not.toBeNull();
    });

    it('a text area with no head of its own name takes a visible top label, not `hidden`', () => {
      render(<InlineTextArea label="Критерии" value="" onChange={() => {}} />);
      const label = root().querySelector('label') as HTMLElement;
      expect(getComputedStyle(label).position).not.toBe('absolute');
      expect(px(getComputedStyle(label).fontSize)).toBe(12);
    });
  });

  it('forwards the ref to the textarea', () => {
    const ref = createRef<HTMLTextAreaElement>();
    render(<InlineTextArea ref={ref} label="Описание" value="" onChange={() => {}} />);
    expect(ref.current).toBe(screen.getByRole('textbox', { name: 'Описание' }));
  });
});
