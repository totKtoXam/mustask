import { describe, expect, it, vi } from 'vitest';
import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { HttpResponse, http } from 'msw';
import { Route } from 'react-router-dom';
import { server } from '../../../test/mocks/server';
import { authValue, renderShell } from '../../../test/renderShell';
import { READ_CODES, servedFor, servedWith } from '../../../test/organizationRoles';
import { OrganizationGate, OrganizationPage } from '../../organizations';
import { cardTexts } from '../../workitems/lib/cardTexts';
import { WORK_ITEM_URL } from '../../workitems/testFixtures';
import { serveBoards } from '../testApi';
import { findBacklogTable, rowsOf } from '../testBacklog';
import {
  API,
  PROBLEM_HEADERS,
  boardsDataset,
  platformItem,
  problem,
  stateId,
} from '../testFixtures';
import { boardsPermissions } from '../permissions';
import { BACKLOG_ROUTE } from '../paths';
import { backlogAddress, renderBoards, teamContext } from '../testRender';
import { BacklogPage } from './BacklogPage';

// mod-016 design § 2.2, § 2.3, § 6.5, DC-12 on the card that opens as a dialog over the
// backlog (feat-029): the dialog asks the served access like the page does — a reader's
// card is read-only, a member without `workitems.link.write` has «Родитель» read-only, a
// refused save keeps the dialog with its alert and turns it read-only when the role went,
// and a save that answers «Организация не найдена» closes the dialog with the whole page.
// The card inside is the kit's fields in jsdom, which are slow.

vi.setConfig({ testTimeout: 120_000 });

const ACCESS_URL = `${API}/api/organizations/:organizationId/access`;
const OPEN = `${backlogAddress()}?workitem=MSK-BUG-4`;

function dataset() {
  const data = boardsDataset();
  data.items.push(
    platformItem(4, 'BUG', 'Падение при входе', { stateId: stateId('BUG', 'Активна') }),
  );
  return data;
}

async function openCard(
  role: Parameters<typeof renderBoards>[1] = {},
  { gated = false }: { gated?: boolean } = {},
) {
  const api = serveBoards(dataset());
  const shell = gated ? renderGated(backlogAddress()) : renderBoards(OPEN, role);
  if (gated) {
    // As a user opens it: a click on the row’s title, so focus has been in the main region.
    const table = await findBacklogTable();
    const [row] = rowsOf(table);
    if (!row) throw new Error('No row.');
    await userEvent.click(within(row).getByRole('link', { name: 'Падение при входе' }));
  }
  const dialog = await screen.findByRole('dialog', { name: 'Баг MSK-BUG-4' });
  await within(dialog).findByRole('textbox', { name: 'Заголовок' });
  return { api, shell, dialog };
}

/**
 * The backlog as `app/routes.tsx` mounts it: under the organization gate, wrapped by
 * `OrganizationPage`, which draws the gate's five rows — the row 3 of a 404 included, which
 * the slice's own harness (`renderBoards`, no wrapper) never meets.
 */
function renderGated(path: string) {
  return renderShell({
    path,
    auth: authValue([]),
    contextSource: teamContext(),
    // The administrator is the default handler's answer, which a test replaces with its own;
    // another role is served by a handler that would shadow the 404 below.
    access: 'admin',
    routes: (
      <Route element={<OrganizationGate />}>
        <Route
          path={BACKLOG_ROUTE}
          element={
            <OrganizationPage
              code={boardsPermissions.workItemRead}
              title="Бэклоги"
              skeleton="registry"
            >
              <BacklogPage />
            </OrganizationPage>
          }
        />
      </Route>
    ),
  });
}

const title = () => screen.getByRole('textbox', { name: 'Заголовок' });
const parent = () => screen.getByRole('combobox', { name: 'Родитель' });

function refuseSave(
  code: 'organizations.access.forbidden' | 'organizations.organization.not_found',
) {
  const body = problem(code);
  server.use(
    http.patch(WORK_ITEM_URL, () =>
      HttpResponse.json(body, { status: body.status, headers: PROBLEM_HEADERS }),
    ),
  );
}

describe('mod-016 design § 6.5 Scenario: the card of the dialog for a reader', () => {
  it('is read-only: the fields refuse typing, there is no Save group, the card says why, the backlog stays', async () => {
    const { dialog } = await openCard({ role: 'viewer' });

    expect(title()).toHaveAttribute('readonly');
    await userEvent.type(title(), 'x');
    expect(title()).toHaveValue('Падение при входе');
    expect(parent()).toHaveAttribute('aria-readonly', 'true');
    expect(within(dialog).queryByRole('button', { name: 'Сохранить' })).not.toBeInTheDocument();
    expect(
      within(dialog).queryByRole('button', { name: 'Отменить все изменения' }),
    ).not.toBeInTheDocument();
    expect(within(dialog).getByText(cardTexts.readOnly)).toBeVisible();
    // What only reads stays: «Обновить», «Ещё действия» and the dialog’s own controls.
    expect(within(dialog).getByRole('button', { name: 'Обновить' })).toBeEnabled();
    expect(within(dialog).getByRole('button', { name: 'Ещё действия' })).toBeEnabled();
    expect(within(dialog).getByRole('button', { name: 'Развернуть на весь экран' })).toBeVisible();
    expect(screen.getByRole('table', { name: 'Бэклог', hidden: true })).toBeInTheDocument();
  });

  it('is editable for a member, who holds both codes', async () => {
    const { dialog } = await openCard({ role: 'member' });

    expect(title()).not.toHaveAttribute('readonly');
    expect(parent()).not.toHaveAttribute('aria-readonly', 'true');
    expect(within(dialog).getByRole('button', { name: 'Сохранить' })).toBeVisible();
    expect(within(dialog).queryByText(cardTexts.readOnly)).not.toBeInTheDocument();
  });
});

