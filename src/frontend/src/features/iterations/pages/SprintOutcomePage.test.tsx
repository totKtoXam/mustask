import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { iterationErrorCodes } from '../../../shared/api/errorCodes';
import { recordRequests, serveProblem } from '../../../test/api';
import { ADMIN_CODES, VIEWER_CODES, servedObjectCodes } from '../../../test/organizationRoles';
import { renderPage } from '../../../test/renderPage';
import type { Participant, ParticipantsPage } from '../api/iterationsApi';
import { LIST_PATH, SPRINTS_ROUTE, dynamicsOf, dynamicsRow, sprintId, sprintRow } from '../testApi';

// mod-007 design § 6, US-5, US-9, US-10: a Closed sprint's outcome through the real routes.

vi.setConfig({ testTimeout: 120_000 });

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

const dynamics = dynamicsOf(dynamicsRow(5), [dynamicsRow(4), dynamicsRow(3)]);

const figures = {
  closedItems: { min: 2, average: 3.5, max: 5 },
  closedItemsPercent: { min: 20, average: 35, max: 50 },
  closedStoryPoints: { min: 3, average: 6.3, max: 8 },
  closedStoryPointsPercent: { min: 30, average: 63, max: 80 },
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
  rows: [
    person({}),
    person({ userId: 'u2', displayName: 'Иван Сидоров', isMember: false, comparedSprints: 0 }),
    person({ userId: null, displayName: null }),
  ],
  total: person({ userId: null, displayName: null, items: 10, storyPoints: 20 }),
  totalRows: 3,
};

const items = {
  items: [
    {
      identifier: 'PRM-US-1',
      title: 'Вход в систему',
      project: { id: 'p1', name: 'Проект Альфа' },
      storyPoints: 5,
      purged: false,
    },
    {
      identifier: 'PRM-US-9',
      title: null,
      project: { id: 'p1', name: 'Проект Альфа' },
      storyPoints: 3,
      purged: true,
    },
  ],
  totalItems: 2,
  totalStoryPoints: 8,
};

function serveOutcome() {
  recordRequests('get', ROUTE, () => closed);
  recordRequests('get', `${ROUTE}/dynamics`, () => dynamics);
  recordRequests('get', `${ROUTE}/participants`, () => participants);
  recordRequests('get', `${ROUTE}/items`, () => items);
}

describe('mod-007 design § 6: the outcome page', () => {
  it('shows the facts and the three blocks of a Closed sprint', async () => {
    serveOutcome();
    renderPage(PATH);

    expect(await screen.findByRole('heading', { name: 'Спринт 5' })).toBeInTheDocument();
    expect(screen.getByText(/Анна Петрова, /)).toBeInTheDocument();
    expect(await screen.findByRole('heading', { name: 'Динамика' })).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'Итоги по участникам' })).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'Итог спринта' })).toBeInTheDocument();
    expect(await screen.findByText('В диапазоне: 2 спринта')).toBeInTheDocument();
  });

  it('writes this sprint first and bold, the range’s sprints as links, and a decimal comma in the summary', async () => {
    serveOutcome();
    renderPage(PATH);

    const table = await screen.findByRole('table', { name: 'Динамика спринта' });
    const lines = within(table)
      .getAllByRole('row')
      .slice(1)
      .map((row) => within(row).getAllByRole('cell')[0]?.textContent);
    expect(lines).toEqual(['Спринт 5 (этот)', 'Спринт 4', 'Спринт 3', 'Мин', 'Среднее', 'Макс']);
    expect(within(table).getByRole('link', { name: 'Спринт 4' })).toHaveAttribute(
      'href',
      expect.stringContaining(`/sprints/${sprintId(4)}`),
    );
    const average = within(table).getByRole('row', { name: /Среднее/ });
    expect(within(average).getByText('6,3')).toBeInTheDocument();
    expect(within(average).getByText('63 %')).toBeInTheDocument();
  });

  it('expands a person to the min, average and max, and says «k из N»', async () => {
    serveOutcome();
    renderPage(PATH);

    const table = await screen.findByRole('table', { name: 'Итоги по участникам' });
    expect(within(table).getAllByText('2 из 3').length).toBeGreaterThan(0);
    expect(within(table).getByText('0 из 3')).toBeInTheDocument();
    expect(within(table).getByText('не в организации')).toBeInTheDocument();
    expect(within(table).getByText('Без исполнителя')).toBeInTheDocument();
    expect(within(table).getByText('Итого')).toBeInTheDocument();

    const open = within(table).getByRole('button', { name: 'Динамика: Анна Петрова' });
    expect(open).toHaveAttribute('aria-expanded', 'false');
    await userEvent.click(open);
    expect(open).toHaveAttribute('aria-expanded', 'true');
    expect(within(table).getAllByText('Мин').length).toBeGreaterThan(0);
    expect(within(table).getByText('6,3')).toBeInTheDocument();
    await userEvent.click(open);
    expect(open).toHaveAttribute('aria-expanded', 'false');
  });

  it('says there is nothing to compare for a person compared across no sprint', async () => {
    serveOutcome();
    renderPage(PATH);

    const table = await screen.findByRole('table', { name: 'Итоги по участникам' });
    await userEvent.click(within(table).getByRole('button', { name: 'Динамика: Иван Сидоров' }));
    expect(within(table).getByText('Нет данных для сравнения')).toBeInTheDocument();
  });

  it('writes a purged item as its identifier and «Удалён навсегда», without a link', async () => {
    serveOutcome();
    renderPage(PATH);

    const table = await screen.findByRole('table', { name: 'Итог спринта' });
    expect(within(table).getByRole('link', { name: 'PRM-US-1' })).toBeInTheDocument();
    const purged = within(table).getByRole('row', { name: /PRM-US-9/ });
    expect(within(purged).getByText('Удалён навсегда')).toBeInTheDocument();
    expect(within(purged).queryByRole('link')).not.toBeInTheDocument();
    expect(screen.getByText('2 элемента, 8 SP')).toBeInTheDocument();
  });

  it('keeps the other blocks when one fails, and reads it again on «Повторить»', async () => {
    serveOutcome();
    serveProblem('get', `${ROUTE}/dynamics`, iterationErrorCodes.notFound, {}, { status: 500 });
    renderPage(PATH);

    expect(await screen.findByText('Не удалось загрузить динамику спринта')).toBeInTheDocument();
    expect(await screen.findByRole('table', { name: 'Итоги по участникам' })).toBeInTheDocument();
    expect(await screen.findByRole('table', { name: 'Итог спринта' })).toBeInTheDocument();

    recordRequests('get', `${ROUTE}/dynamics`, () => dynamics);
    await userEvent.click(screen.getByRole('button', { name: 'Повторить' }));
    expect(await screen.findByRole('table', { name: 'Динамика спринта' })).toBeInTheDocument();
  });

  it('says «Спринт не найден» for a sprint that is not Closed or not there', async () => {
    serveProblem('get', ROUTE, iterationErrorCodes.notFound);
    renderPage(PATH);
    expect(await screen.findByRole('heading', { name: 'Спринт не найден' })).toBeInTheDocument();
  });
});

