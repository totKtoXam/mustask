import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { apiUrl, recordRequests } from '../../../test/api';
import { server } from '../../../test/mocks/server';
import { ADMIN_CODES, servedObjectCodes } from '../../../test/organizationRoles';
import { renderPage } from '../../../test/renderPage';
import type { Participant, ParticipantsPage } from '../api/iterationsApi';
import { LIST_PATH, SPRINTS_ROUTE, dynamicsOf, dynamicsRow, sprintId, sprintRow } from '../testApi';

// mod-007 QA (frontend): the outcome's address fallbacks, empty states, «Итого», purged rows.

vi.setConfig({ testTimeout: 120_000 });

const SLOW = { timeout: 20_000 };
const SPRINT = sprintId(5);
const PATH = `${LIST_PATH}/${SPRINT}`;
const ROUTE = `${SPRINTS_ROUTE}/${SPRINT}`;

beforeEach(() => {
  servedObjectCodes.current = ADMIN_CODES;
});
afterEach(() => {
  servedObjectCodes.current = ADMIN_CODES;
});

const closed = {
  sprint: {
    ...sprintRow(5, { state: 'Closed', startDate: '2026-03-01', endDate: '2026-03-14' }),
    teamId: 'team',
    createdAt: '2026-02-20T09:00:00Z',
    createdBy: 'user',
    closedAt: '2026-03-15T09:00:00Z',
    closedBy: 'user',
  },
  closedByName: 'Анна Петрова',
  itemCount: 10,
  storyPoints: 20,
};

const figure = { min: 1, average: 2.5, max: 4 };
const figures = {
  closedItems: figure,
  closedItemsPercent: { min: 10, average: 62.5, max: 90 },
  closedStoryPoints: figure,
  closedStoryPointsPercent: { min: 10, average: 20, max: 30 },
};

function person(patch: Partial<Participant>): Participant {
  return {
    userId: 'u1',
    displayName: 'Анна Петрова',
    isMember: true,
    items: 5,
    storyPoints: 10,
    closedStoryPoints: 8,
    closedStoryPointsPercent: 80,
    closedItems: 4,
    closedItemsPercent: 80,
    comparedSprints: 2,
    dynamics: figures,
    ...patch,
  };
}

const participants: ParticipantsPage = {
  level: 'stories',
  from: '2025-12-01',
  to: '2026-03-31',
  rangeSprints: 3,
  rows: [person({}), person({ userId: null, displayName: null })],
  total: person({ userId: null, displayName: null, items: 10, storyPoints: 20 }),
  totalRows: 2,
};

const doneItems = {
  items: [
    {
      identifier: 'PRM-US-9',
      title: null,
      project: { id: 'p1', name: 'Проект Альфа' },
      storyPoints: 3,
      purged: true,
    },
  ],
  totalItems: 1,
  totalStoryPoints: 3,
};

function serve(patch: { dynamics?: object; participants?: object; items?: object } = {}) {
  recordRequests('get', ROUTE, () => closed);
  const dynamics = recordRequests(
    'get',
    `${ROUTE}/dynamics`,
    () => patch.dynamics ?? dynamicsOf(dynamicsRow(5), [dynamicsRow(4)]),
  );
  const people = recordRequests(
    'get',
    `${ROUTE}/participants`,
    () => patch.participants ?? participants,
  );
  recordRequests('get', `${ROUTE}/items`, () => patch.items ?? doneItems);
  return { dynamics, people };
}

