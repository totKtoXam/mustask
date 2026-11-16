import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { iterationErrorCodes } from '../../../shared/api/errorCodes';
import { hold, problemText, recordRequests, serveProblem } from '../../../test/api';
import { ADMIN_CODES, VIEWER_CODES, servedObjectCodes } from '../../../test/organizationRoles';
import { renderPage } from '../../../test/renderPage';
import { LIST_PATH, SPRINTS_ROUTE, serveSprintList, sprintId, sprintRow } from '../testApi';

// mod-007 design § 3, § 4, § 5, § 8, US-1..US-3: the team's «Спринты» through the real routes.

vi.setConfig({ testTimeout: 120_000 });

beforeEach(() => {
  servedObjectCodes.current = ADMIN_CODES;
});
afterEach(() => {
  servedObjectCodes.current = ADMIN_CODES;
});

const rows = [
  sprintRow(1, {
    state: 'Closed',
    startDate: '2026-02-01',
    endDate: '2026-02-14',
    itemCount: 10,
    storyPoints: 20,
    closedItems: 6,
    closedStoryPoints: 13,
  }),
  sprintRow(2, { state: 'Active', startDate: '2026-03-01', endDate: '2026-03-14', itemCount: 4 }),
  sprintRow(3),
];

const TABLE = 'Спринты команды';

async function table() {
  return screen.findByRole('table', { name: TABLE });
}

async function rowOf(name: string) {
  return within(await table()).getByRole('row', { name: new RegExp(name) });
}

async function chooseFromMenu(sprint: string, entry: string) {
  await userEvent.click(screen.getByRole('button', { name: `Действия со спринтом ${sprint}` }));
  await userEvent.click(await screen.findByRole('menuitem', { name: entry }));
}

describe('mod-007 design § 3: the list', () => {
  it('shows the sprints highest number first with their states and figures', async () => {
    serveSprintList(rows);
    renderPage(LIST_PATH);

    const names = within(await table())
      .getAllByRole('row')
      .slice(1)
      .map((row) => within(row).getAllByRole('cell')[1]?.textContent);
    expect(names).toEqual(['Спринт 3', 'Спринт 2', 'Спринт 1']);
    const closed = await rowOf('Спринт 1');
    expect(within(closed).getByText('6 из 10')).toBeInTheDocument();
    expect(within(closed).getByText('13 из 20')).toBeInTheDocument();
    expect(within(closed).getByRole('link', { name: 'Спринт 1' })).toHaveAttribute(
      'href',
      expect.stringContaining(`/sprints/${sprintId(1)}`),
    );
    expect(within(await rowOf('Спринт 2')).getByText('Активен')).toBeInTheDocument();
    expect(within(await rowOf('Спринт 2')).queryByRole('link')).not.toBeInTheDocument();
  });

  it('says there are no sprints, with the planner’s hint for a manager', async () => {
    serveSprintList([]);
    renderPage(LIST_PATH);
    expect(await screen.findByText('У команды пока нет спринтов')).toBeInTheDocument();
    expect(
      screen.getByText('Создайте первый спринт, чтобы запланировать работу команды.'),
    ).toBeInTheDocument();
  });

  it('shows a skeleton while the list is read', async () => {
    const held = hold('get', SPRINTS_ROUTE, { items: [], nextBefore: null });
    renderPage(LIST_PATH);
    await held.reached;
    expect(screen.getByRole('status', { name: 'Загрузка данных' })).toBeInTheDocument();
    held.release();
    expect(await screen.findByText('У команды пока нет спринтов')).toBeInTheDocument();
  });

  it('says the list failed to load and reads again on «Повторить»', async () => {
    serveProblem('get', SPRINTS_ROUTE, iterationErrorCodes.notFound, {}, { status: 500 });
    renderPage(LIST_PATH);
    expect(await screen.findByText('Не удалось загрузить спринты')).toBeInTheDocument();
    serveSprintList(rows);
    await userEvent.click(screen.getByRole('button', { name: 'Повторить' }));
    expect(await table()).toBeInTheDocument();
  });

  it('answers «Команда не найдена» when the team is gone', async () => {
    serveProblem('get', SPRINTS_ROUTE, 'organizations.team.not_found');
    renderPage(LIST_PATH);
    expect(await screen.findByText('Команда не найдена')).toBeInTheDocument();
  });
});

