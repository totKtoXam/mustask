/**
 * The shell's main region (`AppLayout` renders it as `<main id="main-content">`,
 * mod-015 § 3.2): the one place focus goes when the control that had it
 * disappears or a dialog goes with its page (mod-016 design § 2.2, plan P-10),
 * never `body`.
 */
export const MAIN_REGION_ID = 'main-content';

/** Moves focus to the main region; a no-op where the shell is not mounted. */
export function focusMainRegion(): void {
  document.getElementById(MAIN_REGION_ID)?.focus();
}
