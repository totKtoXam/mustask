import { useCallback, useEffect, useRef, useState, type RefObject } from 'react';
import { apiErrorText } from '../../../shared/lib/apiErrorText';
import { useNotify } from '../components/notice';

type PendingFocus =
  | { kind: 'restored'; restoredId: string; next?: string; previous?: string }
  | { kind: 'failed'; id: string };

/**
 * «Восстановить» on a deactivated tab (design § 5.5, § 7.5): each row busy on
 * its own, other rows usable; on success the row leaves, the notice, and
 * focus moves to the next row's «Восстановить», else the previous one's,
 * else the selected tab; on failure the row and its focus stay and the error
 * notice carries the server's text.
 */
export function useRestore<R>({
  rows,
  restore,
  notice,
  container,
}: {
  rows: readonly { id: string; name: string }[] | undefined;
  restore(id: string): Promise<R>;
  notice(name: string): string;
  /** The page, to find its selected tab when the list is left empty. */
  container: RefObject<HTMLElement | null>;
}) {
  const notify = useNotify();
  const [busyIds, setBusyIds] = useState<ReadonlySet<string>>(new Set());
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
    if (!rows || rows.some((row) => row.id === target.restoredId)) {
      return;
    }
    pending.current = null;
    const next = target.next ? buttons.current.get(target.next) : undefined;
    const previous = target.previous ? buttons.current.get(target.previous) : undefined;
    const tab = container.current?.querySelector<HTMLElement>('[role="tab"][aria-selected="true"]');
    (next ?? previous ?? tab ?? undefined)?.focus();
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

  async function run(id: string, name: string) {
    const index = rows?.findIndex((row) => row.id === id) ?? -1;
    setBusyIds((current) => new Set(current).add(id));
    try {
      await restore(id);
      pending.current = {
        kind: 'restored',
        restoredId: id,
        next: rows?.[index + 1]?.id,
        previous: index > 0 ? rows?.[index - 1]?.id : undefined,
      };
      notify(notice(name));
    } catch (error) {
      pending.current = { kind: 'failed', id };
      notify(apiErrorText(error), 'error');
    } finally {
      setBusyIds((current) => {
        const next = new Set(current);
        next.delete(id);
        return next;
      });
    }
  }

  return {
    isBusy: (id: string) => busyIds.has(id),
    buttonRef,
    restore: (id: string, name: string) => void run(id, name),
  };
}
