import { describe, expect, it, vi } from 'vitest';
import { Route, useNavigate, useNavigationType } from 'react-router-dom';
import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { authValue, renderShell } from '../../../test/renderShell';
import { OrganizationGate } from '../../organizations';
import { serveWorkItems } from '../testApi';
import {
  deleteRecord,
  emptyDataset,
  ids,
  item,
  listFilterDataset,
  type Dataset,
} from '../testFixtures';
import { MSK_LIST, mskForm, projectContext } from '../testRender';
import { WORK_ITEMS_ROUTE, WORK_ITEM_ROUTE } from '../paths';
import { WorkItemFormPage } from './WorkItemFormPage';
import { WorkItemsPage } from './WorkItemsPage';

// feat-003 BR-WS-5, BR-WS-11, design § 6, R-1: a purge on a card opened from a row of the list
// returns to that list under its filters, page 1, with mod-005's notice, by a replace; a card
// reached any other way lands on the list unfiltered; the card's trail «Рабочие элементы» opens the
// list unfiltered, and Back is the way back under the filters.

vi.setConfig({ testTimeout: 120_000 });

const enc = encodeURIComponent;
const DELETED = `${MSK_LIST}?state=${enc('Удалена')}`;

/** How the list was reached: a replace leaves no entry of the purged address to go back to. */
function ReachedBy() {
  return <output data-testid="reached">{useNavigationType()}</output>;
}

/** The browser’s Back, which the memory router has no button for. */
function BackButton() {
  const navigate = useNavigate();
  return (
    <button type="button" onClick={() => void navigate(-1)}>
      Назад в истории
    </button>
  );
}

function mount(
  path: string,
  data: Dataset = listFilterDataset(),
  role: 'admin' | 'member' = 'admin',
) {
  serveWorkItems(data);
  return renderShell({
    path,
    auth: authValue([]),
    contextSource: projectContext(),
    access: role,
    routes: (
      <Route element={<OrganizationGate />}>
        <Route
          path={WORK_ITEMS_ROUTE}
          element={
            <>
              <WorkItemsPage />
              <ReachedBy />
            </>
          }
        />
        <Route
          path={WORK_ITEM_ROUTE}
          element={
            <>
              <WorkItemFormPage />
              <BackButton />
            </>
          }
        />
      </Route>
    ),
  });
}

const table = () => screen.findByRole('table', { name: 'Рабочие элементы' });
const rowsOf = (element: HTMLElement) => within(element).getAllByRole('row').slice(1);
const identifiers = async () =>
  rowsOf(await table()).map((row) => within(row).getAllByRole('cell')[0]?.textContent ?? '');
const addressSearch = () => screen.getByTestId('location').getAttribute('data-search') ?? '';
const addressPath = () => screen.getByTestId('location').textContent ?? '';

async function purgeOnTheCard() {
  await userEvent.click(await screen.findByRole('button', { name: 'Ещё действия' }));
  await userEvent.click(await screen.findByRole('menuitem', { name: 'Удалить навсегда' }));
  await userEvent.click(
    within(
      await screen.findByRole('dialog', { name: 'Удалить рабочий элемент навсегда?' }),
    ).getByRole('button', { name: 'Удалить навсегда' }),
  );
}

