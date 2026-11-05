import { createRef, useState } from 'react';
import { describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { reaches, rulesFor, theme } from '../../../test/kitTesting';
import { InlinePickerField, type InlinePickerFieldProps } from './InlinePickerField';

// tech-015 design § 5.7 «InlinePickerField»: a search among many options on the kit's
// frame; the input is always there; loading, empty and end-action states; the host's
// own list, lines and texts.

interface Item {
  id: string;
  title: string;
}

const ITEMS: Item[] = [
  { id: 'a', title: 'Письмо со ссылкой' },
  { id: 'b', title: 'Вход по ссылке' },
  { id: 'c', title: 'Выход из системы' },
];

type Props = Partial<InlinePickerFieldProps<Item>>;

function Harness({ initial = null, onChange, ...rest }: Props & { initial?: Item | null }) {
  const [value, setValue] = useState<Item | null>(initial);
  return (
    <InlinePickerField<Item>
      label="Родитель"
      value={value}
      options={ITEMS}
      getOptionLabel={(item) => item.title}
      isOptionEqualToValue={(a, b) => a.id === b.id}
      getOptionKey={(item) => item.id}
      onChange={(next) => {
        setValue(next);
        onChange?.(next);
      }}
      {...rest}
    />
  );
}

const input = () => screen.getByRole('combobox', { name: 'Родитель' }) as HTMLInputElement;
const root = () => document.querySelector('[data-field]') as HTMLElement;

describe('tech-015 Scenario: InlinePickerField (design § 5.7)', () => {
  describe('Scenario: the input is always there', () => {
    it('shows the chosen option’s text at rest, in an input named by its label', () => {
      render(<Harness initial={ITEMS[0]} />);
      expect(input()).toHaveValue('Письмо со ссылкой');
      expect(input().tagName).toBe('INPUT');
      expect(input()).toHaveAccessibleName('Родитель');
    });

    it('selects its text on focus, so a search starts by typing', async () => {
      const user = userEvent.setup();
      render(<Harness initial={ITEMS[0]} />);
      await user.click(input());
      expect(input().selectionStart).toBe(0);
      expect(input().selectionEnd).toBe('Письмо со ссылкой'.length);
    });
  });

  describe('Scenario: searching and choosing', () => {
    it('filters by what is typed and reports the option chosen', async () => {
      const user = userEvent.setup();
      const onChange = vi.fn();
      render(<Harness onChange={onChange} />);
      await user.click(input());
      await user.type(input(), 'ссылк');
      expect((await screen.findAllByRole('option')).map((option) => option.textContent)).toEqual([
        'Письмо со ссылкой',
        'Вход по ссылке',
      ]);
      await user.click(screen.getByRole('option', { name: 'Вход по ссылке' }));
      expect(onChange).toHaveBeenCalledExactlyOnceWith(ITEMS[1]);
      expect(input()).toHaveValue('Вход по ссылке');
    });

    it('opens by the keyboard, walks the list and chooses', async () => {
      const user = userEvent.setup();
      const onChange = vi.fn();
      render(<Harness onChange={onChange} />);
      await user.tab();
      await user.keyboard('{ArrowDown}{ArrowDown}{ArrowDown}{Enter}');
      expect(onChange).toHaveBeenCalledExactlyOnceWith(ITEMS[1]);
    });

    it('closes on Escape and keeps the chosen value', async () => {
      const user = userEvent.setup();
      render(<Harness initial={ITEMS[2]} />);
      await user.click(input());
      await screen.findByRole('listbox');
      await user.keyboard('{Escape}');
      await waitFor(() => expect(screen.queryByRole('listbox')).toBeNull());
      expect(input()).toHaveValue('Выход из системы');
    });

    it('names the option by its label, as the host’s lines draw it', async () => {
      const user = userEvent.setup();
      render(
        <Harness
          renderOption={(item, { selected }) => (
            <span data-testid={`line-${item.id}`} data-selected={selected}>
              {item.id.toUpperCase()} · {item.title}
            </span>
          )}
        />,
      );
      await user.click(input());
      expect(await screen.findByTestId('line-a')).toHaveTextContent('A · Письмо со ссылкой');
      expect(screen.getByRole('option', { name: 'A · Письмо со ссылкой' })).toBeInTheDocument();
    });

    it('marks the chosen option selected for the host’s lines', async () => {
      const user = userEvent.setup();
      render(
        <Harness
          initial={ITEMS[1]}
          renderOption={(item, { selected }) => (
            <span data-testid={`line-${item.id}`} data-selected={String(selected)}>
              {item.title}
            </span>
          )}
        />,
      );
      await user.click(input());
      expect(await screen.findByTestId('line-b')).toHaveAttribute('data-selected', 'true');
      expect(screen.getByTestId('line-a')).toHaveAttribute('data-selected', 'false');
    });

    it('cannot choose a line the host disabled — a hint, «Поиск…»', async () => {
      const user = userEvent.setup();
      const onChange = vi.fn();
      render(<Harness onChange={onChange} getOptionDisabled={(item) => item.id === 'a'} />);
      await user.click(input());
      const line = await screen.findByRole('option', { name: 'Письмо со ссылкой' });
      // MUI draws it `pointer-events: none` and the arrows walk past it.
      expect(line).toHaveAttribute('aria-disabled', 'true');
      expect(getComputedStyle(line).pointerEvents).toBe('none');
      await user.keyboard('{ArrowDown}{Enter}');
      expect(onChange).toHaveBeenCalledExactlyOnceWith(ITEMS[1]);
    });

    it('lists what the host answers, whole, when it does the filtering itself', async () => {
      const user = userEvent.setup();
      render(<Harness filterOptions={(options) => options} />);
      await user.click(input());
      await user.type(input(), 'zzz');
      expect(await screen.findAllByRole('option')).toHaveLength(3);
    });
  });

  describe('Scenario: the typed text, when the host searches by it', () => {
    it('reports the text and why it changed, and shows the host’s inputValue', async () => {
      const onInputChange = vi.fn();
      render(<Harness inputValue="ссыл" onInputChange={onInputChange} />);
      expect(input()).toHaveValue('ссыл');
      fireEvent.change(input(), { target: { value: 'ссылк' } });
      expect(onInputChange).toHaveBeenCalledWith('ссылк', 'input');
      // Controlled: the text stays the host's until the host changes it.
      expect(input()).toHaveValue('ссыл');
    });

    it('tells the host the list opens, so it can read again', async () => {
      const user = userEvent.setup();
      const onOpen = vi.fn();
      render(<Harness onOpen={onOpen} />);
      await user.click(input());
      expect(onOpen).toHaveBeenCalledTimes(1);
    });
  });

  describe('Scenario: loading and empty', () => {
    it('says «Загрузка…» in the list while it loads, and the input stays usable', async () => {
      const user = userEvent.setup();
      render(<Harness options={[]} loading />);
      await user.click(input());
      expect(await screen.findByText('Загрузка…')).toBeInTheDocument();
      expect(input()).not.toBeDisabled();
      await user.type(input(), 'ссы');
      expect(input()).toHaveValue('ссы');
    });

    it('takes the host’s own loading text', async () => {
      const user = userEvent.setup();
      render(<Harness options={[]} loading loadingText="Идёт поиск…" />);
      await user.click(input());
      expect(await screen.findByText('Идёт поиск…')).toBeInTheDocument();
      expect(screen.queryByText('Загрузка…')).toBeNull();
    });

    it('says «Ничего не найдено» when nothing matches, or the host’s own words', async () => {
      const user = userEvent.setup();
      const { unmount } = render(<Harness />);
      await user.click(input());
      await user.type(input(), 'яяя');
      expect(await screen.findByText('Ничего не найдено')).toBeInTheDocument();
      unmount();
      render(<Harness noOptionsText="Не удалось загрузить области. Попробуйте ещё раз." />);
      await user.click(input());
      await user.type(input(), 'яяя');
      expect(
        await screen.findByText('Не удалось загрузить области. Попробуйте ещё раз.'),
      ).toBeInTheDocument();
    });
  });

  describe('Scenario: an action after the value', () => {
    it('draws the host’s link in the box, after the text and before the arrow', () => {
      render(<Harness initial={ITEMS[0]} endAction={<a href="/items/a">Открыть MSK-FEAT-6</a>} />);
      const link = screen.getByRole('link', { name: 'Открыть MSK-FEAT-6' });
      const box = root().querySelector('[data-field-box]') as HTMLElement;
      expect(box).toContainElement(link);
      expect(input().compareDocumentPosition(link) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
    });

    it('keeps the action out of the way of a click on the text', async () => {
      const user = userEvent.setup();
      render(<Harness endAction={<button type="button">Действие</button>} />);
      await user.click(screen.getByRole('button', { name: 'Действие' }));
      expect(screen.queryByRole('listbox')).toBeNull();
    });
  });

  describe('Scenario: the arrow and the clear button', () => {
    const indicators = () => rulesFor(root(), 'MuiAutocomplete-popupIndicator');

    it('draws the popup arrow as the thin chevron', () => {
      render(<Harness />);
      expect(root().querySelector('[data-testid="ThinChevronDownIcon"]')).not.toBeNull();
      expect(root().querySelector('[data-testid="ArrowDropDownIcon"]')).toBeNull();
    });

    it('hides the arrow at rest, shows it with the frame, and sizes it to a 24 px target', () => {
      render(<Harness />);
      const rest = indicators().find((rule) => rule.media === '' && !rule.selector.includes(':'));
      expect(rest?.declarations.opacity).toBe('0');
      expect(rest?.declarations.width).toBe(`${theme.layout.iconAction.row.box}px`);
      expect(
        indicators().some(
          (rule) => rule.selector.includes(':hover') && rule.declarations.opacity === '1',
        ),
      ).toBe(true);
      expect(
        indicators().some(
          (rule) => rule.selector.includes(':focus-within') && rule.declarations.opacity === '1',
        ),
      ).toBe(true);
    });

    it('can match: each state rule reaches the arrow and the clear button once the state holds', () => {
      render(<Harness initial={ITEMS[0]} clearable />);
      const arrow = root().querySelector('.MuiAutocomplete-popupIndicator') as Element;
      const clear = root().querySelector('.MuiAutocomplete-clearIndicator') as Element;
      for (const state of [':hover', ':focus-within', '[data-open="true"]']) {
        const rules = rulesFor(root(), 'MuiAutocomplete-').filter(
          (rule) => rule.selector.includes(state) && rule.declarations.opacity === '1',
        );
        // One rule per indicator, each with the state in front of it.
        expect(rules.length, state).toBeGreaterThanOrEqual(1);
        const reached = new Set(
          [arrow, clear].filter((target) => rules.some((rule) => reaches(rule.selector, target))),
        );
        expect(reached.size, `${state} reaches both indicators`).toBe(2);
      }
    });

    it('draws neither at rest, and both once the field has focus', async () => {
      const user = userEvent.setup();
      render(<Harness initial={ITEMS[0]} clearable />);
      const arrow = root().querySelector('.MuiAutocomplete-popupIndicator') as HTMLElement;
      const clear = root().querySelector('.MuiAutocomplete-clearIndicator') as HTMLElement;
      // The clear button was drawn at rest by a rule that lost its state when the
      // indicators were joined in one selector list.
      expect(getComputedStyle(arrow).opacity).toBe('0');
      expect(getComputedStyle(clear).opacity).toBe('0');
      await user.tab();
      expect(input()).toHaveFocus();
      expect(getComputedStyle(arrow).opacity).toBe('1');
      expect(getComputedStyle(clear).opacity).toBe('1');
    });

    it('draws the arrow at rest in a framed field', () => {
      render(<Harness frame="always" />);
      const rest = indicators().find((rule) => rule.media === '' && !rule.selector.includes(':'));
      expect(rest?.declarations.opacity).toBe('1');
    });

    it('has no clear button by default: a value is replaced, not emptied', () => {
      render(<Harness initial={ITEMS[0]} />);
      expect(screen.queryByRole('button', { name: /Clear|Очистить/i })).toBeNull();
      expect(root().querySelector('.MuiAutocomplete-clearIndicator')).toBeNull();
    });

    it('has a thin clear button when clearable, and clearing gives null', async () => {
      const user = userEvent.setup();
      const onChange = vi.fn();
      render(<Harness initial={ITEMS[0]} clearable onChange={onChange} />);
      const clear = root().querySelector('.MuiAutocomplete-clearIndicator') as HTMLElement;
      expect(clear).not.toBeNull();
      expect(clear.querySelector('[data-testid="ThinCloseIcon"]')).not.toBeNull();
      await user.click(clear);
      expect(onChange).toHaveBeenCalledExactlyOnceWith(null);
    });
  });

  describe('Scenario: the frame knows the list is open (review R1-2)', () => {
    it('sets data-open on the field’s root while the list is open, and takes it off when it closes', async () => {
      const user = userEvent.setup();
      render(<Harness />);
      expect(root()).not.toHaveAttribute('data-open');
      await user.click(input());
      await screen.findByRole('listbox');
      expect(root()).toHaveAttribute('data-open', 'true');
      await user.keyboard('{Escape}');
      await waitFor(() => expect(root()).not.toHaveAttribute('data-open'));
    });

    it('takes it off when an option is chosen', async () => {
      const user = userEvent.setup();
      render(<Harness />);
      await user.click(input());
      await user.click(await screen.findByRole('option', { name: 'Вход по ссылке' }));
      await waitFor(() => expect(root()).not.toHaveAttribute('data-open'));
    });

    it('never sets it on a read-only picker, which shows no list', async () => {
      const user = userEvent.setup();
      render(<Harness initial={ITEMS[0]} readOnly />);
      await user.click(input());
      await user.keyboard('{ArrowDown}');
      expect(root()).not.toHaveAttribute('data-open');
    });
  });

  describe('Scenario: the host drives the input', () => {
    it('forwards the ref to the input', () => {
      const ref = createRef<HTMLInputElement>();
      render(<Harness ref={ref} />);
      expect(ref.current).toBe(input());
    });

    it('passes aria-* and data-* to the input', () => {
      render(
        <Harness inputProps={{ 'data-testid': 'parent-input', 'aria-keyshortcuts': 'Alt+P' }} />,
      );
      expect(screen.getByTestId('parent-input')).toBe(input());
      expect(input()).toHaveAttribute('aria-keyshortcuts', 'Alt+P');
    });

    it('does not open a read-only picker, by pointer or by key', async () => {
      const user = userEvent.setup();
      render(<Harness initial={ITEMS[0]} readOnly />);
      await user.click(input());
      await user.keyboard('{ArrowDown}');
      expect(screen.queryByRole('listbox')).toBeNull();
    });
  });
});

describe('mod-017: a description the host draws under the picker', () => {
  it('is the combobox’s description unless the field has an error or a hint of its own', () => {
    const { rerender } = render(
      <Harness initial={ITEMS[0]} inputProps={{ 'aria-describedby': 'caption' }} />,
    );
    expect(input()).toHaveAttribute('aria-describedby', 'caption');

    rerender(
      <Harness
        initial={ITEMS[0]}
        error="Команда не найдена."
        inputProps={{ 'aria-describedby': 'caption' }}
      />,
    );
    expect(input().getAttribute('aria-describedby')).not.toBe('caption');
    expect(input()).toHaveAccessibleDescription('Команда не найдена.');
  });
});
