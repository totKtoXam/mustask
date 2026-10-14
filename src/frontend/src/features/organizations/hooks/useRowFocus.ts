import { useCallback, useEffect, useRef } from 'react';

/** The rows beside a row, as the list stood before a request removed it. */
export interface RowNeighbours {
  next?: string;
  previous?: string;
}

type PendingFocus =
  ({ kind: 'removed'; removedId: string } & RowNeighbours) | { kind: 'failed'; id: string };

/**
 * Where focus goes after a row's action (mod-016 design § 3.7, § 4.4, the rule
 * of mod-002 § 5.5): once a removed row is gone from the list read again,
 * to the next row's «Действия», else the previous one's, else `fallback()` —
 * the page's or the section's command; after a refusal, back on the row's own
 * button once it is enabled again. `rowIds` is the list in its order; the
 * rows' buttons are registered through `buttonRef(id)`.
 *
 * The neighbours are taken before the request (`neighboursOf`): the list the
 * request's success reads again no longer holds the row.
 */
export function useRowFocus(
  rowIds: readonly string[] | undefined,
  fallback: () => HTMLElement | null | undefined,
) {
  const buttons = useRef(new Map<string, HTMLButtonElement>());
  const pending = useRef<PendingFocus | null>(null);

  // Focus follows the row: once the list is read again without it, or, after
  // a refusal, back on its button once it is enabled again.
  useEffect(() => {
    const target = pending.current;
    if (!target) {
      return;
    }
    if (target.kind === 'failed') {
      const button = buttons.current.get(target.id);
      if (button && !button.disabled) {
        pending.current = null;
        button.focus();
      }
      return;
    }
    if (!rowIds || rowIds.includes(target.removedId)) {
      return;
    }
    pending.current = null;
    const next = target.next ? buttons.current.get(target.next) : undefined;
    const previous = target.previous ? buttons.current.get(target.previous) : undefined;
    (next ?? previous ?? fallback() ?? undefined)?.focus();
  });

  const buttonRef = useCallback(
    (id: string) => (element: HTMLButtonElement | null) => {
      if (element) {
        buttons.current.set(id, element);
      } else {
        buttons.current.delete(id);
      }
    },
    [],
  );

  return {
    buttonRef,
    /** The rows beside `id` in the list as it stands now. */
    neighboursOf(id: string): RowNeighbours {
      const index = rowIds?.indexOf(id) ?? -1;
      return { next: rowIds?.[index + 1], previous: index > 0 ? rowIds?.[index - 1] : undefined };
    },
    /** The row `id` was removed: focus a neighbour once the list is read again without it. */
    afterRemoval(id: string, neighbours: RowNeighbours) {
      pending.current = { kind: 'removed', removedId: id, ...neighbours };
    },
    /** The row `id`'s action was refused: focus its button again. */
    afterFailure(id: string) {
      pending.current = { kind: 'failed', id };
    },
  };
}