describe('mod-007 design § 6.2: the level and the range', () => {
  it('keeps the level in the address', async () => {
    serveOutcome();
    renderPage(PATH);
    await screen.findByRole('table', { name: 'Динамика спринта' });

    await userEvent.click(screen.getByRole('combobox', { name: 'Уровень' }));
    await userEvent.click(await screen.findByRole('option', { name: 'Эпики' }));
    expect(screen.getByTestId('location')).toHaveAttribute('data-search', '?level=epics');
  });

  it('refuses a reversed range under «По» and leaves the address alone', async () => {
    serveOutcome();
    renderPage(PATH);
    await screen.findByRole('table', { name: 'Динамика спринта' });

    const from = screen.getByLabelText('С');
    const to = screen.getByLabelText('По');
    await userEvent.clear(from);
    await userEvent.type(from, '2026-03-01');
    await userEvent.clear(to);
    await userEvent.type(to, '2026-01-01');
    await userEvent.click(screen.getByRole('button', { name: 'Применить' }));

    expect(
      await screen.findByText('Дата окончания не может быть раньше даты начала.'),
    ).toBeInTheDocument();
    expect(screen.getByTestId('location').dataset.search).toBe('');
  });

  it('puts a valid range in the address', async () => {
    serveOutcome();
    renderPage(PATH);
    await screen.findByRole('table', { name: 'Динамика спринта' });

    const from = screen.getByLabelText('С');
    const to = screen.getByLabelText('По');
    await userEvent.clear(from);
    await userEvent.type(from, '2026-01-01');
    await userEvent.clear(to);
    await userEvent.type(to, '2026-03-31');
    await userEvent.click(screen.getByRole('button', { name: 'Применить' }));

    expect(screen.getByTestId('location')).toHaveAttribute(
      'data-search',
      '?from=2026-01-01&to=2026-03-31',
    );
  });

  it('says how many sprints the range holds and that the list is cut at the cap', async () => {
    recordRequests('get', ROUTE, () => closed);
    recordRequests('get', `${ROUTE}/dynamics`, () => ({
      ...dynamics,
      rangeSprints: 120,
      shownSprints: 100,
      truncated: true,
    }));
    recordRequests('get', `${ROUTE}/participants`, () => participants);
    recordRequests('get', `${ROUTE}/items`, () => items);
    renderPage(PATH);

    expect(await screen.findByText('В диапазоне: 120 спринтов')).toBeInTheDocument();
    expect(
      screen.getByText('Показаны последние 100 из 120 спринтов диапазона.'),
    ).toBeInTheDocument();
  });
});

describe('mod-007 design § 8: who may rename', () => {
  it.each<[string, boolean, readonly string[]]>([
    ['an administrator', true, ADMIN_CODES],
    ['a reader', false, VIEWER_CODES],
  ])('%s: «Переименовать» is present is %s', async (_who, sees, codes) => {
    servedObjectCodes.current = codes;
    serveOutcome();
    renderPage(PATH);
    await screen.findByRole('table', { name: 'Динамика спринта' });
    expect(Boolean(screen.queryByRole('button', { name: /Переименовать|Ещё действия/ }))).toBe(
      sees,
    );
  });
});
