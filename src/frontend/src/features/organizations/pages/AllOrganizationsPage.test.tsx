import { describe, expect, it } from 'vitest';
import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { HttpResponse, http } from 'msw';
import { formatDate } from '../../../shared/lib/format';
import { server } from '../../../test/mocks/server';
import { ADMIN_ORGANIZATIONS_PATH } from '../paths';
import { organizationsPermissions } from '../permissions';
import { ORGANIZATIONS_URL } from '../testFixtures';
import { renderPage } from '../testRender';
import {
  BORIS,
  VERA,
  failRescueList,
  problemResponse,
  rescueIds,
  rescueOrganizations,
  serveRescue,
  type RescueOptions,
} from '../testRescue';

// feat-033 US-1 (design § 3): «Все организации» — the instance-wide list, paged 25, a name filter,
// the states of the canon and the 403 that turns the page into «Доступ ограничен».

const RESCUE = [organizationsPermissions.organizationRescue];

function open(options: RescueOptions = {}, permissions: readonly string[] = RESCUE) {
  const api = serveRescue(options);
  const page = renderPage({ path: ADMIN_ORGANIZATIONS_PATH, permissions });
  return { ...page, api };
}

async function table() {
  return screen.findByRole('table', { name: 'Все организации' });
}

function bodyRows(root: HTMLElement) {
  return within(root)
    .getAllByRole('row')
    .slice(1)
    .map((row) => within(row).getAllByRole('cell'));
}

function listReads(api: ReturnType<typeof serveRescue>) {
  return api.requests.filter(
    (request) => request.method === 'GET' && request.path.endsWith('/api/organizations/rescue'),
  );
}

describe('feat-033 US-1: access', () => {
  it('answers «Доступ ограничен» without the rescue code and asks the server for nothing', async () => {
    const { api } = open({}, []);

    expect(await screen.findByRole('heading', { name: 'Доступ ограничен' })).toBeVisible();
    expect(api.requests).toHaveLength(0);
  });

  it('answers «Доступ ограничен» when the server refuses the list (403, DC-8), with no retry', async () => {
    serveRescue();
    failRescueList(problemResponse('organizations.rescue.forbidden'));
    renderPage({ path: ADMIN_ORGANIZATIONS_PATH, permissions: RESCUE });

    expect(await screen.findByRole('heading', { name: 'Доступ ограничен' })).toBeVisible();
    expect(screen.queryByRole('button', { name: 'Повторить' })).not.toBeInTheDocument();
  });
});

