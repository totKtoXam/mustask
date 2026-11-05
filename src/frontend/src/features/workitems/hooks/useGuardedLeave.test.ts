import { describe, expect, it, vi } from 'vitest';
import { act, renderHook } from '@testing-library/react';
import { useGuardedLeave } from './useGuardedLeave';

describe('feat-029 US-6: one question before leaving a card with edits', () => {
  it('leaves at once, and asks nothing, while nothing is unsaved', () => {
    const view = renderHook(() => useGuardedLeave(false));
    const leave = vi.fn();

    act(() => view.result.current.ask(leave));

    expect(leave).toHaveBeenCalledTimes(1);
    expect(view.result.current.asking).toBe(false);
  });

  it('holds the leave while edits stand, and runs it only when confirmed', () => {
    const view = renderHook(() => useGuardedLeave(true));
    const leave = vi.fn();

    act(() => view.result.current.ask(leave));
    expect(leave).not.toHaveBeenCalled();
    expect(view.result.current.asking).toBe(true);

    act(() => view.result.current.confirm());
    expect(leave).toHaveBeenCalledTimes(1);
    expect(view.result.current.asking).toBe(false);

    act(() => view.result.current.confirm());
    expect(leave).toHaveBeenCalledTimes(1);
  });

  it('drops the leave on «Продолжить редактирование»', () => {
    const view = renderHook(() => useGuardedLeave(true));
    const leave = vi.fn();

    act(() => view.result.current.ask(leave));
    act(() => view.result.current.cancel());

    expect(view.result.current.asking).toBe(false);
    act(() => view.result.current.confirm());
    expect(leave).not.toHaveBeenCalled();
  });
});
