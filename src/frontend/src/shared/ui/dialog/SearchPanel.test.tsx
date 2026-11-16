import { useState } from 'react';
import { describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { SearchPanel, type SearchPanelProps } from './SearchPanel';

// feat-070 / feat-065 design § 1, § 3.1, D-9: the non-modal dropdown.

function Host(props: Partial<SearchPanelProps>) {
  const [anchor, setAnchor] = useState<HTMLElement | null>(null);
  return (
    <div>
      <input ref={setAnchor} aria-label="Поле" />
      <button type="button">Снаружи</button>
      <SearchPanel
        anchorEl={anchor}
        open
        onClose={() => {}}
        scope={<span>Искать в: Ромашка</span>}
        fullSearch={<button type="button">Полный поиск</button>}
        keys={<span>↑↓ выбрать</span>}
        {...props}
      >
        <ul role="listbox" aria-label="Результаты поиска">
          <li role="option" aria-selected="false">
            Строка
          </li>
        </ul>
      </SearchPanel>
    </div>
  );
}

describe('feat-070 Scenario: SearchPanel (feat-065 design § 1)', () => {
  it('shows the scope row, the body and the key hints', async () => {
    render(<Host />);
    expect(await screen.findByText('Искать в: Ромашка')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Полный поиск' })).toBeInTheDocument();
    expect(screen.getByRole('listbox', { name: 'Результаты поиска' })).toBeInTheDocument();
    expect(screen.getByText('↑↓ выбрать')).toBeInTheDocument();
  });

  it('is not a dialog: no modal, no backdrop, and the focus stays where it was', async () => {
    const user = userEvent.setup();
    render(<Host />);
    await screen.findByText('Искать в: Ромашка');
    await user.click(screen.getByLabelText('Поле'));
    expect(screen.queryByRole('dialog')).toBeNull();
    expect(document.querySelector('[aria-modal]')).toBeNull();
    expect(document.querySelector('.MuiBackdrop-root')).toBeNull();
    expect(screen.getByLabelText('Поле')).toHaveFocus();
  });

  it('draws nothing while closed', () => {
    render(<Host open={false} />);
    expect(screen.queryByText('Искать в: Ромашка')).toBeNull();
  });

  it('asks to close on a click outside, but not on the anchor or inside the panel', async () => {
    const user = userEvent.setup();
    const onClose = vi.fn();
    render(<Host onClose={onClose} />);
    await screen.findByText('Искать в: Ромашка');
    await user.click(screen.getByLabelText('Поле'));
    await user.click(screen.getByText('Строка'));
    expect(onClose).not.toHaveBeenCalled();
    await user.click(screen.getByRole('button', { name: 'Снаружи' }));
    expect(onClose).toHaveBeenCalledWith('clickAway');
  });

  it('asks to close on Esc pressed inside the panel', async () => {
    const user = userEvent.setup();
    const onClose = vi.fn();
    render(<Host onClose={onClose} />);
    await user.click(await screen.findByRole('button', { name: 'Полный поиск' }));
    await user.keyboard('{Escape}');
    expect(onClose).toHaveBeenCalledWith('escape');
  });

  it('keeps the focus in the field when a row is pressed, but lets its own buttons take it', async () => {
    render(<Host />);
    await screen.findByText('Строка');
    screen.getByLabelText('Поле').focus();
    // fireEvent returns false when the event was cancelled (default prevented).
    expect(fireEvent.mouseDown(screen.getByText('Строка'))).toBe(false);
    expect(fireEvent.mouseDown(screen.getByRole('button', { name: 'Полный поиск' }))).toBe(true);
  });

  it('marks the body busy and dims it while a read is pending, rows kept', async () => {
    render(<Host busy />);
    const body = (await screen.findByRole('listbox')).parentElement as HTMLElement;
    expect(body).toHaveAttribute('aria-busy', 'true');
    expect(getComputedStyle(body).opacity).toBe('0.55');
    expect(screen.getByText('Строка')).toBeInTheDocument();
  });

  it('is not busy by default', async () => {
    render(<Host />);
    const body = (await screen.findByRole('listbox')).parentElement as HTMLElement;
    expect(body).not.toHaveAttribute('aria-busy');
    expect(getComputedStyle(body).opacity).toBe('1');
  });

  it('compact hides the key hints and drops the corners', async () => {
    render(<Host compact />);
    await screen.findByText('Строка');
    expect(screen.queryByText('↑↓ выбрать')).toBeNull();
    const panel = document.querySelector('[data-compact="true"]') as HTMLElement;
    expect(panel).not.toBeNull();
    expect(parseFloat(getComputedStyle(panel).borderRadius)).toBe(0);
    expect(screen.getByText('Искать в: Ромашка')).toBeInTheDocument();
  });
});
