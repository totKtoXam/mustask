import { describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { SearchField } from './SearchField';

// feat-070 QA: field edges.

const BARE = {
  onChange: () => {},
  label: 'Поиск',
  placeholder: 'Поиск…',
  clearLabel: 'Очистить поле',
  onClear: () => {},
};

describe('feat-070 QA: SearchField edges', () => {
  it('shows no hint chip when none is given and the field is empty', () => {
    const { container } = render(<SearchField {...BARE} value="" />);
    expect(container.querySelector('kbd')).toBeNull();
  });

  it('hides the hint from assistive tech', () => {
    const { container } = render(<SearchField {...BARE} value="" hint="Ctrl K" />);
    expect(container.querySelector('kbd')).toHaveAttribute('aria-hidden', 'true');
  });

  it('omits aria-keyshortcuts when none is given and declares a listbox popup', () => {
    render(<SearchField {...BARE} value="" />);
    const input = screen.getByRole('combobox');
    expect(input).not.toHaveAttribute('aria-keyshortcuts');
    expect(input).toHaveAttribute('aria-haspopup', 'listbox');
    expect(input).toHaveAttribute('autocomplete', 'off');
  });

  it('reports the value, not the event, on every keystroke', async () => {
    const user = userEvent.setup();
    const onChange = vi.fn();
    render(<SearchField {...BARE} value="" onChange={onChange} />);
    await user.type(screen.getByRole('combobox'), 'аб');
    expect(onChange).toHaveBeenNthCalledWith(1, 'а');
    expect(onChange).toHaveBeenNthCalledWith(2, 'б');
  });

  it('forwards composition events and focus and blur handlers', async () => {
    const user = userEvent.setup();
    const start = vi.fn();
    const end = vi.fn();
    const onFocus = vi.fn();
    const onBlur = vi.fn();
    render(
      <SearchField
        {...BARE}
        value=""
        onCompositionStart={start}
        onCompositionEnd={end}
        onFocus={onFocus}
        onBlur={onBlur}
      />,
    );
    const input = screen.getByRole('combobox');
    fireEvent.compositionStart(input);
    fireEvent.compositionEnd(input);
    expect(start).toHaveBeenCalledOnce();
    expect(end).toHaveBeenCalledOnce();
    await user.click(input);
    expect(onFocus).toHaveBeenCalledOnce();
    await user.tab();
    expect(onBlur).toHaveBeenCalled();
  });

  it('puts a host id and data attribute on the input', () => {
    render(<SearchField {...BARE} value="" id="search-input" data-testid="field" />);
    expect(screen.getByRole('combobox')).toHaveAttribute('id', 'search-input');
    expect(screen.getByTestId('field')).toBe(screen.getByRole('combobox'));
  });

  it('keeps the combobox name when the clear button is shown', () => {
    render(<SearchField {...BARE} value="x" />);
    expect(screen.getByRole('combobox', { name: 'Поиск' })).toBeInTheDocument();
  });
});
