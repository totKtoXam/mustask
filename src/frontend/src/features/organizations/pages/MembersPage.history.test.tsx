import { describe, expect, it } from 'vitest';
import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { HttpResponse, http } from 'msw';
import { server } from '../../../test/mocks/server';
import { ORGANIZATIONS_URL, PROBLEM_HEADERS, ids, problem } from '../testFixtures';
import { membersTable, openPage } from '../testMembersPage';
import { ROOT_ADMIN, VERA, serveRescue } from '../testRescue';

// feat-033 US-3 (design § 5): «История назначений» on «Участники», for the organization's
// administrators only, read through the organization's own route.

const HISTORY = 'История назначений';

describe('feat-033 US-3: «История назначений» on «Участники»', () => {
  it('is a header command for an administrator, beside the one primary «Добавить участника»', async () => {
    openPage({ role: 'admin' });
    serveRescue();
    await membersTable();

    expect(screen.getByRole('button', { name: HISTORY })).toBeVisible();
    expect(screen.getByRole('button', { name: 'Добавить участника' })).toBeVisible();
  });

  it.each(['member', 'viewer'] as const)('is not shown to a %s', async (role) => {
    openPage({ role });
    serveRescue();
    await membersTable();

    expect(screen.queryByRole('button', { name: HISTORY })).not.toBeInTheDocument();
  });

  it('opens the record read from the organization’s route, newest first, with the full reason', async () => {
    openPage({ role: 'admin' });
    const api = serveRescue({
      records: {
        [ids.alfa]: [
          {
            id: 'r1',
            appointedAt: '2026-11-03T11:05:00+00:00',
            appointedBy: ROOT_ADMIN,
            appointee: VERA,
            reason: 'Борис уволился',
          },
        ],
      },
    });
    await membersTable();

    await userEvent.click(screen.getByRole('button', { name: HISTORY }));

    const dialog = await screen.findByRole('dialog', { name: 'История назначений — «Альфа»' });
    expect(await within(dialog).findByText('Борис уволился')).toBeVisible();
    expect(within(dialog).getByText('Системный Администратор')).toBeVisible();
    expect(api.requests.map((request) => request.path)).toContain(
      `/api/organizations/${ids.alfa}/appointments`,
    );
    expect(api.requests.map((request) => request.path).join()).not.toContain('/rescue/');
  });

  it('says «Назначений не было» when the organization has no appointments, and closes on «Закрыть»', async () => {
    openPage({ role: 'admin' });
    serveRescue();
    await membersTable();

    await userEvent.click(screen.getByRole('button', { name: HISTORY }));
    const dialog = await screen.findByRole('dialog');
    expect(
      await within(dialog).findByRole('heading', { name: 'Назначений не было' }),
    ).toBeVisible();
    await userEvent.click(within(dialog).getAllByRole('button', { name: 'Закрыть' }).at(-1)!);

    await dialogCloses();
  });

  it('shows «Недостаточно прав» in the dialog when the read is refused', async () => {
    openPage({ role: 'admin' });
    serveRescue();
    server.use(
      http.get(`${ORGANIZATIONS_URL}/:organizationId/appointments`, () => {
        const body = problem('organizations.access.forbidden');
        return HttpResponse.json(body, { status: 403, headers: PROBLEM_HEADERS });
      }),
    );
    await membersTable();

    await userEvent.click(screen.getByRole('button', { name: HISTORY }));

    const dialog = await screen.findByRole('dialog');
    expect(await within(dialog).findByRole('alert')).toHaveTextContent('Недостаточно прав');
  });

  it('shows «Организация не найдена» in the dialog on a 404', async () => {
    openPage({ role: 'admin' });
    serveRescue();
    server.use(
      http.get(`${ORGANIZATIONS_URL}/:organizationId/appointments`, () => {
        const body = problem('organizations.organization.not_found');
        return HttpResponse.json(body, { status: 404, headers: PROBLEM_HEADERS });
      }),
    );
    await membersTable();

    await userEvent.click(screen.getByRole('button', { name: HISTORY }));

    const dialog = await screen.findByRole('dialog', { name: /История назначений/ });
    expect(await within(dialog).findByText('Организация не найдена')).toBeVisible();
  });
});

function dialogCloses() {
  return waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
}
