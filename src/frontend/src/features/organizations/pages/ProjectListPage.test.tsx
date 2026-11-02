import { describe, expect, it } from 'vitest';
import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { PROJECTS_URL, SELECTOR_OFFERS_URL, ids, problem } from '../testFixtures';
import { serveOrganizations, serveProblem } from '../testApi';
import { contextSource, renderPage } from '../testRender';

const ALFA_PROJECTS = `/organizations/${ids.alfa}/projects`;
const ALFA_DEACTIVATED = `${ALFA_PROJECTS}?status=deactivated`;

async function table() {
  return screen.findByRole('table', { name: 'Проекты' });
}

describe('feat-043 design § 5.3: «Проекты» loads, fails and lists (US-2)', () => {
  it('loads with the real headers, then lists the active projects with key and place, each name a link to the project', async () => {
    serveOrganizations();
    renderPage({ path: ALFA_PROJECTS });

    const skeleton = screen.getByRole('status', { name: 'Загрузка данных' });
    expect(within(skeleton).getByText('Расположение')).toBeInTheDocument();

    const rows = within(await table())
      .getAllByRole('row')
      .slice(1);
    expect(rows.map((row) => row.textContent)).toEqual([
      'АналитикаANL—',
      'Пример проектаPRMПлатформа',
    ]);
    expect(within(rows[1]!).getByRole('link', { name: 'Пример проекта' })).toHaveAttribute(
      'href',
      `/organizations/${ids.alfa}/projects/${ids.prm}/general`,
    );
    expect(screen.getByRole('button', { name: 'Создать проект' })).toBeInTheDocument();
  });

  it('reads the one shared list: a single request of selector-offers, none of the projects list', async () => {
    const api = serveOrganizations();
    renderPage({ path: ALFA_PROJECTS });
    await table();

    const paths = api.requests.map((request) => request.path);
    expect(paths.filter((path) => path.endsWith('/selector-offers'))).toHaveLength(1);
    expect(paths.filter((path) => path.endsWith('/projects'))).toHaveLength(0);
  });

  it('fails with «Повторить» and reads again', async () => {
    serveOrganizations();
    serveProblem('get', SELECTOR_OFFERS_URL, problem('organizations.validation_failed'), {
      once: true,
    });
    renderPage({ path: ALFA_PROJECTS });

    await userEvent.click(await screen.findByRole('button', { name: 'Повторить' }));

    expect(await table()).toBeInTheDocument();
  });

  it('narrows by «Найти проект» (name or key) and says so when nothing matches, «Сбросить фильтр» brings the rows back', async () => {
    serveOrganizations();
    renderPage({ path: ALFA_PROJECTS });
    await table();

    await userEvent.type(screen.getByRole('textbox', { name: 'Найти проект' }), 'anl');
    const rows = within(await table())
      .getAllByRole('row')
      .slice(1);
    expect(rows).toHaveLength(1);

    await userEvent.clear(screen.getByRole('textbox', { name: 'Найти проект' }));
    await userEvent.type(screen.getByRole('textbox', { name: 'Найти проект' }), 'нет такого');
    expect(await screen.findByRole('heading', { name: 'Ничего не найдено' })).toBeInTheDocument();

    await userEvent.click(screen.getByRole('button', { name: 'Сбросить фильтр' }));
    expect(
      within(await table())
        .getAllByRole('row')
        .slice(1),
    ).toHaveLength(2);
  });

  it('a name opens the project where the shell’s context says (D-3)', async () => {
    serveOrganizations();
    renderPage({
      path: ALFA_PROJECTS,
      context: contextSource({
        objectLink: (_kind, id) => ({
          to: `/organizations/${ids.alfa}/projects/${id}/general?departmentId=d1`,
          notice: null,
        }),
      }),
    });

    const link = within(await table()).getByRole('link', { name: 'Аналитика' });
    expect(link).toHaveAttribute(
      'href',
      `/organizations/${ids.alfa}/projects/${ids.anl}/general?departmentId=d1`,
    );
  });
});

describe('feat-043 design § 5.3: the empty states of «Проекты»', () => {
  it('a new organization: «Проектов пока нет» with the holder’s hint and «Создать проект»', async () => {
    const api = serveOrganizations();
    api.data.organizations.push({
      id: 'a0000000-0000-4000-8000-0000000000aa',
      name: 'Новая',
      active: true,
    });
    renderPage({ path: '/organizations/a0000000-0000-4000-8000-0000000000aa/projects' });

    expect(
      await screen.findByRole('heading', { level: 2, name: 'Проектов пока нет' }),
    ).toBeInTheDocument();
    expect(screen.getByText('Создайте первый проект.')).toBeInTheDocument();
    expect(screen.getAllByRole('button', { name: 'Создать проект' })).toHaveLength(2);
  });

  it('a reader is told who creates projects and is offered no command', async () => {
    const api = serveOrganizations();
    api.data.organizations.push({
      id: 'a0000000-0000-4000-8000-0000000000aa',
      name: 'Новая',
      active: true,
    });
    renderPage({
      path: '/organizations/a0000000-0000-4000-8000-0000000000aa/projects',
      role: 'viewer',
    });

    expect(
      await screen.findByText('Проекты создаёт администратор организации.'),
    ).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Создать проект' })).not.toBeInTheDocument();
    expect(screen.queryByRole('combobox', { name: 'Показывать' })).not.toBeInTheDocument();
  });
});

