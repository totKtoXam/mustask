import { useState } from 'react';
import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { SearchPanel, type SearchPanelProps } from './SearchPanel';

// feat-070 QA: panel edges not pinned by the developer's tests.

function Host(props: Partial<SearchPanelProps> & { withAnchor?: boolean }) {
  const { withAnchor = true, ...rest } = props;
  const [anchor, setAnchor] = useState<HTMLElement | null>(null);
  return (
    <div>
      <input ref={setAnchor} aria-label="Поле" />
      <button type="button">Снаружи</button>
      <SearchPanel
        anchorEl={withAnchor ? anchor : null}
        open
        onClose={() => {}}
        scope={<span>Искать в</span>}
        {...rest}
      >
        <p>Тело</p>
      </SearchPanel>
    </div>
  );
}

describe('feat-070 QA: SearchPanel edges', () => {
  it('draws nothing without an anchor element', () => {
    render(<Host withAnchor={false} />);
    expect(screen.queryByText('Тело')).toBeNull();
  });

  it('has no footer without keys', async () => {
    render(<Host />);
    await screen.findByText('Тело');
    expect(screen.queryByText('↑↓ выбрать')).toBeNull();
  });

  it('shows the keys by default and not in compact mode', async () => {
    const { unmount } = render(<Host keys={<span>↑↓ выбрать</span>} />);
    expect(await screen.findByText('↑↓ выбрать')).toBeInTheDocument();
    unmount();
    render(<Host keys={<span>↑↓ выбрать</span>} compact />);
    await screen.findByText('Тело');
    expect(screen.queryByText('↑↓ выбрать')).toBeNull();
  });

  it('does not ask to close on Esc pressed outside the panel (the host owns the field Esc)', async () => {
    const user = userEvent.setup();
    const onClose = vi.fn();
    render(<Host onClose={onClose} />);
    await screen.findByText('Тело');
    await user.click(screen.getByLabelText('Поле'));
    await user.keyboard('{Escape}');
    expect(onClose).not.toHaveBeenCalled();
  });

  it('leaves the page behind interactive: no scroll lock, no aria-hidden', async () => {
    render(<Host />);
    await screen.findByText('Тело');
    expect(document.body.style.overflow).not.toBe('hidden');
    expect(screen.getByLabelText('Поле').closest('[aria-hidden="true"]')).toBeNull();
    expect(screen.getByRole('button', { name: 'Снаружи' })).toBeEnabled();
  });
});
