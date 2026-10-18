import { describe, expect, it, vi } from 'vitest';
import { act, fireEvent, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http } from 'msw';
import { gate } from '../../../test/gate';
import { server } from '../../../test/mocks/server';
import { serveWorkItems } from '../testApi';
import { WORK_ITEMS_URL, emptyDataset, ids, item, listFilterDataset } from '../testFixtures';
import { MSK_LIST, renderWorkItems } from '../testRender';

// feat-003 QA pass (qa.md, Frontend): what the developer's suites leave open, written from the
// spec before the code was read. The three `it.fails` are open defects (qa.md D-F1..D-F3): each
// states the behaviour the spec asks for and passes while the defect stands. When a defect is
// fixed its test starts failing; drop `.fails` then.

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

describe('QA D-3 the keyword and a clear in one tick', () => {
  // The blur that pressing «Очистить фильтры» causes writes the typed text, and the click clears
  // it before the router has rendered the first write: the field never sees the address change and
  // keeps the text, though the address and the list carry no keyword.
  it.fails('D-F3: text typed and not yet applied does not outlive «Очистить фильтры»', async () => {
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
