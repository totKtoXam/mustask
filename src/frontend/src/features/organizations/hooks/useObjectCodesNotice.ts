import { useEffect, useRef } from 'react';
import { useNotify } from '../components/notice';

/**
 * «Ваш доступ в … изменился.» (mod-019 design § 7): when a re-read of the object serves other codes than the
 * ones the page showed, one notice per object says so — without naming a role. The first read of an object
 * says nothing; the codes are compared as a set, so a reordered answer is no change.
 */
export function useObjectCodesNotice(
  objectId: string,
  name: string | undefined,
  codes: readonly string[] | null | undefined,
  text: (name: string) => string,
): void {
  const notify = useNotify();
  const seen = useRef<{ objectId: string; key: string } | null>(null);
  const key = codes ? [...codes].sort().join('|') : null;

  useEffect(() => {
    if (key === null || name === undefined) return;
    const last = seen.current;
    seen.current = { objectId, key };
    if (last !== null && last.objectId === objectId && last.key !== key) {
      notify(text(name));
    }
    // `text` is a module-level function of the caller; the codes and the object decide.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [objectId, key, name]);
}