describe('mod-016 design § 6.2 Scenario: the card of the dialog for a member without the link right', () => {
  it('edits the item and has «Родитель» read-only', async () => {
    const { dialog } = await openCard({
      role: servedWith('member', [...READ_CODES, 'workitems.workitem.write']),
    });

    expect(title()).not.toHaveAttribute('readonly');
    expect(parent()).toHaveAttribute('aria-readonly', 'true');
    expect(within(dialog).getByRole('button', { name: 'Сохранить' })).toBeVisible();
    expect(within(dialog).queryByText(cardTexts.readOnly)).not.toBeInTheDocument();
  });
});

describe('mod-016 design § 2.3 Scenario: a save the card’s dialog is refused', () => {
  it('a changed role: the dialog stays, keeps the typed title under the alert and turns read-only', async () => {
    const { dialog } = await openCard({ role: 'member' });
    refuseSave('organizations.access.forbidden');
    server.use(http.get(ACCESS_URL, () => HttpResponse.json(servedFor('viewer'))));

    await userEvent.clear(title());
    await userEvent.type(title(), 'Другое название');
    await userEvent.click(within(dialog).getByRole('button', { name: 'Сохранить' }));

    // The role-change notice stays 6 s: it is looked for first, while it is on screen.
    expect(
      await screen.findByText(
        'Ваша роль в организации «Пример организации» изменилась: «Читатель».',
      ),
    ).toBeVisible();
    await waitFor(() => expect(title()).toHaveAttribute('readonly'));
    expect(within(dialog).getByText('Недостаточно прав')).toBeVisible();
    expect(title()).toHaveValue('Другое название');
    expect(within(dialog).queryByRole('button', { name: 'Сохранить' })).not.toBeInTheDocument();
  });

  it('an unchanged role: the dialog stays editable with its alert and typed title, and no role-change notice', async () => {
    const { dialog } = await openCard({ role: 'member' });
    refuseSave('organizations.access.forbidden');

    await userEvent.clear(title());
    await userEvent.type(title(), 'Другое название');
    await userEvent.click(within(dialog).getByRole('button', { name: 'Сохранить' }));

    expect(await within(dialog).findByText('Недостаточно прав')).toBeVisible();
    expect(title()).toHaveValue('Другое название');
    expect(title()).not.toHaveAttribute('readonly');
    expect(screen.queryByText(/Ваша роль в организации/)).not.toBeInTheDocument();
  });
});

describe('mod-016 DC-12 Scenario: a save the card’s dialog finds the organization gone', () => {
  it('turns the page into the gate’s frame at once: the dialog and the backlog are gone, no Alert, focus in the main region', async () => {
    const { dialog } = await openCard({}, { gated: true });
    refuseSave('organizations.organization.not_found');
    server.use(
      http.get(ACCESS_URL, () =>
        HttpResponse.json(problem('organizations.organization.not_found'), {
          status: 404,
          headers: PROBLEM_HEADERS,
        }),
      ),
    );

    await userEvent.clear(title());
    await userEvent.type(title(), 'Другое название');
    await userEvent.click(within(dialog).getByRole('button', { name: 'Сохранить' }));

    expect(
      await screen.findByRole(
        'heading',
        { level: 1, name: 'Организация не найдена' },
        { timeout: 15_000 },
      ),
    ).toBeVisible();
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
    expect(screen.queryByRole('table', { name: 'Бэклог', hidden: true })).not.toBeInTheDocument();
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
    await waitFor(() => expect(document.getElementById('main-content')).toHaveFocus());
  });
});

describe('mod-016 DC-12 Scenario: the card’s dialog reads an item of an organization that is gone', () => {
  it('shows the frame of the organization in place of the page, not a dialog over a backlog', async () => {
    serveBoards(dataset());
    const gone = problem('organizations.organization.not_found');
    server.use(
      http.get(WORK_ITEM_URL, () =>
        HttpResponse.json(gone, { status: gone.status, headers: PROBLEM_HEADERS }),
      ),
      http.get(ACCESS_URL, () =>
        HttpResponse.json(gone, { status: gone.status, headers: PROBLEM_HEADERS }),
      ),
    );
    renderGated(OPEN);

    expect(
      await screen.findByRole(
        'heading',
        { level: 1, name: 'Организация не найдена' },
        { timeout: 15_000 },
      ),
    ).toBeVisible();
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
    // The backlog is not drawn behind the frame.
    expect(screen.queryByRole('table', { name: 'Бэклог', hidden: true })).not.toBeInTheDocument();
  });
});
