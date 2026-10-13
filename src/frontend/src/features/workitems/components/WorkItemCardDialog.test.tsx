import { describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Route, Routes, useLocation, useNavigate } from 'react-router-dom';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { HttpResponse, http } from 'msw';
import { server } from '../../../test/mocks/server';
import { NoticeProvider } from '../../../shared/ui';
import { useWorkItemDialog } from '../hooks/useWorkItemDialog';
import { serveProblem, serveWorkItems, type WorkItemsTestApi } from '../testApi';
import { WORK_ITEM_URL, hierarchyDataset, ids, problem, recordOf } from '../testFixtures';
import { workItemPath } from '../paths';
import { fill, held } from './card/testCard';
import { WorkItemCardDialog } from './WorkItemCardDialog';

// The dialog as a page hosts it (feat-029 US-1, US-6; BR-C-1..BR-C-5, BR-C-21): it opens by the
// address, shows the state the read is in, and closes, expands and switches by the address too.
// The card inside is the kit's fields in jsdom, which are slow.

vi.setConfig({ testTimeout: 60_000 });

const BACKLOG = `/organizations/${ids.example}/teams/t/backlogs/stories`;
const OPEN = `${BACKLOG}?workitem=MSK-US-2`;
const PAGE = workItemPath(ids.example, ids.msk, 'MSK-US-2');

function Where() {
  const location = useLocation();
  return (
    <p data-testid="where" data-state={JSON.stringify(location.state ?? null)}>
      {`${location.pathname}${location.search}`}
    </p>
  );
}

/** A stand-in for the backlog: a titled row, as the real row marks it, and the dialog. */
function Backlog({ onChanged }: { onChanged?: () => void }) {
  const dialog = useWorkItemDialog();
  const navigate = useNavigate();
  return (
    <>
      <Where />
      <div data-identifier="MSK-US-2">
        <a
          href={PAGE}
          onClick={(event) => {
            event.preventDefault();
            dialog.open('MSK-US-2');
          }}
        >
          Письмо со ссылкой
        </a>
      </div>
      <button type="button" onClick={() => navigate(-1)}>
        Назад в истории
      </button>
      <WorkItemCardDialog organizationId={ids.example} projectId={ids.msk} onChanged={onChanged} />
    </>
  );
}

interface Entry {
  pathname: string;
  search?: string;
}

function renderBacklog(
  entries: (string | Entry)[],
  { onChanged, index = entries.length - 1 }: { onChanged?: () => void; index?: number } = {},
) {
  const client = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  return render(
    <QueryClientProvider client={client}>
      <NoticeProvider>
        <MemoryRouter initialEntries={entries} initialIndex={index}>
          <Routes>
            <Route path={BACKLOG} element={<Backlog onChanged={onChanged} />} />
            <Route path="*" element={<Where />} />
          </Routes>
        </MemoryRouter>
      </NoticeProvider>
    </QueryClientProvider>,
  );
}

const where = () => screen.getByTestId('where');
const title = held(() => screen.getByRole('textbox', { name: 'Заголовок' }));
const patches = (api: WorkItemsTestApi) => api.writes().filter((r) => r.method === 'PATCH');

/** Holds the item's read until `release()`; the real handler then answers it. */
function holdRead() {
  let release: () => void = () => {};
  const held = new Promise<void>((resolve) => {
    release = resolve;
  });
  server.use(
    http.get(WORK_ITEM_URL, async () => {
      await held;
      // No answer of its own: MSW passes the request on to the next handler.
      return undefined;
    }),
  );
  return release;
}

async function openLoaded(
  entries: (string | Entry)[] = [OPEN],
  prepare?: (api: WorkItemsTestApi) => void,
) {
  const api = serveWorkItems(hierarchyDataset());
  prepare?.(api);
  const onChanged = vi.fn();
  const view = renderBacklog(entries, { onChanged });
  const dialog = await screen.findByRole('dialog', { name: 'Пользовательская история MSK-US-2' });
  await screen.findByRole('textbox', { name: 'Заголовок' });
  return { api, view, dialog, onChanged };
}

