import { useCallback } from 'react';

/**
 * A callback ref that focuses its element when it mounts — a dialog's first field. The kit's
 * fields take no `autoFocus`; the ref is stable, so a re-render does not take the focus back,
 * and it runs with the field, before the dialog's focus trap looks where the focus is.
 */
export function useFocusOnMount<T extends HTMLElement>(): (element: T | null) => void {
  return useCallback((element: T | null) => element?.focus(), []);
}
