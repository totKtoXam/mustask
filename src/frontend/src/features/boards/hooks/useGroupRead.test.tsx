import { act, renderHook, waitFor } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { ApiError } from '../../../shared/api/httpClient';
import { MAX_IN_FLIGHT, acquireSlot, readQueueState } from './readQueue';
import { MAX_RETRIES, useGroupRead, type ReadPage } from './useGroupRead';

interface Row {
  id: string;
}

const page = (ids: string[], next: string | null = null): ReadPage<Row> => ({
  items: ids.map((id) => ({ id })),
  nextCursor: next,
});

const refusal = (retryAfterMs?: number) =>
  new ApiError(429, { status: 429, errorCode: 'http.too_many_requests' }, '/x', retryAfterMs);
const cursorRefusal = () =>
  new ApiError(400, {
    status: 400,
    errors: { cursor: [{ code: 'boards.invalid_cursor', detail: '' }] },
  });

afterEach(() => {
  vi.useRealTimers();
});

describe('readQueue', () => {
  it('grants four slots at once and the fifth when one is released', async () => {
    const releases = await Promise.all(Array.from({ length: MAX_IN_FLIGHT }, () => acquireSlot()));
    expect(readQueueState()).toEqual({ inFlight: 4, waiting: 0 });
    let fifth = false;
    const pending = acquireSlot().then((release) => {
      fifth = true;
      return release;
    });
    await Promise.resolve();
    expect(fifth).toBe(false);
    expect(readQueueState().waiting).toBe(1);
    releases[0]();
    releases[0](); // idempotent
    const release = await pending;
    expect(readQueueState()).toEqual({ inFlight: 4, waiting: 0 });
    release();
    releases.slice(1).forEach((r) => r());
    expect(readQueueState()).toEqual({ inFlight: 0, waiting: 0 });
  });
});

