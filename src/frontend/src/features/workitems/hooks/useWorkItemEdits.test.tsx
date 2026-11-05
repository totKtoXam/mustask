import type { ReactNode } from 'react';
import { describe, expect, it, vi } from 'vitest';
import { act, renderHook, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { HttpResponse, http } from 'msw';
import { server } from '../../../test/mocks/server';
import { getWorkItem, type WorkItem } from '../api/workItemsApi';
import { workItemsKeys } from '../api/queryKeys';
import { cardTexts } from '../lib/cardTexts';
import type { Edit } from '../lib/edits';
import { patches } from '../testFields';
import { serveProblem, serveWorkItems, type WorkItemsTestApi } from '../testApi';
import {
  WORK_ITEM_URL,
  catalogText,
  hierarchyDataset,
  ids,
  problem,
  recordOf,
  validationProblem,
} from '../testFixtures';
import { useWorkItem } from './queries';
import { useWorkItemEdits, type SaveOutcome, type WorkItemEditsOptions } from './useWorkItemEdits';

const STORY = 'MSK-US-2';

/**
 * The hook over a real read of `identifier` (MSW serving the stories' dataset):
 * the cache holds the item, as the card's page would have read it.
 */
async function setup(
  options: {
    identifier?: string;
    prepare?: (api: WorkItemsTestApi) => void;
    hook?: Partial<WorkItemEditsOptions>;
  } = {},
) {
  const identifier = options.identifier ?? STORY;
  const api = serveWorkItems(hierarchyDataset());
  options.prepare?.(api);
  const client = new QueryClient({
    defaultOptions: {
      queries: { retry: false, staleTime: Infinity },
      mutations: { retry: false },
    },
  });
  const item = await getWorkItem(ids.example, ids.msk, identifier);
  client.setQueryData(workItemsKeys.item(ids.example, ids.msk, identifier), item);
  const notify = vi.fn();
  const onGone = vi.fn();
  const onSaved = vi.fn();
  const view = renderHook(
    () => {
      const query = useWorkItem(ids.example, ids.msk, identifier);
      return useWorkItemEdits({
        organizationId: ids.example,
        projectId: ids.msk,
        identifier,
        item: query.data as WorkItem,
        notify,
        onGone,
        onSaved,
        ...options.hook,
      });
    },
    {
      wrapper: ({ children }: { children: ReactNode }) => (
        <QueryClientProvider client={client}>{children}</QueryClientProvider>
      ),
    },
  );
  return { api, client, item, view, notify, onGone, onSaved, state: () => view.result.current };
}

type Setup = Awaited<ReturnType<typeof setup>>;

async function save(t: Setup): Promise<SaveOutcome> {
  let outcome: SaveOutcome = 'clean';
  await act(async () => {
    outcome = await t.state().save();
  });
  return outcome;
}

function edit(t: Setup, ...edits: Edit[]) {
  for (const next of edits) {
    act(() => t.state().change(next));
  }
}

describe('feat-029 US-4: the touched fields are saved, and only they', () => {
  it('sends the touched fields alone and shows the stored values after the save', async () => {
    const t = await setup({
      prepare: (api) => {
        api.record(ids.msk, 2).fields.risk = 3;
      },
    });
    // Another user changes «Область» after the card was read.
    t.api.record(ids.msk, 2).areaId = ids.api;

    edit(
      t,
      { field: 'title', value: 'Письмо со ссылкой для входа' },
      { field: 'priority', value: '1' },
    );
    expect(t.state().isDirty).toBe(true);

    expect(await save(t)).toBe('saved');

    expect(patches(t.api)).toHaveLength(1);
    expect(patches(t.api)[0]?.body).toEqual({
      title: 'Письмо со ссылкой для входа',
      priority: { value: 1 },
    });
    expect(t.state().edits).toEqual({});
    expect(t.state().isDirty).toBe(false);
    expect(t.notify).toHaveBeenCalledWith(cardTexts.notices.saved);
    expect(t.onSaved).toHaveBeenCalledWith(
      expect.objectContaining({ identifier: STORY, title: 'Письмо со ссылкой для входа' }),
    );
    await waitFor(() => expect(t.state().draft.area.path).toBe('Платформа / API'));
    expect(t.state().draft.title).toBe('Письмо со ссылкой для входа');
    expect(t.state().draft.fields.risk).toBe('3');
  });

  it('has nothing to save while nothing is touched, or a value is changed back', async () => {
    const t = await setup();
    expect(t.state().isDirty).toBe(false);
    expect(await save(t)).toBe('clean');

    edit(t, { field: 'priority', value: '1' }, { field: 'priority', value: '2' });
    expect(t.state().isDirty).toBe(false);
    expect(await save(t)).toBe('clean');
    expect(patches(t.api)).toHaveLength(0);
  });

  it('absorbs a second trigger while a save runs: Enter and a click send one request', async () => {
    const t = await setup();
    edit(t, { field: 'title', value: 'Новый' });

    let outcomes: SaveOutcome[] = [];
    await act(async () => {
      const first = t.state().save();
      const second = t.state().save();
      outcomes = await Promise.all([first, second]);
    });

    expect(outcomes).toEqual(['saved', 'busy']);
    expect(patches(t.api)).toHaveLength(1);
  });

  it('does not read the item again over a save that is on its way', async () => {
    const t = await setup();
    edit(t, { field: 'title', value: 'Новый' });
    const reads = t.api.reads(`/workitems/${STORY}`).length;

    await act(async () => {
      const saving = t.state().save();
      void t.state().refresh();
      await saving;
    });

    expect(t.api.reads(`/workitems/${STORY}`)).toHaveLength(reads);
    expect(t.notify).toHaveBeenCalledTimes(1);
    expect(t.notify).toHaveBeenCalledWith(cardTexts.notices.saved);
  });

  it('closes after a save that was accepted, and only then', async () => {
    const t = await setup();
    const close = vi.fn();

    edit(t, { field: 'title', value: '' });
    await act(async () => {
      expect(await t.state().saveAndClose(close)).toBe('invalid');
    });
    expect(close).not.toHaveBeenCalled();

    edit(t, { field: 'title', value: 'Новый' });
    await act(async () => {
      expect(await t.state().saveAndClose(close)).toBe('saved');
    });
    expect(close).toHaveBeenCalledTimes(1);
  });
});

describe('feat-029 US-4: refusals', () => {
  it('stops at the browser’s checks: nothing is sent, the error stands under the title with focus', async () => {
    const t = await setup();
    edit(t, { field: 'title', value: '' }, { field: 'storyPoints', value: 'пять' });

    expect(await save(t)).toBe('invalid');

    expect(patches(t.api)).toHaveLength(0);
    expect(t.state().errors).toEqual({
      title: 'Введите заголовок.',
      storyPoints: 'Введите число.',
    });
    expect(t.state().focus?.field).toBe('title');

    edit(t, { field: 'title', value: 'Письмо' });
    expect(t.state().errors).toEqual({ storyPoints: 'Введите число.' });
    expect(await save(t)).toBe('invalid');
    expect(t.state().focus?.field).toBe('storyPoints');
  });

  it('puts a refusal for a field on that field and keeps every edit', async () => {
    const t = await setup({ identifier: 'MSK-EPIC-1' });
    serveProblem(
      'patch',
      WORK_ITEM_URL,
      validationProblem({ targetDate: 'workitems.workitem.target_before_start' }),
    );
    edit(t, { field: 'targetDate', value: '2026-12-31' }, { field: 'title', value: 'Вход' });

    expect(await save(t)).toBe('refused');

    expect(t.state().errors).toEqual({
      targetDate: catalogText('workitems.workitem.target_before_start'),
    });
    expect(t.state().alert).toBeNull();
    expect(t.state().focus?.field).toBe('targetDate');
    expect(t.state().edits).toEqual({ title: 'Вход', fields: { targetDate: '2026-12-31' } });

    // «Дата начала» edited clears «Целевая дата»'s error as well.
    edit(t, { field: 'startDate', value: '2026-10-02' });
    expect(t.state().errors).toEqual({});
  });

  it('puts a refusal no field owns in the card’s alert, until the next save', async () => {
    const t = await setup();
    server.use(
      http.patch(WORK_ITEM_URL, () => HttpResponse.json({ detail: 'Сбой.' }, { status: 500 }), {
        once: true,
      }),
    );
    edit(t, { field: 'title', value: 'Новый' });

    expect(await save(t)).toBe('refused');

    expect(t.state().alert).toBe('Сбой.');
    expect(t.state().errors).toEqual({});
    expect(t.state().edits).toEqual({ title: 'Новый' });

    expect(await save(t)).toBe('saved');
    expect(t.state().alert).toBeNull();
  });

  it('hands the frame of an item that has gone to the host', async () => {
    const t = await setup();
    serveProblem('patch', WORK_ITEM_URL, problem('workitems.workitem.not_found'));
    edit(t, { field: 'title', value: 'Новый' });

    expect(await save(t)).toBe('gone');

    expect(t.onGone).toHaveBeenCalledWith('workItem');
  });

  it('reads the item again when the parent changed, shows the stored parent, keeps the other edits', async () => {
    const t = await setup({
      prepare: (api) => {
        api.record(ids.msk, 2).parentId = recordOf(api.data, 6).id;
      },
    });
    expect(t.item.parent?.identifier).toBe('MSK-FEAT-6');
    // Someone gave MSK-US-2 another parent after the card was read.
    t.api.record(ids.msk, 2).parentId = recordOf(t.api.data, 8).id;

    edit(t, { field: 'description', value: 'Мой текст' }, { field: 'parent', value: null });
    expect(await save(t)).toBe('refused');

    expect(t.state().errors).toEqual({ parent: catalogText('workitems.link.parent_changed') });
    expect(t.state().focus?.field).toBe('parent');
    expect(t.state().edits).toEqual({ description: 'Мой текст' });
    await waitFor(() => expect(t.state().draft.parent?.identifier).toBe('MSK-FEAT-8'));

    // The next save sends «Описание» alone, and clears nothing it did not touch.
    expect(await save(t)).toBe('saved');
    expect(patches(t.api).at(-1)?.body).toEqual({ description: 'Мой текст' });
  });
});

describe('feat-029 US-5: refresh and undo', () => {
  it('shows what others changed and keeps what the user touched', async () => {
    const t = await setup();
    edit(t, { field: 'description', value: 'Мой текст' });
    const record = t.api.record(ids.msk, 2);
    record.description = 'Чужой текст';
    record.fields.priority = 1;
    record.changedAt = new Date(2026, 9, 6, 9, 0).toISOString();

    await act(async () => {
      await t.state().refresh();
    });

    expect(t.notify).toHaveBeenCalledWith(cardTexts.notices.refreshedKept);
    expect(t.state().draft.description).toBe('Мой текст');
    expect(t.state().draft.fields.priority).toBe('1');
    expect(t.state().isDirty).toBe(true);
    expect(patches(t.api)).toHaveLength(0);

    expect(await save(t)).toBe('saved');
    expect(patches(t.api)[0]?.body).toEqual({ description: 'Мой текст' });
  });

  it('says only «Данные обновлены.» when nothing is touched', async () => {
    const t = await setup();
    t.api.record(ids.msk, 2).fields.priority = 4;

    await act(async () => {
      await t.state().refresh();
    });

    expect(t.notify).toHaveBeenCalledWith(cardTexts.notices.refreshed);
    expect(t.state().draft.fields.priority).toBe('4');
    expect(t.state().refreshing).toBe(false);
  });

  it('shows a failed read in the card’s alert and leaves the card as it was', async () => {
    const t = await setup();
    edit(t, { field: 'title', value: 'Новый' });
    server.use(
      http.get(WORK_ITEM_URL, () => HttpResponse.json({ detail: 'Сбой.' }, { status: 500 })),
    );

    await act(async () => {
      await t.state().refresh();
    });

    expect(t.state().alert).toBe('Сбой.');
    expect(t.state().edits).toEqual({ title: 'Новый' });
    expect(t.notify).not.toHaveBeenCalled();
  });

  it('hands the frame to the host when a read finds the item gone', async () => {
    const t = await setup();
    serveProblem('get', WORK_ITEM_URL, problem('workitems.workitem.not_found'));

    await act(async () => {
      await t.state().refresh();
    });

    expect(t.onGone).toHaveBeenCalledWith('workItem');
  });

  it('drops every edit, error and alert on undo, and tells the user', async () => {
    const t = await setup();
    edit(t, { field: 'title', value: '' });
    await save(t);
    expect(t.state().errors.title).toBeDefined();

    act(() => t.state().undo());

    expect(t.state().edits).toEqual({});
    expect(t.state().errors).toEqual({});
    expect(t.state().alert).toBeNull();
    expect(t.state().draft.title).toBe('Письмо со ссылкой');
    expect(t.notify).toHaveBeenCalledWith(cardTexts.notices.undone);
  });
});
