/** The main navigation's kept look at 1024 px and wider (BR-10). */
export type NavigationChoice = 'expanded' | 'rail';

/** Per browser: reloads and new tabs read it; clearing storage forgets it. */
export const KEPT_CHOICE_KEY = 'mustask.navigation';

export function readKeptChoice(): NavigationChoice | null {
  try {
    const value = window.localStorage.getItem(KEPT_CHOICE_KEY);
    return value === 'expanded' || value === 'rail' ? value : null;
  } catch {
    // Storage unavailable (a locked-down browser): the width default applies.
    return null;
  }
}

export function writeKeptChoice(choice: NavigationChoice): void {
  try {
    window.localStorage.setItem(KEPT_CHOICE_KEY, choice);
  } catch {
    // Storage unavailable: the choice lasts until the page is left.
  }
}
