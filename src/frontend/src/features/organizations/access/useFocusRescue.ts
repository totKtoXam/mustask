import { useEffect, useLayoutEffect, useRef } from 'react';
import { MAIN_REGION_ID, focusMainRegion } from '../../../shared/lib/mainRegion';

/**
 * Hands focus to the main region when the control that had it is gone
 * (mod-016 design § 2.2, plan-frontend P-10) — never to `body`. One rule
 * covers a control that disappears because the served codes changed
 * (stepping down from administrator removes the very button that was
 * pressed), a dialog or form that goes with a page turned «Организация не
 * найдена», and the frame that replaces a page.
 *
 * It remembers the last element focused inside `#main-content`. After a commit
 * in which `signature` (the served codes and the page's row) changed, if focus
 * has fallen to `body` and that element is gone, it focuses the main region.
 * A dialog lives outside the main region, so the element remembered while one
 * is open is the control that opened it.
 */
export function useFocusRescue(signature: string): void {
  const lastFocused = useRef<Element | null>(null);

  useEffect(() => {
    const onFocusIn = (event: FocusEvent) => {
      const main = document.getElementById(MAIN_REGION_ID);
      if (main && event.target instanceof Element && main.contains(event.target)) {
        lastFocused.current = event.target;
      }
    };
    document.addEventListener('focusin', onFocusIn);
    return () => document.removeEventListener('focusin', onFocusIn);
  }, []);

  useLayoutEffect(() => {
    const last = lastFocused.current;
    const active = document.activeElement;
    if (last && !last.isConnected && (active === null || active === document.body)) {
      lastFocused.current = null;
      focusMainRegion();
    }
  }, [signature]);
}