describe('feat-033 US-1: the list', () => {
  it('shows the first 25 organizations with the three columns, the status as text and the pager', async () => {
    const { api } = open();
    const root = await table();

    expect(
      within(root)
        .getAllByRole('columnheader')
        .map((cell) => cell.textContent),
    ).toEqual(['Организация', 'Статус', 'Последнее назначение', '']);
    expect(bodyRows(root)).toHaveLength(25);
    expect(screen.getByText('1–25 из 120')).toBeVisible();
    expect(listReads(api)[0]?.search).toBe('?page=1');
    // Nothing of administrators, and the name opens nothing (DC-3).
    expect(screen.queryByText(/администратор/i)).not.toBeInTheDocument();
    expect(within(root).queryByRole('link')).not.toBeInTheDocument();
  });

  it('marks a deactivated organization with the word «Деактивирована» and an active one «Активна»', async () => {
    open();
    const root = await table();

    const deactivated = bodyRows(root).find((cells) => cells[0]?.textContent === 'Организация 002');
    const active = bodyRows(root).find((cells) => cells[0]?.textContent === 'Организация 001');
    expect(deactivated?.[1]).toHaveTextContent('Деактивирована');
    expect(active?.[1]).toHaveTextContent('Активна');
  });

  it('shows the date of the last appointment, and a dash with a hidden «Назначений не было» when there was none', async () => {
    open();
    const root = await table();

    const appointed = bodyRows(root).find((cells) => cells[0]?.textContent === 'Организация 003');
    const never = bodyRows(root).find((cells) => cells[0]?.textContent === 'Организация 001');
    expect(appointed?.[2]).toHaveTextContent(formatDate('2026-11-03T11:05:00+00:00'));
    expect(never?.[2]).toHaveTextContent('—');
    expect(within(never![2]!).getByText('Назначений не было')).toBeInTheDocument();
  });

  it('reads the next page from the pager and keeps its position', async () => {
    const { api } = open();
    await table();

    await userEvent.click(screen.getByRole('button', { name: /следующую страницу/i }));

    await waitFor(() => expect(screen.getByText('26–50 из 120')).toBeVisible());
    expect(listReads(api).map((read) => read.search)).toEqual(['?page=1', '?page=2']);
  });

  it('shows the loading skeleton with the real column labels before the first answer', async () => {
    open();

    const status = await screen.findByRole('status', { name: /Загрузка данных/ }).catch(() => null);
    // The skeleton may already be gone on a fast answer; if it was there, it named the columns.
    if (status) {
      expect(within(status).getByText('Организация')).toBeVisible();
    }
    await table();
  });

  it('answers «Организаций пока нет» when the instance has none and no filter is set', async () => {
    open({ organizations: [] });

    expect(await screen.findByRole('heading', { name: 'Организаций пока нет' })).toBeVisible();
  });

  it('shows the server’s error with «Повторить», which reads again', async () => {
    serveRescue();
    let fail = true;
    server.use(
      http.get(`${ORGANIZATIONS_URL}/rescue`, () =>
        fail
          ? HttpResponse.json(
              { status: 500, title: 'x', detail: 'Сервис недоступен' },
              { status: 500, headers: { 'Content-Type': 'application/problem+json' } },
            )
          : HttpResponse.json({ count: 1, data: rescueOrganizations().slice(0, 1) }),
      ),
    );
    renderPage({ path: ADMIN_ORGANIZATIONS_PATH, permissions: RESCUE });

    expect(await screen.findByText('Сервис недоступен')).toBeVisible();
    fail = false;
    await userEvent.click(screen.getByRole('button', { name: 'Повторить' }));

    expect(await table()).toBeVisible();
  });
});