describe('mod-002 US-2 Scenario: An unknown organization answers not found (its «Проекты»)', () => {
  it.each([
    ['a deactivated organization', ids.gamma],
    ['an unknown one', 'a0000000-0000-4000-8000-0000000000ff'],
  ])('%s answers «Организация не найдена» on either view', async (_case, organizationId) => {
    serveOrganizations();
    renderPage({ path: `/organizations/${organizationId}/projects?status=deactivated` });

    expect(
      await screen.findByRole('heading', { level: 1, name: 'Организация не найдена' }),
    ).toBeInTheDocument();
    expect(screen.queryByRole('table')).not.toBeInTheDocument();
  });
});

describe('feat-043 design § 5.3: «Деактивировать» from the row menu (BR-NV-14)', () => {
  it('asks the confirmation of mod-002, deactivates, says so and the row leaves', async () => {
    const api = serveOrganizations();
    renderPage({ path: ALFA_PROJECTS });
    await table();

    await userEvent.click(screen.getByRole('button', { name: 'Ещё действия: Аналитика' }));
    await userEvent.click(await screen.findByRole('menuitem', { name: 'Деактивировать' }));
    const dialog = await screen.findByRole('dialog', { name: 'Деактивировать проект?' });
    await userEvent.click(within(dialog).getByRole('button', { name: 'Деактивировать' }));

    expect(await screen.findByText('Проект «Аналитика» деактивирован.')).toBeInTheDocument();
    await waitFor(() =>
      expect(
        within(screen.getByRole('table', { name: 'Проекты' })).queryByText('Аналитика'),
      ).not.toBeInTheDocument(),
    );
    expect(api.writes()).toEqual([
      {
        method: 'POST',
        path: `/api/organizations/${ids.alfa}/projects/${ids.anl}/deactivate`,
        body: undefined,
      },
    ]);
  });

  it('is not offered to a member', async () => {
    serveOrganizations();
    renderPage({ path: ALFA_PROJECTS, role: 'member' });
    await table();

    expect(screen.queryByRole('button', { name: /^Ещё действия: / })).not.toBeInTheDocument();
  });
});

describe('feat-043 D-5 and mod-002 US-3 Scenario: «Показывать» and restore', () => {
  it('the select switches to the deactivated view: names are no links, «Старый проект» is restored with the notice', async () => {
    const api = serveOrganizations();
    renderPage({ path: ALFA_PROJECTS });
    await table();

    await userEvent.click(screen.getByRole('combobox', { name: 'Показывать' }));
    await userEvent.click(await screen.findByRole('option', { name: 'Деактивированные' }));

    const deactivated = await screen.findByText('Старый проект');
    expect(deactivated).toBeInTheDocument();
    expect(within(await table()).queryByRole('link')).not.toBeInTheDocument();

    await userEvent.click(
      within(await table()).getByRole('button', { name: 'Восстановить «Старый проект»' }),
    );

    expect(await screen.findByText('Проект «Старый проект» восстановлен.')).toBeInTheDocument();
    expect(
      await screen.findByRole('heading', { level: 2, name: 'Деактивированных проектов нет' }),
    ).toBeInTheDocument();
    await waitFor(() => expect(screen.getByRole('combobox', { name: 'Показывать' })).toHaveFocus());
    expect(api.writes()).toEqual([
      {
        method: 'POST',
        path: `/api/organizations/${ids.alfa}/projects/${ids.old}/restore`,
        body: undefined,
      },
    ]);
  });
});

describe('mod-016 design § 9 Scenario: a restore refused on the deactivated view', () => {
  async function restoreOld() {
    renderPage({ path: ALFA_DEACTIVATED });
    const restore = await screen.findByRole('button', { name: 'Восстановить «Старый проект»' });
    await userEvent.click(restore);
    await waitFor(() => expect(restore).toBeEnabled());
    return restore;
  }

  // The 404 «Организация не найдена» takes the whole page (the gate's row 3, DC-12) and has no
  // message of its own: under an organization no Snackbar shows it over the frame.
  it('shows no notice for the 404 «Организация не найдена», which the gate answers with its frame', async () => {
    serveOrganizations();
    serveProblem(
      'post',
      `${PROJECTS_URL}/:projectId/restore`,
      problem('organizations.organization.not_found'),
    );

    await restoreOld();

    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
    expect(screen.queryByText('Организация не найдена.')).not.toBeInTheDocument();
  });

  it('keeps the error notice with the server’s text for any other refusal, and the row with its focus', async () => {
    serveOrganizations();
    serveProblem(
      'post',
      `${PROJECTS_URL}/:projectId/restore`,
      problem('organizations.project.not_found'),
    );

    const restore = await restoreOld();

    expect(await screen.findByRole('alert')).toHaveTextContent('Проект не найден.');
    await waitFor(() => expect(restore).toHaveFocus());
  });
});
