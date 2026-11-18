import type { ReactNode } from 'react';
import { describe, expect, it, vi } from 'vitest';
import { act, renderHook } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { getWorkItem, type WorkItem } from '../api/workItemsApi';
import { workItemsKeys } from '../api/queryKeys';
import { patches } from '../testFields';
import { serveWorkItems } from '../testApi';
import { hierarchyDataset, ids } from '../testFixtures';
import { useWorkItem } from './queries';
import { useWorkItemEdits, type SaveOutcome, type WorkItemEditsOptions } from './useWorkItemEdits';

/**
 * feat-078 QA, frontend (qa.md F-9): a save that waits for the description's uploads
 * (BR-IM-14, design § Save with an upload running: «the form shows its usual saving state»).
 * A test marked `it.fails` pins the open defect D-4 of qa.md and is flipped to `it` by its fix.
 */

const STORY = 'MSK-US-2';

async function setup(hook: Partial<WorkItemEditsOptions>) {
  const api = serveWorkItems(hierarchyDataset());
  const client = new QueryClient({
    defaultOptions: { queries: { retry: false, staleTime: Infinity }, mutations: { retry: false } },
  });
  const item = await getWorkItem(ids.example, ids.msk, STORY);
  client.setQueryData(workItemsKeys.item(ids.example, ids.msk, STORY), item);
  const view = renderHook(
    () => {
      const query = useWorkItem(ids.example, ids.msk, STORY);
      return useWorkItemEdits({
        organizationId: ids.example,
        projectId: ids.msk,
        identifier: STORY,
        item: query.data as WorkItem,
        notify: vi.fn(),
        onGone: vi.fn(),
        onSaved: vi.fn(),
        ...hook,
      });
    },
    {
      wrapper: ({ children }: { children: ReactNode }) => (
        <QueryClientProvider client={client}>{children}</QueryClientProvider>
      ),
    },
  );
  return { api, state: () => view.result.current };
}

function waitingSave(state: () => { save: () => Promise<SaveOutcome> }) {
  let outcome: SaveOutcome | undefined;
  let running: Promise<void> = Promise.resolve();
  act(() => {
    running = state()
      .save()
      .then((value) => {
        outcome = value;
      });
  });
  return { running, outcome: () => outcome };
}

describe('QA F-9: a save waiting for image uploads', () => {
  // D-4: the wait runs before `setSaving(true)`, so the card is not busy and its fields stay open.
  it('shows the usual saving state while it waits (design § Save with an upload running)', async () => {
    let uploaded: () => void = () => {};
    const t = await setup({
      settleDescription: () => new Promise<void>((resolve) => (uploaded = resolve)),
      flushDescription: () => '<p>с картинкой</p>',
    });

    const save = waitingSave(t.state);

    expect(patches(t.api)).toHaveLength(0);
    expect(t.state().saving).toBe(true);

    await act(async () => {
      uploaded();
      await save.running;
    });
    expect(save.outcome()).toBe('saved');
  });

  // D-4: the save sends the edits it captured before the wait, then empties the draft: the title
  // typed during the wait is neither sent nor kept.
  it('does not lose an edit of another field made while the save waits', async () => {
    let uploaded: () => void = () => {};
    const t = await setup({
      settleDescription: () => new Promise<void>((resolve) => (uploaded = resolve)),
      flushDescription: () => '<p>с картинкой</p>',
    });

    const save = waitingSave(t.state);
    // The card is not busy during the wait, so the member can still type into «Название».
    act(() => t.state().change({ field: 'title', value: 'Название во время ожидания' }));

    await act(async () => {
      uploaded();
      await save.running;
    });

    const sent = JSON.stringify(patches(t.api).map((request) => request.body));
    const kept = sent.includes('Название во время ожидания') || t.state().isDirty;
    expect(kept).toBe(true);
  });

  it('a failed upload does not stop the save: the description is sent as the editor holds it', async () => {
    const t = await setup({
      settleDescription: () => Promise.reject(new Error('upload failed')),
      flushDescription: () => '<p>без картинки</p>',
    });

    let outcome: SaveOutcome | undefined;
    await act(async () => {
      outcome = await t.state().save();
    });

    expect(outcome).toBe('saved');
    expect(JSON.stringify(patches(t.api)[0]?.body)).toContain('без картинки');
  });
});
