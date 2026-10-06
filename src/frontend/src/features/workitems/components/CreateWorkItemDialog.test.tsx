import { describe, expect, it } from 'vitest';
import { fireEvent, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { HttpResponse, http } from 'msw';
import { server } from '../../../test/mocks/server';
import { serveWorkItems, type WorkItemsTestApi } from '../testApi';
import { WORK_ITEMS_URL, emptyDataset, ids, processType, validationProblem } from '../testFixtures';
import { MSK_LIST, renderWorkItems } from '../testRender';

const TYPES_IN_ORDER = ['Эпик', 'Фича', 'Пользовательская история', 'Баг', 'Задача'];
const ICONS_IN_ORDER = [
  'LandscapeIcon',
  'EmojiEventsIcon',
  'MenuBookIcon',
  'BugReportIcon',
  'AssignmentIcon',
];
const UUID = /^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/;

/** The header's primary: an empty list's frame repeats it as its action. */
async function primary() {
  const [header] = await screen.findAllByRole('button', { name: 'Новый рабочий элемент' });
  if (!header) throw new Error('No «Новый рабочий элемент».');
  return header;
}

async function openPicker() {
  await userEvent.click(await primary());
  return screen.findByRole('menu', { name: 'Новый рабочий элемент' });
}

/** Opens the dialog for `type` from the list of «Мастаск». */
async function openDialog(type: string, data = emptyDataset()) {
  const api = serveWorkItems(data);
  renderWorkItems(MSK_LIST);
  const menu = await openPicker();
  await userEvent.click(within(menu).getByRole('menuitem', { name: type }));
  const dialog = await screen.findByRole('dialog', { name: 'Новый рабочий элемент' });
  return {
    api,
    dialog,
    title: within(dialog).getByRole('textbox', { name: 'Заголовок' }),
    submit: () => userEvent.click(within(dialog).getByRole('button', { name: 'Создать' })),
  };
}

function posts(api: WorkItemsTestApi) {
  return api.writes().filter((request) => request.method === 'POST');
}

describe('mod-003 US-1 Scenario: The type picker offers the organization’s five types', () => {
  it('lists exactly the five types in the process order, each with its icon', async () => {
    serveWorkItems();
    renderWorkItems(MSK_LIST);

    const button = await screen.findByRole('button', { name: 'Новый рабочий элемент' });
    expect(button).toHaveAttribute('aria-haspopup', 'menu');
    const menu = await openPicker();

    const items = within(menu).getAllByRole('menuitem');
    expect(items.map((menuItem) => menuItem.textContent)).toEqual(TYPES_IN_ORDER);
    items.forEach((menuItem, index) => {
      expect(menuItem.querySelector(`[data-testid="${ICONS_IN_ORDER[index]}"]`)).not.toBeNull();
    });
    expect(button).toHaveAttribute('aria-expanded', 'true');
  });

  it('choosing «Задача» opens the dialog with «Тип» set to «Задача» and «Заголовок» focused', async () => {
    const { dialog, title } = await openDialog('Задача');

    expect(within(dialog).getByRole('combobox', { name: 'Тип' })).toHaveTextContent('Задача');
    expect(title).toHaveFocus();
    expect(within(dialog).getByText('До 255 символов')).toBeInTheDocument();
  });
});

describe('mod-003 US-1 Scenario: A work item is created with only a title', () => {
  it('names the initial state and the root area, creates, closes, notifies, and the row is on top', async () => {
    const { api, dialog, title, submit } = await openDialog('Задача');
    expect(
      await within(dialog).findByText(
        'Рабочий элемент будет создан в статусе «Новая» в области «Мастаск». Остальные поля заполняются в его форме.',
      ),
    ).toBeInTheDocument();

    await userEvent.type(title, '  Настроить сборку  ');
    await submit();

    expect(await screen.findByText('Рабочий элемент MSK-TASK-1 создан.')).toBeInTheDocument();
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
    const [post] = posts(api);
    expect(post?.path).toBe(`/api/organizations/${ids.example}/projects/${ids.msk}/workitems`);
    const body = post?.body as { requestId: string; typeId: string; title: string };
    expect(body.typeId).toBe(processType('TASK').id);
    expect(body.title).toBe('  Настроить сборку  ');
    expect(body.requestId).toMatch(UUID);
    // mod-006 US-7 Scenario "An item created from the project's list goes to
    // the first team": the list names no team, so the item takes the root.
    expect(body).not.toHaveProperty('teamId');

    const table = await screen.findByRole('table', { name: 'Рабочие элементы' });
    const first = within(table).getAllByRole('row')[1] as HTMLElement;
    expect(
      within(first)
        .getAllByRole('cell')
        .map((cell) => cell.textContent),
    ).toEqual([
      'MSK-TASK-1',
      'Задача',
      'Настроить сборку',
      'Не назначен',
      'Новая',
      'Мастаск',
      expect.any(String) as unknown as string,
    ]);
    expect(screen.getByTestId('location')).toHaveTextContent(MSK_LIST);

    // «And its form shows an empty Описание».
    await userEvent.click(within(first).getByRole('link', { name: 'Настроить сборку' }));
    expect(await screen.findByRole('textbox', { name: 'Описание' })).toHaveValue('');
  });

  it('names each type’s own initial state as «Тип» changes', async () => {
    const { dialog } = await openDialog('Баг');
    await within(dialog).findByText(/в статусе «Новая»/);

    await userEvent.click(within(dialog).getByRole('combobox', { name: 'Тип' }));
    await userEvent.click(await screen.findByRole('option', { name: 'Эпик' }));

    expect(within(dialog).getByRole('combobox', { name: 'Тип' })).toHaveTextContent('Эпик');
    expect(dialog).toHaveTextContent('в статусе «Новая» в области «Мастаск»');
  });
});

describe('mod-003 US-1 Scenario: Each type gets its own type key and its initial state', () => {
  it('creates an «Эпик» and then a «Баг» in a row: MSK-EPIC-1 and MSK-BUG-2, both «Новая»', async () => {
    const { title, submit } = await openDialog('Эпик');
    await userEvent.type(title, 'Первый');
    await submit();
    expect(await screen.findByText('Рабочий элемент MSK-EPIC-1 создан.')).toBeInTheDocument();

    const menu = await openPicker();
    await userEvent.click(within(menu).getByRole('menuitem', { name: 'Баг' }));
    const dialog = await screen.findByRole('dialog', { name: 'Новый рабочий элемент' });
    await userEvent.type(within(dialog).getByRole('textbox', { name: 'Заголовок' }), 'Второй');
    await userEvent.click(within(dialog).getByRole('button', { name: 'Создать' }));

    expect(await screen.findByText('Рабочий элемент MSK-BUG-2 создан.')).toBeInTheDocument();
    const table = screen.getByRole('table', { name: 'Рабочие элементы' });
    const rows = () =>
      within(table)
        .getAllByRole('row')
        .slice(1)
        .map((row) => {
          const cells = within(row).getAllByRole('cell');
          return [cells[0]?.textContent, cells[4]?.textContent];
        });
    await waitFor(() =>
      expect(rows()).toEqual([
        ['MSK-BUG-2', 'Новая'],
        ['MSK-EPIC-1', 'Новая'],
      ]),
    );
  });
});

describe('mod-003 US-1 Scenario Outline: An invalid title is refused and nothing is created', () => {
  const LINE_SEPARATOR = String.fromCharCode(0x2028);
  it.each([
    ['empty', 'Введите заголовок.', ''],
    ['three spaces', 'Введите заголовок.', '   '],
    ['256 characters after trimming', 'Не более 255 символов.', ` ${'я'.repeat(256)} `],
    [
      '«Сборка» + a line break + «падает»',
      'Заголовок должен быть в одну строку.',
      `Сборка${LINE_SEPARATOR}падает`,
    ],
  ])(
    '%s: «%s» under «Заголовок», the dialog stays, nothing is sent',
    async (_case, message, value) => {
      const { api, dialog, title, submit } = await openDialog('Баг');
      fireEvent.change(title, { target: { value } });

      await submit();

      expect(within(dialog).getByText(message)).toBeInTheDocument();
      expect(title).toHaveAttribute('aria-invalid', 'true');
      expect(title).toHaveValue(value);
      expect(posts(api)).toEqual([]);

      // Cleared as the field is edited.
      await userEvent.type(title, 'x');
      expect(within(dialog).queryByText(message)).not.toBeInTheDocument();
    },
  );

  it('places the server’s refusal of the title under «Заголовок» with the server’s text', async () => {
    const { dialog, title, submit } = await openDialog('Баг');
    server.use(
      http.post(
        WORK_ITEMS_URL,
        () =>
          HttpResponse.json(validationProblem({ title: 'workitems.workitem.title_multiline' }), {
            status: 400,
          }),
        { once: true },
      ),
    );
    await userEvent.type(title, 'Сборка падает');

    await submit();

    expect(
      await within(dialog).findByText('Заголовок должен быть в одну строку.'),
    ).toBeInTheDocument();
    expect(title).toHaveValue('Сборка падает');
    expect(within(dialog).queryByRole('alert')).not.toBeInTheDocument();
  });
});

describe('mod-003 US-1 Scenario: A title of exactly 255 characters is accepted', () => {
  it('creates the item with that title', async () => {
    const { api, title, submit } = await openDialog('Задача');
    const long = 'ж'.repeat(255);
    fireEvent.change(title, { target: { value: long } });

    await submit();

    expect(await screen.findByText('Рабочий элемент MSK-TASK-1 создан.')).toBeInTheDocument();
    expect(api.record(ids.msk, 1).title).toBe(long);
  });
});

describe('mod-003 US-1 Scenario: A repeated submission creates one item', () => {
  it('a double activation of «Создать» sends one submission', async () => {
    const { api, dialog, title } = await openDialog('Задача');
    await userEvent.type(title, 'Один раз');

    await userEvent.dblClick(within(dialog).getByRole('button', { name: 'Создать' }));

    expect(await screen.findByText('Рабочий элемент MSK-TASK-1 создан.')).toBeInTheDocument();
    const sent = posts(api).map((request) => (request.body as { requestId: string }).requestId);
    expect(new Set(sent).size).toBe(1);
    expect(api.data.items).toHaveLength(1);
  });

  it('a resend after a lost answer carries the same requestId, and the replay’s 200 lands as the item', async () => {
    const { api, dialog, title, submit } = await openDialog('Задача');
    // The first submission reaches the server and creates; its answer is lost.
    server.use(
      http.post(
        WORK_ITEMS_URL,
        async ({ request }) => {
          api.acceptCreate(ids.example, ids.msk, (await request.json()) as Record<string, unknown>);
          return HttpResponse.error();
        },
        { once: true },
      ),
    );
    await userEvent.type(title, 'Потерянный ответ');

    await submit();
    expect(await within(dialog).findByRole('alert')).toBeInTheDocument();
    expect(api.data.items).toHaveLength(1);

    await submit();

    expect(await screen.findByText('Рабочий элемент MSK-TASK-1 создан.')).toBeInTheDocument();
    const sent = posts(api).map((request) => (request.body as { requestId: string }).requestId);
    expect(sent).toHaveLength(2);
    expect(sent[0]).toBe(sent[1]);
    expect(api.data.items).toHaveLength(1);
  });

  it('a new dialog makes a new requestId', async () => {
    const { api, title, submit } = await openDialog('Задача');
    await userEvent.type(title, 'Первый');
    await submit();
    await screen.findByText('Рабочий элемент MSK-TASK-1 создан.');

    const menu = await openPicker();
    await userEvent.click(within(menu).getByRole('menuitem', { name: 'Задача' }));
    const dialog = await screen.findByRole('dialog', { name: 'Новый рабочий элемент' });
    await userEvent.type(within(dialog).getByRole('textbox', { name: 'Заголовок' }), 'Второй');
    await userEvent.click(within(dialog).getByRole('button', { name: 'Создать' }));
    await screen.findByText('Рабочий элемент MSK-TASK-2 создан.');

    const sent = posts(api).map((request) => (request.body as { requestId: string }).requestId);
    expect(sent).toHaveLength(2);
    expect(sent[0]).not.toBe(sent[1]);
  });
});

describe('mod-003 US-1 Scenario: The project is deactivated while the dialog is open', () => {
  it('shows «Проект не найден.» in the dialog, creates nothing, and closing shows the frame', async () => {
    const { api, dialog, title, submit } = await openDialog('Задача');
    const project = api.data.projects.find((candidate) => candidate.id === ids.msk);
    if (project) project.active = false;
    await userEvent.type(title, 'Поздно');

    await submit();

    expect(await within(dialog).findByRole('alert')).toHaveTextContent('Проект не найден.');
    expect(api.data.items).toHaveLength(0);
    expect(title).toHaveValue('Поздно');

    await userEvent.click(within(dialog).getByRole('button', { name: 'Отмена' }));

    expect(
      await screen.findByRole('heading', { level: 1, name: 'Проект не найден' }),
    ).toBeInTheDocument();
  });
});

describe('mod-003 spec § Error codes: a missing type is the dialog’s Alert', () => {
  it('shows «Тип рабочего элемента не найден.» in the Alert, the input kept', async () => {
    const { dialog, title, submit } = await openDialog('Задача');
    server.use(
      http.post(
        WORK_ITEMS_URL,
        () =>
          HttpResponse.json(
            {
              status: 404,
              title: 'workitems.workitem.type_not_found',
              detail: 'Тип рабочего элемента не найден.',
              errorCode: 'workitems.workitem.type_not_found',
            },
            { status: 404 },
          ),
        { once: true },
      ),
    );
    await userEvent.type(title, 'Без типа');

    await submit();

    expect(await within(dialog).findByRole('alert')).toHaveTextContent(
      'Тип рабочего элемента не найден.',
    );
    expect(title).toHaveValue('Без типа');
    expect(screen.queryByRole('heading', { level: 1, name: /не найден/ })).toBeNull();
  });
});
