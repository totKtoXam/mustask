// Reading the board as a test sees it: the board region, a column by its
// name, a column's cards in order, a card's own controls.
import { screen, within } from '@testing-library/react';

/** The board region, «Канбан» since feat-001 (BR-H-16). */
export async function findBoard(): Promise<HTMLElement> {
  return screen.findByRole('region', { name: 'Канбан' });
}

/** Every column's list, in board order. */
export function columnLists(board: HTMLElement): HTMLElement[] {
  return within(board).getAllByRole('list');
}

/** The columns' list names, «<колонка>, рабочих элементов: <n>», in board order. */
export function columnNames(board: HTMLElement): string[] {
  return columnLists(board).map((list) => list.getAttribute('aria-label') ?? '');
}

/** A column's list, by its header. */
export function columnList(board: HTMLElement, header: string): HTMLElement {
  const list = columnLists(board).find((candidate) =>
    candidate.getAttribute('aria-label')?.startsWith(`${header}, `),
  );
  if (!list) throw new Error(`No column «${header}».`);
  return list;
}

/** A column's whole box: its header, its count, its cards; the drop target. */
export function columnBox(board: HTMLElement, header: string): HTMLElement {
  const box = columnList(board, header).closest<HTMLElement>('[data-column]');
  if (!box) throw new Error(`No column box «${header}».`);
  return box;
}

/** A column's cards' identifiers, top to bottom. */
export function cardsIn(board: HTMLElement, header: string): string[] {
  return within(columnList(board, header))
    .queryAllByRole('listitem')
    .map((card) => card.getAttribute('data-card') ?? '');
}

/** The card of `identifier`, wherever it is. */
export function cardOf(board: HTMLElement, identifier: string): HTMLElement {
  const card = within(board)
    .getAllByRole('listitem')
    .find((candidate) => candidate.getAttribute('data-card') === identifier);
  if (!card) throw new Error(`No card ${identifier}.`);
  return card;
}

/** A card's «Ещё действия: <ID>»; found while an open menu hides the page too. */
export function cardMenuButton(identifier: string): HTMLElement {
  return screen.getByRole('button', { name: `Ещё действия: ${identifier}`, hidden: true });
}