describe('feat-029 US-1: the frame at once, then the card', () => {
  it('shows the frame with one «Загрузка данных» while the item is read, then names the card by its type and identifier', async () => {
    serveWorkItems(hierarchyDataset());
    const release = holdRead();
    renderBacklog([OPEN]);

    const loading = await screen.findByRole('dialog', { name: 'Рабочий элемент MSK-US-2' });
    expect(within(loading).getAllByRole('status')).toHaveLength(1);
    expect(within(loading).getByRole('status')).toHaveAccessibleName('Загрузка данных');
    expect(within(loading).getByRole('button', { name: 'Развернуть на весь экран' })).toBeVisible();
    expect(within(loading).getByRole('button', { name: 'Закрыть' })).toBeVisible();

    release();
    const card = await screen.findByRole('dialog', { name: 'Пользовательская история MSK-US-2' });
    await screen.findByRole('textbox', { name: 'Заголовок' });
    // One dialog before and after the read: not a second one that replaces the first (R1-3).
    expect(card).toBe(loading);
    expect(screen.getAllByRole('dialog')).toHaveLength(1);
    expect(card).toContainElement(document.activeElement as HTMLElement);
    expect(document.activeElement).not.toHaveAttribute('role', 'textbox');
    expect(
      within(card).getByRole('heading', { level: 2, name: 'Пользовательская история MSK-US-2' }),
    ).toBeInTheDocument();
  });

  it('keeps the same dialog element from the first frame through the not-found frame a save finds', async () => {
    const { dialog } = await openLoaded();
    serveProblem('patch', WORK_ITEM_URL, problem('workitems.workitem.not_found'));
    fill(title(), 'Письмо для входа');

    await userEvent.click(screen.getByRole('button', { name: 'Сохранить' }));

    expect(
      await within(dialog).findByRole('heading', { level: 2, name: 'Рабочий элемент не найден' }),
    ).toBeVisible();
    expect(screen.getAllByRole('dialog')).toEqual([dialog]);
    expect(dialog).toHaveAccessibleName('Рабочий элемент MSK-US-2');
    expect(within(dialog).getByRole('button', { name: 'Развернуть на весь экран' })).toBeVisible();
  });

  it('opens from a title with one history entry; Esc goes back and focus returns to the title', async () => {
    serveWorkItems(hierarchyDataset());
    const release = holdRead();
    renderBacklog([BACKLOG]);
    const opener = screen.getByRole('link', { name: 'Письмо со ссылкой' });

    await userEvent.click(opener);

    expect(await screen.findByRole('dialog', { name: 'Рабочий элемент MSK-US-2' })).toBeVisible();
    expect(where()).toHaveTextContent(OPEN);
    expect(where()).toHaveAttribute('data-state', '{"cardDepth":1}');

    await userEvent.keyboard('{Escape}');

    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
    expect(where()).toHaveTextContent(BACKLOG);
    expect(where()).not.toHaveTextContent('workitem');
    expect(opener).toHaveFocus();
    release();
  });

  it('a dialog the address opened closes by replacing it, so Back still leaves the page; focus goes to the title', async () => {
    serveWorkItems(hierarchyDataset());
    const release = holdRead();
    renderBacklog(['/home', OPEN]);
    await screen.findByRole('dialog', { name: 'Рабочий элемент MSK-US-2' });

    await userEvent.click(screen.getByRole('button', { name: 'Закрыть' }));

    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
    expect(where()).toHaveTextContent(BACKLOG);
    expect(where()).not.toHaveTextContent('workitem');
    // The browser has nothing to give focus back to: the dialog was not opened from a title.
    await waitFor(() =>
      expect(screen.getByRole('link', { name: 'Письмо со ссылкой' })).toHaveFocus(),
    );
    await userEvent.click(screen.getByRole('button', { name: 'Назад в истории', hidden: true }));
    expect(where()).toHaveTextContent('/home');
    release();
  });

  it('answers an item that is not there with the frame, the hint and «Закрыть», which drops the parameter', async () => {
    serveWorkItems(hierarchyDataset());
    renderBacklog([`${BACKLOG}?workitem=MSK-US-999`]);

    const dialog = await screen.findByRole('dialog', { name: 'Рабочий элемент MSK-US-999' });
    expect(
      await within(dialog).findByRole('heading', { level: 2, name: 'Рабочий элемент не найден' }),
    ).toBeVisible();
    expect(within(dialog).getByText('Проверьте адрес или закройте окно.')).toBeVisible();
    expect(screen.queryByRole('heading', { level: 1 })).not.toBeInTheDocument();
    expect(within(dialog).getByRole('button', { name: 'Развернуть на весь экран' })).toBeVisible();
    const frame = dialog.querySelector<HTMLElement>('.StateFrame-root');
    if (!frame) throw new Error('No frame.');

    await userEvent.click(within(frame).getByRole('button', { name: 'Закрыть' }));

    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
    expect(where()).toHaveTextContent(BACKLOG);
    expect(where()).not.toHaveTextContent('workitem');
  });

  it('shows a failed read as the banner in the frame, and «Повторить» reads again', async () => {
    serveWorkItems(hierarchyDataset());
    server.use(
      http.get(
        WORK_ITEM_URL,
        () => HttpResponse.json({ detail: 'Сбой чтения.' }, { status: 500 }),
        {
          once: true,
        },
      ),
    );
    renderBacklog([OPEN]);

    const dialog = await screen.findByRole('dialog', { name: 'Рабочий элемент MSK-US-2' });
    const banner = await within(dialog).findByRole('alert');
    expect(banner).toHaveTextContent('Рабочий элемент');
    expect(banner).toHaveTextContent('Сбой чтения.');
    expect(within(dialog).getByRole('button', { name: 'Закрыть' })).toBeVisible();

    await userEvent.click(within(banner).getByRole('button', { name: 'Повторить' }));

    expect(
      await screen.findByRole('dialog', { name: 'Пользовательская история MSK-US-2' }),
    ).toBeVisible();
    await screen.findByRole('textbox', { name: 'Заголовок' });
  });
});

