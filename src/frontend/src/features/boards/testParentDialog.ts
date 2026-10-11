// The parent write and its dialog «Изменить родителя» as a test drives them
// (feat-001 US-5, feat-027 US-5, US-6): a row's «Ещё действия» opens it, its
// «Родитель» picker lists the allowed parents, and the write is the PUT of
// feat-001 api-contract § 3.2.
import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http } from 'msw';
import { server } from '../../test/mocks/server';
import { WORK_ITEM_PARENT_URL } from '../workitems/testFixtures';
import type { BoardsTestApi } from './testApi';
import { openActions } from './testBacklog';

/** The parent writes the API received. */
export function parentWrites(api: BoardsTestApi) {
  return api.writes().filter((request) => request.path.endsWith('/parent'));
}

/** Opens «Изменить родителя…» from a row's «Ещё действия» and returns the dialog. */
export async function openParentDialog(identifier: string): Promise<HTMLElement> {
  const menu = await openActions(identifier);
  await userEvent.click(within(menu).getByRole('menuitem', { name: 'Изменить родителя…' }));
  return screen.findByRole('dialog', { name: 'Изменить родителя' });
}

/** The dialog's «Родитель» picker. */
export function parentField(dialog: HTMLElement): HTMLElement {
  return within(dialog).getByRole('combobox', { name: 'Родитель' });
}

/** The dialog's «Сохранить». */
export function saveButton(dialog: HTMLElement): HTMLElement {
  return within(dialog).getByRole('button', { name: 'Сохранить' });
}

/** The open list's lines, «<ID> <Заголовок>» without the state. */
export function listedParents(): string[] {
  return within(screen.getByRole('listbox'))
    .getAllByRole('option')
    .map((option) => {
      const primary = option.querySelector('.MuiListItemText-primary');
      return (primary ?? option).textContent ?? '';
    });
}

/** Opens the dialog's «Родитель», waits for the list, and picks the line starting with `identifier`. */
export async function pickParent(dialog: HTMLElement, identifier: string) {
  await userEvent.click(parentField(dialog));
  await userEvent.click(await screen.findByRole('option', { name: new RegExp(`^${identifier} `) }));
}

/** Holds every parent write until `release()`, then lets the in-memory API take it. */
export function holdParentWrites() {
  let release: () => void = () => {};
  const held = new Promise<void>((resolve) => {
    release = resolve;
  });
  server.use(
    http.put(WORK_ITEM_PARENT_URL, async () => {
      await held;
      return undefined;
    }),
  );
  return { release };
}
