import { describe, expect, it, vi } from 'vitest';
import { Route, useNavigate } from 'react-router-dom';
import { act, fireEvent, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http } from 'msw';
import { gate } from '../../../test/gate';
import { server } from '../../../test/mocks/server';
import { authValue, renderShell } from '../../../test/renderShell';
import { OrganizationGate } from '../../organizations';
import { WORK_ITEMS_ROUTE } from '../paths';
import { serveWorkItems } from '../testApi';
import { WORK_ITEMS_URL, emptyDataset, ids, item, listFilterDataset } from '../testFixtures';
import { MSK_LIST, projectContext, renderWorkItems } from '../testRender';
import { WorkItemsPage } from './WorkItemsPage';

// feat-003 QA pass (qa.md, Frontend): what the developer's suites leave open, written from the
// spec before the code was read. Each `it.fails` is an open defect (qa.md D-F1..D-F4): it states
// the behaviour the spec asks for and passes while the defect stands. When a defect is fixed its
// test starts failing; drop `.fails` then.

vi.setConfig({ testTimeout: 120_000 });

const enc = encodeURIComponent;

const band = () => screen.getByRole('group', { name: 'Фильтры' });
const table = () => screen.findByRole('table', { name: 'Рабочие элементы' });
const rowsOf = (element: HTMLElement) => within(element).getAllByRole('row').slice(1);
const addressSearch = () => screen.getByTestId('location').getAttribute('data-search') ?? '';
const keyword = () => within(band()).getByRole('textbox', { name: 'Поиск' });

async function listed(): Promise<string[]> {
  return rowsOf(await table()).map((row) => within(row).getAllByRole('cell')[0]?.textContent ?? '');
}

async function pickState(name: string) {
  await userEvent.click(within(band()).getByRole('combobox', { name: 'Статус' }));
  await userEvent.click(await screen.findByRole('option', { name }));
  await userEvent.keyboard('{Escape}');
  await waitFor(() => expect(screen.queryByRole('listbox')).not.toBeInTheDocument());
}

/** 120 live «Новая» tasks: three pages unfiltered, and the same three under «Новая». */
function manyTasks() {
  const data = emptyDataset();
  for (let number = 1; number <= 120; number += 1) {
    data.items.push(
      item(ids.msk, number, 'TASK', `Задача ${number}`, {
        changedAt: new Date(2026, 9, 1, 0, number).toISOString(),
      }),
    );
  }
  return data;
}

describe('QA BR-WS-11 the page returns to 1 whenever any filter’s content changes', () => {
  // The page number is kept per filter content, so coming back to an earlier content (clearing the
  // filter just set) brings that content's old page back.
  it.fails('D-F1: clearing a filter set from page 2 shows the wider list on page 1', async () => {
    const api = serveWorkItems(manyTasks());
    renderWorkItems(MSK_LIST);
    await table();
    await userEvent.click(screen.getByRole('button', { name: 'Перейти на следующую страницу' }));
    await screen.findByText('51–100 из 120');

    await pickState('Новая');
    await screen.findByText('1–50 из 120');

    await pickState('Новая'); // unticks it: the filter is empty again
    await waitFor(() => expect(addressSearch()).toBe(''));

    await screen.findByText('1–50 из 120', {}, { timeout: 3_000 });
    expect(api.reads('/workitems?').at(-1)?.path).toContain('page=1');
  });
});

describe('QA design § 5 states 3 and 7: a change being read', () => {
  // Leaving the filtered-empty frame by «Сбросить фильтр»: until the wider list is read, the page
  // says the project has no items. The previous (empty) data is kept as placeholder while the
  // frame is chosen from the new address.
  it.fails(
    'D-F2: «Сбросить фильтр» never says «Рабочих элементов пока нет» on the way',
    async () => {
      serveWorkItems(listFilterDataset());
      renderWorkItems(`${MSK_LIST}?state=${enc('Закрыта')}&teamId=${ids.venusTeam}`);
      expect(
        await screen.findByRole('heading', { level: 2, name: 'Ничего не найдено' }),
      ).toBeVisible();

      const held = gate();
      server.use(
        http.get(WORK_ITEMS_URL, async ({ request }) => {
          const query = new URL(request.url).searchParams;
          if (!query.has('state') && !query.has('teamId')) {
            await held.opened;
          }
          return undefined;
        }),
      );

      await userEvent.click(screen.getByRole('button', { name: 'Сбросить фильтр' }));
      await waitFor(() => expect(addressSearch()).toBe(''));

      try {
        expect(screen.queryByRole('heading', { name: 'Рабочих элементов пока нет' })).toBeNull();
      } finally {
        held.open();
      }
    },
  );
});

describe('QA D-3 a typed keyword and what clears it', () => {
  // The field's blur handler calls `setText` with the text it already has. When the address then
  // drops the keyword (a clear, a link to the unfiltered list), the reset the field makes in its
  // render is replayed over by that update: the text comes back, the pause (250 ms) re-applies it,
  // and the address carries the keyword again. Only a keyword that was typed here and left the
  // field is affected; one that came from a link clears. Also seen in headless Chrome 155 on a
  // production build (the field beside a router link): the control without the blur clears.
  it.fails('D-F3: «Очистить фильтры» clears a keyword that was typed and applied', async () => {
    serveWorkItems(listFilterDataset());
    renderWorkItems(`${MSK_LIST}?state=${enc('Новая')}`);
    await table();

    await userEvent.type(keyword(), 'оплат{Enter}');
    await waitFor(() => expect(addressSearch()).toContain('search='));
    await new Promise((resolve) => setTimeout(resolve, 400));
    await userEvent.click(within(band()).getByRole('button', { name: 'Очистить фильтры' }));
    await new Promise((resolve) => setTimeout(resolve, 800));

    expect(addressSearch()).toBe('');
    expect(keyword()).toHaveValue('');
  });

  it.fails('D-F3: the main navigation’s «Рабочие элементы» opens the list unfiltered', async () => {
    serveWorkItems(listFilterDataset());
    renderWorkItems(MSK_LIST);
    await table();

    await userEvent.type(keyword(), 'оплат{Enter}');
    await waitFor(() => expect(addressSearch()).toContain('search='));
    await new Promise((resolve) => setTimeout(resolve, 400));
    const navigation = screen.getByRole('navigation', { name: 'Основное меню' });
    await userEvent.click(within(navigation).getByRole('link', { name: 'Рабочие элементы' }));
    await new Promise((resolve) => setTimeout(resolve, 800));

    expect(addressSearch()).toBe('');
    expect(keyword()).toHaveValue('');
  });
});

