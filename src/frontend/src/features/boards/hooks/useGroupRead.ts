// One group's (or lane cell's, or the stream's) rows, read page by page (feat-048 design § 3;
// ADR-0036): the tab's 4-slot queue, a 429 retried by `Retry-After` with the slot released
// meanwhile, the opaque forward cursor, the held-row set by id, the silent reload on a refused
// cursor, and the project-gone answer. Generic: the caller supplies the page fetch.
import { useCallback, useEffect, useReducer, useRef } from 'react';
import { ApiError } from '../../../shared/api/httpClient';
import { acquireSlot } from './readQueue';

export interface ReadPage<T> {
  items: T[];
  nextCursor: string | null;
}

/** 429 retries after the first refusal; the wait is `Retry-After`, else 1 s doubling (OQ-1). */
export const MAX_RETRIES = 3;
const BASE_WAIT_MS = 1000;

export type FirstPhase =
  | 'idle' // closed, nothing asked
  | 'waiting' // asked, no slot yet
  | 'loading'
  | 'retrying' // refused, waiting to try again
  | 'ready'
  | 'refused' // last try refused: «Повторить»
  | 'failed' // any other error: «Повторить»
  | 'gone'; // the project is not found (deactivated meanwhile)

export type MorePhase = 'idle' | 'loading' | 'retrying' | 'refused' | 'failed';

export interface GroupReadState<T> {
  phase: FirstPhase;
  items: T[];
  nextCursor: string | null;
  more: MorePhase;
  /** Bumped when rows were dropped and read again from the first page (cursor reload, D-3). */
  reloads: number;
}

type Action<T> =
  | { type: 'phase'; phase: FirstPhase }
  | { type: 'first'; page: ReadPage<T> }
  | { type: 'more-phase'; more: MorePhase }
  | { type: 'more'; page: ReadPage<T> }
  | { type: 'drop'; id: string }
  | { type: 'reset' }
  | { type: 'reload' };

const INITIAL = { phase: 'idle', items: [], nextCursor: null, more: 'idle', reloads: 0 } as const;

function reducer<T extends { id: string }>(
  state: GroupReadState<T>,
  action: Action<T>,
): GroupReadState<T> {
  switch (action.type) {
    case 'phase':
      return { ...state, phase: action.phase };
    case 'first':
      return {
        ...state,
        phase: 'ready',
        items: dedupe([], action.page.items),
        nextCursor: action.page.nextCursor,
        more: 'idle',
      };
    case 'more-phase':
      return { ...state, more: action.more };
    case 'more':
      return {
        ...state,
        items: dedupe(state.items, action.page.items),
        nextCursor: action.page.nextCursor,
        more: 'idle',
      };
    case 'drop':
      return { ...state, items: state.items.filter((item) => item.id !== action.id) };
    case 'reload':
      return {
        ...state,
        phase: 'loading',
        items: [],
        nextCursor: null,
        more: 'idle',
        reloads: state.reloads + 1,
      };
    case 'reset':
      return { ...INITIAL, items: [], reloads: state.reloads };
  }
}

/** No row twice: a held row wins by id (the panel drops a held row, never shows one twice). */
function dedupe<T extends { id: string }>(held: readonly T[], incoming: readonly T[]): T[] {
  const ids = new Set(held.map((item) => item.id));
  const fresh: T[] = [];
  for (const item of incoming) {
    if (!ids.has(item.id)) {
      ids.add(item.id);
      fresh.push(item);
    }
  }
  return [...held, ...fresh];
}

export function isRefusal(error: unknown): boolean {
  return error instanceof ApiError && error.status === 429;
}

/** A 400 that names the field `cursor` (ADR-0036). */
export function isCursorRefusal(error: unknown): boolean {
  return (
    error instanceof ApiError &&
    error.status === 400 &&
    error.body?.errors !== undefined &&
    'cursor' in error.body.errors
  );
}

export const PROJECT_NOT_FOUND = 'organizations.project.not_found';

export function isProjectGone(error: unknown): boolean {
  return error instanceof ApiError && error.status === 404 && error.errorCode === PROJECT_NOT_FOUND;
}

const sleep = (ms: number) => new Promise<void>((resolve) => setTimeout(resolve, ms));

export interface UseGroupRead<T> {
  state: GroupReadState<T>;
  /** «Показать ещё». */
  loadMore(): void;
  /** «Повторить» of whichever read failed. */
  retry(): void;
  /** Drop the rows and read the first page again (a 409, a card moved into a part-loaded cell). */
  reload(): void;
  /** Take a row out of the held set by id (a row moved to the project's end, OQ-6 A). */
  dropRow(id: string): void;
  /** The error of the last failed read, for the caller's message. */
  lastError: unknown;
}

