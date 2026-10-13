import { describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Route, Routes, useLocation } from 'react-router-dom';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { HttpResponse, http } from 'msw';
import { server } from '../../../test/mocks/server';
import { NoticeProvider } from '../../../shared/ui';
import { fill, held } from '../components/card/testCard';
import { cardTexts } from '../lib/cardTexts';
import { serveWorkItems, type WorkItemsTestApi } from '../testApi';
import { WORK_ITEM_URL, hierarchyDataset, ids, recordOf } from '../testFixtures';
import { WorkItemFormPage } from './WorkItemFormPage';
import { WORK_ITEM_ROUTE, workItemsPath } from '../paths';

// The page as the card’s host: it reads, keys and hosts (feat-029 US-7, BR-C-3, BR-C-6).

vi.setConfig({ testTimeout: 60_000 });

const STORY = `/organizations/${ids.example}/projects/${ids.msk}/workitems/MSK-US-2`;
const LIST = workItemsPath(ids.example, ids.msk);

interface Entry {
  pathname: string;
  state?: unknown;
}

function Where() {
  const location = useLocation();
  return <p data-testid="where">{`${location.pathname}${location.search}`}</p>;
}

/** The page in its route, inside the providers the shell mounts, with a history of `entries`. */
function renderPage(entries: (string | Entry)[], index = entries.length - 1) {
  const client = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  return render(
    <QueryClientProvider client={client}>
      <NoticeProvider>
        <MemoryRouter initialEntries={entries} initialIndex={index}>
          <Routes>
            <Route path={WORK_ITEM_ROUTE} element={<WorkItemFormPage />} />
            <Route path="*" element={<Where />} />
          </Routes>
        </MemoryRouter>
      </NoticeProvider>
    </QueryClientProvider>,
  );
}

const title = held(() => screen.getByRole('textbox', { name: 'Заголовок' }));
const patches = (api: WorkItemsTestApi) => api.writes().filter((r) => r.method === 'PATCH');

async function openPage(entries: (string | Entry)[], index?: number) {
  const api = serveWorkItems(hierarchyDataset());
  const view = renderPage(entries, index);
  await screen.findByRole('textbox', { name: 'Заголовок' });
  return { api, view };
}

describe('feat-029 US-7: the page is the card', () => {
  it('names the type line as the page’s only h1 and the tab, and has no dialog controls', async () => {
    await openPage([STORY]);

    expect(
      screen.getByRole('heading', { level: 1, name: 'Пользовательская история MSK-US-2' }),
    ).toBeInTheDocument();
    expect(screen.getAllByRole('heading', { level: 1 })).toHaveLength(1);
    await waitFor(() => expect(document.title).toBe('Пользовательская история MSK-US-2 — Mustask'));
    expect(
      screen.queryByRole('button', { name: 'Развернуть на страницу' }),
    ).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Закрыть' })).not.toBeInTheDocument();
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
  });

  it('shows the card’s frame at once while the item is read: an h1 of its own, one «Загрузка данных»', async () => {
    serveWorkItems(hierarchyDataset());
    let release: () => void = () => {};
    const held = new Promise<void>((resolve) => {
      release = resolve;
    });
    server.use(
      http.get(
        WORK_ITEM_URL,
        async () => {
          await held;
          return HttpResponse.json({ status: 500, detail: 'Нет ответа.' }, { status: 500 });
        },
        { once: true },
      ),
    );
    renderPage([STORY]);

    expect(await screen.findByRole('status', { name: 'Загрузка данных' })).toBeInTheDocument();
    expect(
      screen.getByRole('heading', { level: 1, name: 'Рабочий элемент MSK-US-2' }),
    ).toBeVisible();
    expect(screen.getAllByRole('status')).toHaveLength(1);
    release();
    expect(await screen.findByRole('alert')).toHaveTextContent('Нет ответа.');
  });
});

describe('feat-029 BR-C-3: edits the dialog carried to the page', () => {
  it('stand as they stood, with the stored values for every field the user did not touch', async () => {
    const data = hierarchyDataset();
    // Another user changed «Приоритет» before the page was read.
    recordOf(data, 2).fields.priority = 1;
    const api = serveWorkItems(data);
    renderPage([
      {
        pathname: STORY,
        state: { edits: { description: 'Мой текст', title: 'Письмо для входа' } },
      },
    ]);
    await screen.findByRole('textbox', { name: 'Заголовок' });

    expect(title()).toHaveValue('Письмо для входа');
    expect(screen.getByRole('textbox', { name: 'Описание' })).toHaveValue('Мой текст');
    expect(screen.getByRole('combobox', { name: 'Приоритет' })).toHaveTextContent('1');
    expect(screen.getByRole('button', { name: 'Сохранить' })).toBeEnabled();

    await userEvent.click(screen.getByRole('button', { name: 'Сохранить' }));

    await waitFor(() => expect(patches(api)).toHaveLength(1));
    expect(patches(api)[0]?.body).toEqual({ title: 'Письмо для входа', description: 'Мой текст' });
  });

  it('are read once: a page opened by its address starts clean', async () => {
    await openPage([STORY]);

    expect(screen.getByRole('button', { name: 'Сохранить' })).toBeDisabled();
    expect(title()).toHaveValue('Письмо со ссылкой');
  });
});

describe('feat-029 BR-C-8: «Сохранить и закрыть» on the page', () => {
  async function saveAndClose() {
    await userEvent.click(screen.getByRole('button', { name: 'Ещё варианты сохранения' }));
    await userEvent.click(await screen.findByRole('menuitem', { name: 'Сохранить и закрыть' }));
  }

  it('saves, then goes back in history', async () => {
    const { api } = await openPage(['/home', STORY]);
    fill(title(), 'Письмо для входа');

    await saveAndClose();

    expect(await screen.findByTestId('where')).toHaveTextContent('/home');
    expect(patches(api)).toHaveLength(1);
  });

  it('goes to «Рабочие элементы» when the page was the first the user opened', async () => {
    await openPage([STORY]);
    fill(title(), 'Письмо для входа');

    await saveAndClose();

    expect(await screen.findByTestId('where')).toHaveTextContent(LIST);
  });

  it('closes nothing when the save is refused', async () => {
    await openPage(['/home', STORY]);
    fill(title(), '');

    await saveAndClose();

    expect(await screen.findByText('Введите заголовок.')).toBeVisible();
    expect(screen.queryByTestId('where')).not.toBeInTheDocument();
  });
});

describe('feat-029 BR-C-17: the page keeps mod-003’s answers', () => {
  it('replaces the card with the frame of the level that is gone, when a save finds it so', async () => {
    const { api } = await openPage([STORY]);
    const project = api.data.projects.find((candidate) => candidate.id === ids.msk);
    if (project) project.active = false;
    fill(title(), 'Письмо для входа');

    await userEvent.click(screen.getByRole('button', { name: 'Сохранить' }));

    expect(
      await screen.findByRole('heading', { level: 1, name: 'Проект не найден' }),
    ).toBeInTheDocument();
    expect(screen.queryByRole('textbox', { name: 'Заголовок' })).not.toBeInTheDocument();
  });

  it('says «Данные обновлены.» after a refresh, in a notice', async () => {
    await openPage([STORY]);

    await userEvent.click(screen.getByRole('button', { name: 'Обновить' }));

    expect(await screen.findByText(cardTexts.notices.refreshed)).toBeVisible();
  });
});