describe('mod-007 design § 8: who may manage', () => {
  it.each<[string, boolean, readonly string[]]>([
    ['an administrator', true, ADMIN_CODES],
    ['a team member holding the code', true, [...VIEWER_CODES, 'iterations.sprint.manage']],
    ['a reader', false, VIEWER_CODES],
    ['a member without the grant', false, [...VIEWER_CODES, 'boards.backlog.reorder']],
  ])('%s: manager commands are present is %s', async (_who, sees, codes) => {
    servedObjectCodes.current = codes;
    serveSprintList(rows);
    renderPage(LIST_PATH);

    await table();
    expect(Boolean(screen.queryByRole('button', { name: 'Создать спринт' }))).toBe(sees);
    expect(Boolean(screen.queryByRole('button', { name: 'Действия со спринтом Спринт 3' }))).toBe(
      sees,
    );
  });

  it('gives each state its own row menu', async () => {
    serveSprintList(rows);
    renderPage(LIST_PATH);
    await table();

    const entries = async (name: string) => {
      await userEvent.click(screen.getByRole('button', { name: `Действия со спринтом ${name}` }));
      const labels = within(await screen.findByRole('menu'))
        .getAllByRole('menuitem')
        .map((item) => item.textContent);
      await userEvent.keyboard('{Escape}');
      await waitFor(() => expect(screen.queryByRole('menu')).not.toBeInTheDocument());
      return labels;
    };
    expect(await entries('Спринт 3')).toEqual(['Изменить', 'Начать спринт', 'Удалить']);
    expect(await entries('Спринт 2')).toEqual(['Изменить', 'Завершить спринт']);
    expect(await entries('Спринт 1')).toEqual(['Переименовать']);
  });
});

describe('mod-007 design § 5: the commands', () => {
  it('creates a sprint and says so', async () => {
    serveSprintList(rows);
    renderPage(LIST_PATH);
    await table();
    const sent = recordRequests('post', SPRINTS_ROUTE, () => ({ id: sprintId(4) }));

    await userEvent.click(screen.getByRole('button', { name: 'Создать спринт' }));
    const dialog = await screen.findByRole('dialog', { name: 'Новый спринт' });
    await userEvent.type(within(dialog).getByRole('textbox', { name: 'Название' }), 'Спринт Альфа');
    await userEvent.click(within(dialog).getByRole('button', { name: 'Создать' }));

    expect(await screen.findByText('Спринт создан.')).toBeInTheDocument();
    expect(sent.requests[0]?.body).toEqual({
      name: 'Спринт Альфа',
      startDate: null,
      endDate: null,
    });
  });

  it('keeps the dialog open with the server’s text under «Название» when the name is taken', async () => {
    serveSprintList(rows);
    renderPage(LIST_PATH);
    await table();
    serveProblem('post', SPRINTS_ROUTE, iterationErrorCodes.nameTaken);

    await userEvent.click(screen.getByRole('button', { name: 'Создать спринт' }));
    const dialog = await screen.findByRole('dialog', { name: 'Новый спринт' });
    await userEvent.type(within(dialog).getByRole('textbox', { name: 'Название' }), 'Спринт 3');
    await userEvent.click(within(dialog).getByRole('button', { name: 'Создать' }));

    expect(
      await within(dialog).findByText(problemText(iterationErrorCodes.nameTaken)),
    ).toBeVisible();
  });

  it('refuses a reversed range in the browser and sends nothing', async () => {
    serveSprintList(rows);
    renderPage(LIST_PATH);
    await table();
    const sent = recordRequests('post', SPRINTS_ROUTE);

    await userEvent.click(screen.getByRole('button', { name: 'Создать спринт' }));
    const dialog = await screen.findByRole('dialog', { name: 'Новый спринт' });
    await userEvent.type(within(dialog).getByLabelText('Дата начала'), '2026-05-10');
    await userEvent.type(within(dialog).getByLabelText('Дата окончания'), '2026-05-01');
    await userEvent.click(within(dialog).getByRole('button', { name: 'Создать' }));

    expect(
      await within(dialog).findByText('Дата окончания не может быть раньше даты начала.'),
    ).toBeVisible();
    expect(sent.requests).toEqual([]);
  });

  it('starts a dated Planned sprint after the confirmation', async () => {
    serveSprintList([sprintRow(3, { startDate: '2026-04-01', endDate: '2026-04-14' })]);
    renderPage(LIST_PATH);
    await table();
    const sent = recordRequests('post', `${SPRINTS_ROUTE}/${sprintId(3)}/start`, () => ({}));

    await chooseFromMenu('Спринт 3', 'Начать спринт');
    const dialog = await screen.findByRole('dialog', { name: 'Начать спринт' });
    await userEvent.click(within(dialog).getByRole('button', { name: 'Начать' }));

    expect(await screen.findByText('Спринт начат.')).toBeInTheDocument();
    expect(sent.requests).toHaveLength(1);
  });

  it('asks for the dates first when the Planned sprint has none', async () => {
    serveSprintList([sprintRow(3)]);
    renderPage(LIST_PATH);
    await table();

    await chooseFromMenu('Спринт 3', 'Начать спринт');
    const dialog = await screen.findByRole('dialog', { name: 'Начать спринт' });
    expect(within(dialog).getByText('Чтобы начать спринт, укажите обе даты.')).toBeInTheDocument();
    expect(within(dialog).queryByRole('button', { name: 'Начать' })).not.toBeInTheDocument();
    await userEvent.click(within(dialog).getByRole('button', { name: 'Указать даты' }));
    expect(await screen.findByRole('dialog', { name: 'Изменить спринт' })).toBeInTheDocument();
  });

  it('says another sprint is active and keeps the start dialog open', async () => {
    serveSprintList([sprintRow(3, { startDate: '2026-04-01', endDate: '2026-04-14' })]);
    renderPage(LIST_PATH);
    await table();
    serveProblem('post', `${SPRINTS_ROUTE}/${sprintId(3)}/start`, iterationErrorCodes.activeExists);

    await chooseFromMenu('Спринт 3', 'Начать спринт');
    const dialog = await screen.findByRole('dialog', { name: 'Начать спринт' });
    await userEvent.click(within(dialog).getByRole('button', { name: 'Начать' }));

    expect(
      await within(dialog).findByText(problemText(iterationErrorCodes.activeExists)),
    ).toBeVisible();
  });

  it('removes a Planned sprint after the confirmation', async () => {
    serveSprintList(rows);
    renderPage(LIST_PATH);
    await table();
    const sent = recordRequests('delete', `${SPRINTS_ROUTE}/${sprintId(3)}`);

    await chooseFromMenu('Спринт 3', 'Удалить');
    const dialog = await screen.findByRole('dialog', { name: 'Удалить спринт' });
    await userEvent.click(within(dialog).getByRole('button', { name: 'Удалить' }));

    expect(await screen.findByText('Спринт удалён.')).toBeInTheDocument();
    expect(sent.requests).toHaveLength(1);
  });

  it('renames a Closed sprint', async () => {
    serveSprintList(rows);
    renderPage(LIST_PATH);
    await table();
    const sent = recordRequests('put', `${SPRINTS_ROUTE}/${sprintId(1)}`, () => ({}));

    await chooseFromMenu('Спринт 1', 'Переименовать');
    const dialog = await screen.findByRole('dialog', { name: 'Переименовать спринт' });
    const name = within(dialog).getByRole('textbox', { name: 'Название' });
    await userEvent.clear(name);
    await userEvent.type(name, 'Итоговый');
    await userEvent.click(within(dialog).getByRole('button', { name: 'Сохранить' }));

    expect(await screen.findByText('Спринт переименован.')).toBeInTheDocument();
    expect(sent.requests[0]?.body).toMatchObject({ name: 'Итоговый' });
  });
});