export function useGroupRead<T extends { id: string }>({
  enabled,
  fetchPage,
  onGone,
  onCursorReload,
}: {
  /** The group is open (or the stream is shown): nothing is read while it is `false`. */
  enabled: boolean;
  fetchPage(cursor: string | null): Promise<ReadPage<T>>;
  /** The project of the group is gone: the caller removes the group and re-reads the headers. */
  onGone?(): void;
  /** A cursor refusal reloaded the group silently: the caller re-reads the headers. */
  onCursorReload?(): void;
}): UseGroupRead<T> {
  const [state, dispatch] = useReducer(
    reducer as (s: GroupReadState<T>, a: Action<T>) => GroupReadState<T>,
    INITIAL as GroupReadState<T>,
  );
  const fetchRef = useRef(fetchPage);
  const cursorRef = useRef<string | null>(null);
  const callbacks = useRef({ onGone, onCursorReload });
  useEffect(() => {
    fetchRef.current = fetchPage;
    cursorRef.current = state.nextCursor;
    callbacks.current = { onGone, onCursorReload };
  });
  // Every start/reset bumps the generation; an answer of an older one is thrown away.
  const generation = useRef(0);
  const lastError = useRef<unknown>(null);

  const failedAt = useRef<'first' | 'more'>('first');

  /** One page through the queue, with the 429 retries; throws the final error. */
  const readPage = useCallback(
    async (cursor: string | null, gen: number, set: (p: 'loading' | 'retrying') => void) => {
      for (let attempt = 0; ; attempt += 1) {
        const release = await acquireSlot();
        if (gen !== generation.current) {
          release();
          return null;
        }
        set('loading');
        try {
          const page = await fetchRef.current(cursor);
          return page;
        } catch (error) {
          if (isRefusal(error) && attempt < MAX_RETRIES) {
            release();
            set('retrying');
            await sleep((error as ApiError).retryAfterMs ?? BASE_WAIT_MS * 2 ** attempt);
            if (gen !== generation.current) return null;
            continue;
          }
          throw error;
        } finally {
          release();
        }
      }
    },
    [],
  );

  const first = useCallback(
    async (silent = false) => {
      const gen = (generation.current += 1);
      failedAt.current = 'first';
      if (!silent) dispatch({ type: 'phase', phase: 'waiting' });
      try {
        const page = await readPage(null, gen, (p) => dispatch({ type: 'phase', phase: p }));
        if (page && gen === generation.current) dispatch({ type: 'first', page });
      } catch (error) {
        if (gen !== generation.current) return;
        lastError.current = error;
        if (isProjectGone(error)) {
          dispatch({ type: 'phase', phase: 'gone' });
          callbacks.current.onGone?.();
        } else {
          dispatch({ type: 'phase', phase: isRefusal(error) ? 'refused' : 'failed' });
        }
      }
    },
    [readPage],
  );

  const more = useCallback(async () => {
    const cursor = cursorRef.current;
    if (cursor === null) return;
    const gen = (generation.current += 1);
    failedAt.current = 'more';
    dispatch({ type: 'more-phase', more: 'loading' });
    try {
      const page = await readPage(cursor, gen, (p) => dispatch({ type: 'more-phase', more: p }));
      if (page && gen === generation.current) dispatch({ type: 'more', page });
    } catch (error) {
      if (gen !== generation.current) return;
      lastError.current = error;
      if (isCursorRefusal(error)) {
        // OQ-4 A (1): no message; rows dropped, the first page read again, headers re-read.
        dispatch({ type: 'reload' });
        callbacks.current.onCursorReload?.();
        await first(true);
      } else if (isProjectGone(error)) {
        dispatch({ type: 'phase', phase: 'gone' });
        callbacks.current.onGone?.();
      } else {
        dispatch({ type: 'more-phase', more: isRefusal(error) ? 'refused' : 'failed' });
      }
    }
  }, [readPage, first]);

  useEffect(() => {
    if (!enabled) return undefined;
    void first();
    return () => {
      generation.current += 1;
      dispatch({ type: 'reset' });
    };
  }, [enabled, first]);

  return {
    state,
    lastError: lastError.current,
    loadMore: useCallback(() => void more(), [more]),
    retry: useCallback(() => {
      if (failedAt.current === 'more') void more();
      else void first();
    }, [first, more]),
    reload: useCallback(() => {
      dispatch({ type: 'reload' });
      void first(true);
    }, [first]),
    dropRow: useCallback((id: string) => dispatch({ type: 'drop', id }), []),
  };
}
