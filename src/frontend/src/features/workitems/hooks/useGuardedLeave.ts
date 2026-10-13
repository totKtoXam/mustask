import { useCallback, useState } from 'react';

export interface GuardedLeave {
  /** A leave is waiting for the user's answer: the host shows its confirmation. */
  asking: boolean;
  /**
   * Runs `leave` at once while nothing is unsaved; otherwise holds it and
   * `asking` becomes true. This is the one place a card with edits is left
   * from (D-4): the dialog's own closes go through the shell's `guard`, which
   * does the same, and a switch to a related item goes through here.
   */
  ask: (leave: () => void) => void;
  /** «Закрыть без сохранения» / «Перейти без сохранения»: the held leave runs. */
  confirm: () => void;
  /** «Продолжить редактирование»: the held leave is dropped and the card stays. */
  cancel: () => void;
}

/** One question before leaving a card with unsaved edits. */
export function useGuardedLeave(dirty: boolean): GuardedLeave {
  const [held, setHeld] = useState<(() => void) | null>(null);

  const ask = useCallback(
    (leave: () => void) => {
      if (dirty) {
        setHeld(() => leave);
      } else {
        leave();
      }
    },
    [dirty],
  );
  const confirm = useCallback(() => {
    setHeld(null);
    held?.();
  }, [held]);
  const cancel = useCallback(() => setHeld(null), []);

  return { asking: held !== null, ask, confirm, cancel };
}
