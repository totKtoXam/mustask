import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { iterationErrorCodes } from '../../../shared/api/errorCodes';
import { apiUrl, problemText, recordRequests, serveProblem } from '../../../test/api';
import { server } from '../../../test/mocks/server';
import { ADMIN_CODES, VIEWER_CODES, servedObjectCodes } from '../../../test/organizationRoles';
import { renderPage } from '../../../test/renderPage';
import { LIST_PATH, SPRINTS_ROUTE, serveSprintList, sprintId, sprintRow } from '../testApi';

// mod-007 QA (frontend): paging, the Active edit, the close refusals, the reader's empty list.

vi.setConfig({ testTimeout: 120_000 });

beforeEach(() => {
  servedObjectCodes.current = ADMIN_CODES;
});
afterEach(() => {
  servedObjectCodes.current = ADMIN_CODES;
});

const TABLE = 'Спринты команды';

async function chooseFromMenu(sprint: string, entry: string) {
  await userEvent.click(screen.getByRole('button', { name: `Действия со спринтом ${sprint}` }));
  await userEvent.click(await screen.findByRole('menuitem', { name: entry }));
}

describe('mod-007 design § 3, § 4 QA: the list', () => {
  it('appends the next page on «Показать ещё» and drops the button on the last', async () => {
    const first = [sprintRow(3), sprintRow(2)];
    const last = [sprintRow(1)];
    recordRequests('get', SPRINTS_ROUTE, (request) =>
      request.search.includes('before=2')
        ? { items: last, nextBefore: null }
        : { items: first, nextBefore: 2 },
    );
    renderPage(LIST_PATH);

    const table = await screen.findByRole('table', { name: TABLE });
    expect(within(table).getAllByRole('row')).toHaveLength(3);
    await userEvent.click(screen.getByRole('button', { name: 'Показать ещё' }));
    await within(table).findByText('Спринт 1');
    expect(within(table).getAllByRole('row')).toHaveLength(4);
    expect(screen.queryByRole('button', { name: 'Показать ещё' })).not.toBeInTheDocument();
  });

  it('keeps the rows and says «Не удалось загрузить ещё спринты.» when the next page fails', async () => {
    recordRequests('get', SPRINTS_ROUTE, (request) =>
      request.search.includes('before=')
        ? new HttpResponse(null, { status: 500 })
        : { items: [sprintRow(3)], nextBefore: 3 },
    );
    renderPage(LIST_PATH);

    await screen.findByRole('table', { name: TABLE });
    await userEvent.click(screen.getByRole('button', { name: 'Показать ещё' }));
    expect(await screen.findByText('Не удалось загрузить ещё спринты.')).toBeInTheDocument();
    expect(screen.getByText('Спринт 3')).toBeInTheDocument();
    expect(screen.getAllByRole('button', { name: 'Повторить' }).length).toBeGreaterThan(0);
  });

  it('gives a reader the empty hint and no command', async () => {
    servedObjectCodes.current = VIEWER_CODES;
    serveSprintList([]);
    renderPage(LIST_PATH);

    expect(await screen.findByText('У команды пока нет спринтов')).toBeInTheDocument();
    expect(screen.getByText('Спринты создают участники и менеджеры команды.')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Создать спринт' })).not.toBeInTheDocument();
  });

  it('keeps the planner command in the bar next to the empty hint', async () => {
    serveSprintList([]);
    renderPage(LIST_PATH);

    expect(
      await screen.findByText('Создайте первый спринт, чтобы запланировать работу команды.'),
    ).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Создать спринт' })).toBeInTheDocument();
  });

  it('makes only a Closed name a link, and writes «закрыто из всего» for it', async () => {
    serveSprintList([
      sprintRow(1, {
        state: 'Closed',
        itemCount: 10,
        storyPoints: 40,
        closedItems: 6,
        closedStoryPoints: 25,
      }),
      sprintRow(2, { state: 'Active', startDate: '2026-03-01', endDate: '2026-03-14' }),
    ]);
    renderPage(LIST_PATH);

    const table = await screen.findByRole('table', { name: TABLE });
    expect(within(table).getByRole('link', { name: 'Спринт 1' })).toBeInTheDocument();
    expect(within(table).queryByRole('link', { name: 'Спринт 2' })).not.toBeInTheDocument();
    expect(within(table).getByText('6 из 10')).toBeInTheDocument();
    expect(within(table).getByText('25 из 40')).toBeInTheDocument();
  });
});

describe('mod-007 design § 5.1 QA: editing an Active sprint', () => {
  const active = sprintRow(2, { state: 'Active', startDate: '2026-03-01', endDate: '2026-03-14' });

  it('refuses clearing both dates in the browser, sends nothing, and shows the hint', async () => {
    serveSprintList([active]);
    renderPage(LIST_PATH);
    await screen.findByRole('table', { name: TABLE });
    const sent = recordRequests('put', `${SPRINTS_ROUTE}/${sprintId(2)}`, () => ({}));

    await chooseFromMenu('Спринт 2', 'Изменить');
    const dialog = await screen.findByRole('dialog', { name: 'Изменить спринт' });
    expect(
      within(dialog).getByText('Даты активного спринта можно менять, но не очищать.'),
    ).toBeInTheDocument();
    await userEvent.clear(within(dialog).getByLabelText('Дата начала'));
    await userEvent.clear(within(dialog).getByLabelText('Дата окончания'));
    await userEvent.click(within(dialog).getByRole('button', { name: 'Сохранить' }));

    expect(
      await within(dialog).findByText('У активного спринта должны быть обе даты.'),
    ).toBeInTheDocument();
    expect(sent.requests).toHaveLength(0);
  });

  it('refuses a half-cleared pair of an Active sprint with the Active text', async () => {
    serveSprintList([active]);
    renderPage(LIST_PATH);
    await screen.findByRole('table', { name: TABLE });
    const sent = recordRequests('put', `${SPRINTS_ROUTE}/${sprintId(2)}`, () => ({}));

    await chooseFromMenu('Спринт 2', 'Изменить');
    const dialog = await screen.findByRole('dialog', { name: 'Изменить спринт' });
    await userEvent.clear(within(dialog).getByLabelText('Дата начала'));
    await userEvent.click(within(dialog).getByRole('button', { name: 'Сохранить' }));

    expect(
      await within(dialog).findByText('У активного спринта должны быть обе даты.'),
    ).toBeInTheDocument();
    expect(sent.requests).toHaveLength(0);
  });
});

describe('mod-007 design § 5.5 QA: the close refusals', () => {
  const active = sprintRow(2, { state: 'Active', startDate: '2026-03-01', endDate: '2026-03-14' });
  const PREVIEW = `${SPRINTS_ROUTE}/${sprintId(2)}/close-preview`;
  const CLOSE = `${SPRINTS_ROUTE}/${sprintId(2)}/close`;
  const preview = {
    moving: { items: 1, storyPoints: 2 },
    staying: { items: 0, storyPoints: 0 },
    destinations: [
      { id: sprintId(3), name: 'Спринт 3', startDate: '2026-04-01', endDate: '2026-04-14' },
    ],
  };

  async function open() {
    serveSprintList([active, sprintRow(3)]);
    renderPage(LIST_PATH);
    await screen.findByRole('table', { name: TABLE });
    await chooseFromMenu('Спринт 2', 'Завершить спринт');
    return screen.findByRole('dialog', { name: 'Завершить спринт «Спринт 2»' });
  }

  it('shows a dated destination as «Название (начало – окончание)» and a singular count', async () => {
    recordRequests('get', PREVIEW, () => preview);
    const dialog = await open();
    expect(
      await within(dialog).findByText('Незавершённые элементы: 1 — их нужно перенести.'),
    ).toBeVisible();
    expect(within(dialog).getByText(/0 элементов, 0 SP/)).toBeVisible();
    expect(
      within(dialog).getByText(
        'Завершение нельзя отменить: спринт не вернуть в работу, а итог не изменится.',
      ),
    ).toBeVisible();
    expect(within(dialog).getByRole('combobox', { name: /Куда перенести/ })).toHaveTextContent(
      'Спринт 3 (01.04.2026 – 14.04.2026)',
    );
  });

  it('puts «destination invalid» under the field and keeps the dialog open', async () => {
    recordRequests('get', PREVIEW, () => preview);
    serveProblem('post', CLOSE, iterationErrorCodes.closeDestinationInvalid);
    const dialog = await open();
    await within(dialog).findByText('Незавершённые элементы: 1 — их нужно перенести.');
    await userEvent.click(within(dialog).getByRole('button', { name: 'Завершить спринт' }));

    expect(
      await within(dialog).findByText(problemText(iterationErrorCodes.closeDestinationInvalid)),
    ).toBeVisible();
    expect(screen.getByRole('dialog', { name: 'Завершить спринт «Спринт 2»' })).toBeInTheDocument();
  });

  it('clears the choice and reads the options again when the destination is gone', async () => {
    const reads = recordRequests('get', PREVIEW, () => preview);
    serveProblem('post', CLOSE, iterationErrorCodes.notFound);
    const dialog = await open();
    await within(dialog).findByText('Незавершённые элементы: 1 — их нужно перенести.');
    await userEvent.click(within(dialog).getByRole('button', { name: 'Завершить спринт' }));

    expect(
      await within(dialog).findByText(problemText(iterationErrorCodes.notFound)),
    ).toBeVisible();
    expect(reads.requests.length).toBeGreaterThan(1);
    expect(within(dialog).getByRole('button', { name: 'Завершить спринт' })).toBeDisabled();
  });

  it('closes the dialog when the sprint is no longer active', async () => {
    recordRequests('get', PREVIEW, () => preview);
    serveProblem('post', CLOSE, iterationErrorCodes.notActive);
    const dialog = await open();
    await within(dialog).findByText('Незавершённые элементы: 1 — их нужно перенести.');
    await userEvent.click(within(dialog).getByRole('button', { name: 'Завершить спринт' }));

    await vi.waitFor(() =>
      expect(
        screen.queryByRole('dialog', { name: 'Завершить спринт «Спринт 2»' }),
      ).not.toBeInTheDocument(),
    );
  });
});

describe('mod-007 design § 4 state 8 QA: a write that is refused for rights', () => {
  it('says «Недостаточно прав» when the create is refused', async () => {
    serveSprintList([sprintRow(3)]);
    server.use(
      http.post(apiUrl(SPRINTS_ROUTE), () =>
        HttpResponse.json(
          { errorCode: 'organizations.access.forbidden', title: 'Недостаточно прав' },
          { status: 403 },
        ),
      ),
    );
    renderPage(LIST_PATH);
    await screen.findByRole('table', { name: TABLE });

    await userEvent.click(screen.getByRole('button', { name: 'Создать спринт' }));
    const dialog = await screen.findByRole('dialog', { name: 'Новый спринт' });
    await userEvent.click(within(dialog).getByRole('button', { name: 'Создать' }));

    expect((await screen.findAllByText(/Недостаточно прав/)).length).toBeGreaterThan(0);
  });
});
