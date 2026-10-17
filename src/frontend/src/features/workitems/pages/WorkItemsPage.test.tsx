import { describe, expect, it } from 'vitest';
import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { HttpResponse, http } from 'msw';
import { gate } from '../../../test/gate';
import { server } from '../../../test/mocks/server';
import { CATEGORY_MARKER_CLASS } from '../../../shared/ui/WorkTrackingMarks';
import { serveProblem, serveWorkItems } from '../testApi';
import {
  SAVED_AT_TEXT,
  TEAMS_URL,
  WORK_ITEMS_URL,
  emptyDataset,
  exampleDataset,
  ids,
  item,
  problem,
} from '../testFixtures';
import { MSK_LIST, OPS_LIST, mskForm, projectContext, renderWorkItems } from '../testRender';

// mod-017 BR-OT-9: «Команда» stands before «Область», eight columns.
const COLUMNS = [
  'ID',
  'Тип',
  'Заголовок',
  'Исполнитель',
  'Статус',
  'Команда',
  'Область',
  'Изменён',
];

async function table() {
  return screen.findByRole('table', { name: 'Рабочие элементы' });
}

function rowsOf(element: HTMLElement): HTMLElement[] {
  return within(element).getAllByRole('row').slice(1);
}

function cellsOf(row: HTMLElement): string[] {
  return within(row)
    .getAllByRole('cell')
    .map((cell) => cell.textContent ?? '');
}

describe('mod-003 US-2 Scenario: A row shows the item’s columns', () => {
  it('reads the identifier, the type with its icon, the title link, the assignee, the state with its dot, the team, the area and the time', async () => {
    serveWorkItems();
    renderWorkItems(MSK_LIST);

    const list = await table();
    expect(
      within(list)
        .getAllByRole('columnheader')
        .map((header) => header.textContent),
    ).toEqual(COLUMNS);
    const row = rowsOf(list).find((candidate) => candidate.textContent?.includes('MSK-BUG-4'));
    if (!row) throw new Error('MSK-BUG-4 is not listed.');
    expect(cellsOf(row)).toEqual([
      'MSK-BUG-4',
      'Баг',
      'Падение при входе',
      'АСАнна Смирнова',
      'Активна',
      'Без команды',
      'Платформа / API',
      SAVED_AT_TEXT,
    ]);
    const cells = within(row).getAllByRole('cell');
    expect(cells[1]?.querySelector('[data-testid="BugReportIcon"]')).not.toBeNull();
    expect(cells[4]?.querySelector(`.${CATEGORY_MARKER_CLASS}`)).not.toBeNull();

    await userEvent.click(within(row).getByRole('link', { name: 'Падение при входе' }));

    expect(screen.getByTestId('location')).toHaveTextContent(mskForm('MSK-BUG-4'));
    expect(
      await screen.findByRole('heading', { level: 1, name: 'Баг MSK-BUG-4' }),
    ).toBeInTheDocument();
  });

  it('shows «Не назначен» for an item with no assignee', async () => {
    serveWorkItems();
    renderWorkItems(MSK_LIST);

    const row = rowsOf(await table()).find((candidate) =>
      candidate.textContent?.includes('MSK-TASK-5'),
    );
    expect(row && cellsOf(row)[3]).toBe('Не назначен');
  });
});

describe('mod-003 US-2 Scenario: Only this project’s items are listed', () => {
  it('lists «Мастаск»’s items only, every identifier starting with «MSK-»', async () => {
    serveWorkItems();
    renderWorkItems(MSK_LIST);

    const identifiers = rowsOf(await table()).map((row) => cellsOf(row)[0]);
    expect(identifiers).toEqual(['MSK-BUG-4', 'MSK-TASK-5']);
    expect(screen.queryByText('Склад не отвечает')).not.toBeInTheDocument();
  });
});

describe('mod-003 US-2 Scenario: The latest change comes first', () => {
  it('renders the rows in the server’s order: latest change first, then the higher number', async () => {
    const data = emptyDataset();
    const sameMoment = new Date(2026, 9, 5, 10, 0).toISOString();
    data.items.push(
      item(ids.msk, 1, 'TASK', 'Вчерашняя', {
        changedAt: new Date(2026, 9, 4, 9, 0).toISOString(),
      }),
      item(ids.msk, 2, 'TASK', 'Сегодняшняя', { changedAt: sameMoment }),
      item(ids.msk, 3, 'TASK', 'Тоже сегодня', { changedAt: sameMoment }),
    );
    serveWorkItems(data);
    renderWorkItems(MSK_LIST);

    expect(rowsOf(await table()).map((row) => cellsOf(row)[0])).toEqual([
      'MSK-TASK-3',
      'MSK-TASK-2',
      'MSK-TASK-1',
    ]);
  });
});