describe('mod-007 design § 6.2 QA: an address that cannot be applied opens with the default', () => {
  it.each([
    ['an unknown level', '?level=zzz'],
    ['a half-given range (from only)', '?from=2026-01-01'],
    ['a half-given range (to only)', '?to=2026-01-01'],
    ['a reversed range', '?from=2026-05-01&to=2026-01-01'],
    ['a garbage range', '?from=abc&to=def'],
    ['an impossible date', '?from=2026-02-31&to=2026-13-01'],
  ])('%s sends no range and no error shows', async (_name, search) => {
    const { dynamics, people } = serve();
    renderPage(`${PATH}${search}`);

    await screen.findByRole('table', { name: 'Динамика спринта' }, SLOW);
    await screen.findByRole('table', { name: 'Итоги по участникам' }, SLOW);
    expect(dynamics.requests[0]?.search).toContain('level=stories');
    expect(dynamics.requests[0]?.search).not.toContain('from=');
    expect(people.requests[0]?.search).not.toContain('to=');
    expect(
      screen.queryByText('Дата окончания не может быть раньше даты начала.'),
    ).not.toBeInTheDocument();
  });

  it('reads a level and a whole range from the address and sends them', async () => {
    const { dynamics } = serve();
    renderPage(`${PATH}?level=features&from=2026-01-01&to=2026-02-01`);

    await screen.findByRole('table', { name: 'Динамика спринта' }, SLOW);
    const search = dynamics.requests[0]?.search ?? '';
    expect(search).toContain('level=features');
    expect(search).toContain('from=2026-01-01');
    expect(search).toContain('to=2026-02-01');
    expect(screen.getByRole('combobox', { name: 'Уровень' })).toHaveTextContent('Фичи');
  });

  it('does not apply a half-filled pair typed into the controls', async () => {
    serve();
    renderPage(PATH);
    await screen.findByRole('table', { name: 'Динамика спринта' }, SLOW);

    await userEvent.clear(screen.getByLabelText('С'));
    await userEvent.click(screen.getByRole('button', { name: 'Применить' }));
    expect(screen.getByTestId('location').dataset.search).toBe('');
  });
});

describe('mod-007 design § 6.3, § 6.6 QA: empty states', () => {
  it('says the range has no Closed sprint and writes «—» in the summary', async () => {
    const none = { min: null, average: null, max: null };
    serve({
      dynamics: {
        ...dynamicsOf(dynamicsRow(5), []),
        summary: {
          closedItems: none,
          closedItemsPercent: none,
          closedStoryPoints: none,
          closedStoryPointsPercent: none,
        },
      },
    });
    renderPage(PATH);

    expect(
      await screen.findByText('В выбранном диапазоне нет завершённых спринтов.'),
    ).toBeInTheDocument();
    expect(await screen.findByText('В диапазоне: 0 спринтов')).toBeInTheDocument();
    const table = screen.getByRole('table', { name: 'Динамика спринта' });
    const average = within(table).getByRole('row', { name: /Среднее/ });
    expect(within(average).getAllByText('—')).toHaveLength(4);
  });

  it('says nothing was held at the level, with no «Итого»', async () => {
    serve({ participants: { ...participants, rows: [], total: null, totalRows: 0 } });
    renderPage(PATH);

    expect(
      await screen.findByText('На выбранном уровне в спринте не было элементов.'),
    ).toBeInTheDocument();
    expect(screen.queryByText('Итого')).not.toBeInTheDocument();
  });

  it('says there is no done item', async () => {
    serve({ items: { items: [], totalItems: 0, totalStoryPoints: 0 } });
    renderPage(PATH);
    expect(await screen.findByText('В итоге нет выполненных элементов.')).toBeInTheDocument();
  });

  it('writes a percent as whole, half up, and «—» when nothing was held', async () => {
    serve({
      participants: {
        ...participants,
        rows: [person({ closedItemsPercent: 62.5, closedStoryPointsPercent: null })],
      },
    });
    renderPage(PATH);
    const table = await screen.findByRole('table', { name: 'Итоги по участникам' }, SLOW);
    const row = within(table).getByRole('row', { name: /Анна Петрова/ });
    expect(within(row).getByText('63 %')).toBeInTheDocument();
    expect(within(row).getByText('—')).toBeInTheDocument();
  });

  it('lets a person who left the organization keep a name and the tag', async () => {
    serve({ participants: { ...participants, rows: [person({ isMember: false })] } });
    renderPage(PATH);
    const table = await screen.findByRole('table', { name: 'Итоги по участникам' }, SLOW);
    expect(within(table).getByText('Анна Петрова')).toBeInTheDocument();
    expect(within(table).getByText('не в организации')).toBeInTheDocument();
  });
});

