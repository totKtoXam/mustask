import { useCallback, useState } from 'react';

interface MoveViewState<T> {
  /** What is shown instead of the last read. */
  shown: T;
  /** Set once the read after the move has answered: the data the move started from. */
  from?: T;
}

/**
 * What a surface shows around one move (spec § Page «Бэклоги», § Page
 * «Доски»): the last read; while the move is saved, the order or board the
 * move gives, at once; once the read after it has answered, that read as the
 * cache holds it, until the component's own data catches up. TanStack Query
 * resolves the read before it tells the component, so without the last step
 * the data the move started from would show for a moment in between — the
 * moved row or card jumping back and forth, and focus lost with the node.
 *
 * The page caught up when its data is no longer the one the move started from
 * (the read, or anything newer), or already is the read (a read equal to the
 * old data keeps its reference).
 */
export function useMoveView<T>(data: T) {
  const [state, setState] = useState<MoveViewState<T> | null>(null);

  const caughtUp =
    state !== null && state.from !== undefined && (data !== state.from || data === state.shown);
  const begin = useCallback((next: T) => setState({ shown: next }), []);
  const end = useCallback(
    (read: T | undefined, from: T) => setState({ shown: read ?? from, from }),
    [],
  );

  return {
    shown: state === null || caughtUp ? data : state.shown,
    /** A move is being saved: its view is shown and the controls are off. */
    saving: state !== null && state.from === undefined,
    /** The move starts: `next` is shown at once. */
    begin,
    /** The read after the move has answered: `read` from the cache, `from` the data it started on. */
    end,
  };
}
