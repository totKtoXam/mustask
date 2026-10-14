// Reading a board card's checklist as a test sees it (feat-032): the counters group, a
// counter by its kind, a list by its name, a row by the child's identifier; and holding a
// write on the wire to look at the screen while it is saved.
import { screen, within } from '@testing-library/react';
import { HttpResponse, http } from 'msw';
import { server } from '../../test/mocks/server';
import { cardOf } from './testBoard';
import { WORK_ITEM_URL, WORK_ITEMS_URL } from '../workitems/testFixtures';

/** A card's counters, in the order they are drawn. */
export function countersOfCard(board: HTMLElement, identifier: string): HTMLElement[] {
  const group = within(cardOf(board, identifier)).queryByRole('group', {
    name: `Вложенные элементы: ${identifier}`,
  });
  return group ? within(group).queryAllByRole('button') : [];
}

/** A card's counter of one kind (`task`, `bug`, `testcase`). */
export function counterOf(board: HTMLElement, identifier: string, kind: string): HTMLElement {
  const found = countersOfCard(board, identifier).find(
    (button) => button.getAttribute('data-counter') === kind,
  );
  if (!found) throw new Error(`No ${kind} counter on ${identifier}.`);
  return found;
}

/** A card's open list of a type, by the list's name «Список: <тип> (<ID>)». */
export function listOf(board: HTMLElement, identifier: string, typeName: string): HTMLElement {
  return within(cardOf(board, identifier)).getByRole('list', {
    name: `Список: ${typeName} (${identifier})`,
  });
}

/** The rows of a list, as the child identifiers, top to bottom. */
export function rowsOf(list: HTMLElement): string[] {
  return Array.from(list.querySelectorAll('[data-child]')).map(
    (row) => row.getAttribute('data-child') ?? '',
  );
}

/** One row of a list by the child's identifier. */
export function rowOf(list: HTMLElement, identifier: string): HTMLElement {
  const row = list.querySelector<HTMLElement>(`[data-child="${identifier}"]`);
  if (!row) throw new Error(`No row ${identifier}.`);
  return row;
}

/** A row's checkbox, named «<ID>: выполнено». */
export function checkboxOf(list: HTMLElement, identifier: string): HTMLElement {
  return within(rowOf(list, identifier)).getByRole('checkbox', {
    name: `${identifier}: выполнено`,
  });
}

/** The page's announcement region: the `role="status"` that is not the skeleton. */
export function announcer(): HTMLElement {
  const region = screen
    .getAllByRole('status')
    .find((element) => !element.hasAttribute('aria-label'));
  if (!region) throw new Error('No announcement region.');
  return region;
}

/** Holds every state PATCH until `release()`, then lets the in-memory API take it. */
export function holdPatches() {
  return hold('patch', WORK_ITEM_URL);
}

/** Holds every create POST until `release()`, then lets the in-memory API take it. */
export function holdCreates() {
  return hold('post', WORK_ITEMS_URL);
}

function hold(method: 'patch' | 'post', url: string) {
  let release: () => void = () => {};
  const held = new Promise<void>((resolve) => {
    release = resolve;
  });
  /** The requests that reached the wire: the in-memory API records one only once it takes it. */
  const seen: unknown[] = [];
  server.use(
    http[method](url, async ({ request }) => {
      seen.push(await request.clone().json());
      await held;
      return undefined;
    }),
  );
  return { release, seen };
}

/** Holds the create POST, then answers it with `body` as a refusal. */
export function holdCreateThenRefuse(body: { status: number; errorCode: string; detail: string }) {
  let release: () => void = () => {};
  const held = new Promise<void>((resolve) => {
    release = resolve;
  });
  server.use(
    http.post(WORK_ITEMS_URL, async () => {
      await held;
      return HttpResponse.json(body, {
        status: body.status,
        headers: { 'Content-Type': 'application/problem+json' },
      });
    }),
  );
  return { release };
}