describe('feat-029 US-6: closing and expanding with edits (BR-C-3, BR-C-4)', () => {
  it('asks «Закрыть без сохранения?» on Esc with an edit, keeps it on «Продолжить редактирование», and closes on the answer, sending nothing', async () => {
    const { api } = await openLoaded();
    fill(title(), 'Письмо для входа');

    await userEvent.keyboard('{Escape}');
    const question = await screen.findByRole('dialog', { name: 'Закрыть без сохранения?' });
    expect(question).toHaveTextContent(
      'Несохранённые изменения рабочего элемента MSK-US-2 будут потеряны.',
    );
    await userEvent.click(
      within(question).getByRole('button', { name: 'Продолжить редактирование' }),
    );
    await waitFor(() =>
      expect(screen.queryByRole('dialog', { name: 'Закрыть без сохранения?' })).toBeNull(),
    );
    expect(title()).toHaveValue('Письмо для входа');
    expect(where()).toHaveTextContent(OPEN);

    await userEvent.click(screen.getByRole('button', { name: 'Закрыть' }));
    await userEvent.click(await screen.findByRole('button', { name: 'Закрыть без сохранения' }));

    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
    expect(where()).not.toHaveTextContent('workitem');
    expect(api.writes()).toHaveLength(0);
  });

  it('stretches over the whole window with «Развернуть на весь экран», keeping the address and the edits', async () => {
    await openLoaded(['/home', OPEN]);
    fill(title(), 'Письмо для входа');
    const before = where().textContent;

    await userEvent.click(screen.getByRole('button', { name: 'Развернуть на весь экран' }));

    expect(screen.getByRole('button', { name: 'Свернуть' })).toBeInTheDocument();
    expect(screen.getByRole('dialog')).toBeInTheDocument();
    expect(where().textContent).toBe(before);
    expect(title()).toHaveValue('Письмо для входа');

    await userEvent.click(screen.getByRole('button', { name: 'Свернуть' }));
    expect(screen.getByRole('button', { name: 'Развернуть на весь экран' })).toBeInTheDocument();
  });

  it('leads the type line to the item’s page in a new tab', async () => {
    await openLoaded(['/home', OPEN]);

    const link = within(screen.getByRole('dialog')).getByRole('link', {
      name: /Пользовательская история MSK-US-2/,
    });
    expect(link).toHaveAttribute('href', PAGE);
    expect(link).toHaveAttribute('target', '_blank');
  });
});

describe('feat-029 BR-C-5: a related item in the dialog', () => {
  const withParent = (api: WorkItemsTestApi) => {
    recordOf(api.data, 2).parentId = recordOf(api.data, 6).id;
  };

  it('switches the dialog to the parent with one more history entry, and Back shows the item again', async () => {
    await openLoaded([BACKLOG, OPEN], withParent);
    // The dialog the address opened has no entry of its own: the switch is the first.
    expect(where()).toHaveAttribute('data-state', 'null');

    await userEvent.click(screen.getByRole('link', { name: 'Открыть MSK-FEAT-6' }));

    expect(
      await screen.findByRole('dialog', { name: 'Фича MSK-FEAT-6' }, { timeout: 15_000 }),
    ).toBeVisible();
    expect(where()).toHaveTextContent(`${BACKLOG}?workitem=MSK-FEAT-6`);
    expect(where()).toHaveAttribute('data-state', '{"cardDepth":1,"cardBase":"address"}');

    await userEvent.click(screen.getByRole('button', { name: 'Назад в истории', hidden: true }));

    expect(
      await screen.findByRole('dialog', { name: 'Пользовательская история MSK-US-2' }),
    ).toBeVisible();
    expect(where()).toHaveTextContent(OPEN);
  });
});

