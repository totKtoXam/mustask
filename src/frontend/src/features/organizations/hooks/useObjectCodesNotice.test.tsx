import type { ReactNode } from 'react';
import { describe, expect, it, vi } from 'vitest';
import { renderHook } from '@testing-library/react';
import { NotifyContext } from '../components/notice';
import { useObjectCodesNotice } from './useObjectCodesNotice';

// mod-019 design § 7: one notice per object when a re-read serves other codes, none on the first read.

function setup() {
  const notify = vi.fn();
  const wrapper = ({ children }: { children: ReactNode }) => (
    <NotifyContext.Provider value={notify}>{children}</NotifyContext.Provider>
  );
  const view = renderHook(
    (props: { id: string; codes: string[] | undefined }) =>
      useObjectCodesNotice(props.id, 'Зета', props.codes, (name) => `Изменился доступ: ${name}`),
    { wrapper, initialProps: { id: 'p1', codes: undefined as string[] | undefined } },
  );
  return { notify, view };
}

describe('useObjectCodesNotice', () => {
  it('says nothing on the first read and nothing for a reordered answer', () => {
    const { notify, view } = setup();

    view.rerender({ id: 'p1', codes: ['a', 'b'] });
    view.rerender({ id: 'p1', codes: ['b', 'a'] });

    expect(notify).not.toHaveBeenCalled();
  });

  it('says once, with the object’s name, when the codes differ', () => {
    const { notify, view } = setup();
    view.rerender({ id: 'p1', codes: ['a', 'b'] });

    view.rerender({ id: 'p1', codes: ['a'] });
    view.rerender({ id: 'p1', codes: ['a'] });

    expect(notify).toHaveBeenCalledTimes(1);
    expect(notify).toHaveBeenCalledWith('Изменился доступ: Зета');
  });

  it('does not compare the codes of two different objects', () => {
    const { notify, view } = setup();
    view.rerender({ id: 'p1', codes: ['a', 'b'] });

    view.rerender({ id: 'p2', codes: ['a'] });

    expect(notify).not.toHaveBeenCalled();
  });
});