describe('feat-003 BR-WS-5 a purge on a card opened from a row of the list', () => {
  it('returns to that list under its filters, with the notice, by a replace, and the purged item is not listed', async () => {
    mount(`${DELETED}&assigneeId=${ids.zoya}&assigneeId=${ids.ivan}`);
    await waitFor(async () => expect(await identifiers()).toEqual(['MSK-FEAT-6', 'MSK-US-5']));

    await userEvent.click(screen.getByRole('link', { name: 'Импорт склада' }));
    await screen.findByRole('heading', { level: 1, name: 'Фича MSK-FEAT-6' });
    await purgeOnTheCard();

    expect(await screen.findByText('Рабочий элемент MSK-FEAT-6 удалён навсегда.')).toBeVisible();
    await waitFor(() => expect(addressPath()).toBe(MSK_LIST));
    expect(addressSearch()).toBe(
      `?state=${enc('Удалена')}&assigneeId=${ids.zoya}&assigneeId=${ids.ivan}`,
    );
    expect(screen.getByTestId('reached')).toHaveTextContent('REPLACE');
    await waitFor(async () => expect(await identifiers()).toEqual(['MSK-US-5']));
  });

  it('shows the first page of the list, whatever page the row was on', async () => {
    const data = emptyDataset();
    for (let number = 1; number <= 120; number += 1) {
      data.items.push(
        deleteRecord(item(ids.msk, number, 'TASK', `Удалённая ${number}`), {
          at: new Date(2026, 9, 1, 0, number).toISOString(),
        }),
      );
    }
    mount(DELETED, data);
    await table();
    await userEvent.click(screen.getByRole('button', { name: 'Перейти на следующую страницу' }));
    await screen.findByText('51–100 из 120');

    await userEvent.click(screen.getByRole('link', { name: 'Удалённая 70' }));
    await screen.findByRole('heading', { level: 1, name: 'Задача MSK-TASK-70' });
    await purgeOnTheCard();

    expect(await screen.findByText('1–50 из 119')).toBeInTheDocument();
    expect(addressSearch()).toBe(`?state=${enc('Удалена')}`);
  });

  it('Back from the card shows the list under the same filters', async () => {
    mount(`${DELETED}&search=${enc('склада')}`);
    await waitFor(async () => expect(await identifiers()).toEqual(['MSK-FEAT-6']));

    await userEvent.click(screen.getByRole('link', { name: 'Импорт склада' }));
    await screen.findByRole('heading', { level: 1, name: 'Фича MSK-FEAT-6' });
    await userEvent.click(screen.getByRole('button', { name: 'Назад в истории' }));

    await waitFor(() => expect(addressPath()).toBe(MSK_LIST));
    expect(addressSearch()).toBe(`?state=${enc('Удалена')}&search=${enc('склада')}`);
    await waitFor(async () => expect(await identifiers()).toEqual(['MSK-FEAT-6']));
  });

  it('after a restore Back shows the list under its filters, the item gone from «Удалена»', async () => {
    mount(DELETED, listFilterDataset(), 'member');
    await waitFor(async () => expect(await identifiers()).toEqual(['MSK-FEAT-6', 'MSK-US-5']));

    await userEvent.click(screen.getByRole('link', { name: 'Импорт склада' }));
    await userEvent.click(await screen.findByRole('button', { name: 'Восстановить' }));
    await userEvent.click(
      within(
        await screen.findByRole('dialog', { name: 'Восстановить рабочий элемент?' }),
      ).getByRole('button', { name: 'Восстановить' }),
    );
    expect(await screen.findByText('Рабочий элемент MSK-FEAT-6 восстановлен.')).toBeVisible();
    await userEvent.click(screen.getByRole('button', { name: 'Назад в истории' }));

    await waitFor(async () => expect(await identifiers()).toEqual(['MSK-US-5']));
    expect(addressSearch()).toBe(`?state=${enc('Удалена')}`);
  });
});

describe('feat-003 BR-WS-5 a card reached any other way', () => {
  it('lands on the list unfiltered after a purge, by a replace', async () => {
    mount(mskForm('MSK-FEAT-6'));
    await screen.findByRole('heading', { level: 1, name: 'Фича MSK-FEAT-6' });

    await purgeOnTheCard();

    expect(await screen.findByText('Рабочий элемент MSK-FEAT-6 удалён навсегда.')).toBeVisible();
    await waitFor(() => expect(addressPath()).toBe(MSK_LIST));
    expect(addressSearch()).toBe('');
    expect(screen.getByTestId('reached')).toHaveTextContent('REPLACE');
  });
});

describe('feat-003 BR-WS-11 the card’s trail', () => {
  it('«Рабочие элементы» opens the list unfiltered, and Back is the way back under the filters', async () => {
    mount(`${DELETED}&assigneeId=${ids.zoya}`);
    await waitFor(async () => expect(await identifiers()).toEqual(['MSK-FEAT-6']));
    await userEvent.click(screen.getByRole('link', { name: 'Импорт склада' }));
    await screen.findByRole('heading', { level: 1, name: 'Фича MSK-FEAT-6' });

    await userEvent.click(
      within(screen.getByRole('navigation', { name: 'Путь к странице' })).getByRole('link', {
        name: 'Рабочие элементы',
      }),
    );

    await waitFor(() => expect(addressPath()).toBe(MSK_LIST));
    expect(addressSearch()).toBe('');
    await waitFor(async () =>
      expect(await identifiers()).toEqual([
        'MSK-BUG-42',
        'MSK-US-3',
        'MSK-TASK-4',
        'MSK-BUG-2',
        'MSK-US-1',
      ]),
    );
  });
});
