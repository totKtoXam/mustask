import { describe, expect, it, vi } from 'vitest';
import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { HttpResponse, http } from 'msw';
import { Route } from 'react-router-dom';
import { gate } from '../../../test/gate';
import { server } from '../../../test/mocks/server';
import { authValue, renderShell } from '../../../test/renderShell';
import { OrganizationGate, OrganizationPage } from '../../organizations';
import { serveProblem, serveWorkItems } from '../testApi';
import { retype, save, textbox } from '../testFields';
import {
  API,
  PROBLEM_HEADERS,
  WORK_ITEM_URL,
  hierarchyDataset,
  problem,
  recordOf,
} from '../testFixtures';
import { WORK_ITEM_ROUTE } from '../paths';
import { workItemsPermissions } from '../permissions';
import { mskForm, projectContext } from '../testRender';
import { WorkItemFormPage } from './WorkItemFormPage';

// mod-016 design § 2.2, § 9, DC-12 as the card meets them: a 404 «Организация не найдена»
// from any scoped call — the card's read or its save — turns the page into row 3 of the gate
// at once, the card goes with it and shows no `Alert` of its own, and focus is in the main
// region. The user was removed from the organization, so the codes read again answer 404 too.

// Each user action draws the kit’s fields in jsdom; a test of several steps outlasts the default.
vi.setConfig({ testTimeout: 120_000 });

const STORY = mskForm('MSK-US-2');
const ACCESS_URL = `${API}/api/organizations/:organizationId/access`;
const FRAME = 'Организация не найдена';

/**
 * The page as `app/routes.tsx` mounts it: under the organization gate, wrapped by `OrganizationPage`,
 * which draws the gate's five rows — the row 3 of a 404 included, which the slice's own harness
 * (`renderWorkItems`, no wrapper) never meets.
 */
function renderGated(path: string) {
  return renderShell({
    path,
    auth: authValue([]),
    contextSource: projectContext(),
    // The administrator is the default handler's answer, which a test replaces with its own;
    // another role is served by a handler that would shadow the 404 below.
    access: 'admin',
    routes: (
      <Route element={<OrganizationGate />}>
        <Route
          path={WORK_ITEM_ROUTE}
          element={
            <OrganizationPage
              code={workItemsPermissions.workItemRead}
              title="Рабочий элемент"
              skeleton="form"
            >
              <WorkItemFormPage />
            </OrganizationPage>
          }
        />
      </Route>
    ),
  });
}

function organizationGone() {
  return HttpResponse.json(problem('organizations.organization.not_found'), {
    status: 404,
    headers: PROBLEM_HEADERS,
  });
}

describe('mod-016 DC-12 Scenario: the card’s save answers «Организация не найдена»', () => {
  it('turns the page into the gate’s frame at once: no card, no Alert of its own, focus in the main region', async () => {
    const data = hierarchyDataset();
    serveWorkItems(data);
    renderGated(STORY);
    await screen.findByRole('textbox', { name: 'Заголовок' });
    serveProblem('patch', WORK_ITEM_URL, problem('organizations.organization.not_found'), {
      once: true,
    });
    // The member was removed: the codes the 404 makes the shell read again answer 404 too.
    server.use(http.get(ACCESS_URL, () => organizationGone()));

    await userEvent.clear(textbox('Заголовок'));
    await userEvent.type(textbox('Заголовок'), 'Другое название');
    await userEvent.click(save());

    expect(await screen.findByRole('heading', { level: 1, name: FRAME })).toBeVisible();
    expect(screen.queryByRole('textbox', { name: 'Заголовок' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Сохранить' })).not.toBeInTheDocument();
    // The refusal is the frame, not an `Alert` over the card: nothing in the organization takes it.
    expect(screen.queryByText('Организация не найдена.')).not.toBeInTheDocument();
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
    await waitFor(() => expect(screen.getByRole('main')).toHaveFocus());
    // What was typed is dropped with the card; the stored item is untouched.
    expect(recordOf(data, 2).title).toBe('Письмо со ссылкой');
  });

  it('keeps the frame while the codes are read again: no skeleton, no banner, no card', async () => {
    serveWorkItems(hierarchyDataset());
    const shell = renderGated(STORY);
    await screen.findByRole('textbox', { name: 'Заголовок' });
    serveProblem('patch', WORK_ITEM_URL, problem('organizations.organization.not_found'), {
      once: true,
    });
    const reread = gate();
    server.use(
      http.get(ACCESS_URL, async () => {
        await reread.opened;
        return organizationGone();
      }),
    );

    await retype('Оценка в пунктах', '3');
    await userEvent.click(save());

    // The scoped call's 404 is held at once, before the re-read it starts has answered.
    expect(await screen.findByRole('heading', { level: 1, name: FRAME })).toBeVisible();
    expect(screen.queryByRole('status', { name: 'Загрузка данных' })).not.toBeInTheDocument();
    expect(screen.queryByRole('textbox', { name: 'Заголовок' })).not.toBeInTheDocument();
    reread.open();
    await waitFor(() => expect(shell.queryClient.isFetching()).toBe(0));
    expect(screen.getByRole('heading', { level: 1, name: FRAME })).toBeVisible();
    expect(screen.getByRole('link', { name: 'На главную' })).toBeVisible();
  });
});

describe('mod-016 DC-12 Scenario: the card’s read answers «Организация не найдена»', () => {
  it('shows the frame of the organization in place of the page', async () => {
    serveWorkItems();
    serveProblem('get', WORK_ITEM_URL, problem('organizations.organization.not_found'));
    server.use(http.get(ACCESS_URL, () => organizationGone()));
    renderGated(STORY);

    expect(await screen.findByRole('heading', { level: 1, name: FRAME })).toBeVisible();
    expect(screen.queryByRole('textbox', { name: 'Заголовок' })).not.toBeInTheDocument();
    expect(screen.getAllByRole('heading', { level: 1 })).toHaveLength(1);
  });
});