describe('mod-007 design § 5.5, US-3: closing', () => {
  const preview = {
    moving: { items: 3, storyPoints: 8 },
    staying: { items: 7, storyPoints: 12 },
    destinations: [
      { id: sprintId(3), name: 'Спринт 3', startDate: null, endDate: null },
      { id: sprintId(4), name: 'Спринт 4', startDate: null, endDate: null },
    ],
  };
  const PREVIEW_ROUTE = `${SPRINTS_ROUTE}/${sprintId(2)}/close-preview`;
  const CLOSE_ROUTE = `${SPRINTS_ROUTE}/${sprintId(2)}/close`;

  async function openClose() {
    serveSprintList(rows);
    renderPage(LIST_PATH);
    await table();
    await chooseFromMenu('Спринт 2', 'Завершить спринт');
    return screen.findByRole('dialog', { name: 'Завершить спринт «Спринт 2»' });
  }

  it('preselects the first destination and moves the unfinished items there', async () => {
    recordRequests('get', PREVIEW_ROUTE, () => preview);
    const sent = recordRequests('post', CLOSE_ROUTE, () => ({}));
    const dialog = await openClose();

    expect(
      await within(dialog).findByText('Незавершённые элементы: 3 — их нужно перенести.'),
    ).toBeVisible();
    expect(
      within(dialog).getByText(/Останутся в спринте и войдут в итог: 7 элементов, 12 SP/),
    ).toBeVisible();
    await userEvent.click(within(dialog).getByRole('button', { name: 'Завершить спринт' }));

    expect(await screen.findByText('Спринт завершён.')).toBeInTheDocument();
    expect(sent.requests[0]?.body).toEqual({ destinationSprintId: sprintId(3) });
  });

  it('asks for «Без спринта» when the team has no sprint to move to, and sends none', async () => {
    recordRequests('get', PREVIEW_ROUTE, () => ({
      ...preview,
      moving: { items: 0, storyPoints: 0 },
      destinations: [],
    }));
    const sent = recordRequests('post', CLOSE_ROUTE, () => ({}));
    const dialog = await openClose();

    expect(await within(dialog).findByText('Незавершённых элементов нет.')).toBeVisible();
    expect(within(dialog).getByRole('button', { name: 'Завершить спринт' })).toBeDisabled();
    await userEvent.click(within(dialog).getByRole('combobox', { name: /Куда перенести/ }));
    await userEvent.click(await screen.findByRole('option', { name: 'Без спринта' }));
    await userEvent.click(within(dialog).getByRole('button', { name: 'Завершить спринт' }));
    await screen.findByText('Спринт завершён.');
    expect(sent.requests[0]?.body).toEqual({ destinationSprintId: null });
  });

  it('shows the preview’s failure with «Повторить»', async () => {
    serveProblem('get', PREVIEW_ROUTE, iterationErrorCodes.notActive);
    const dialog = await openClose();
    expect(await within(dialog).findByText('Не удалось загрузить спринты.')).toBeVisible();
    expect(within(dialog).getByRole('button', { name: 'Повторить' })).toBeInTheDocument();
  });
});
