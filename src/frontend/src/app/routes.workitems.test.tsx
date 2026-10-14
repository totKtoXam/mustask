import { describe, expect, it } from 'vitest';
import { MemoryRouter } from 'react-router-dom';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { AuthContext } from '../features/auth/AuthContext';
import {
  exampleDataset as organizationsDataset,
  ids as organizationIds,
} from '../features/organizations/testFixtures';
import { serveOrganizations } from '../features/organizations/testApi';
import { serveWorkItems } from '../features/workitems/testApi';
import { emptyDataset, item, type Dataset } from '../features/workitems/testFixtures';
import { authValue, LocationProbe } from '../test/renderShell';
import { AppRoutes } from './routes';

// mod-003 through the panel's own router and the real context source: the two
// routes registered in `app/routes.tsx`, the «Рабочие элементы» entry of the
// navigation model, the switcher following the address (D-5). The work items
// API serves mod-002's «Альфа» / «Пример проекта» (`PRM`).

const LIST = `/organizations/${organizationIds.alfa}/projects/${organizationIds.prm}/workitems`;

function alfaDataset(): Dataset {
  const data = emptyDataset();
  data.organizations = [{ id: organizationIds.alfa, name: 'Альфа', active: true }];
  data.projects = [
    {
      id: organizationIds.prm,
      organizationId: organizationIds.alfa,
      name: 'Пример проекта',
      key: 'PRM',
      active: true,
    },
  ];
  data.areas = [
    {
      id: organizationIds.prmRoot,
      projectId: organizationIds.prm,
      parentId: null,
      name: 'Пример проекта',
    },
  ];
  data.items = [
    item(organizationIds.prm, 7, 'BUG', 'Падение при пустом заголовке', {
      areaId: organizationIds.prmRoot,
    }),
  ];
  return data;
}

function renderRoutes(path: string) {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  return render(
    <QueryClientProvider client={queryClient}>
      <AuthContext.Provider value={{ ...authValue([]), subject: 'user-1' }}>
        <MemoryRouter initialEntries={[path]}>
          <AppRoutes />
          <LocationProbe />
        </MemoryRouter>
      </AuthContext.Provider>
    </QueryClientProvider>,
  );
}

const mainNavigation = () => screen.getByRole('navigation', { name: 'Основное меню' });

describe('mod-003 the two routes in the shell', () => {
  it('the list opens «Рабочие элементы» as the one h1 and tab, its entry current, no trail', async () => {
    serveOrganizations();
    serveWorkItems(alfaDataset());
    renderRoutes(LIST);

    // The gate draws the page's header over a skeleton while the codes are
    // read (mod-016 design § 2.1, row 1) and the page's own header replaces it.
    await waitFor(() =>
      expect(screen.queryByRole('status', { name: 'Загрузка данных' })).not.toBeInTheDocument(),
    );
    expect(screen.getByRole('heading', { level: 1, name: 'Рабочие элементы' })).toBeInTheDocument();
    expect(screen.getAllByRole('heading', { level: 1 })).toHaveLength(1);
    await waitFor(() => expect(document.title).toBe('Рабочие элементы — Mustask'));
    const entry = await within(mainNavigation()).findByRole('link', { name: 'Рабочие элементы' });
    expect(entry).toHaveAttribute('aria-current', 'page');
    expect(screen.queryByRole('navigation', { name: 'Путь к странице' })).toBeNull();
    expect(screen.queryByRole('navigation', { name: 'Меню настроек' })).toBeNull();
  });

  it('the form is a project-level page: the switcher follows the address and «Рабочие элементы» is marked (D-5)', async () => {
    serveOrganizations();
    serveWorkItems(alfaDataset());
    renderRoutes(LIST);

    await userEvent.click(
      await screen.findByRole('link', { name: 'Падение при пустом заголовке' }),
    );

    expect(screen.getByTestId('location')).toHaveTextContent(`${LIST}/PRM-BUG-7`);
    expect(
      await screen.findByRole('heading', { level: 1, name: 'Баг PRM-BUG-7' }),
    ).toBeInTheDocument();
    expect(
      within(mainNavigation()).getByRole('link', { name: 'Рабочие элементы' }),
    ).toHaveAttribute('aria-current', 'true');
    const switcher = screen.getByRole('group', { name: 'Контекст работы' });
    expect(within(switcher).getByRole('button', { name: /^Проект:/ })).toHaveAttribute(
      'aria-label',
      'Проект: Пример проекта',
    );
    const trail = screen.getByRole('navigation', { name: 'Путь к странице' });
    expect(within(trail).getByRole('link', { name: 'Рабочие элементы' })).toHaveAttribute(
      'href',
      LIST,
    );
  });
});

describe('mod-003 US-2 Scenario: The navigation leads to the chosen project’s list', () => {
  it('«Рабочие элементы» in the main navigation opens the chosen project’s list', async () => {
    serveOrganizations();
    serveWorkItems(alfaDataset());
    renderRoutes(
      `/organizations/${organizationIds.alfa}/projects/${organizationIds.prm}/settings/general`,
    );

    const entry = await within(mainNavigation()).findByRole('link', { name: 'Рабочие элементы' });
    await userEvent.click(entry);

    expect(screen.getByTestId('location')).toHaveTextContent(LIST);
    expect(
      await screen.findByRole('heading', { level: 1, name: 'Рабочие элементы' }),
    ).toBeInTheDocument();
  });

  it('with no project in the switcher, the entry is not shown', async () => {
    const data = organizationsDataset();
    data.projects = data.projects.filter(
      (project) => project.organizationId !== organizationIds.beta,
    );
    serveOrganizations(data);
    renderRoutes(`/organizations/${organizationIds.beta}/settings/general`);

    await screen.findByRole('heading', { level: 1, name: 'Общие' });
    const switcher = await screen.findByRole('group', { name: 'Контекст работы' });
    expect(await within(switcher).findByText('Нет проектов')).toBeInTheDocument();
    // The organization-scoped entries are shown, so the navigation has its real context.
    expect(
      within(screen.getByRole('navigation', { name: 'Меню настроек' })).getByRole('link', {
        name: 'Проекты',
      }),
    ).toBeInTheDocument();
    expect(
      within(mainNavigation()).queryByRole('link', { name: 'Рабочие элементы' }),
    ).not.toBeInTheDocument();
  });
});