describe('mod-003 US-2 Scenario: The list is paged by 50', () => {
  function bigProject() {
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

  it('shows 50 rows reading «1–50 из 120», and 20 on the third page', async () => {
    const api = serveWorkItems(bigProject());
    renderWorkItems(MSK_LIST);

    expect(rowsOf(await table())).toHaveLength(50);
    expect(screen.getByText('1–50 из 120')).toBeInTheDocument();
    expect(api.reads('/workitems?').map((request) => request.path)).toEqual([
      `/api/organizations/${ids.example}/projects/${ids.msk}/workitems?page=1`,
    ]);

    const next = screen.getByRole('button', { name: 'Перейти на следующую страницу' });
    await userEvent.click(next);
    expect(await screen.findByText('51–100 из 120')).toBeInTheDocument();
    await userEvent.click(next);
    expect(await screen.findByText('101–120 из 120')).toBeInTheDocument();
    await waitFor(() => expect(rowsOf(screen.getByRole('table'))).toHaveLength(20));
    expect(cellsOf(rowsOf(screen.getByRole('table'))[0] as HTMLElement)[0]).toBe('MSK-TASK-20');
    expect(api.reads('/workitems?').at(-1)?.path).toMatch(/\?page=3$/);
  });
});

describe('mod-003 US-2 Scenario: An empty project invites the first item', () => {
  it('shows the frame with its hint, and its action opens the type picker', async () => {
    serveWorkItems(emptyDataset());
    renderWorkItems(OPS_LIST, { context: projectContext({ projectId: ids.ops }) });

    const heading = await screen.findByRole('heading', {
      level: 2,
      name: 'Рабочих элементов пока нет',
    });
    expect(heading).toBeInTheDocument();
    expect(
      screen.getByText('Создайте первый рабочий элемент кнопкой «Новый рабочий элемент» выше.'),
    ).toBeInTheDocument();
    // The header's primary and the frame's action: two ways to the one picker.
    const actions = await screen.findAllByRole('button', { name: 'Новый рабочий элемент' });
    expect(actions).toHaveLength(2);

    await userEvent.click(actions[1] as HTMLElement);

    const menu = await screen.findByRole('menu', { name: 'Новый рабочий элемент' });
    expect(within(menu).getAllByRole('menuitem')).toHaveLength(5);
  });
});

describe('mod-003 US-2 Scenario: A failed read can be retried', () => {
  it('keeps the header, shows the server’s text with «Повторить», and reads again', async () => {
    const api = serveWorkItems();
    server.use(
      http.get(
        WORK_ITEMS_URL,
        () =>
          HttpResponse.json(
            { status: 500, title: 'Ошибка', detail: 'Сервис не ответил.' },
            { status: 500 },
          ),
        { once: true },
      ),
    );
    renderWorkItems(MSK_LIST);

    const alert = await screen.findByRole('alert');
    expect(alert).toHaveTextContent('Сервис не ответил.');
    expect(screen.getByRole('heading', { level: 1, name: 'Рабочие элементы' })).toBeInTheDocument();
    expect(screen.queryByRole('table', { name: 'Рабочие элементы' })).not.toBeInTheDocument();

    await userEvent.click(within(alert).getByRole('button', { name: 'Повторить' }));

    expect(await table()).toBeInTheDocument();
    expect(api.reads('/workitems?')).toHaveLength(1);
  });
});

describe('mod-003 spec § Page «Рабочие элементы», State Loading', () => {
  it('shows the skeleton under the eight real headers inside «Загрузка данных»', async () => {
    serveWorkItems();
    let release: () => void = () => {};
    const held = new Promise<void>((resolve) => {
      release = resolve;
    });
    server.use(
      http.get(
        WORK_ITEMS_URL,
        async () => {
          await held;
          return HttpResponse.json({ count: 0, data: [] });
        },
        { once: true },
      ),
    );
    renderWorkItems(MSK_LIST);

    const status = await screen.findByRole('status', { name: 'Загрузка данных' });
    expect(
      within(status)
        .getAllByRole('columnheader')
        .map((header) => header.textContent),
    ).toEqual(COLUMNS);
    release();
    expect(
      await screen.findByRole('heading', { level: 2, name: 'Рабочих элементов пока нет' }),
    ).toBeInTheDocument();
  });
});

describe('mod-003 US-2 Scenario Outline: A list whose context is not found', () => {
  it.each([
    [
      'an unknown organization id',
      'Организация не найдена',
      `/organizations/${ids.msk}/projects/${ids.msk}/workitems`,
    ],
    [
      'a malformed organization id',
      'Организация не найдена',
      `/organizations/abc/projects/${ids.msk}/workitems`,
    ],
    [
      'the id of a project of another organization',
      'Проект не найден',
      `/organizations/${ids.example}/projects/${ids.otherMsk}/workitems`,
    ],
  ])('%s shows «%s»', async (_case, frame, path) => {
    serveWorkItems();
    renderWorkItems(path);

    expect(await screen.findByRole('heading', { level: 1, name: frame })).toBeInTheDocument();
    expect(screen.queryByRole('heading', { level: 1, name: 'Рабочие элементы' })).toBeNull();
  });

  it('a deactivated project shows «Проект не найден»', async () => {
    const data = exampleDataset();
    const project = data.projects.find((candidate) => candidate.id === ids.msk);
    if (project) project.active = false;
    serveWorkItems(data);
    renderWorkItems(MSK_LIST);

    expect(
      await screen.findByRole('heading', { level: 1, name: 'Проект не найден' }),
    ).toBeInTheDocument();
  });
});

describe('mod-003 US-2 Scenario: An assignee the directory does not know', () => {
  it('reads «Неизвестный пользователь»', async () => {
    const data = emptyDataset();
    data.items.push(item(ids.msk, 1, 'TASK', 'Чужая задача', { assigneeId: ids.ghost }));
    serveWorkItems(data);
    renderWorkItems(MSK_LIST);

    const row = rowsOf(await table())[0] as HTMLElement;
    expect(cellsOf(row)[3]).toBe('Неизвестный пользователь');
  });
});

describe('mod-003 spec § Page «Рабочие элементы»: the primary waits for the process', () => {
  it('renders «Новый рабочий элемент» only once the process is read, and a failed read offers «Повторить»', async () => {
    serveWorkItems();
    serveProblem(
      'get',
      `http://localhost:5000/api/organizations/:organizationId/process`,
      { ...problem('workitems.validation_failed'), status: 503, detail: 'Процесс недоступен.' },
      { once: true },
    );
    renderWorkItems(MSK_LIST);

    await table();
    const alert = await screen.findByRole('alert');
    expect(alert).toHaveTextContent('Процесс недоступен.');
    expect(screen.queryByRole('button', { name: 'Новый рабочий элемент' })).toBeNull();

    await userEvent.click(within(alert).getByRole('button', { name: 'Повторить' }));

    expect(
      await screen.findByRole('button', { name: 'Новый рабочий элемент' }),
    ).toBeInTheDocument();
    expect(screen.queryByRole('alert')).toBeNull();
  });
});

// mod-017 US-5, design § 5: the column «Команда» and the filter of the list.

/** «Мастаск» with three items: one of MARS, one of VENUS, one of none. */
function teamsDataset() {
  const data = emptyDataset();
  data.items.push(
    item(ids.msk, 1, 'TASK', 'Марсианская', {
      teamId: ids.marsTeam,
      changedAt: new Date(2026, 9, 3, 9, 0).toISOString(),
    }),
    item(ids.msk, 2, 'TASK', 'Венерианская', {
      teamId: ids.venusTeam,
      changedAt: new Date(2026, 9, 2, 9, 0).toISOString(),
    }),
    item(ids.msk, 3, 'TASK', 'Ничья', { changedAt: new Date(2026, 9, 1, 9, 0).toISOString() }),
  );
  return data;
}

function band() {
  return screen.getByRole('group', { name: 'Фильтры' });
}

async function chooseTeams(...names: string[]) {
  await userEvent.click(await within(band()).findByRole('combobox', { name: 'Команда' }));
  for (const name of names) {
    await userEvent.click(await screen.findByRole('option', { name }));
  }
  await userEvent.keyboard('{Escape}');
  await waitFor(() => expect(screen.queryByRole('listbox')).not.toBeInTheDocument());
}

async function titles() {
  return rowsOf(await table()).map((row) => cellsOf(row)[2]);
}

describe('mod-017 US-5 Scenario: the team column', () => {
  it('names the team, or «Без команды» in the secondary colour, before «Область»', async () => {
    serveWorkItems(teamsDataset());
    renderWorkItems(MSK_LIST);

    const rows = rowsOf(await table());

    expect(rows.map((row) => cellsOf(row)[5])).toEqual(['MARS', 'VENUS', 'Без команды']);
    const none = within(rows[2]!).getByText('Без команды');
    expect(getComputedStyle(none).color).not.toBe(getComputedStyle(rows[0]!).color);
  });
});

describe('mod-017 US-5 Scenario: the «Команда» filter on the list', () => {
  it('stands in the band «Фильтры» as «Все команды», offers the teams by name and then «Без команды», no counts', async () => {
    serveWorkItems(teamsDataset());
    renderWorkItems(MSK_LIST);
    await table();

    const filter = within(band()).getByRole('combobox', { name: 'Команда' });
    expect(screen.getByText('Все команды')).toBeInTheDocument();
    expect(within(band()).queryByRole('button', { name: 'Очистить фильтры' })).toBeNull();
    await userEvent.click(filter);

    const list = await screen.findByRole('listbox');
    expect(list).toHaveAttribute('aria-multiselectable', 'true');
    expect(
      within(list)
        .getAllByRole('option')
        .map((option) => option.textContent),
    ).toEqual(['MARS', 'VENUS', 'Без команды']);
  });

  it('any chosen value matches: a team narrows the rows and the count, «Без команды» adds the items of none, the request carries `teamId`', async () => {
    const api = serveWorkItems(teamsDataset());
    renderWorkItems(MSK_LIST);
    await table();

    await chooseTeams('MARS');
    await waitFor(async () => expect(await titles()).toEqual(['Марсианская']));
    expect(api.reads('/workitems?').at(-1)?.path).toContain(`teamId=${ids.marsTeam}`);
    expect(within(band()).getByRole('combobox', { name: 'Команда' })).toHaveTextContent('MARS');

    await chooseTeams('Без команды');
    await waitFor(async () => expect(await titles()).toEqual(['Марсианская', 'Ничья']));
    expect(api.reads('/workitems?').at(-1)?.path).toContain('teamId=none');
    expect(within(band()).getByRole('combobox', { name: 'Команда' })).toHaveTextContent(
      'MARS, Без команды',
    );
  });

  it('«Очистить фильтры» shows while a value is chosen and brings the whole list back', async () => {
    serveWorkItems(teamsDataset());
    renderWorkItems(MSK_LIST);
    await table();
    await chooseTeams('VENUS');
    await waitFor(async () => expect(await titles()).toEqual(['Венерианская']));

    await userEvent.click(within(band()).getByRole('button', { name: 'Очистить фильтры' }));

    await waitFor(async () => expect(await titles()).toHaveLength(3));
    expect(screen.getByText('Все команды')).toBeInTheDocument();
    expect(within(band()).queryByRole('button', { name: 'Очистить фильтры' })).toBeNull();
  });

  it('a change returns to page 1', async () => {
    const data = emptyDataset();
    for (let number = 1; number <= 120; number += 1) {
      data.items.push(
        item(ids.msk, number, 'TASK', `Задача ${number}`, {
          teamId: number % 2 === 0 ? ids.marsTeam : null,
          changedAt: new Date(2026, 9, 1, 0, number).toISOString(),
        }),
      );
    }
    const api = serveWorkItems(data);
    renderWorkItems(MSK_LIST);
    await table();
    await userEvent.click(screen.getByRole('button', { name: 'Перейти на следующую страницу' }));
    await waitFor(() => expect(api.reads('page=2')).not.toHaveLength(0));

    await chooseTeams('MARS');

    await waitFor(() => expect(api.reads('page=1&teamId=').length).toBeGreaterThan(0));
  });

  it('keeps the previous rows, busy, while a change is read; a failed read replaces them with the banner and «Повторить»', async () => {
    serveWorkItems(teamsDataset());
    renderWorkItems(MSK_LIST);
    await table();
    const held = gate();
    let fail = true;
    server.use(
      http.get(WORK_ITEMS_URL, async ({ request }) => {
        if (new URL(request.url).searchParams.has('teamId')) {
          await held.opened;
          if (fail) {
            fail = false;
            return HttpResponse.json(problem('organizations.access.forbidden'), { status: 500 });
          }
        }
        return undefined;
      }),
    );

    await chooseTeams('MARS');

    const list = await table();
    expect(list).toHaveAttribute('aria-busy', 'true');
    expect(await titles()).toHaveLength(3);
    expect(screen.queryByRole('status', { name: 'Загрузка данных' })).not.toBeInTheDocument();

    held.open();
    await userEvent.click(await screen.findByRole('button', { name: 'Повторить' }));
    await waitFor(async () => expect(await titles()).toEqual(['Марсианская']));
  });

  it('shows «Ничего не найдено» after the filter, with «Сбросить фильтр» (and the band kept)', async () => {
    const data = emptyDataset();
    data.items.push(item(ids.msk, 1, 'TASK', 'Ничья'));
    serveWorkItems(data);
    renderWorkItems(MSK_LIST);
    await table();

    await chooseTeams('VENUS');

    const frame = await screen.findByRole('heading', { level: 2, name: 'Ничего не найдено' });
    expect(frame).toBeInTheDocument();
    expect(
      screen.getByText('Ни один рабочий элемент не подходит под заданные условия фильтра.'),
    ).toBeInTheDocument();
    expect(within(band()).getByRole('button', { name: 'Очистить фильтры' })).toBeInTheDocument();

    await userEvent.click(screen.getByRole('button', { name: 'Сбросить фильтр' }));

    expect(await table()).toBeInTheDocument();
    expect(screen.queryByRole('heading', { name: 'Ничего не найдено' })).not.toBeInTheDocument();
  });

  it('the field works while the teams are read (the list says «Загрузка…») and says why when they failed', async () => {
    serveWorkItems(teamsDataset());
    const held = gate();
    let failing = true;
    server.use(
      http.get(TEAMS_URL, async () => {
        await held.opened;
        if (failing) {
          failing = false;
          return HttpResponse.json(problem('organizations.access.forbidden'), { status: 500 });
        }
        return undefined;
      }),
    );
    renderWorkItems(MSK_LIST);
    await table();

    await userEvent.click(within(band()).getByRole('combobox', { name: 'Команда' }));
    expect(await screen.findByRole('option', { name: 'Загрузка…' })).toBeInTheDocument();
    held.open();
    expect(
      await screen.findByRole('option', {
        name: 'Не удалось загрузить команды. Попробуйте ещё раз.',
      }),
    ).toBeInTheDocument();
    await userEvent.keyboard('{Escape}');
    await waitFor(() => expect(screen.queryByRole('listbox')).not.toBeInTheDocument());

    await userEvent.click(within(band()).getByRole('combobox', { name: 'Команда' }));
    expect(await screen.findByRole('option', { name: 'MARS' })).toBeInTheDocument();
  });

  it('a reader filters too', async () => {
    serveWorkItems(teamsDataset());
    renderWorkItems(MSK_LIST, { role: 'viewer' });
    await table();

    await chooseTeams('MARS');

    await waitFor(async () => expect(await titles()).toEqual(['Марсианская']));
  });

  it('a creation the filter hides is noticed as such (design § 6.5); one it shows reads as today', async () => {
    serveWorkItems(teamsDataset());
    renderWorkItems(MSK_LIST);
    await table();
    await chooseTeams('MARS');

    await userEvent.click(await screen.findByRole('button', { name: 'Новый рабочий элемент' }));
    await userEvent.click(await screen.findByRole('menuitem', { name: 'Задача' }));
    const dialog = await screen.findByRole('dialog', { name: 'Новый рабочий элемент' });
    await within(dialog).findByRole('combobox', { name: 'Команда' });
    await userEvent.type(within(dialog).getByRole('textbox', { name: 'Заголовок' }), 'Новая');
    await userEvent.click(within(dialog).getByRole('button', { name: 'Создать' }));

    expect(
      await screen.findByText(
        'Рабочий элемент MSK-TASK-4 создан. Он не подходит под текущий фильтр.',
      ),
    ).toBeInTheDocument();
  });
});
