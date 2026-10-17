import { useState } from 'react';
import { describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { rulesFor } from '../../../test/kitTesting';
import {
  InlineMultiPickerField,
  type InlineMultiPickerFieldProps,
} from './InlineMultiPickerField';

// feat-028 design § 2 «InlineMultiPickerField» (D-1): a searchable multiselect on the kit's
// frame; the chosen are tags with a remove button; the list stays open while picking.

interface Item {
  id: string;
  name: string;
}

const ITEMS: Item[] = [
  { id: 'a', name: 'Alpha' },
  { id: 'b', name: 'Beta' },
  { id: 'g', name: 'Gamma' },
];

type Props = Partial<InlineMultiPickerFieldProps<Item>>;

function Harness({ initial = [], onChange, ...rest }: Props & { initial?: Item[] }) {
  const [value, setValue] = useState<Item[]>(initial);
  return (
    <InlineMultiPickerField<Item>
      label="Проекты"
      value={value}
      options={ITEMS}
      getOptionLabel={(item) => item.name}
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

const input = () => screen.getByRole('combobox', { name: 'Проекты' }) as HTMLInputElement;
const root = () => document.querySelector('[data-field]') as HTMLElement;

describe('feat-028 Scenario: InlineMultiPickerField (design § 2, D-1)', () => {
  describe('Scenario: its name and its chosen', () => {
    it('is named by its label alone, however many are chosen', () => {
      render(<Harness initial={[ITEMS[0]!, ITEMS[1]!]} />);
      expect(input()).toHaveAccessibleName('Проекты');
      expect(input().tagName).toBe('INPUT');
    });

    it('draws each chosen as a kit tag with a remove button «Убрать «<имя>»», before the input', () => {
      render(<Harness initial={[ITEMS[0]!, ITEMS[1]!]} />);
      const buttons = screen.getAllByRole('button', { name: /^Убрать/ });
      expect(buttons.map((button) => button.getAttribute('aria-label'))).toEqual([
        'Убрать «Alpha»',
        'Убрать «Beta»',
      ]);
      expect(root().querySelectorAll('.MuiChip-root')).toHaveLength(2);
      expect(
        buttons[1]!.compareDocumentPosition(input()) & Node.DOCUMENT_POSITION_FOLLOWING,
      ).toBeTruthy();
    });

    it('takes the host’s own remove names', () => {
      render(<Harness initial={[ITEMS[0]!]} removeLabel={(label) => `Снять ${label}`} />);
      expect(screen.getByRole('button', { name: 'Снять Alpha' })).toBeInTheDocument();
    });
  });

  describe('Scenario: choosing several', () => {
    it('keeps the list open while picking, marks the chosen and reports the whole choice each time', async () => {
      const user = userEvent.setup();
      const onChange = vi.fn();
      render(<Harness onChange={onChange} />);
      await user.click(input());
      expect(await screen.findByRole('listbox')).toHaveAttribute('aria-multiselectable', 'true');

      await user.click(screen.getByRole('option', { name: 'Alpha' }));
      expect(onChange).toHaveBeenLastCalledWith([ITEMS[0]]);
      await user.click(screen.getByRole('option', { name: 'Gamma' }));
      expect(onChange).toHaveBeenLastCalledWith([ITEMS[0], ITEMS[2]]);

      expect(screen.getByRole('listbox')).toBeInTheDocument();
      expect(screen.getByRole('option', { name: 'Alpha' })).toHaveAttribute('aria-selected', 'true');
      expect(screen.getByRole('option', { name: 'Beta' })).toHaveAttribute('aria-selected', 'false');
    });

    it('draws a checkbox in each option for the eye only: hidden from the tree, out of the tab order', async () => {
      const user = userEvent.setup();
      render(<Harness initial={[ITEMS[1]!]} />);
      await user.click(input());
      const option = await screen.findByRole('option', { name: 'Beta' });
      const box = option.querySelector('.MuiCheckbox-root') as HTMLElement;
      expect(box).toHaveAttribute('aria-hidden', 'true');
      expect(box.querySelector('input')).toHaveAttribute('tabindex', '-1');
      expect(box.querySelector('input')).toBeChecked();
      expect(within(option).queryByRole('checkbox')).toBeNull();
    });

    it('filters by what is typed and clears the text after a choice', async () => {
      const user = userEvent.setup();
      render(<Harness />);
      await user.click(input());
      await user.type(input(), 'amm');
      expect((await screen.findAllByRole('option')).map((option) => option.textContent)).toEqual([
        'Gamma',
      ]);
      await user.click(screen.getByRole('option', { name: 'Gamma' }));
      expect(input()).toHaveValue('');
    });

    it('opens by the keyboard, walks the list and chooses with Enter', async () => {
      const user = userEvent.setup();
      const onChange = vi.fn();
      render(<Harness onChange={onChange} />);
      await user.tab();
      await user.keyboard('{ArrowDown}{ArrowDown}{ArrowDown}{Enter}');
      expect(onChange).toHaveBeenLastCalledWith([ITEMS[1]]);
    });

    it('closes the list on Escape and keeps the chosen', async () => {
      const user = userEvent.setup();
      render(<Harness initial={[ITEMS[2]!]} />);
      await user.click(input());
      await screen.findByRole('listbox');
      await user.keyboard('{Escape}');
      await waitFor(() => expect(screen.queryByRole('listbox')).toBeNull());
      expect(screen.getByRole('button', { name: 'Убрать «Gamma»' })).toBeInTheDocument();
    });
  });

  describe('Scenario: removing', () => {
    it('removes a tag by its button and keeps the others in order', async () => {
      const user = userEvent.setup();
      const onChange = vi.fn();
      render(<Harness initial={ITEMS} onChange={onChange} />);
      await user.click(screen.getByRole('button', { name: 'Убрать «Beta»' }));
      expect(onChange).toHaveBeenCalledExactlyOnceWith([ITEMS[0], ITEMS[2]]);
      expect(screen.queryByRole('button', { name: 'Убрать «Beta»' })).toBeNull();
    });

    it('removes the last with Backspace in an empty input, and not while text is typed', async () => {
      const user = userEvent.setup();
      const onChange = vi.fn();
      render(<Harness initial={[ITEMS[0]!, ITEMS[1]!]} onChange={onChange} />);
      await user.click(input());
      await user.type(input(), 'x');
      await user.keyboard('{Backspace}');
      expect(onChange).not.toHaveBeenCalled();
      await user.keyboard('{Backspace}');
      expect(onChange).toHaveBeenCalledExactlyOnceWith([ITEMS[0]]);
    });

    it('offers no remove button and no list to a read-only field', async () => {
      const user = userEvent.setup();
      render(<Harness initial={[ITEMS[0]!]} readOnly />);
      expect(screen.queryByRole('button', { name: /^Убрать/ })).toBeNull();
      expect(root().querySelectorAll('.MuiChip-root')).toHaveLength(1);
      await user.click(input());
      expect(screen.queryByRole('listbox')).toBeNull();
    });

    it('offers no remove button to a busy field, which says so', () => {
      render(<Harness initial={[ITEMS[0]!]} busy />);
      expect(screen.queryByRole('button', { name: /^Убрать/ })).toBeNull();
      expect(input()).toHaveAttribute('aria-busy', 'true');
    });
  });

  describe('Scenario: the host searches by the text', () => {
    it('reports the text and why it changed, and lists the host’s answer whole', async () => {
      const onInputChange = vi.fn();
      render(
        <Harness
          inputValue="ал"
          onInputChange={onInputChange}
          filterOptions={(options) => options}
        />,
      );
      expect(input()).toHaveValue('ал');
      fireEvent.change(input(), { target: { value: 'алф' } });
      expect(onInputChange).toHaveBeenCalledWith('алф', 'input');
      fireEvent.focus(input());
      fireEvent.keyDown(input(), { key: 'ArrowDown' });
      expect(await screen.findAllByRole('option')).toHaveLength(3);
    });

    it('tells the host the list opens, so it can read again', async () => {
      const user = userEvent.setup();
      const onOpen = vi.fn();
      render(<Harness onOpen={onOpen} />);
      await user.click(input());
      expect(onOpen).toHaveBeenCalledTimes(1);
    });
  });

  describe('Scenario: loading, empty and the host’s own lines', () => {
    it('says «Загрузка…» while it loads and «Ничего не найдено» when nothing matches', async () => {
      const user = userEvent.setup();
      const { unmount } = render(<Harness options={[]} loading />);
      await user.click(input());
      expect(await screen.findByText('Загрузка…')).toBeInTheDocument();
      expect(input()).not.toBeDisabled();
      unmount();

      render(<Harness />);
      await user.click(input());
      await user.type(input(), 'яяя');
      expect(await screen.findByText('Ничего не найдено')).toBeInTheDocument();
    });

    it('takes the host’s words for an empty list', async () => {
      const user = userEvent.setup();
      render(<Harness options={[]} noOptionsText="В организации нет активных проектов." />);
      await user.click(input());
      expect(await screen.findByText('В организации нет активных проектов.')).toBeInTheDocument();
    });

    it('draws the host’s line after the checkbox, and still names the option by its text', async () => {
      const user = userEvent.setup();
      render(
        <Harness
          renderOption={(item) => (
            <span>
              {item.name} <small>{item.id.toUpperCase()}</small>
            </span>
          )}
        />,
      );
      await user.click(input());
      expect(await screen.findByRole('option', { name: 'Alpha A' })).toBeInTheDocument();
    });

    it('cannot choose a line the host disabled', async () => {
      const user = userEvent.setup();
      const onChange = vi.fn();
      render(<Harness onChange={onChange} getOptionDisabled={(item) => item.id === 'a'} />);
      await user.click(input());
      expect(await screen.findByRole('option', { name: 'Alpha' })).toHaveAttribute(
        'aria-disabled',
        'true',
      );
      await user.keyboard('{ArrowDown}{Enter}');
      expect(onChange).toHaveBeenLastCalledWith([ITEMS[1]]);
    });
  });

  describe('Scenario: the frame', () => {
    it('shows the label, the required mark, the hint and the error as the family does', () => {
      render(<Harness required hint="Необязательно." error="Проект не найден." />);
      expect(screen.getByText('Проект не найден.')).toHaveAttribute('role', 'alert');
      expect(input()).toHaveAttribute('aria-invalid', 'true');
      expect(input()).toHaveAttribute('aria-required', 'true');
      expect(input()).toHaveAccessibleDescription('Проект не найден.');
    });

    it('sets data-open on the root while the list is open and takes it off when it closes', async () => {
      const user = userEvent.setup();
      render(<Harness />);
      expect(root()).not.toHaveAttribute('data-open');
      await user.click(input());
      await screen.findByRole('listbox');
      expect(root()).toHaveAttribute('data-open', 'true');
      await user.keyboard('{Escape}');
      await waitFor(() => expect(root()).not.toHaveAttribute('data-open'));
    });

    it('lets the tags wrap onto rows, the box growing in height and never in width', () => {
      render(<Harness initial={ITEMS} />);
      const wrapping = rulesFor(root(), 'MuiAutocomplete-inputRoot').find(
        (rule) => rule.declarations['flex-wrap'] !== undefined,
      );
      expect(wrapping?.declarations['flex-wrap']).toBe('wrap');
      const box = root().querySelector('[data-field-box]') as HTMLElement;
      expect(getComputedStyle(box).minHeight).not.toBe('');
      expect(getComputedStyle(box).height).toBe('');
    });

    it('draws the popup arrow as the thin chevron and no clear button', () => {
      render(<Harness initial={[ITEMS[0]!]} />);
      expect(root().querySelector('[data-testid="ThinChevronDownIcon"]')).not.toBeNull();
      expect(root().querySelector('.MuiAutocomplete-clearIndicator')).toBeNull();
    });
  });
});
