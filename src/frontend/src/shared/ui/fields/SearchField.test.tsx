import { useState } from 'react';
import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { px, theme } from '../../../test/kitTesting';
import { SearchField } from './SearchField';

// feat-070 / feat-065 design § 1, § 3.4, D-14: the header field.

function Harness({
  initial = '',
  onClear = () => {},
  hint = 'Ctrl K',
}: {
  initial?: string;
  onClear?: () => void;
  hint?: string;
}) {
  const [value, setValue] = useState(initial);
  return (
    <SearchField
      value={value}
      onChange={setValue}
      label="Поиск"
      placeholder="Поиск…"
      hint={hint}
      keyShortcuts="Control+K"
      clearLabel="Очистить поле"
      onClear={() => {
        setValue('');
        onClear();
      }}
      maxLength={5}
      aria-expanded={true}
      aria-controls="results"
      aria-activedescendant="option-1"
    />
  );
}

const BARE = {
  value: '',
  onChange: () => {},
  label: 'Поиск',
  placeholder: 'Поиск…',
  clearLabel: 'Очистить поле',
  onClear: () => {},
};

describe('feat-070 Scenario: SearchField (feat-065 design § 1)', () => {
  it('is a combobox named «Поиск» with the placeholder, and the host’s aria props', () => {
    render(<Harness />);
    const input = screen.getByRole('combobox', { name: 'Поиск' });
    expect(input).toHaveAttribute('placeholder', 'Поиск…');
    expect(input).toHaveAttribute('aria-expanded', 'true');
    expect(input).toHaveAttribute('aria-controls', 'results');
    expect(input).toHaveAttribute('aria-activedescendant', 'option-1');
    expect(input).toHaveAttribute('aria-autocomplete', 'list');
    expect(input).toHaveAttribute('aria-keyshortcuts', 'Control+K');
  });

  it('shows the shortcut hint and no clear button while empty', () => {
    render(<Harness />);
    expect(screen.getByText('Ctrl K')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Очистить поле' })).toBeNull();
  });

  it('shows the hint it is given («⌘K» on macOS)', () => {
    render(<Harness hint="⌘K" />);
    expect(screen.getByText('⌘K')).toBeInTheDocument();
  });

  it('swaps the hint for the clear button once there is text', async () => {
    const user = userEvent.setup();
    render(<Harness />);
    await user.type(screen.getByRole('combobox'), 'оп');
    expect(screen.queryByText('Ctrl K')).toBeNull();
    expect(screen.getByRole('button', { name: 'Очистить поле' })).toBeInTheDocument();
  });

  it('stops at maxLength without an error text', async () => {
    const user = userEvent.setup();
    render(<Harness />);
    await user.type(screen.getByRole('combobox'), 'абвгдеж');
    expect(screen.getByRole('combobox')).toHaveValue('абвгд');
  });

  it('clears on the button and returns the focus to the field', async () => {
    const user = userEvent.setup();
    const onClear = vi.fn();
    render(<Harness initial="оплата" onClear={onClear} />);
    await user.click(screen.getByRole('button', { name: 'Очистить поле' }));
    expect(onClear).toHaveBeenCalledOnce();
    expect(screen.getByRole('combobox')).toHaveValue('');
    expect(screen.getByRole('combobox')).toHaveFocus();
  });

  it('reaches the clear button by Tab straight after the field', async () => {
    const user = userEvent.setup();
    render(<Harness initial="оплата" />);
    await user.click(screen.getByRole('combobox'));
    await user.tab();
    expect(screen.getByRole('button', { name: 'Очистить поле' })).toHaveFocus();
  });

  it('passes key handlers to the input', async () => {
    const user = userEvent.setup();
    const onKeyDown = vi.fn();
    render(<SearchField {...BARE} onKeyDown={onKeyDown} />);
    await user.type(screen.getByRole('combobox'), '{ArrowDown}');
    expect(onKeyDown).toHaveBeenCalledOnce();
  });

  it('is 28 px high and at most 300 px wide, from the tokens', () => {
    const { container } = render(<Harness />);
    const style = getComputedStyle(container.firstElementChild as HTMLElement);
    expect(px(style.height)).toBe(theme.layout.searchField.height);
    expect(px(style.maxWidth)).toBe(theme.layout.searchField.width);
  });

  it('gives the frame to the anchor ref and the input to the ref', () => {
    let anchor: HTMLDivElement | null = null;
    let input: HTMLInputElement | null = null;
    render(
      <SearchField
        {...BARE}
        anchorRef={(node) => {
          anchor = node;
        }}
        ref={(node) => {
          input = node;
        }}
      />,
    );
    expect(anchor).toBeInstanceOf(HTMLDivElement);
    expect(input).toBe(screen.getByRole('combobox'));
    expect((anchor as unknown as HTMLElement).contains(input)).toBe(true);
  });
});
