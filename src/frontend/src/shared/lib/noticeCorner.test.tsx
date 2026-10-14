import { useState } from 'react';
import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { NoticeCornerContext, useNoticeHost } from './noticeCorner';
import { WorkContextContext, type WorkContextSource } from './workContext';

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

/** A host that shows one notice and closes it as the hook says. */
function Host() {
  const [shown, setShown] = useState(true);
  const claim = useNoticeHost(() => setShown(false));
  return (
    <>
      {shown && <p>Заметка страницы</p>}
      <button
        type="button"
        onClick={() => {
          claim();
          setShown(true);
        }}
      >
        Показать
      </button>
    </>
  );
}

function tree(props: {
  fallbackId: number | null;
  roleChangeId: number | null;
  claim?: () => void;
}) {
  return (
    <WorkContextContext.Provider value={source(props.fallbackId)}>
      <NoticeCornerContext.Provider
        value={{ roleChangeId: props.roleChangeId, claim: props.claim ?? (() => {}) }}
      >
        <Host />
      </NoticeCornerContext.Provider>
    </WorkContextContext.Provider>
  );
}

describe('useNoticeHost (mod-016 plan P-6, design § 2.4)', () => {
  it('keeps the host’s notice while the shell raises nothing', () => {
    const { rerender } = render(tree({ fallbackId: null, roleChangeId: null }));

    rerender(tree({ fallbackId: null, roleChangeId: null }));

    expect(screen.getByText('Заметка страницы')).toBeInTheDocument();
  });

  it('closes the host’s notice when the shell raises a fallback notice', () => {
    const { rerender } = render(tree({ fallbackId: null, roleChangeId: null }));

    rerender(tree({ fallbackId: 1, roleChangeId: null }));

    expect(screen.queryByText('Заметка страницы')).not.toBeInTheDocument();
  });

  it('closes the host’s notice when the shell raises a role-change notice', () => {
    const { rerender } = render(tree({ fallbackId: null, roleChangeId: null }));

    rerender(tree({ fallbackId: null, roleChangeId: 1 }));

    expect(screen.queryByText('Заметка страницы')).not.toBeInTheDocument();
  });

  it('closes again for the next notice, but not when a signal is cleared', () => {
    const { rerender } = render(tree({ fallbackId: 1, roleChangeId: null }));
    // The host starts shown; a fallback that is already up when it mounts is not "new".
    expect(screen.getByText('Заметка страницы')).toBeInTheDocument();

    rerender(tree({ fallbackId: null, roleChangeId: null }));
    expect(screen.getByText('Заметка страницы')).toBeInTheDocument();

    rerender(tree({ fallbackId: 2, roleChangeId: null }));
    expect(screen.queryByText('Заметка страницы')).not.toBeInTheDocument();
  });

  it('hands back the shell’s claim, to call when the host shows a notice', async () => {
    const claim = vi.fn();
    render(tree({ fallbackId: null, roleChangeId: 3, claim }));

    await userEvent.click(screen.getByRole('button', { name: 'Показать' }));

    expect(claim).toHaveBeenCalledTimes(1);
  });

  it('is inert outside the shell', () => {
    render(<Host />);

    expect(screen.getByText('Заметка страницы')).toBeInTheDocument();
  });
});
