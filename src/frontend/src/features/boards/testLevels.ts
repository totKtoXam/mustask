// The level drop-down as a test reads and uses it (feat-001 US-6): the button
// «Уровень: <уровень>» and its menu of the three levels.
import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

/** The level drop-down's button, whatever level it reads; found while a menu hides the page too. */
export function levelButton(): HTMLElement {
  return screen.getByRole('button', { name: /^Уровень: /, hidden: true });
}

/** The open level menu. */
export function levelMenu(): Promise<HTMLElement> {
  return screen.findByRole('menu', { name: /^Уровень: / });
}

/** Opens the drop-down with the pointer and chooses the level named `name`. */
export async function chooseLevel(name: string): Promise<void> {
  await userEvent.click(levelButton());
  await userEvent.click(within(await levelMenu()).getByRole('menuitem', { name }));
}
