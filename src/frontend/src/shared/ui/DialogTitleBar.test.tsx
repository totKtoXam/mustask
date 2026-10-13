import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { DialogTitleBar } from './DialogTitleBar';

// US-9 / mod-015 design § 9.1: the legacy dialogs' title row — the title as the dialog's
// `h2`, «Закрыть» a sibling of it so the heading's name stays the title. ConfirmDialog and
// FormDialog draw it; their own tests go through it. This is its own.

describe('mod-015 Scenario: DialogTitleBar (US-9)', () => {
  it('is the title as an h2 and «Закрыть» beside it, never inside it', () => {
    render(<DialogTitleBar title="Новая область" onClose={() => {}} />);
    const heading = screen.getByRole('heading', { level: 2, name: 'Новая область' });
    const close = screen.getByRole('button', { name: 'Закрыть' });
    expect(heading).not.toContainElement(close);
  });

  it('closes on «Закрыть»', async () => {
    const onClose = vi.fn();
    render(<DialogTitleBar title="Новая область" onClose={onClose} />);
    await userEvent.setup().click(screen.getByRole('button', { name: 'Закрыть' }));
    expect(onClose).toHaveBeenCalledTimes(1);
  });

  it('disables «Закрыть» while a request runs', () => {
    render(<DialogTitleBar title="Новая область" onClose={() => {}} busy />);
    expect(screen.getByRole('button', { name: 'Закрыть' })).toBeDisabled();
  });
});
