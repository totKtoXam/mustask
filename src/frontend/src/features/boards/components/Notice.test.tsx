import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { NoticeCornerContext } from '../../../shared/lib/noticeCorner';
import { WorkContextContext, type WorkContextSource } from '../../../shared/lib/workContext';
import { useNotice } from './Notice';

// mod-016 design § 2.4, DC-11: the backlog and the board host their own
// notice; it shares the corner with the shell's fallback and role-change
// notices and is never on screen together with either.

function Host() {
  const { notice, showSuccess, showSuccessAlert, showError } = useNotice();
  return (
    <>
      <button type="button" onClick={() => showSuccess('Рабочий элемент PRM-1 создан.')}>
        Создать
      </button>
      <button type="button" onClick={() => showSuccessAlert('Родитель изменён.')}>
        Родитель
      </button>
      <button type="button" onClick={() => showError('Недостаточно прав')}>
        Отказ
      </button>
      {notice}
    </>
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
}) {
  return (
    <WorkContextContext.Provider value={source(options.fallbackId ?? null)}>
      <NoticeCornerContext.Provider
        value={{ roleChangeId: options.roleChangeId ?? null, claim: options.claim ?? (() => {}) }}
      >
        <Host />
      </NoticeCornerContext.Provider>
    </WorkContextContext.Provider>
  );
}

describe('the boards’ notice host shares the corner', () => {
  it('shows a plain notice, a success alert and an error alert as before', async () => {
    render(tree({}));

    await userEvent.click(screen.getByRole('button', { name: 'Создать' }));
    expect(screen.getByText('Рабочий элемент PRM-1 создан.')).toBeInTheDocument();
    expect(document.querySelector('.MuiAlert-root')).toBeNull();

    await userEvent.click(screen.getByRole('button', { name: 'Родитель' }));
    expect(screen.getByText('Родитель изменён.')).toBeInTheDocument();

    await userEvent.click(screen.getByRole('button', { name: 'Отказ' }));
    expect(screen.getByRole('alert')).toHaveTextContent('Недостаточно прав');
  });

  it('closes its notice when the shell raises the switcher’s fallback notice', async () => {
    const { rerender } = render(tree({}));
    await userEvent.click(screen.getByRole('button', { name: 'Отказ' }));

    rerender(tree({ fallbackId: 1 }));

    expect(screen.queryByText('Недостаточно прав')).not.toBeInTheDocument();
  });

  it('closes its notice when the shell raises the role-change notice: a refusal after which the role changed is replaced by the notice that says why', async () => {
    const { rerender } = render(tree({}));
    await userEvent.click(screen.getByRole('button', { name: 'Отказ' }));
    expect(screen.getByText('Недостаточно прав')).toBeInTheDocument();

    rerender(tree({ roleChangeId: 1 }));

    expect(screen.queryByText('Недостаточно прав')).not.toBeInTheDocument();
  });

  it('keeps a refusal after which the role is unchanged', async () => {
    const { rerender } = render(tree({}));
    await userEvent.click(screen.getByRole('button', { name: 'Отказ' }));

    rerender(tree({}));

    expect(screen.getByText('Недостаточно прав')).toBeInTheDocument();
  });

  it('claims the corner for every kind of notice it shows', async () => {
    const claim = vi.fn();
    render(tree({ roleChangeId: 1, claim }));

    await userEvent.click(screen.getByRole('button', { name: 'Создать' }));
    await userEvent.click(screen.getByRole('button', { name: 'Родитель' }));
    await userEvent.click(screen.getByRole('button', { name: 'Отказ' }));

    expect(claim).toHaveBeenCalledTimes(3);
  });
});
