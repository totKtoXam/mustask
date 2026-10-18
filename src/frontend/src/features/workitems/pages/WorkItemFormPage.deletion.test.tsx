import { describe, expect, it, vi } from 'vitest';
import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { Route, useNavigationType } from 'react-router-dom';
import { authValue, renderShell } from '../../../test/renderShell';
import { OrganizationGate } from '../../organizations';
import { serveWorkItems } from '../testApi';
import { deleteRecord, hierarchyDataset, recordOf, stateId } from '../testFixtures';
import { MSK_LIST, mskForm, projectContext, renderWorkItems } from '../testRender';
import { WORK_ITEMS_ROUTE, WORK_ITEM_ROUTE } from '../paths';
import { WorkItemFormPage } from './WorkItemFormPage';

// mod-005 as the card's page meets it (design § 3-§ 6 on the item's own page): the three writes
// from the page, the deleted card in a page, and a purge that leaves the page by a replace so Back
// never meets the purged address. Each user action draws the kit's fields in jsdom; a test of
// several steps outlasts the default.

vi.setConfig({ testTimeout: 120_000 });

const STORY = mskForm('MSK-US-2');
const HEADING = 'Пользовательская история MSK-US-2';

const restoreButton = () => screen.queryByRole('button', { name: 'Восстановить' });

async function chooseFromMenu(name: string) {
  await userEvent.click(screen.getByRole('button', { name: 'Ещё действия' }));
  await userEvent.click(await screen.findByRole('menuitem', { name }));
}

describe('mod-005 US-1, US-4 on the page: the card stays on its address through Delete and Restore', () => {
  it('Delete turns the page into the deleted card, with the notice in the shell; Restore turns it back', async () => {
    const data = hierarchyDataset();
    recordOf(data, 2).stateId = stateId('US', 'Активна');
    serveWorkItems(data);
    renderWorkItems(STORY, { role: 'member' });
    await screen.findByRole('heading', { level: 1, name: HEADING });
    await screen.findByRole('textbox', { name: 'Заголовок' });

    await chooseFromMenu('Удалить');
    await userEvent.click(
      within(await screen.findByRole('dialog', { name: 'Удалить рабочий элемент?' })).getByRole(
        'button',
        { name: 'Удалить' },
      ),
    );

    expect(await screen.findByText('Рабочий элемент MSK-US-2 удалён.')).toBeVisible();
    await waitFor(() => expect(restoreButton()).toBeEnabled());
    expect(screen.getByRole('heading', { level: 1, name: HEADING })).toBeVisible();
    expect(screen.getByTestId('location')).toHaveTextContent(STORY);
    expect(screen.queryByRole('button', { name: 'Сохранить' })).not.toBeInTheDocument();

    await userEvent.click(restoreButton() as HTMLElement);
    await userEvent.click(
      within(
        await screen.findByRole('dialog', { name: 'Восстановить рабочий элемент?' }),
      ).getByRole('button', { name: 'Восстановить' }),
    );

    expect(await screen.findByText('Рабочий элемент MSK-US-2 восстановлен.')).toBeVisible();
    await waitFor(() => expect(screen.getByRole('button', { name: 'Сохранить' })).toBeDisabled());
    expect(screen.getByRole('combobox', { name: 'Статус' })).toHaveTextContent('Активна');
    expect(restoreButton()).not.toBeInTheDocument();
  });
});

describe('mod-005 design § 9 on the page: a deleted item by its address, per role', () => {
  it('an administrator reads the deleted card with «Восстановить» and «Удалить навсегда»', async () => {
    const data = hierarchyDataset();
    deleteRecord(recordOf(data, 2));
    serveWorkItems(data);
    renderWorkItems(STORY, { role: 'admin' });
    await screen.findByRole('heading', { level: 1, name: HEADING });

    expect(await screen.findByRole('button', { name: 'Восстановить' })).toBeEnabled();
    await userEvent.click(screen.getByRole('button', { name: 'Ещё действия' }));
    const menu = await screen.findByRole('menu', { name: 'Ещё действия' });
    expect(
      within(menu)
        .getAllByRole('menuitem')
        .map((entry) => entry.textContent),
    ).toEqual(['Копировать ссылку', 'Удалить навсегда']);
  });

  it('a reader reads the first sentence, with no way back and no «Удалить навсегда»', async () => {
    const data = hierarchyDataset();
    deleteRecord(recordOf(data, 2));
    serveWorkItems(data);
    renderWorkItems(STORY, { role: 'viewer' });
    await screen.findByRole('heading', { level: 1, name: HEADING });

    expect(
      await screen.findByText('Рабочий элемент удалён и доступен только для чтения.', {
        exact: false,
      }),
    ).toBeVisible();
    expect(screen.queryByText('Восстановите его', { exact: false })).not.toBeInTheDocument();
    expect(restoreButton()).not.toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: 'Ещё действия' }));
    const menu = await screen.findByRole('menu', { name: 'Ещё действия' });
    expect(within(menu).getAllByRole('menuitem')).toHaveLength(1);
  });
});

/** The list page as a stand-in, which says how it was reached: a replace leaves no entry to go back to. */
function ListStandIn() {
  return <p data-testid="reached">{useNavigationType()}</p>;
}

describe('mod-005 US-6 on the page: Purge leaves by a replace', () => {
  it('goes to «Рабочие элементы» with the notice, and the purged address is not one Back returns to', async () => {
    const data = hierarchyDataset();
    deleteRecord(recordOf(data, 2));
    serveWorkItems(data);
    renderShell({
      path: STORY,
      auth: authValue([]),
      contextSource: projectContext(),
      access: 'admin',
      routes: (
        <Route element={<OrganizationGate />}>
          <Route path={WORK_ITEMS_ROUTE} element={<ListStandIn />} />
          <Route path={WORK_ITEM_ROUTE} element={<WorkItemFormPage />} />
        </Route>
      ),
    });
    await screen.findByRole('heading', { level: 1, name: HEADING });

    await chooseFromMenu('Удалить навсегда');
    await userEvent.click(
      within(
        await screen.findByRole('dialog', { name: 'Удалить рабочий элемент навсегда?' }),
      ).getByRole('button', { name: 'Удалить навсегда' }),
    );

    expect(await screen.findByTestId('reached')).toHaveTextContent('REPLACE');
    expect(screen.getByTestId('location')).toHaveTextContent(MSK_LIST);
    expect(await screen.findByText('Рабочий элемент MSK-US-2 удалён навсегда.')).toBeVisible();
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
  });
});