describe('mod-007 design § 6.4, § 6.5 QA: the rows that differ', () => {
  it('expands «Без исполнителя»', async () => {
    serve();
    renderPage(PATH);
    const table = await screen.findByRole('table', { name: 'Итоги по участникам' }, SLOW);
    await userEvent.click(within(table).getByRole('button', { name: 'Динамика: Без исполнителя' }));
    expect(within(table).getByText('Мин')).toBeInTheDocument();
    expect(within(table).getAllByText('2,5').length).toBeGreaterThan(0);
  });

  // QA D-2: design § 6.4 and US-9 say «Итого» expands like every row; it has no control.
  it('expands «Итого» to its own min, average and max', async () => {
    serve();
    renderPage(PATH);
    const table = await screen.findByRole('table', { name: 'Итоги по участникам' }, SLOW);
    expect(within(table).getByRole('button', { name: 'Динамика: Итого' })).toBeInTheDocument();
  });

  // QA D-3: D-9 A keeps the project of a purged row as recorded; the project cell is blank.
  it('keeps the recorded project of a purged row', async () => {
    serve();
    renderPage(PATH);
    const table = await screen.findByRole('table', { name: 'Итог спринта' }, SLOW);
    const purged = within(table).getByRole('row', { name: /PRM-US-9/ });
    expect(within(purged).getByText('Проект Альфа')).toBeInTheDocument();
  });

  it('keeps the recorded points of a purged row', async () => {
    serve();
    renderPage(PATH);
    const table = await screen.findByRole('table', { name: 'Итог спринта' }, SLOW);
    const purged = within(table).getByRole('row', { name: /PRM-US-9/ });
    expect(within(purged).getByText('3')).toBeInTheDocument();
    expect(within(purged).getByText('Удалён навсегда')).toBeInTheDocument();
  });

  it('retries a failed participants block on its own', async () => {
    recordRequests('get', ROUTE, () => closed);
    recordRequests('get', `${ROUTE}/dynamics`, () => dynamicsOf(dynamicsRow(5)));
    recordRequests('get', `${ROUTE}/items`, () => doneItems);
    server.use(
      http.get(apiUrl(`${ROUTE}/participants`), () => new HttpResponse(null, { status: 500 })),
    );
    renderPage(PATH);

    expect(await screen.findByText('Не удалось загрузить итоги по участникам')).toBeInTheDocument();
    expect(screen.getByRole('table', { name: 'Динамика спринта' })).toBeInTheDocument();
    recordRequests('get', `${ROUTE}/participants`, () => participants);
    await userEvent.click(screen.getByRole('button', { name: 'Повторить' }));
    expect(
      await screen.findByRole('table', { name: 'Итоги по участникам' }, SLOW),
    ).toBeInTheDocument();
  });
});

describe('mod-007 design § 6.1 QA: the head', () => {
  it('has one h1', async () => {
    serve();
    renderPage(PATH);
    await screen.findByRole('table', { name: 'Динамика спринта' }, SLOW);
    expect(screen.getAllByRole('heading', { level: 1 })).toHaveLength(1);
  });

  // QA D-1: design § 6.1 makes the sprint's name the h1; the page's h1 is the team name.
  it('makes the sprint name the h1', async () => {
    serve();
    renderPage(PATH);
    await screen.findByRole('table', { name: 'Динамика спринта' }, SLOW);
    await waitFor(() =>
      expect(screen.getByRole('heading', { level: 1 })).toHaveTextContent('Спринт 5'),
    );
  });
});
