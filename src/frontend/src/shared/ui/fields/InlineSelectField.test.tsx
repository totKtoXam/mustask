import { createRef, useState } from 'react';
import { describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { reaches, rulesFor, theme } from '../../../test/kitTesting';
import { CategoryMarker } from '../WorkTrackingMarks';
import { InlineSelectField, type SelectOption } from './InlineSelectField';

// tech-015 design § 5.7 «InlineSelectField»: a choice among a few options; its name is
// its label only; an optional one has an `emptyLabel`; the caret shows with the frame.

const STATES: SelectOption<'new' | 'work' | 'done'>[] = [
  { value: 'new', label: 'Новая' },
  { value: 'work', label: 'В работе' },
  { value: 'done', label: 'Готово' },
];

function Harness({
  initial = 'work',
  onChange,
  options = STATES,
  ...rest
}: {
  initial?: 'new' | 'work' | 'done' | '';
  onChange?: (value: string) => void;
  options?: SelectOption<'new' | 'work' | 'done'>[];
} & Partial<React.ComponentProps<typeof InlineSelectField<'new' | 'work' | 'done'>>>) {
  const [value, setValue] = useState<'new' | 'work' | 'done' | ''>(initial);
  return (
    <InlineSelectField
      label="Статус"
      value={value}
      options={options}
      onChange={(next) => {
        setValue(next);
        onChange?.(next);
      }}
      {...rest}
    />
  );
}

const combobox = () => screen.getByRole('combobox', { name: 'Статус' });
const root = () => document.querySelector('[data-field]') as HTMLElement;

describe('tech-015 Scenario: InlineSelectField (design § 5.7)', () => {
  describe('Scenario: its name is its label only', () => {
    it('is named «Статус», not «Статус В работе»: MUI would add the shown value', () => {
      render(<Harness />);
      expect(combobox()).toHaveAccessibleName('Статус');
      expect(combobox()).toHaveTextContent('В работе');
      expect(combobox().getAttribute('aria-labelledby')).toBe(
        (document.querySelector('label') as HTMLLabelElement).id,
      );
    });

    it('is a combobox that announces a listbox', () => {
      render(<Harness />);
      expect(combobox()).toHaveAttribute('aria-haspopup', 'listbox');
      expect(combobox()).toHaveAttribute('aria-expanded', 'false');
    });
  });

  describe('Scenario: opening and choosing', () => {
    it('opens on a click, lists the options, and reports the one chosen', async () => {
      const user = userEvent.setup();
      const onChange = vi.fn();
      render(<Harness onChange={onChange} />);
      await user.click(combobox());
      const list = await screen.findByRole('listbox', { name: 'Статус' });
      expect(screen.getAllByRole('option').map((option) => option.textContent)).toEqual([
        'Новая',
        'В работе',
        'Готово',
      ]);
      expect(list).toBeInTheDocument();
      await user.click(screen.getByRole('option', { name: 'Готово' }));
      expect(onChange).toHaveBeenCalledExactlyOnceWith('done');
      await waitFor(() => expect(screen.queryByRole('listbox')).toBeNull());
      expect(combobox()).toHaveTextContent('Готово');
    });

    it.each([
      ['Enter', '{Enter}'],
      ['Space', ' '],
      ['ArrowDown', '{ArrowDown}'],
    ])('%s opens it, with the current option selected', async (_name, keys) => {
      const user = userEvent.setup();
      render(<Harness />);
      await user.tab();
      expect(combobox()).toHaveFocus();
      await user.keyboard(keys);
      const current = await screen.findByRole('option', { name: 'В работе' });
      expect(current).toHaveAttribute('aria-selected', 'true');
    });

    it('chooses with the keyboard and gives focus back to the combobox', async () => {
      const user = userEvent.setup();
      const onChange = vi.fn();
      render(<Harness onChange={onChange} />);
      await user.tab();
      await user.keyboard('{ArrowDown}');
      await screen.findByRole('listbox');
      await user.keyboard('{ArrowDown}{Enter}');
      expect(onChange).toHaveBeenCalledExactlyOnceWith('done');
      await waitFor(() => expect(screen.queryByRole('listbox')).toBeNull());
      expect(combobox()).toHaveFocus();
    });

    it('closes on Escape without choosing', async () => {
      const user = userEvent.setup();
      const onChange = vi.fn();
      render(<Harness onChange={onChange} />);
      await user.click(combobox());
      await screen.findByRole('listbox');
      await user.keyboard('{Escape}');
      await waitFor(() => expect(screen.queryByRole('listbox')).toBeNull());
      expect(onChange).not.toHaveBeenCalled();
    });

    it('lists the options as the host gave them: no hidden empty option when it is not optional', async () => {
      const user = userEvent.setup();
      render(<Harness />);
      await user.click(combobox());
      expect(await screen.findAllByRole('option')).toHaveLength(3);
    });
  });

  describe('Scenario: an optional select has an emptyLabel', () => {
    it('adds a first option with that label, and choosing it gives an empty value', async () => {
      const user = userEvent.setup();
      const onChange = vi.fn();
      render(<Harness emptyLabel="Не указан" onChange={onChange} />);
      await user.click(combobox());
      const options = await screen.findAllByRole('option');
      expect(options.map((option) => option.textContent)).toEqual([
        'Не указан',
        'Новая',
        'В работе',
        'Готово',
      ]);
      await user.click(options[0] as HTMLElement);
      expect(onChange).toHaveBeenCalledExactlyOnceWith('');
    });

    it('shows the emptyLabel as the value while nothing is chosen', () => {
      render(<Harness initial="" emptyLabel="Не указан" />);
      expect(combobox()).toHaveTextContent('Не указан');
    });

    it('shows nothing — and the host’s emptyText — when nothing is chosen and it is required', () => {
      render(<Harness initial="" emptyText="Выберите статус" />);
      expect(combobox().textContent?.replaceAll(String.fromCharCode(0x200b), '')).toBe('');
      expect(root().querySelector('[data-empty-text]')).toHaveTextContent('Выберите статус');
    });
  });

  describe('Scenario: options with an adornment', () => {
    const withDots: SelectOption<'new' | 'work' | 'done'>[] = [
      {
        value: 'new',
        label: 'Новая',
        startAdornment: <CategoryMarker code="Proposed" size="field" />,
      },
      {
        value: 'work',
        label: 'В работе',
        startAdornment: <CategoryMarker code="InProgress" size="field" />,
      },
    ];

    it('draws the adornment in the list and in the value, `nameGap` before the label', async () => {
      const user = userEvent.setup();
      render(<Harness options={withDots} />);
      expect(combobox().querySelector('.WorkTrackingCategoryMarker')).not.toBeNull();
      await user.click(combobox());
      const option = await screen.findByRole('option', { name: 'Новая' });
      const marker = option.querySelector('.WorkTrackingCategoryMarker') as HTMLElement;
      expect(marker).not.toBeNull();
      const gap = getComputedStyle(marker.parentElement as HTMLElement);
      expect(gap.columnGap || gap.gap).toBe(`${theme.layout.mark.nameGap}px`);
    });

    it('names an option by its label alone: the dot is hidden', async () => {
      const user = userEvent.setup();
      render(<Harness options={withDots} />);
      await user.click(combobox());
      expect(await screen.findByRole('option', { name: 'Новая' })).toBeInTheDocument();
    });
  });

  describe('Scenario: renderValue', () => {
    it('draws what the host says for the chosen option', () => {
      render(
        <Harness
          renderValue={(option) => <span data-testid="shown">{option.label.toUpperCase()}</span>}
        />,
      );
      expect(screen.getByTestId('shown')).toHaveTextContent('В РАБОТЕ');
      expect(combobox()).toHaveAccessibleName('Статус');
    });
  });

  describe('Scenario: the caret', () => {
    const caret = () => rulesFor(root(), '.MuiSelect-icon');

    it('is the thin chevron, never Material’s filled drop-down triangle', () => {
      render(<Harness />);
      expect(root().querySelector('[data-testid="ThinChevronDownIcon"]')).not.toBeNull();
      expect(root().querySelector('[data-testid="ArrowDropDownIcon"]')).toBeNull();
    });

    it('is hidden at rest and shows on hover, on focus, while open and with no hover', () => {
      render(<Harness />);
      const icon = root().querySelector('.MuiSelect-icon') as Element;
      const rest = caret().find(
        (rule) => rule.media === '' && !rule.selector.includes(':') && !rule.selector.includes('['),
      );
      expect(rest?.declarations.opacity).toBe('0');
      for (const state of [':hover', ':focus-within', '[data-open="true"]']) {
        const shown = caret().find((rule) => rule.selector.includes(state));
        expect(shown?.declarations.opacity, state).toBe('1');
        // The rule must be able to match: a state rule that names the root's own class a
        // second time, as a descendant, reads right and never applies.
        expect(reaches(shown?.selector ?? '', icon), `${state} reaches the caret`).toBe(true);
      }
      const touch = caret().find((rule) => rule.media.includes('hover: none'));
      expect(touch?.declarations.opacity).toBe('1');
    });

    it('is drawn once the combobox has focus, and not at rest', async () => {
      const user = userEvent.setup();
      render(<Harness />);
      const icon = root().querySelector('.MuiSelect-icon') as HTMLElement;
      expect(getComputedStyle(icon).opacity).toBe('0');
      await user.tab();
      expect(combobox()).toHaveFocus();
      expect(getComputedStyle(icon).opacity).toBe('1');
    });

    it('is drawn while the menu is open, when the focus has left the field', async () => {
      const user = userEvent.setup();
      render(<Harness />);
      const icon = root().querySelector('.MuiSelect-icon') as HTMLElement;
      await user.click(combobox());
      await screen.findByRole('listbox');
      expect(root().contains(document.activeElement)).toBe(false);
      expect(getComputedStyle(icon).opacity).toBe('1');
    });

    it('is drawn at rest in a framed field, where a blank form needs it', () => {
      render(<Harness frame="always" />);
      const rest = caret().find((rule) => rule.media === '' && !rule.selector.includes(':'));
      expect(rest?.declarations.opacity).toBe('1');
    });

    it('keeps its room, so showing it moves nothing', () => {
      render(<Harness />);
      const display = rulesFor(root(), '.MuiSelect-select')[0];
      expect(display?.declarations['padding-right']).toBe(
        `${theme.layout.iconAction.chevron.glyph}px`,
      );
    });
  });

  describe('Scenario: the frame knows the menu is open (review R1-2)', () => {
    it('sets data-open on the field’s root while the menu is open, and takes it off when it closes', async () => {
      const user = userEvent.setup();
      render(<Harness />);
      expect(root()).not.toHaveAttribute('data-open');
      await user.click(combobox());
      await screen.findByRole('listbox');
      expect(root()).toHaveAttribute('data-open', 'true');
      await user.keyboard('{Escape}');
      await waitFor(() => expect(root()).not.toHaveAttribute('data-open'));
    });

    it('takes it off when an option is chosen', async () => {
      const user = userEvent.setup();
      render(<Harness />);
      await user.click(combobox());
      await user.click(await screen.findByRole('option', { name: 'Готово' }));
      await waitFor(() => expect(root()).not.toHaveAttribute('data-open'));
    });

    it('keeps the focus frame, the hint and the caret on the root while open — by that attribute, not by :has', () => {
      render(<Harness hint="Подсказка" />);
      const open = (fragment: string) => rulesFor(root(), `[data-open="true"] ${fragment}`);
      expect(open('.MuiSelect-icon').some((rule) => rule.declarations.opacity === '1')).toBe(true);
      expect(
        open('[data-field-hint]').some((rule) => rule.declarations.visibility === 'visible'),
      ).toBe(true);
      const ring = rulesFor(root(), '[data-open="true"] [data-frame-part="whole"]');
      expect(ring[0]?.declarations['box-shadow']).toContain(theme.palette.primary.main);
    });
  });

  describe('Scenario: the label and the ref', () => {
    it('moves focus to the combobox when the label is clicked — a label cannot name a div', async () => {
      const user = userEvent.setup();
      render(<Harness />);
      await user.click(document.querySelector('label') as HTMLElement);
      expect(combobox()).toHaveFocus();
    });

    it('forwards the ref to the combobox', () => {
      const ref = createRef<HTMLElement>();
      render(<Harness ref={ref} />);
      expect(ref.current).toBe(combobox());
    });
  });

  it('does not open a read-only select, by pointer or by key', async () => {
    const user = userEvent.setup();
    render(<Harness readOnly />);
    await user.click(combobox());
    await user.keyboard('{Enter}{ArrowDown} ');
    expect(screen.queryByRole('listbox')).toBeNull();
  });

  it('passes aria-* and data-* to the combobox and cannot override the kit’s', () => {
    render(
      <Harness
        error="Выберите статус"
        inputProps={{
          'data-testid': 'status',
          'aria-keyshortcuts': 'Alt+S',
          'aria-invalid': false,
        }}
      />,
    );
    expect(screen.getByTestId('status')).toBe(combobox());
    expect(combobox()).toHaveAttribute('aria-keyshortcuts', 'Alt+S');
    expect(combobox()).toHaveAttribute('aria-invalid', 'true');
  });
});
