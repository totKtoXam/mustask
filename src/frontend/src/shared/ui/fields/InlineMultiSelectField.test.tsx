import { useState } from 'react';
import { describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { InlineMultiSelectField, type InlineMultiSelectFieldProps } from './InlineMultiSelectField';
import type { SelectOption } from './InlineSelectField';

// mod-017 design § 2: the multi-value filter field; its name is its label only; the list is a
// multiselectable listbox that stays open while the user ticks.

const TEAMS: SelectOption<string>[] = [
  { value: 'mars', label: 'MARS' },
  { value: 'venus', label: 'VENUS' },
  { value: 'none', label: 'Без команды' },
];

function Harness({
  initial = [],
  onChange,
  ...rest
}: {
  initial?: string[];
  onChange?: (values: string[]) => void;
} & Partial<InlineMultiSelectFieldProps<string>>) {
  const [value, setValue] = useState<string[]>(initial);
  return (
    <InlineMultiSelectField
      label="Команда"
      emptyText="Все команды"
      value={value}
      options={TEAMS}
      onChange={(next) => {
        setValue(next);
        onChange?.(next);
      }}
      {...rest}
    />
  );
}

const combobox = () => screen.getByRole('combobox', { name: 'Команда' });

describe('mod-017 design § 2 Scenario: InlineMultiSelectField', () => {
  it('is named «Команда», shows «Все команды» while empty, and the chosen names joined by «, » otherwise', () => {
    const { unmount } = render(<Harness />);
    expect(combobox()).toHaveAccessibleName('Команда');
    expect(screen.getByText('Все команды')).toBeInTheDocument();
    unmount();

    render(<Harness initial={['mars', 'none']} />);
    expect(combobox()).toHaveAccessibleName('Команда');
    expect(combobox()).toHaveTextContent('MARS, Без команды');
    expect(screen.queryByText('Все команды')).not.toBeInTheDocument();
  });

  it('names only what it knows: a chosen value no option names is not shown', () => {
    render(<Harness initial={['mars', 'deleted-team']} />);

    expect(combobox()).toHaveTextContent('MARS');
    expect(combobox()).not.toHaveTextContent('deleted-team');
  });

  it('opens a multiselectable listbox of options with aria-selected, a decorative checkbox each', async () => {
    const user = userEvent.setup();
    render(<Harness initial={['venus']} />);

    await user.click(combobox());

    const list = await screen.findByRole('listbox');
    expect(list).toHaveAttribute('aria-multiselectable', 'true');
    const options = screen.getAllByRole('option');
    expect(options.map((option) => option.textContent)).toEqual(['MARS', 'VENUS', 'Без команды']);
    expect(options.map((option) => option.getAttribute('aria-selected'))).toEqual([
      'false',
      'true',
      'false',
    ]);
    for (const option of options) {
      const checkbox = option.querySelector('input[type="checkbox"]')!;
      expect(checkbox).toHaveAttribute('tabindex', '-1');
      expect(checkbox).toHaveAttribute('aria-hidden', 'true');
    }
  });

  it('stays open while ticking, reports the whole set each time, and unticks', async () => {
    const user = userEvent.setup();
    const onChange = vi.fn();
    render(<Harness onChange={onChange} />);
    await user.click(combobox());

    await user.click(await screen.findByRole('option', { name: 'MARS' }));
    await user.click(screen.getByRole('option', { name: 'Без команды' }));

    expect(onChange).toHaveBeenNthCalledWith(1, ['mars']);
    expect(onChange).toHaveBeenNthCalledWith(2, ['mars', 'none']);
    expect(screen.getByRole('listbox')).toBeInTheDocument();

    await user.click(screen.getByRole('option', { name: 'MARS' }));

    expect(onChange).toHaveBeenNthCalledWith(3, ['none']);
  });

  it('Escape closes the list and the focus stays on the field', async () => {
    const user = userEvent.setup();
    render(<Harness />);
    await user.click(combobox());
    await screen.findByRole('listbox');

    await user.keyboard('{Escape}');

    await waitFor(() => expect(screen.queryByRole('listbox')).not.toBeInTheDocument());
    await waitFor(() => expect(combobox()).toHaveFocus());
  });

  it('says «Загрузка…» in an empty list while loading and the host’s text when it has failed', async () => {
    const user = userEvent.setup();
    const { rerender } = render(
      <InlineMultiSelectField
        label="Команда"
        value={[]}
        options={[]}
        loading
        onChange={() => undefined}
      />,
    );
    await user.click(combobox());
    expect(await screen.findByRole('option', { name: 'Загрузка…' })).toBeInTheDocument();
    await user.keyboard('{Escape}');
    await waitFor(() => expect(screen.queryByRole('listbox')).not.toBeInTheDocument());

    rerender(
      <InlineMultiSelectField
        label="Команда"
        value={[]}
        options={[]}
        noOptionsText="Не удалось загрузить команды. Попробуйте ещё раз."
        onChange={() => undefined}
      />,
    );
    await user.click(combobox());
    expect(
      await screen.findByRole('option', {
        name: 'Не удалось загрузить команды. Попробуйте ещё раз.',
      }),
    ).toBeInTheDocument();
  });

  it('calls `onOpen` when the list opens, and a read-only field does not open', async () => {
    const user = userEvent.setup();
    const onOpen = vi.fn();
    const { rerender } = render(<Harness onOpen={onOpen} />);
    await user.click(combobox());
    expect(onOpen).toHaveBeenCalledTimes(1);
    await user.keyboard('{Escape}');
    await waitFor(() => expect(screen.queryByRole('listbox')).not.toBeInTheDocument());

    rerender(<Harness onOpen={onOpen} readOnly />);
    await user.click(combobox());

    expect(screen.queryByRole('listbox')).not.toBeInTheDocument();
    expect(onOpen).toHaveBeenCalledTimes(1);
  });
});