describe('QA D-3 the keyword and a clear in one tick', () => {
  // The blur that pressing «Очистить фильтры» causes writes the typed text, and the click clears
  // it before the router has rendered the first write: the field never sees the address change and
  // keeps the text, though the address and the list carry no keyword.
  it.fails('D-F4: text typed and not yet applied does not outlive «Очистить фильтры»', async () => {
    serveWorkItems(listFilterDataset());
    renderWorkItems(`${MSK_LIST}?state=${enc('Новая')}`);
    await table();
    const input = keyword();
    const clear = within(band()).getByRole('button', { name: 'Очистить фильтры' });

    input.focus();
    act(() => {
      fireEvent.change(input, { target: { value: 'abc' } });
    });
    act(() => {
      fireEvent.blur(input);
      fireEvent.click(clear);
    });

    await waitFor(() => expect(addressSearch()).toBe(''));
    await new Promise((resolve) => setTimeout(resolve, 600));
    expect(keyword()).toHaveValue('');
  });
});

describe('QA BR-WS-9, BR-WS-11 the keyword’s characters survive the address', () => {
  it('a keyword of reserved URL characters is written, kept by a reload and sent whole', async () => {
    const data = emptyDataset();
    data.items.push(item(ids.msk, 1, 'TASK', 'Скидка 100% & #1 + бонус'));
    const api = serveWorkItems(data);
    const text = '100% & #1 +';
    renderWorkItems(MSK_LIST);
    await table();

    await userEvent.type(keyword(), `${text}{Enter}`);
    await waitFor(async () => expect(await listed()).toEqual(['MSK-TASK-1']));

    expect(new URLSearchParams(addressSearch()).get('search')).toBe(text);
    expect(addressSearch()).not.toContain('#');
    const read = api.reads('/workitems?').at(-1)?.path ?? '';
    expect(new URL(read, 'http://x').searchParams.get('search')).toBe(text);
  });

  it('the same link opened afresh fills the field with the same text and lists the same rows', async () => {
    const data = emptyDataset();
    data.items.push(
      item(ids.msk, 1, 'TASK', 'Скидка 100% & #1 + бонус'),
      item(ids.msk, 2, 'TASK', 'Другое'),
    );
    serveWorkItems(data);
    renderWorkItems(`${MSK_LIST}?search=${enc('100% & #1 +')}`);

    await waitFor(async () => expect(await listed()).toEqual(['MSK-TASK-1']));
    expect(keyword()).toHaveValue('100% & #1 +');
  });
});

describe('QA design § 9 the keyboard', () => {
  it('Tab visits «Поиск», «Статус», «Исполнитель», «Команда» and «Очистить фильтры», in that order', async () => {
    serveWorkItems(listFilterDataset());
    renderWorkItems(`${MSK_LIST}?state=${enc('Новая')}`);
    await table();

    keyword().focus();
    for (const [role, name] of [
      ['combobox', 'Статус'],
      ['combobox', 'Исполнитель'],
      ['combobox', 'Команда'],
      ['button', 'Очистить фильтры'],
    ] as const) {
      await userEvent.tab();
      expect(within(band()).getByRole(role, { name })).toHaveFocus();
    }
  });
});

function BackButton() {
  const navigate = useNavigate();
  return (
    <button type="button" onClick={() => void navigate(-1)}>
      Назад в истории
    </button>
  );
}

describe('QA BR-WS-11 the address and the history', () => {
  // US-1 "The filter lives in the address": going Back after choosing leaves the page, it does not
  // step through the choices (the entry is replaced; the list before it is the one navigated from).
  it('Back after several changes leaves «Рабочие элементы»', async () => {
    serveWorkItems(listFilterDataset());
    renderShell({
      path: '/elsewhere',
      auth: authValue([]),
      contextSource: projectContext(),
      access: 'admin',
      routes: (
        <>
          <Route element={<OrganizationGate />}>
            <Route
              path={WORK_ITEMS_ROUTE}
              element={
                <>
                  <WorkItemsPage />
                  <BackButton />
                </>
              }
            />
          </Route>
          <Route path="*" element={<p>Другая страница</p>} />
        </>
      ),
    });
    const navigation = () => screen.getByRole('navigation', { name: 'Основное меню' });
    await userEvent.click(within(navigation()).getByRole('link', { name: 'Рабочие элементы' }));
    await table();

    await pickState('Новая');
    await pickState('Разрешена');
    await userEvent.type(keyword(), 'оплат{Enter}');
    await waitFor(() => expect(addressSearch()).toContain('search='));
    expect(addressSearch()).toContain('state=');

    await userEvent.click(screen.getByRole('button', { name: 'Назад в истории' }));

    expect(await screen.findByText('Другая страница')).toBeInTheDocument();
  });
});
