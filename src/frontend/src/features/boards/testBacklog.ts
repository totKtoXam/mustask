// Reading the backlog table as a test sees it (feat-027 US-1..US-4; design
// § 7): rows are found by their `data-identifier` — there is no ID cell — cells
// by their column's position (design § 4.3), controls by design § 5's names.
import { fireEvent, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

/** The cells' positions (feat-027 design § 4.3). */
export const CELL = {
  hierarchy: 0,
  order: 1,
  type: 2,
  title: 3,
  state: 4,
  effort: 5,
  businessValue: 6,
  valueArea: 7,
} as const;

export function findBacklogTable(): Promise<HTMLElement> {
  return screen.findByRole('table', { name: 'Бэклог' });
}

/** The table's rows of items, top to bottom; the inline add row is not one. */
export function rowsOf(table: HTMLElement): HTMLElement[] {
  return Array.from(table.querySelectorAll<HTMLElement>('tbody > tr[data-identifier]'));
}

/** The inline add row, or `null` while none is open. */
export function addRowOf(table: HTMLElement): HTMLElement | null {
  return table.querySelector<HTMLElement>('tbody > tr[data-add-row]');
}

/** A row's cell at `index` of `CELL`. */
export function cellOf(row: HTMLElement, index: number): HTMLElement {
  const cell = row.querySelectorAll<HTMLElement>(':scope > td')[index];
  if (!cell) throw new Error(`The row has no cell ${index}.`);
  return cell;
}

export function cellText(row: HTMLElement, index: number): string {
  return cellOf(row, index).textContent ?? '';
}

/** The rows' identifiers, top to bottom. */
export function identifiers(table: HTMLElement): string[] {
  return rowsOf(table).map((row) => row.dataset.identifier ?? '');
}

/** «<ID>» for a top-level row, «  <ID>» per tier for a nested one, top to bottom. */
export function outline(table: HTMLElement): string[] {
  return rowsOf(table).map(
    (row) => `${'  '.repeat(Number(row.dataset.tier ?? 0))}${row.dataset.identifier ?? ''}`,
  );
}

/** The rows' «Порядок», top to bottom: blank on a nested row. */
export function orders(table: HTMLElement): string[] {
  return rowsOf(table).map((row) => cellText(row, CELL.order));
}

/** The row of `identifier`. */
export function rowOf(table: HTMLElement, identifier: string): HTMLElement {
  const row = rowsOf(table).find((candidate) => candidate.dataset.identifier === identifier);
  if (!row) throw new Error(`${identifier} is not a row.`);
  return row;
}

/** The title link of a row. */
export function titleLink(row: HTMLElement): HTMLElement {
  return within(cellOf(row, CELL.title)).getByRole('link', { hidden: true });
}

/** «Развернуть <ID>» or «Свернуть <ID>», whichever the row shows now. */
export function expandControl(identifier: string): HTMLElement {
  return screen.getByRole('button', {
    name: new RegExp(`^(Развернуть|Свернуть) ${identifier}$`),
    hidden: true,
  });
}

/** A row's «Ещё действия: <ID>»; found while an open menu hides the page from the accessibility tree too. */
export function actions(identifier: string): HTMLElement {
  return screen.getByRole('button', { name: `Ещё действия: ${identifier}`, hidden: true });
}

/** A row's dotted grip, the source of a drag (it is `aria-hidden`, so found by `data-grip`). */
export function grip(table: HTMLElement, identifier: string): HTMLElement {
  const found = rowOf(table, identifier).querySelector<HTMLElement>('[data-grip]');
  if (!found) throw new Error(`${identifier} has no grip.`);
  return found;
}

/**
 * A row's «+»: «Добавить: <Тип> (<ID>)» for one entry, «Добавить дочерний
 * элемент (<ID>)» for several. Found once the process is read — it names the
 * type — which can be a moment after the table.
 */
export function addControl(identifier: string): Promise<HTMLElement> {
  return screen.findByRole('button', {
    name: new RegExp(`^(Добавить: .+|Добавить дочерний элемент) \\(${identifier}\\)$`),
    hidden: true,
  });
}

/** Opens a row's «⋮» with the pointer and returns its menu. */
export async function openActions(identifier: string): Promise<HTMLElement> {
  await userEvent.click(actions(identifier));
  return screen.findByRole('menu', { name: `Ещё действия: ${identifier}` });
}

/** The page's announcement region: the `role="status"` that is not the skeleton. */
export function announcer(): HTMLElement {
  const region = screen
    .getAllByRole('status')
    .find((element) => !element.hasAttribute('aria-label'));
  if (!region) throw new Error('No announcement region.');
  return region;
}

/** The hierarchy header's two buttons. */
export function expandOneLevel(): HTMLElement {
  return screen.getByRole('button', { name: 'Развернуть на один уровень', hidden: true });
}

export function collapseOneLevel(): HTMLElement {
  return screen.getByRole('button', { name: 'Свернуть на один уровень', hidden: true });
}

/**
 * A click that is only a click: no pointer sequence, no focus move, no delays.
 * The backlog's tests press a row's chevron or a header button dozens of times,
 * and the pointer sequence of `userEvent.click` is what makes them slow; the
 * tests that are about focus or the keyboard keep `userEvent`.
 */
export function press(element: HTMLElement): void {
  fireEvent.click(element);
}
