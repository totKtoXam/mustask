import { describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Route, Routes, useLocation } from 'react-router-dom';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { checkFor, type TestRole } from '../../../test/organizationRoles';
import { AccessCheckContext } from '../../../shared/lib/access';
import { NoticeProvider } from '../../../shared/ui';
import { useWorkItemDialog } from '../hooks/useWorkItemDialog';
import { serveWorkItems, type WorkItemsTestApi } from '../testApi';
import { deleteRecord, hierarchyDataset, ids, recordOf, stateId } from '../testFixtures';
import { workItemPath } from '../paths';
import { WorkItemCardDialog } from './WorkItemCardDialog';

// mod-005 design § 3, § 5, § 6 and § 7 as the card's dialog host meets them: the three writes are
// made in the dialog over a backlog, the host behind reads its views again after each (the
// callback of BR-C-21), and a purge closes the dialog because nothing is left to look at. Each
// user action draws the kit's fields in jsdom, which are slow.

vi.setConfig({ testTimeout: 120_000 });

const BACKLOG = `/organizations/${ids.example}/teams/t/backlogs/stories`;
const OPEN = `${BACKLOG}?workitem=MSK-US-2`;
const PAGE = workItemPath(ids.example, ids.msk, 'MSK-US-2');
const CARD = 'Пользовательская история MSK-US-2';

function Where() {
  const location = useLocation();
  return <p data-testid="where">{`${location.pathname}${location.search}`}</p>;
}

/** A stand-in for the backlog: a titled row, as the real row marks it, and the dialog. */
function Backlog({ onChanged }: { onChanged?: () => void }) {
  const dialog = useWorkItemDialog();
  return (
    <>
      <Where />
      <div data-identifier="MSK-US-2">
        <a
          href={PAGE}
          onClick={(event) => {
            event.preventDefault();
            dialog.open('MSK-US-2');
          }}
        >
          Письмо со ссылкой
        </a>
      </div>
      <WorkItemCardDialog organizationId={ids.example} projectId={ids.msk} onChanged={onChanged} />
    </>
  );
}

async function openAs(role: TestRole, prepare?: (api: WorkItemsTestApi) => void) {
  const api = serveWorkItems(hierarchyDataset());
  prepare?.(api);
  const onChanged = vi.fn();
  const client = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  render(
    <QueryClientProvider client={client}>
      <AccessCheckContext.Provider value={checkFor(role)}>
        <NoticeProvider>
          <MemoryRouter initialEntries={[OPEN]}>
            <Routes>
              <Route path={BACKLOG} element={<Backlog onChanged={onChanged} />} />
            </Routes>
          </MemoryRouter>
        </NoticeProvider>
      </AccessCheckContext.Provider>
    </QueryClientProvider>,
  );
  const dialog = await screen.findByRole('dialog', { name: CARD });
  await screen.findByRole('textbox', { name: 'Заголовок' });
  return { api, dialog, onChanged };
}

const where = () => screen.getByTestId('where');
const restoreButton = () => screen.getByRole('button', { name: 'Восстановить' });

async function chooseFromMenu(name: string) {
  await userEvent.click(screen.getByRole('button', { name: 'Ещё действия' }));
  await userEvent.click(await screen.findByRole('menuitem', { name }));
}

/** The confirmation drawn over the card: a second dialog. */
const confirmation = (title: string) => screen.findByRole('dialog', { name: title });

describe('mod-005 US-1 in the dialog: Delete keeps the card open on the deleted item', () => {
  it('tells the host, shows the notice, and the dialog stays on the address', async () => {
    const { api, onChanged } = await openAs('member');

    await chooseFromMenu('Удалить');
    await userEvent.click(
      within(await confirmation('Удалить рабочий элемент?')).getByRole('button', {
        name: 'Удалить',
      }),
    );

    await waitFor(() => expect(restoreButton()).toBeEnabled());
    expect(onChanged).toHaveBeenCalledTimes(1);
    expect(await screen.findByText('Рабочий элемент MSK-US-2 удалён.')).toBeVisible();
    expect(screen.getByRole('dialog', { name: CARD })).toBeVisible();
    expect(where()).toHaveTextContent(OPEN);
    expect(recordOf(api.data, 2).deletion).not.toBeNull();
  });
});

describe('mod-005 US-4 in the dialog: Restore returns the card to its editable look', () => {
  it('tells the host and shows the notice', async () => {
    const { onChanged } = await openAs('member', (api) => {
      recordOf(api.data, 2).stateId = stateId('US', 'Активна');
      deleteRecord(recordOf(api.data, 2));
    });

    await userEvent.click(restoreButton());
    await userEvent.click(
      within(await confirmation('Восстановить рабочий элемент?')).getByRole('button', {
        name: 'Восстановить',
      }),
    );

    await waitFor(() => expect(screen.getByRole('button', { name: 'Сохранить' })).toBeDisabled());
    expect(onChanged).toHaveBeenCalledTimes(1);
    expect(await screen.findByText('Рабочий элемент MSK-US-2 восстановлен.')).toBeVisible();
  });
});

describe('mod-005 US-6 in the dialog: Purge closes the card', () => {
  it('tells the host, shows the notice, and the address loses the dialog', async () => {
    const { api, onChanged } = await openAs('admin', (stored) =>
      deleteRecord(recordOf(stored.data, 2)),
    );
    const storyId = recordOf(api.data, 2).id;

    await chooseFromMenu('Удалить навсегда');
    await userEvent.click(
      within(await confirmation('Удалить рабочий элемент навсегда?')).getByRole('button', {
        name: 'Удалить навсегда',
      }),
    );

    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
    expect(onChanged).toHaveBeenCalledTimes(1);
    expect(where()).toHaveTextContent(BACKLOG);
    expect(where()).not.toHaveTextContent('workitem');
    expect(await screen.findByText('Рабочий элемент MSK-US-2 удалён навсегда.')).toBeVisible();
    expect(api.data.items.some((record) => record.id === storyId)).toBe(false);
  });
});

describe('mod-005 design § 9 in the dialog: a reader’s deleted card', () => {
  it('reads the first sentence, offers no way back, and has «Копировать ссылку» alone in «⋮»', async () => {
    await openAs('viewer', (api) => deleteRecord(recordOf(api.data, 2)));

    expect(
      screen.getByText('Рабочий элемент удалён и доступен только для чтения.', { exact: false }),
    ).toBeVisible();
    expect(screen.queryByText('Восстановите его', { exact: false })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Восстановить' })).not.toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: 'Ещё действия' }));
    const menu = await screen.findByRole('menu', { name: 'Ещё действия' });
    expect(
      within(menu)
        .getAllByRole('menuitem')
        .map((entry) => entry.textContent),
    ).toEqual(['Копировать ссылку']);
  });
});