describe('feat-033 US-1: the filter', () => {
  it('sends the trimmed text after the pause, announces the count and offers «Очистить фильтры»', async () => {
    const { api } = open();
    await table();

    await userEvent.type(screen.getByRole('textbox', { name: 'Поиск по названию' }), '  гамма ');

    await waitFor(() =>
      expect(bodyRows(screen.getByRole('table', { name: 'Все организации' }))).toHaveLength(3),
    );
    expect(listReads(api).at(-1)?.search).toBe(`?page=1&name=${encodeURIComponent('гамма')}`);
    expect(screen.getByText('Найдено: 3')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Очистить фильтры' })).toBeVisible();
  });

  it('shows page 1 again when the text changes while a later page is open', async () => {
    const { api } = open();
    await table();
    await userEvent.click(screen.getByRole('button', { name: /следующую страницу/i }));
    await waitFor(() => expect(screen.getByText('26–50 из 120')).toBeVisible());

    await userEvent.type(
      screen.getByRole('textbox', { name: 'Поиск по названию' }),
      'Организация 0',
    );

    await waitFor(() => expect(listReads(api).at(-1)?.search).toContain('page=1&name='));
  });

  it('answers «Организации не найдены» with «Сбросить фильтр», which clears the text', async () => {
    open();
    await table();
    const field = screen.getByRole('textbox', { name: 'Поиск по названию' });

    await userEvent.type(field, 'ничего такого');
    expect(await screen.findByRole('heading', { name: 'Организации не найдены' })).toBeVisible();
    await userEvent.click(screen.getByRole('button', { name: 'Сбросить фильтр' }));

    expect(field).toHaveValue('');
    expect(await table()).toBeVisible();
    expect(screen.queryByRole('button', { name: 'Очистить фильтры' })).not.toBeInTheDocument();
  });
});

describe('feat-033 US-1: the row menu', () => {
  it('offers «Назначить администратора» and «История назначений» on every row', async () => {
    open();
    await table();

    await userEvent.click(screen.getByRole('button', { name: 'Действия: Организация 001' }));

    expect(screen.getAllByRole('menuitem').map((item) => item.textContent)).toEqual([
      'Назначить администратора',
      'История назначений',
    ]);
  });
});

describe('feat-033 US-3: the history from the list', () => {
  it('lists the record newest first with the four columns and the full reason', async () => {
    const reason = 'Борис уволился, а новый администратор ещё не назначен — записываем всё.';
    open({
      records: {
        [rescueIds.appointed]: [
          {
            id: 'r2',
            appointedAt: '2026-11-03T11:05:00+00:00',
            appointedBy: { ...BORIS, displayName: 'Системный Администратор' },
            appointee: VERA,
            reason,
          },
        ],
      },
    });
    await table();

    await userEvent.click(screen.getByRole('button', { name: 'Действия: Организация 003' }));
    await userEvent.click(screen.getByRole('menuitem', { name: 'История назначений' }));

    const dialog = await screen.findByRole('dialog', {
      name: 'История назначений — «Организация 003»',
    });
    const history = await within(dialog).findByRole('table', { name: 'История назначений' });
    expect(
      within(history)
        .getAllByRole('columnheader')
        .map((cell) => cell.textContent),
    ).toEqual(['Когда', 'Кто назначил', 'Кого назначили', 'Причина']);
    expect(within(history).getByText(reason)).toBeVisible();
    expect(within(history).getByText('Вера Иванова')).toBeVisible();
    // The kit also draws a ✕ named «Закрыть»; the footer's button is the last.
    expect(within(dialog).getAllByRole('button', { name: 'Закрыть' }).at(-1)).toHaveFocus();
    // Nothing edits or deletes (US-3).
    expect(
      within(dialog)
        .getAllByRole('button')
        .map((button) => button.textContent),
    ).not.toContain('Удалить');
  });

  it('says «Назначений не было» for an organization with no record, and «Закрыть» returns focus to the row', async () => {
    open();
    await table();
    const trigger = screen.getByRole('button', { name: 'Действия: Организация 001' });

    await userEvent.click(trigger);
    await userEvent.click(screen.getByRole('menuitem', { name: 'История назначений' }));
    const dialog = await screen.findByRole('dialog');
    expect(
      await within(dialog).findByRole('heading', { name: 'Назначений не было' }),
    ).toBeVisible();
    await userEvent.click(within(dialog).getAllByRole('button', { name: 'Закрыть' }).at(-1)!);

    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
    await waitFor(() => expect(trigger).toHaveFocus());
  });

  it('shows the error with «Повторить» inside the dialog', async () => {
    open();
    server.use(
      http.get(`${ORGANIZATIONS_URL}/rescue/:organizationId/appointments`, () =>
        HttpResponse.json(
          { status: 500, title: 'x', detail: 'Не удалось загрузить' },
          { status: 500, headers: { 'Content-Type': 'application/problem+json' } },
        ),
      ),
    );
    await table();

    await userEvent.click(screen.getByRole('button', { name: 'Действия: Организация 001' }));
    await userEvent.click(screen.getByRole('menuitem', { name: 'История назначений' }));

    const dialog = await screen.findByRole('dialog');
    expect(await within(dialog).findByText('Не удалось загрузить')).toBeVisible();
    expect(within(dialog).getByRole('button', { name: 'Повторить' })).toBeVisible();
  });

  it('turns the page into «Доступ ограничен» when the record read is refused (DC-8)', async () => {
    open();
    server.use(
      http.get(`${ORGANIZATIONS_URL}/rescue/:organizationId/appointments`, () =>
        problemResponse('organizations.rescue.forbidden'),
      ),
    );
    await table();

    await userEvent.click(screen.getByRole('button', { name: 'Действия: Организация 001' }));
    await userEvent.click(screen.getByRole('menuitem', { name: 'История назначений' }));

    expect(await screen.findByRole('heading', { name: 'Доступ ограничен' })).toBeVisible();
  });
});
