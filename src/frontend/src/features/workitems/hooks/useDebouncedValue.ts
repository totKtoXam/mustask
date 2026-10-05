import { useEffect, useState } from 'react';

/** How long typing pauses before the assignee search asks the server. */
export const SEARCH_DEBOUNCE_MS = 250;

/** `value`, once it has stopped changing for `delayMs`. */
export function useDebouncedValue<T>(value: T, delayMs: number = SEARCH_DEBOUNCE_MS): T {
  const [settled, setSettled] = useState(value);
  useEffect(() => {
    const timer = setTimeout(() => setSettled(value), delayMs);
    return () => clearTimeout(timer);
  }, [value, delayMs]);
  return settled;
}
