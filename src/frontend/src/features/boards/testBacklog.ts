// Reading the backlog table as a test sees it (feat-001 US-4, US-5): its
// rows top to bottom, a row's cells and tier, a row's own controls.
import { screen, within } from '@testing-library/react';

/** The cells' positions (feat-001 spec § Page «Бэклоги»). */
export const CELL = {
  order: 0,
  handle: 1,
  identifier: 2,
  type: 3,
  title: 4,
  parent: 5,
  assignee: 6,
  state: 7,
  actions: 8,
} as const;

export function findBacklogTable(): Promise<HTMLElement> {
  return screen.findByRole('table', { name: 'Бэклог' });
}

/** The table's body rows, top to bottom; found while an open menu or dialog hides the page. */
export function rowsOf(table: HTMLElement): HTMLElement[] {
  return within(table).getAllByRole('row', { hidden: true }).slice(1);
}

export function cellText(row: HTMLElement, index: number): string {
  return within(row).getAllByRole('cell', { hidden: true })[index]?.textContent ?? '';
}

/** The rows' identifiers, top to bottom. */
export function identifiers(table: HTMLElement): string[] {
  return rowsOf(table).map((row) => cellText(row, CELL.identifier));
}

/** «<ID>» for a top-level row, «  <ID>» per tier for a nested one, top to bottom. */
export function outline(table: HTMLElement): string[] {
  return rowsOf(table).map(
    (row) => `${'  '.repeat(Number(row.dataset.tier ?? 0))}${cellText(row, CELL.identifier)}`,
  );
}

/** The row of `identifier`. */
export function rowOf(table: HTMLElement, identifier: string): HTMLElement {
  const row = rowsOf(table).find(
    (candidate) => cellText(candidate, CELL.identifier) === identifier,
  );
  if (!row) throw new Error(`${identifier} is not a row.`);
  return row;
}

/** A row's «Родитель» cell text. */
export function parentOf(table: HTMLElement, identifier: string): string {
  return cellText(rowOf(table, identifier), CELL.parent);
}

/** «Развернуть <ID>» or «Свернуть <ID>», whichever the row shows now. */
export function expandControl(identifier: string): HTMLElement {
  return screen.getByRole('button', {
    name: new RegExp(`^(Развернуть|Свернуть) ${identifier}$`),
    hidden: true,
  });
}

/** A row's handle; found while an open menu hides the page from the accessibility tree too. */
export function handle(identifier: string): HTMLElement {
  return screen.getByRole('button', { name: `Переместить ${identifier}`, hidden: true });
}

/** The page's announcement region: the `role="status"` that is not the skeleton. */
export function announcer(): HTMLElement {
  const region = screen
    .getAllByRole('status')
    .find((element) => !element.hasAttribute('aria-label'));
  if (!region) throw new Error('No announcement region.');
  return region;
}