describe('feat-029 BR-C-4, BR-C-5: closing after a switch', () => {
  it('goes back over both entries and gives focus to the title that opened the dialog, not to the item switched to', async () => {
    const api = serveWorkItems(hierarchyDataset());
    recordOf(api.data, 2).parentId = recordOf(api.data, 6).id;
    renderBacklog([BACKLOG]);
    const opener = screen.getByRole('link', { name: 'Письмо со ссылкой' });
    await userEvent.click(opener);
    await userEvent.click(await screen.findByRole('link', { name: 'Открыть MSK-FEAT-6' }));
    expect(
      await screen.findByRole('dialog', { name: 'Фича MSK-FEAT-6' }, { timeout: 15_000 }),
    ).toBeVisible();
    expect(where()).toHaveAttribute('data-state', '{"cardDepth":2}');

    await userEvent.keyboard('{Escape}');

    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
    expect(where()).toHaveTextContent(BACKLOG);
    expect(where()).not.toHaveTextContent('workitem');
    await waitFor(() => expect(opener).toHaveFocus());
  });

  it('closes a dialog the address opened at once after a switch (R1-1): the parameter goes, and Back leaves the page', async () => {
    await openLoaded(['/home', OPEN], (api) => {
      recordOf(api.data, 2).parentId = recordOf(api.data, 6).id;
    });
    await userEvent.click(screen.getByRole('link', { name: 'Открыть MSK-FEAT-6' }));
    expect(
      await screen.findByRole('dialog', { name: 'Фича MSK-FEAT-6' }, { timeout: 15_000 }),
    ).toBeVisible();

    await userEvent.keyboard('{Escape}');

    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
    expect(where()).toHaveTextContent(BACKLOG);
    expect(where()).not.toHaveTextContent('workitem');
    await userEvent.click(screen.getByRole('button', { name: 'Назад в истории' }));
    expect(where()).toHaveTextContent('/home');
  });
});

describe('feat-029 US-6: a saved card closes at once (R1-6)', () => {
  it('keeps the user’s place after «Сохранить»: focus is in the title, and Esc closes the dialog with no question', async () => {
    const { api } = await openLoaded();
    fill(title(), 'Письмо для входа');

    await userEvent.click(screen.getByRole('button', { name: 'Сохранить' }));

    await waitFor(() => expect(patches(api)).toHaveLength(1));
    await waitFor(() => expect(title()).toHaveFocus());
    await userEvent.keyboard('{Escape}');

    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
    expect(where()).not.toHaveTextContent('workitem');
  });
});

describe('feat-029 BR-C-21: the page behind reads again after the card changed the item', () => {
  it('tells the host after a save, after «Обновить», and after «Сохранить и закрыть», which then closes', async () => {
    const { api, onChanged } = await openLoaded();
    expect(onChanged).not.toHaveBeenCalled();

    fill(title(), 'Письмо для входа');
    await userEvent.click(screen.getByRole('button', { name: 'Сохранить' }));
    await waitFor(() => expect(onChanged).toHaveBeenCalledTimes(1));
    expect(patches(api)).toHaveLength(1);
    expect(screen.getByRole('dialog', { name: 'Пользовательская история MSK-US-2' })).toBeVisible();

    await userEvent.click(screen.getByRole('button', { name: 'Обновить' }));
    await waitFor(() => expect(onChanged).toHaveBeenCalledTimes(2));

    fill(title(), 'Письмо для входа по ссылке');
    await userEvent.click(screen.getByRole('button', { name: 'Ещё варианты сохранения' }));
    await userEvent.click(await screen.findByRole('menuitem', { name: 'Сохранить и закрыть' }));

    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
    expect(onChanged).toHaveBeenCalledTimes(3);
    expect(where()).not.toHaveTextContent('workitem');
    expect(patches(api).map((request) => request.body)).toEqual([
      { title: 'Письмо для входа' },
      { title: 'Письмо для входа по ссылке' },
    ]);
  });
});