describe('useGroupRead', () => {
  it('reads nothing while closed, the first page when enabled, then more by the cursor', async () => {
    const fetchPage = vi
      .fn<(cursor: string | null) => Promise<ReadPage<Row>>>()
      .mockResolvedValueOnce(page(['a', 'b'], 'c1'))
      .mockResolvedValueOnce(page(['b', 'c'], null));
    const { result, rerender } = renderHook(
      ({ enabled }) => useGroupRead<Row>({ enabled, fetchPage }),
      { initialProps: { enabled: false } },
    );
    expect(fetchPage).not.toHaveBeenCalled();
    rerender({ enabled: true });
    await waitFor(() => expect(result.current.state.phase).toBe('ready'));
    expect(fetchPage).toHaveBeenLastCalledWith(null);
    act(() => result.current.loadMore());
    await waitFor(() => expect(result.current.state.nextCursor).toBeNull());
    expect(fetchPage).toHaveBeenLastCalledWith('c1');
    // The held row b is not shown twice.
    expect(result.current.state.items.map((r) => r.id)).toEqual(['a', 'b', 'c']);
  });

  it('retries a 429 after Retry-After, holding no slot while it waits', async () => {
    vi.useFakeTimers();
    const fetchPage = vi
      .fn<(cursor: string | null) => Promise<ReadPage<Row>>>()
      .mockRejectedValueOnce(refusal(2000))
      .mockResolvedValueOnce(page(['a']));
    const { result } = renderHook(() => useGroupRead<Row>({ enabled: true, fetchPage }));
    await act(async () => {
      await vi.advanceTimersByTimeAsync(0);
    });
    expect(result.current.state.phase).toBe('retrying');
    expect(readQueueState().inFlight).toBe(0);
    await act(async () => {
      await vi.advanceTimersByTimeAsync(1999);
    });
    expect(fetchPage).toHaveBeenCalledTimes(1);
    await act(async () => {
      await vi.advanceTimersByTimeAsync(1);
    });
    expect(result.current.state.phase).toBe('ready');
    expect(fetchPage).toHaveBeenCalledTimes(2);
  });

  it('doubles a 1 s wait without a header, then shows the refusal and retries by hand', async () => {
    vi.useFakeTimers();
    const fetchPage = vi.fn<(cursor: string | null) => Promise<ReadPage<Row>>>();
    fetchPage.mockRejectedValue(refusal());
    const { result } = renderHook(() => useGroupRead<Row>({ enabled: true, fetchPage }));
    await act(async () => {
      await vi.advanceTimersByTimeAsync(1000 + 2000 + 4000);
    });
    expect(fetchPage).toHaveBeenCalledTimes(MAX_RETRIES + 1);
    expect(result.current.state.phase).toBe('refused');
    expect(readQueueState().inFlight).toBe(0);
    fetchPage.mockResolvedValueOnce(page(['z']));
    await act(async () => {
      result.current.retry();
      await vi.advanceTimersByTimeAsync(0);
    });
    expect(result.current.state.phase).toBe('ready');
  });

  it('keeps the rows when «Показать ещё» is refused for good', async () => {
    vi.useFakeTimers();
    const fetchPage = vi.fn<(cursor: string | null) => Promise<ReadPage<Row>>>();
    fetchPage.mockResolvedValueOnce(page(['a'], 'c1'));
    const { result } = renderHook(() => useGroupRead<Row>({ enabled: true, fetchPage }));
    await act(async () => {
      await vi.advanceTimersByTimeAsync(0);
    });
    fetchPage.mockRejectedValue(refusal(10));
    await act(async () => {
      result.current.loadMore();
      await vi.advanceTimersByTimeAsync(100);
    });
    expect(result.current.state.more).toBe('refused');
    expect(result.current.state.items).toHaveLength(1);
    expect(result.current.state.nextCursor).toBe('c1');
  });

  it('reloads silently from the first page when the cursor is refused', async () => {
    const onCursorReload = vi.fn();
    const fetchPage = vi
      .fn<(cursor: string | null) => Promise<ReadPage<Row>>>()
      .mockResolvedValueOnce(page(['a'], 'c1'))
      .mockRejectedValueOnce(cursorRefusal())
      .mockResolvedValueOnce(page(['n1', 'n2'], 'c2'));
    const { result } = renderHook(() =>
      useGroupRead<Row>({ enabled: true, fetchPage, onCursorReload }),
    );
    await waitFor(() => expect(result.current.state.phase).toBe('ready'));
    act(() => result.current.loadMore());
    await waitFor(() => expect(result.current.state.items.map((r) => r.id)).toEqual(['n1', 'n2']));
    expect(fetchPage).toHaveBeenLastCalledWith(null);
    expect(result.current.state.more).toBe('idle');
    expect(result.current.state.phase).toBe('ready');
    expect(onCursorReload).toHaveBeenCalledTimes(1);
  });

  it('reports a deactivated project as gone and shows other errors as failed', async () => {
    const onGone = vi.fn();
    const gone = new ApiError(404, { status: 404, errorCode: 'organizations.project.not_found' });
    const fetchPage = vi
      .fn<(cursor: string | null) => Promise<ReadPage<Row>>>()
      .mockRejectedValue(gone);
    const { result } = renderHook(() => useGroupRead<Row>({ enabled: true, fetchPage, onGone }));
    await waitFor(() => expect(result.current.state.phase).toBe('gone'));
    expect(onGone).toHaveBeenCalledTimes(1);

    const other = vi
      .fn<(cursor: string | null) => Promise<ReadPage<Row>>>()
      .mockRejectedValue(new ApiError(400, { status: 400, errors: { level: [] } }));
    const second = renderHook(() => useGroupRead<Row>({ enabled: true, fetchPage: other }));
    await waitFor(() => expect(second.result.current.state.phase).toBe('failed'));
  });

  it('runs at most four reads in flight across groups', async () => {
    let live = 0;
    let peak = 0;
    const releasers: Array<() => void> = [];
    const fetchPage = () =>
      new Promise<ReadPage<Row>>((resolve) => {
        live += 1;
        peak = Math.max(peak, live);
        releasers.push(() => {
          live -= 1;
          resolve(page(['x']));
        });
      });
    const hooks = Array.from({ length: 10 }, () =>
      renderHook(() => useGroupRead<Row>({ enabled: true, fetchPage })),
    );
    await waitFor(() => expect(releasers.length).toBe(4));
    expect(peak).toBe(4);
    expect(hooks.filter((h) => h.result.current.state.phase === 'waiting')).toHaveLength(6);
    while (hooks.some((h) => h.result.current.state.phase !== 'ready')) {
      const next = releasers.shift();
      if (next) await act(async () => next());
      else await act(async () => Promise.resolve());
    }
    expect(peak).toBe(4);
  });
});
