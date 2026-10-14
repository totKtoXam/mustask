import type { ReactNode } from 'react';
import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { NoticeCornerContext } from '../../lib/noticeCorner';
import { WorkContextContext, type WorkContextSource } from '../../lib/workContext';
import { NoticeProvider } from './NoticeProvider';
import { useNotice } from './useNotice';

// mod-016 design § 2.4, DC-11: the kit's notice host shares the bottom-left corner with
// the shell's fallback and role-change notices and is never on screen together with
// either (the work items' own host of mod-016 is the kit's now).

function Host() {
  const { show } = useNotice();
  return (
    <button type="button" onClick={() => show('Изменения сохранены.')}>
      Сохранить
    </button>
  );
}

function source(fallbackId: number | null): WorkContextSource {
  return {
    status: 'ready',
    organizations: [],
    selection: {},
    isReal: true,
    select: vi.fn(),
    fallback: fallbackId === null ? null : { id: fallbackId, names: ['Альфа'] },
  };
}

function tree(options: {
  fallbackId?: number | null;
  roleChangeId?: number | null;
  claim?: () => void;
}): ReactNode {
  return (
    <WorkContextContext.Provider value={source(options.fallbackId ?? null)}>
      <NoticeCornerContext.Provider
        value={{ roleChangeId: options.roleChangeId ?? null, claim: options.claim ?? (() => {}) }}
      >
        <NoticeProvider>
          <Host />
        </NoticeProvider>
      </NoticeCornerContext.Provider>
    </WorkContextContext.Provider>
  );
}

describe('the notice host shares the corner (mod-016 DC-11)', () => {
  it('shows a notice as before', async () => {
    render(tree({}));

    await userEvent.click(screen.getByRole('button', { name: 'Сохранить' }));

    expect(screen.getByText('Изменения сохранены.')).toBeInTheDocument();
  });

  it('closes its notice when the shell raises the switcher’s fallback notice', async () => {
    const { rerender } = render(tree({}));
    await userEvent.click(screen.getByRole('button', { name: 'Сохранить' }));

    rerender(tree({ fallbackId: 1 }));

    expect(screen.queryByText('Изменения сохранены.')).not.toBeInTheDocument();
  });

  it('closes its notice when the shell raises the role-change notice', async () => {
    const { rerender } = render(tree({}));
    await userEvent.click(screen.getByRole('button', { name: 'Сохранить' }));

    rerender(tree({ roleChangeId: 1 }));

    expect(screen.queryByText('Изменения сохранены.')).not.toBeInTheDocument();
  });

  it('claims the corner when it shows a notice, so the role-change notice closes', async () => {
    const claim = vi.fn();
    render(tree({ roleChangeId: 1, claim }));

    await userEvent.click(screen.getByRole('button', { name: 'Сохранить' }));

    expect(claim).toHaveBeenCalledTimes(1);
    expect(screen.getByText('Изменения сохранены.')).toBeInTheDocument();
  });
});
