import { describe, expect, it } from 'vitest';
import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { PROJECTS_URL, ids, problem } from '../testFixtures';
import { serveOrganizations, serveProblem } from '../testApi';
import { renderPage } from '../testRender';

const ALFA_PROJECTS = `/organizations/${ids.alfa}/settings/projects`;
const ALFA_DEACTIVATED = `${ALFA_PROJECTS}/deactivated`;

describe('mod-002 design § 7.3: «Проекты» loads, fails and lists', () => {
  it('loads with the real headers, then lists the active projects with their keys, each a link to its «Общие»', async () => {
    serveOrganizations();
    renderPage({ path: ALFA_PROJECTS });

    const skeleton = screen.getByRole('status', { name: 'Загрузка данных' });
    expect(within(skeleton).getByText('Ключ')).toBeInTheDocument();
    expect(screen.getByRole('tablist', { name: 'Проекты по статусу' })).toBeInTheDocument();

    const table = await screen.findByRole('table', { name: 'Активные проекты' });
    const rows = within(table).getAllByRole('row').slice(1);
    expect(rows.map((row) => row.textContent)).toEqual(['АналитикаANL', 'Пример проектаPRM']);
    expect(within(rows[1]!).getByRole('link', { name: 'Пример проекта' })).toHaveAttribute(
      'href',
      `/organizations/${ids.alfa}/projects/${ids.prm}/settings/general`,
    );
    expect(screen.getByRole('button', { name: 'Создать проект' })).toBeInTheDocument();
  });

  it('fails with «Повторить» and no «Создать проект»', async () => {
    serveOrganizations();
    serveProblem('get', PROJECTS_URL, problem('organizations.validation_failed'), { once: true });
    renderPage({ path: ALFA_PROJECTS });

    await userEvent.click(await screen.findByRole('button', { name: 'Повторить' }));

    expect(await screen.findByRole('table', { name: 'Активные проекты' })).toBeInTheDocument();
  });
});

describe('mod-002 US-1 Scenario: An organization is created with its process (its «Проекты»)', () => {
  it('a new organization’s active tab shows «Пока нет проектов» with «Создать проект»', async () => {
    const api = serveOrganizations();
    api.data.organizations.push({
      id: 'a0000000-0000-4000-8000-0000000000aa',
      name: 'Новая',
      active: true,
    });
    renderPage({ path: '/organizations/a0000000-0000-4000-8000-0000000000aa/settings/projects' });

    expect(
      await screen.findByRole('heading', { level: 2, name: 'Пока нет проектов' }),
    ).toBeInTheDocument();
    expect(
      screen.getByText('Создайте первый проект кнопкой «Создать проект» выше.'),
    ).toBeInTheDocument();
    expect(screen.getAllByRole('button', { name: 'Создать проект' })).toHaveLength(2);
  });
});

describe('mod-002 US-2 Scenario: An unknown organization answers not found (its «Проекты»)', () => {
  it.each([
    ['a deactivated organization', ids.gamma],
    ['an unknown one', 'a0000000-0000-4000-8000-0000000000ff'],
  ])('%s answers «Организация не найдена» on either tab', async (_case, organizationId) => {
    serveOrganizations();
    renderPage({ path: `/organizations/${organizationId}/settings/projects/deactivated` });

    expect(
      await screen.findByRole('heading', { level: 1, name: 'Организация не найдена' }),
    ).toBeInTheDocument();
    expect(screen.queryByRole('tablist')).not.toBeInTheDocument();
  });
});

describe('mod-002 US-3 Scenario: Restored', () => {
  it('lists «Старый проект» with its key and restores it with the notice «Проект «Старый проект» восстановлен.»', async () => {
    const api = serveOrganizations();
    renderPage({ path: ALFA_DEACTIVATED });

    const table = await screen.findByRole('table', { name: 'Деактивированные проекты' });
    expect(within(table).getByText('Старый проект')).toBeInTheDocument();
    expect(within(table).getByText('OLD')).toBeInTheDocument();
    expect(within(table).queryByRole('link')).not.toBeInTheDocument();

    await userEvent.click(
      within(table).getByRole('button', { name: 'Восстановить «Старый проект»' }),
    );

    expect(await screen.findByText('Проект «Старый проект» восстановлен.')).toBeInTheDocument();
    expect(
      await screen.findByRole('heading', { level: 2, name: 'Деактивированных проектов нет' }),
    ).toBeInTheDocument();
    expect(
      screen.getByText(
        'Сюда попадают проекты, деактивированные в их настройках. Их можно восстановить.',
      ),
    ).toBeInTheDocument();
    await waitFor(() =>
      expect(screen.getByRole('tab', { name: 'Деактивированные' })).toHaveFocus(),
    );
    expect(api.writes()).toEqual([
      {
        method: 'POST',
        path: `/api/organizations/${ids.alfa}/projects/${ids.old}/restore`,
        body: undefined,
      },
    ]);
  });
});

describe('mod-002 US-2 Scenario: Restored exactly as it was (a project deactivated on its own)', () => {
  it('«Старый проект» stays under the deactivated tab after its organization is restored', async () => {
    const api = serveOrganizations();
    const alfa = api.data.organizations.find((organization) => organization.id === ids.alfa)!;
    alfa.active = false;
    alfa.active = true;
    renderPage({ path: ALFA_DEACTIVATED });

    expect(
      within(await screen.findByRole('table', { name: 'Деактивированные проекты' })).getByText(
        'Старый проект',
      ),
    ).toBeInTheDocument();
  });
});
