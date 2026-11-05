import { useEffect, useState } from 'react';

/** How long typing pauses before the user search asks the server (design § 3.5). */
export const SEARCH_DEBOUNCE_MS = 250;

/**
 * `value`, once it has stopped changing for `delayMs`. A copy of the work-item
 * slice's hook, deliberately: moving it to `shared/` means editing the slice
 * feat-029 is rewriting, so folding the two user fields into one is the UI
 * kit's job (mod-016 plan-frontend P-7, D-20 OP-3).
 */
export function useDebouncedValue<T>(value: T, delayMs: number = SEARCH_DEBOUNCE_MS): T {
  const [settled, setSettled] = useState(value);
  useEffect(() => {
    const timer = setTimeout(() => setSettled(value), delayMs);
    return () => clearTimeout(timer);
  }, [value, delayMs]);
  return settled;
}
