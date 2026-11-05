/* eslint-disable react-refresh/only-export-components -- a test-only module: the host
   component lives beside the helper that mounts it. */
import { vi } from 'vitest';
import { act, fireEvent, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { pasteInto } from '../../../../test/richTextEditor';
import { renderWithProviders } from '../../../../test/renderWithProviders';
import { useWorkItem } from '../../hooks/queries';
import { serveWorkItems, type WorkItemsTestApi } from '../../testApi';
import { hierarchyDataset, ids, recordOf, type Dataset } from '../../testFixtures';
import type { CardFrameProps } from './cardFrame';
import { PageFrame } from './KitFrames';
import { WorkItemCard, type WorkItemCardProps } from './WorkItemCard';

/** The page's frame, as the page hosts the card. */
function pageFrame(frame: CardFrameProps) {
  return <PageFrame frame={frame} />;
}

export type CardOptions = Partial<Omit<WorkItemCardProps, 'organizationId' | 'projectId' | 'item'>>;

/** What a page or a dialog does for the card: reads the item, keeps it in the cache, hosts it. */
export function CardHost({
  identifier,
  card,
}: {
  identifier: string;
  card: Omit<WorkItemCardProps, 'organizationId' | 'projectId' | 'item'>;
}) {
  const query = useWorkItem(ids.example, ids.msk, identifier);
  if (!query.data) {
    return <p>Чтение…</p>;
  }
  return (
    <WorkItemCard organizationId={ids.example} projectId={ids.msk} item={query.data} {...card} />
  );
}

/** The identifier of each type's item in `hierarchyDataset()`. */
export const ITEM_OF_TYPE = {
  Эпик: 'MSK-EPIC-1',
  Фича: 'MSK-FEAT-6',
  'Пользовательская история': 'MSK-US-2',
  Задача: 'MSK-TASK-3',
  Баг: 'MSK-BUG-4',
} as const;

/**
 * Opens the card of `identifier` on the stories' dataset in the real hook chain
 * (MSW serves the API), with a `notify` and an `onGone` to read.
 */
export async function openCard(
  identifier = 'MSK-US-2',
  options: {
    data?: Dataset;
    /** Prepares the stored data before the card reads it. */
    prepare?: (api: WorkItemsTestApi) => void;
    card?: CardOptions;
    /** False for a card whose «Описание» is not on screen: the lazy editor is not awaited. */
    editor?: boolean;
  } = {},
) {
  const api = serveWorkItems(options.data ?? hierarchyDataset());
  options.prepare?.(api);
  const notify = vi.fn();
  const onGone = vi.fn();
  const view = renderWithProviders(
    <CardHost
      identifier={identifier}
      card={{ mode: 'page', notify, onGone, renderFrame: pageFrame, ...options.card }}
    />,
  );
  await screen.findByRole('textbox', { name: 'Заголовок' });
  if (options.editor !== false) {
    await findDescription();
  }
  return { api, view, notify, onGone };
}

/**
 * The «Описание» editor's body (feat-018). The editor is a lazy chunk, so a card is ready for
 * it only once it has arrived; the first card of a file also pays the chunk's first transform.
 */
export function findDescription(): Promise<HTMLElement> {
  return screen.findByRole('textbox', { name: 'Описание' }, { timeout: 20_000 });
}

/**
 * Inserts `text` into the description at the caret in one paste (one editor transaction, not one per key) and
 * lets the editor report it after its 150 ms delay.
 */
export async function typeDescription(text: string): Promise<void> {
  const body = await findDescription();
  await userEvent.click(body);
  pasteInto(body, { text });
  await act(async () => {
    await new Promise((resolve) => setTimeout(resolve, 200));
  });
}

/**
 * The live region that says `text`. A card holds more than one `role="status"`: the
 * description editor keeps its own notice line (feat-018), always in the DOM.
 */
export function statusSaying(text: string): HTMLElement {
  const match = screen
    .getAllByRole('status')
    .find((element) => element.textContent?.includes(text));
  if (!match) {
    throw new Error(`No status region says ${JSON.stringify(text)}.`);
  }
  return match;
}

/** The record of «Мастаск»'s item `number`, for a test that makes it change under the card. */
export function stored(api: WorkItemsTestApi, number: number) {
  return recordOf(api.data, number);
}

/**
 * Appends `text` to a text control in one change event. Typing key by key costs a
 * render of the kit's field per key; a test that is not about the keys does not pay it.
 */
export function typeInto(element: HTMLElement, text: string): void {
  fill(element, (element as HTMLInputElement).value + text);
}

/**
 * A query that finds its element once and holds it for as long as it is in the document.
 * Re-querying by role and name after every step costs a style computation per element of
 * the page in jsdom (R1-10); a control the card keeps drawing is the same element step
 * after step, and a test that ends gets a new document, so the next query finds a new one.
 */
export function held<T extends HTMLElement>(query: () => T): () => T {
  let element: T | null = null;
  return () => {
    if (element === null || !element.isConnected) {
      element = query();
    }
    return element;
  };
}

/** Replaces a text control's value in one change event. */
export function fill(element: HTMLElement, value: string): void {
  fireEvent.change(element, { target: { value } });
}
