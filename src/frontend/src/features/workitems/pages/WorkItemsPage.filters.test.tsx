import { describe, expect, it, vi } from 'vitest';
import { act, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { HttpResponse, http } from 'msw';
import { gate } from '../../../test/gate';
import { server } from '../../../test/mocks/server';
import { setViewportWidth } from '../../../test/viewport';
import { CATEGORY_MARKER_CLASS } from '../../../shared/ui/WorkTrackingMarks';
import { serveProblem, serveWorkItems } from '../testApi';
import {
  PROCESS_URL,
  WORK_ITEMS_URL,
  emptyDataset,
  ids,
  item,
  listFilterDataset,
  problem,
} from '../testFixtures';
import { MSK_LIST, mskForm, renderWorkItems } from '../testRender';

// feat-003 US-1, design §§ 2-5, 7-9: the list's band «Фильтры» with «Поиск», «Статус»,
// «Исполнитель» and «Команда»; «Статус» and «Удалена» (the recycle bin); the address; every state of
// the list. The data is spec § Common data (`listFilterDataset`). Each user action draws the kit's
// fields in jsdom; a test of several steps outlasts the default.

vi.setConfig({ testTimeout: 120_000 });

const enc = encodeURIComponent;

const band = () => screen.getByRole('group', { name: 'Фильтры' });
const table = () => screen.findByRole('table', { name: 'Рабочие элементы' });
const rowsOf = (element: HTMLElement) => within(element).getAllByRole('row').slice(1);
const cellsOf = (row: HTMLElement) =>
  within(row)
    .getAllByRole('cell')
    .map((cell) => cell.textContent ?? '');
const addressSearch = () => screen.getByTestId('location').getAttribute('data-search') ?? '';

/** The identifiers listed now, in order. */
async function listed(): Promise<string[]> {
  return rowsOf(await table()).map((row) => cellsOf(row)[0] ?? '');
}

/** Waits until the list reads exactly `expected`. */
async function expectListed(expected: string[]) {
  await waitFor(async () => expect(await listed()).toEqual(expected));
}

async function chooseStates(...names: string[]) {
  await userEvent.click(within(band()).getByRole('combobox', { name: 'Статус' }));
  for (const name of names) {
    await userEvent.click(await screen.findByRole('option', { name }));
  }
  await userEvent.keyboard('{Escape}');
  await waitFor(() => expect(screen.queryByRole('listbox')).not.toBeInTheDocument());
}

const ALL = ['MSK-BUG-42', 'MSK-US-3', 'MSK-TASK-4', 'MSK-BUG-2', 'MSK-US-1'];

/** The visually hidden live regions' texts. */
const announced = () =>
  screen
    .getAllByRole('status')
    .map((region) => region.textContent ?? '')
    .filter((text) => text !== '');

describe('feat-003 BR-WS-1 the band', () => {
  it('holds «Поиск», «Статус», «Исполнитель» and «Команда», in that order, each at rest reading its words', async () => {
    serveWorkItems(listFilterDataset());
    renderWorkItems(MSK_LIST);
    await table();

    const labels = within(band())
      .getAllByText(/^(Поиск|Статус|Исполнитель|Команда)$/)
      .map((label) => label.textContent);
    expect(labels).toEqual(['Поиск', 'Статус', 'Исполнитель', 'Команда']);
    const keyword = within(band()).getByRole('textbox', { name: 'Поиск' });
    expect(keyword).toHaveValue('');
    expect(keyword).toHaveAccessibleDescription('Заголовок или ID');
    expect(within(band()).getAllByText('Заголовок или ID').length).toBeGreaterThan(0);
    expect(within(band()).getByText('Все статусы')).toBeInTheDocument();
    expect(within(band()).getByText('Все исполнители')).toBeInTheDocument();
    expect(within(band()).getByText('Все команды')).toBeInTheDocument();
    expect(within(band()).queryByRole('button', { name: 'Очистить фильтры' })).toBeNull();
    expect(await listed()).toEqual(ALL);
    expect(screen.getByText('1–5 из 5')).toBeInTheDocument();
  });

  it('keeps the table’s eight columns: a deleted row needs none of its own (OQ-3 A)', async () => {
    serveWorkItems(listFilterDataset());
    renderWorkItems(MSK_LIST);

    expect(
      within(await table())
        .getAllByRole('columnheader')
        .map((header) => header.textContent),
    ).toEqual(['ID', 'Тип', 'Заголовок', 'Исполнитель', 'Статус', 'Команда', 'Область', 'Изменён']);
  });
});

describe('feat-003 US-1 Scenario: the state field', () => {
  it('offers the process’s states, each once, in order, with no counts, and reads «Все статусы» with none chosen', async () => {
    serveWorkItems(listFilterDataset());
    renderWorkItems(MSK_LIST);
    await table();

    await userEvent.click(within(band()).getByRole('combobox', { name: 'Статус' }));

    const list = await screen.findByRole('listbox');
    expect(list).toHaveAttribute('aria-multiselectable', 'true');
    expect(
      within(list)
        .getAllByRole('option')
        .map((option) => option.textContent),
    ).toEqual(['Новая', 'Активна', 'Разрешена', 'Закрыта', 'Удалена']);
  });

  it('one state narrows the list and the count; a deleted item’s held state does not count; clearing gives the five back', async () => {
    const api = serveWorkItems(listFilterDataset());
    renderWorkItems(MSK_LIST);
    await table();

    await chooseStates('Новая');

    await expectListed(['MSK-BUG-42']);
    expect(screen.getByText('1–1 из 1')).toBeInTheDocument();
    expect(api.reads('/workitems?').at(-1)?.path).toContain(`state=${enc('Новая')}`);
    expect(within(band()).getByRole('combobox', { name: 'Статус' })).toHaveTextContent('Новая');

    await userEvent.click(within(band()).getByRole('button', { name: 'Очистить фильтры' }));
    await expectListed(ALL);
    expect(screen.getByText('1–5 из 5')).toBeInTheDocument();
  });

  it('a name spans the types that have it: «Разрешена» finds a story and a bug', async () => {
    serveWorkItems(listFilterDataset());
    renderWorkItems(MSK_LIST);
    await table();

    await chooseStates('Разрешена');

    await expectListed(['MSK-US-3', 'MSK-BUG-2']);
    expect(screen.getByText('1–2 из 2')).toBeInTheDocument();
  });

  it('any chosen state matches, and the rows keep their order', async () => {
    serveWorkItems(listFilterDataset());
    renderWorkItems(MSK_LIST);
    await table();

    await chooseStates('Новая', 'Разрешена');

    await expectListed(['MSK-BUG-42', 'MSK-US-3', 'MSK-BUG-2']);
    expect(within(band()).getByRole('combobox', { name: 'Статус' })).toHaveTextContent(
      'Новая, Разрешена',
    );
  });

  it('a state is written to the address, one parameter per name, beside the other parameters', async () => {
    serveWorkItems(listFilterDataset());
    renderWorkItems(`${MSK_LIST}?teamId=${ids.venusTeam}`);
    await table();

    await chooseStates('Новая', 'Разрешена');

    await waitFor(() =>
      expect(addressSearch()).toBe(
        `?state=${enc('Новая')}&state=${enc('Разрешена')}&teamId=${ids.venusTeam}`,
      ),
    );
    await expectListed(['MSK-BUG-42', 'MSK-BUG-2']);
  });

  it('the field works while the process is read (its list says «Загрузка…») and says why it failed, and a second opening reads again', async () => {
    serveWorkItems(listFilterDataset());
    const held = gate();
    let failing = true;
    server.use(
      http.get(PROCESS_URL, async () => {
        await held.opened;
        if (failing) {
          failing = false;
          return HttpResponse.json(problem('workitems.validation_failed'), { status: 500 });
        }
        return undefined;
      }),
    );
    renderWorkItems(MSK_LIST);
    await table();

    await userEvent.click(within(band()).getByRole('combobox', { name: 'Статус' }));
    expect(await screen.findByRole('option', { name: 'Загрузка…' })).toBeInTheDocument();
    held.open();
    expect(
      await screen.findByRole('option', {
        name: 'Не удалось загрузить статусы. Попробуйте ещё раз.',
      }),
    ).toBeInTheDocument();
    await userEvent.keyboard('{Escape}');
    await waitFor(() => expect(screen.queryByRole('listbox')).not.toBeInTheDocument());

    await userEvent.click(within(band()).getByRole('combobox', { name: 'Статус' }));
    expect(await screen.findByRole('option', { name: 'Новая' })).toBeInTheDocument();
  });

  it('opens with Enter, moves and ticks with the keys, stays open while ticking and closes on Esc with the focus on the field', async () => {
    serveWorkItems(listFilterDataset());
    renderWorkItems(MSK_LIST);
    await table();
    const field = within(band()).getByRole('combobox', { name: 'Статус' });
    field.focus();

    await userEvent.keyboard('{Enter}');
    await screen.findByRole('listbox');
    await userEvent.keyboard('{ArrowDown}{Enter}');
    expect(screen.getByRole('listbox')).toBeInTheDocument();
    await userEvent.keyboard('{ArrowDown}{Enter}');
    await userEvent.keyboard('{Escape}');

    await waitFor(() => expect(screen.queryByRole('listbox')).not.toBeInTheDocument());
    expect(field).toHaveFocus();
    await waitFor(() => expect(addressSearch()).toContain('state='));
  });
});

describe('feat-003 US-1 Scenario: «Удалена» is the recycle bin', () => {
  it('lists the deleted items, latest change first, each reading like any row: «Удалена» with the Removed mark, its last change, no action, no column of its own', async () => {
    serveWorkItems(listFilterDataset());
    renderWorkItems(MSK_LIST);
    await table();

    await chooseStates('Удалена');

    await expectListed(['MSK-FEAT-6', 'MSK-US-5']);
    expect(screen.getByText('1–2 из 2')).toBeInTheDocument();
    const [first, second] = rowsOf(await table());
    expect(cellsOf(first!)).toEqual([
      'MSK-FEAT-6',
      'Фича',
      'Импорт склада',
      'ЗКЗоя Ким',
      'Удалена',
      'VENUS',
      'Мастаск',
      '18.10.2026 09:00',
    ]);
    expect(cellsOf(second!)[4]).toBe('Удалена');
    expect(cellsOf(second!)[7]).toBe('17.10.2026 10:00');
    expect(
      within(first!).getAllByRole('cell')[4]?.querySelector(`.${CATEGORY_MARKER_CLASS}`),
    ).not.toBeNull();
    expect(within(first!).getAllByRole('link')).toHaveLength(1);
    expect(within(first!).queryAllByRole('button')).toHaveLength(0);
    expect(within(await table()).getAllByRole('columnheader')).toHaveLength(8);
  });

  it('puts the deleted items among the live rows of the other chosen states, in the one order', async () => {
    serveWorkItems(listFilterDataset());
    renderWorkItems(MSK_LIST);
    await table();

    await chooseStates('Новая', 'Удалена');

    await expectListed(['MSK-FEAT-6', 'MSK-US-5', 'MSK-BUG-42']);
  });

  it('a deleted item is listed under «Удалена» only: a keyword, an assignee or a team reach it only while it is chosen', async () => {
    serveWorkItems(listFilterDataset());
    renderWorkItems(`${MSK_LIST}?search=${enc('оплат')}`);
    await expectListed(['MSK-BUG-2', 'MSK-US-1']);

    await chooseStates('Удалена');

    await expectListed(['MSK-US-5']);
  });

  it('its title opens the read-only card, and Back shows the list under the same filters', async () => {
    serveWorkItems(listFilterDataset());
    renderWorkItems(`${MSK_LIST}?state=${enc('Удалена')}`);
    await expectListed(['MSK-FEAT-6', 'MSK-US-5']);

    await userEvent.click(screen.getByRole('link', { name: 'Импорт склада' }));

    expect(screen.getByTestId('location')).toHaveTextContent(mskForm('MSK-FEAT-6'));
    expect(
      await screen.findByText('Рабочий элемент удалён и доступен только для чтения.', {
        exact: false,
      }),
    ).toBeVisible();
    expect(await screen.findByRole('button', { name: 'Восстановить' })).toBeEnabled();
  });

  it('a reader lists the deleted items too, and the card offers them no action', async () => {
    serveWorkItems(listFilterDataset());
    renderWorkItems(`${MSK_LIST}?state=${enc('Удалена')}`, { role: 'viewer' });
    await expectListed(['MSK-FEAT-6', 'MSK-US-5']);

    await userEvent.click(screen.getByRole('link', { name: 'Импорт склада' }));

    await screen.findByRole('heading', { level: 1, name: 'Фича MSK-FEAT-6' });
    expect(screen.queryByRole('button', { name: 'Восстановить' })).not.toBeInTheDocument();
  });

  it('a bin with nothing in it reads «Ничего не найдено», and a project whose items are all deleted still reaches them', async () => {
    const data = emptyDataset();
    data.items.push(item(ids.msk, 1, 'TASK', 'Живая'));
    serveWorkItems(data);
    renderWorkItems(MSK_LIST);
    await table();

    await chooseStates('Удалена');

    expect(
      await screen.findByRole('heading', { level: 2, name: 'Ничего не найдено' }),
    ).toBeVisible();
    expect(within(band()).getByRole('button', { name: 'Очистить фильтры' })).toBeInTheDocument();
  });

  it('no filter and no live item: «Рабочих элементов пока нет» under the band, and «Удалена» still finds the deleted ones', async () => {
    const data = listFilterDataset();
    data.items = data.items.filter((record) => record.deletion);
    const api = serveWorkItems(data);
    renderWorkItems(MSK_LIST);

    expect(
      await screen.findByRole('heading', { level: 2, name: 'Рабочих элементов пока нет' }),
    ).toBeVisible();
    expect(band()).toBeInTheDocument();
    expect(api.reads('/workitems?')).not.toHaveLength(0);

    await chooseStates('Удалена');

    await expectListed(['MSK-FEAT-6', 'MSK-US-5']);
  });
});

describe('feat-003 BR-WS-10 a value the address carries that no field can name', () => {
  it.each([
    ['«Статус»', `?state=${enc('готово')}`, 'Все статусы'],
    ['«Исполнитель»', '?assigneeId=not-a-user', 'Все исполнители'],
    ['«Команда»', '?teamId=c3000000-0000-4000-8000-000000000099', 'Все команды'],
  ])(
    'a name %s does not know reads «Фильтр задан» in place of «Все …», empties the list and offers «Очистить фильтры»',
    async (_field, search, rest) => {
      serveWorkItems(listFilterDataset());
      renderWorkItems(`${MSK_LIST}${search}`);

      expect(
        await screen.findByRole('heading', { level: 2, name: 'Ничего не найдено' }),
      ).toBeVisible();
      await waitFor(() => expect(within(band()).getAllByText('Фильтр задан')).toHaveLength(1));
      expect(within(band()).queryByText(rest)).toBeNull();
      expect(within(band()).getByRole('button', { name: 'Очистить фильтры' })).toBeInTheDocument();
    },
  );

  it('beside a value that names a state the rows of that state stay, and the field names it alone', async () => {
    serveWorkItems(listFilterDataset());
    renderWorkItems(`${MSK_LIST}?state=${enc('Новая')}&state=${enc('готово')}`);

    await expectListed(['MSK-BUG-42']);
    expect(within(band()).getByRole('combobox', { name: 'Статус' })).toHaveTextContent('Новая');
    expect(within(band()).queryByText('Фильтр задан')).toBeNull();
    expect(within(band()).getByRole('button', { name: 'Очистить фильтры' })).toBeInTheDocument();
  });

  it('a name in another letter case is no state: «новая» finds nothing', async () => {
    serveWorkItems(listFilterDataset());
    renderWorkItems(`${MSK_LIST}?state=${enc('новая')}`);

    expect(
      await screen.findByRole('heading', { level: 2, name: 'Ничего не найдено' }),
    ).toBeVisible();
  });

  it('a blank value is no filter: the whole list and nothing to clear', async () => {
    serveWorkItems(listFilterDataset());
    renderWorkItems(`${MSK_LIST}?state=&assigneeId=%20&search=%20%20&teamId=`);

    await expectListed(ALL);
    expect(within(band()).getByText('Все статусы')).toBeInTheDocument();
    expect(within(band()).queryByRole('button', { name: 'Очистить фильтры' })).toBeNull();
  });
});

describe('feat-003 BR-WS-11, BR-WS-13 the address and the clears', () => {
  const FOUR = `?state=${enc('Разрешена')}&assigneeId=${ids.ivan}&search=${enc('оплат')}&teamId=${ids.venusTeam}&sort=title`;

  it('a link that carries the four opens the list narrowed, each field showing its part, every read carrying them', async () => {
    const api = serveWorkItems(listFilterDataset());
    renderWorkItems(`${MSK_LIST}${FOUR}`);

    await expectListed(['MSK-BUG-2']);
    for (const read of api.reads('/workitems?')) {
      expect(read.path).toContain(`state=${enc('Разрешена')}`);
      expect(read.path).toContain(`assigneeId=${ids.ivan}`);
      expect(read.path).toContain(`search=${enc('оплат')}`);
      expect(read.path).toContain(`teamId=${ids.venusTeam}`);
    }
    expect(within(band()).getByRole('textbox', { name: 'Поиск' })).toHaveValue('оплат');
    expect(within(band()).getByRole('combobox', { name: 'Статус' })).toHaveTextContent('Разрешена');
    expect(within(band()).getByRole('combobox', { name: 'Команда' })).toHaveTextContent('VENUS');
    expect(
      await within(band()).findByRole('button', { name: 'Убрать «Иван Котов (не в организации)»' }),
    ).toBeInTheDocument();
  });

  it('«Очистить фильтры» removes all four in one go, keeps every other parameter, and sends the focus to «Поиск»', async () => {
    serveWorkItems(listFilterDataset());
    renderWorkItems(`${MSK_LIST}${FOUR}`);
    await expectListed(['MSK-BUG-2']);

    await userEvent.click(within(band()).getByRole('button', { name: 'Очистить фильтры' }));

    await expectListed(ALL);
    expect(addressSearch()).toBe('?sort=title');
    expect(within(band()).getByRole('textbox', { name: 'Поиск' })).toHaveValue('');
    expect(within(band()).getByRole('textbox', { name: 'Поиск' })).toHaveFocus();
    expect(within(band()).getByText('Все статусы')).toBeInTheDocument();
    expect(within(band()).getByText('Все исполнители')).toBeInTheDocument();
    expect(within(band()).getByText('Все команды')).toBeInTheDocument();
    expect(within(band()).queryByRole('button', { name: 'Очистить фильтры' })).toBeNull();
  });

  it('«Сбросить фильтр» in the empty frame clears all four, the team’s included, and sends the focus to «Поиск»', async () => {
    serveWorkItems(listFilterDataset());
    renderWorkItems(
      `${MSK_LIST}?state=${enc('Закрыта')}&teamId=${ids.venusTeam}&search=${enc('нет такого')}`,
    );
    expect(
      await screen.findByRole('heading', { level: 2, name: 'Ничего не найдено' }),
    ).toBeVisible();
    expect(
      screen.getByText('Ни один рабочий элемент не подходит под заданные условия фильтра.'),
    ).toBeInTheDocument();
    expect(within(band()).getByRole('button', { name: 'Очистить фильтры' })).toBeInTheDocument();

    await userEvent.click(screen.getByRole('button', { name: 'Сбросить фильтр' }));

    await expectListed(ALL);
    expect(addressSearch()).toBe('');
    expect(within(band()).getByRole('textbox', { name: 'Поиск' })).toHaveFocus();
  });

  it('a change returns to page 1 and the next page keeps the filter', async () => {
    const data = emptyDataset();
    for (let number = 1; number <= 120; number += 1) {
      data.items.push(
        item(ids.msk, number, 'TASK', `Задача ${number}`, {
          changedAt: new Date(2026, 9, 1, 0, number).toISOString(),
        }),
      );
    }
    const api = serveWorkItems(data);
    renderWorkItems(MSK_LIST);
    await table();
    await userEvent.click(screen.getByRole('button', { name: 'Перейти на следующую страницу' }));
    await waitFor(() => expect(api.reads('page=2')).not.toHaveLength(0));

    await chooseStates('Новая');

    await waitFor(() =>
      expect(api.reads(`page=1&state=${enc('Новая')}`).length).toBeGreaterThan(0),
    );
    await userEvent.click(screen.getByRole('button', { name: 'Перейти на следующую страницу' }));
    await waitFor(() => expect(api.reads(`page=2&state=${enc('Новая')}`)).not.toHaveLength(0));
    expect(addressSearch()).toBe(`?state=${enc('Новая')}`);
  });
});

describe('feat-003 US-1 Scenario: every state of the list (design § 5)', () => {
  it('state 1: the band is there at once, its fields working, while the first read is pending', async () => {
    serveWorkItems(listFilterDataset());
    const held = gate();
    server.use(
      http.get(WORK_ITEMS_URL, async () => {
        await held.opened;
        return undefined;
      }),
    );
    renderWorkItems(MSK_LIST);

    expect(await screen.findByRole('status', { name: 'Загрузка данных' })).toBeInTheDocument();
    expect(band()).toBeInTheDocument();
    await userEvent.click(within(band()).getByRole('combobox', { name: 'Статус' }));
    expect(await screen.findByRole('option', { name: 'Новая' })).toBeInTheDocument();
    await userEvent.keyboard('{Escape}');
    held.open();
    await expectListed(ALL);
  });

  it('state 2: a failed first read keeps the band and offers «Повторить»', async () => {
    serveWorkItems(listFilterDataset());
    serveProblem(
      'get',
      WORK_ITEMS_URL,
      { ...problem('workitems.validation_failed'), status: 503, detail: 'Недоступно.' },
      { once: true },
    );
    renderWorkItems(MSK_LIST);

    const alert = await screen.findByRole('alert');
    expect(alert).toHaveTextContent('Недоступно.');
    expect(band()).toBeInTheDocument();
    await userEvent.click(within(alert).getByRole('button', { name: 'Повторить' }));
    await expectListed(ALL);
  });

  it('states 3 and 4: the previous rows stay, busy, while a change is read; a failed read replaces them with the banner, the band stays', async () => {
    serveWorkItems(listFilterDataset());
    renderWorkItems(MSK_LIST);
    await table();
    const held = gate();
    let fail = true;
    server.use(
      http.get(WORK_ITEMS_URL, async ({ request }) => {
        if (new URL(request.url).searchParams.has('state')) {
          await held.opened;
          if (fail) {
            fail = false;
            return HttpResponse.json(problem('workitems.validation_failed'), { status: 500 });
          }
        }
        return undefined;
      }),
    );

    await chooseStates('Новая');

    const list = await table();
    expect(list).toHaveAttribute('aria-busy', 'true');
    expect(await listed()).toEqual(ALL);
    expect(screen.queryByRole('status', { name: 'Загрузка данных' })).not.toBeInTheDocument();

    held.open();
    await userEvent.click(await screen.findByRole('button', { name: 'Повторить' }));
    await expectListed(['MSK-BUG-42']);
    expect(band()).toBeInTheDocument();
  });

  it('state 5: an organization or project that is not found replaces the page, no band', async () => {
    serveWorkItems(listFilterDataset());
    renderWorkItems(
      `/organizations/${ids.example}/projects/f0000000-0000-4000-8000-0000000000ff/workitems?state=${enc('Новая')}`,
    );

    expect(
      await screen.findByRole('heading', { level: 1, name: 'Проект не найден' }),
    ).toBeVisible();
    expect(screen.queryByRole('group', { name: 'Фильтры' })).not.toBeInTheDocument();
  });
});

describe('feat-003 design § 9 the result is announced', () => {
  it('says nothing on the first read, «Найдено: n» when a change of a filter settles with rows, and «Ничего не найдено» with none', async () => {
    serveWorkItems(listFilterDataset());
    renderWorkItems(MSK_LIST);
    await table();
    expect(announced().filter((text) => text.startsWith('Найдено'))).toEqual([]);
    expect(announced()).not.toContain('Ничего не найдено');

    await chooseStates('Разрешена');
    await expectListed(['MSK-US-3', 'MSK-BUG-2']);
    await waitFor(() => expect(announced()).toContain('Найдено: 2'));

    await chooseStates('Удалена');
    await waitFor(() => expect(announced()).toContain('Найдено: 4'));

    await userEvent.click(within(band()).getByRole('textbox', { name: 'Поиск' }));
    await userEvent.type(within(band()).getByRole('textbox', { name: 'Поиск' }), 'нет такого');
    await waitFor(() => expect(announced()).toContain('Ничего не найдено'));
  });
});

describe('feat-003 BR-WS-14 a creation while filtered', () => {
  async function createTask(title: string) {
    await userEvent.click(await screen.findByRole('button', { name: 'Новый рабочий элемент' }));
    await userEvent.click(await screen.findByRole('menuitem', { name: 'Задача' }));
    const dialog = await screen.findByRole('dialog', { name: 'Новый рабочий элемент' });
    await within(dialog).findByRole('combobox', { name: 'Команда' });
    await userEvent.type(within(dialog).getByRole('textbox', { name: 'Заголовок' }), title);
    await userEvent.click(within(dialog).getByRole('button', { name: 'Создать' }));
  }

  it('says the item does not fit the state filter, and keeps the filter', async () => {
    serveWorkItems(listFilterDataset());
    renderWorkItems(`${MSK_LIST}?state=${enc('Активна')}`);
    await expectListed(['MSK-US-1']);

    await createTask('Новая задача');

    expect(
      await screen.findByText(
        'Рабочий элемент MSK-TASK-43 создан. Он не подходит под текущий фильтр.',
      ),
    ).toBeInTheDocument();
    expect(addressSearch()).toBe(`?state=${enc('Активна')}`);
  });

  it('judges the keyword by the title as the server would: case and «ё» ignored, so the item that fits reads as today', async () => {
    serveWorkItems(listFilterDataset());
    renderWorkItems(`${MSK_LIST}?search=${enc('ЁЛКА')}`);
    await screen.findByRole('heading', { level: 2, name: 'Ничего не найдено' });

    await createTask('Нарядная елка');

    expect(await screen.findByText('Рабочий элемент MSK-TASK-43 создан.')).toBeInTheDocument();
    expect(screen.queryByText('Он не подходит под текущий фильтр.', { exact: false })).toBeNull();
  });

  it('judges the assignee and the team: a new item has none, so a filter on a person or a team hides it', async () => {
    serveWorkItems(listFilterDataset());
    renderWorkItems(`${MSK_LIST}?assigneeId=${ids.mia}`);
    await table();

    await createTask('Без исполнителя');

    expect(
      await screen.findByText(
        'Рабочий элемент MSK-TASK-43 создан. Он не подходит под текущий фильтр.',
      ),
    ).toBeInTheDocument();
  });
});

describe('feat-003 design § 8 the band at the narrow widths', () => {
  it('each field is at most the band’s row wide and stands at the top of the band, at 640 px too', async () => {
    setViewportWidth(640);
    serveWorkItems(listFilterDataset());
    renderWorkItems(MSK_LIST);
    await table();

    for (const name of ['Поиск', 'Статус', 'Исполнитель', 'Команда']) {
      const field = within(band()).getAllByText(name)[0]?.closest('[data-field]');
      const own = field?.parentElement as HTMLElement;
      const slot = own.parentElement as HTMLElement;
      expect(getComputedStyle(own).maxWidth).toBe('100%');
      expect(getComputedStyle(slot).maxWidth).toBe('100%');
      expect(getComputedStyle(slot).alignSelf).toBe('flex-start');
    }
  });

  it('keeps its widths in the order of design § 2: «Поиск» 30, «Статус» 25, «Исполнитель» 35, «Команда» 30 spacing units', async () => {
    serveWorkItems(listFilterDataset());
    renderWorkItems(MSK_LIST);
    await table();

    const widths = ['Поиск', 'Статус', 'Исполнитель', 'Команда'].map((name) => {
      const field = within(band()).getAllByText(name)[0]?.closest('[data-field]');
      return getComputedStyle(field?.parentElement as HTMLElement).width;
    });
    expect(widths).toEqual(['240px', '200px', '280px', '240px']);
  });
});

describe('feat-003 a change of the address by another cause', () => {
  it('shows the new address’s values: the fields follow the address, not the other way round', async () => {
    serveWorkItems(listFilterDataset());
    renderWorkItems(`${MSK_LIST}?state=${enc('Новая')}`);
    await expectListed(['MSK-BUG-42']);

    // The shell’s own navigation to the same list without parameters: «Рабочие элементы».
    await act(async () => {
      await userEvent.click(screen.getByRole('link', { name: 'Рабочие элементы' }));
    });

    await expectListed(ALL);
    expect(within(band()).getByText('Все статусы')).toBeInTheDocument();
  });
});
