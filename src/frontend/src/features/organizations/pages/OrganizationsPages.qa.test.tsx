// Tests added by the frontend QA pass of mod-002 (qa.md § Frontend, marked
// (QA)): scenarios of the QA plan's "Additional scenarios" that the
// developer's tests did not cover. Each names its plan row.
import { describe, expect, it } from 'vitest';
import type { ReactNode } from 'react';
import { MemoryRouter } from 'react-router-dom';
import { QueryClient, QueryClientProvider, focusManager } from '@tanstack/react-query';
import { act, renderHook, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { useOrganizationsContextSource } from '../context/useOrganizationsContextSource';
import { exampleDataset, ids } from '../testFixtures';
import { serveOrganizations } from '../testApi';
import { CREATE_RIGHT, renderPage } from '../testRender';

const PRM_TEAMS = `/organizations/${ids.alfa}/projects/${ids.prm}/settings/teams`;

describe('(QA) mod-002 F-1: a dialog’s primary activated twice sends one request', () => {
  it('a double click on «Создать» creates one organization', async () => {
    const api = serveOrganizations();
    renderPage({ path: '/organizations', permissions: CREATE_RIGHT });
    await screen.findByRole('table', { name: 'Активные организации' });
    await userEvent.click(screen.getByRole('button', { name: 'Создать организацию' }));
    const dialog = await screen.findByRole('dialog', { name: 'Новая организация' });
    await userEvent.type(within(dialog).getByRole('textbox', { name: 'Название' }), 'Дельта');

    await userEvent.dblClick(within(dialog).getByRole('button', { name: 'Создать' }));

    expect(await screen.findByText('Организация «Дельта» создана.')).toBeInTheDocument();
    expect(api.writes()).toEqual([
      { method: 'POST', path: '/api/organizations', body: { name: 'Дельта' } },
    ]);
  });

  it('Enter, then a click on «Создать» while the first is sent, creates one organization', async () => {
    const api = serveOrganizations();
    renderPage({ path: '/organizations', permissions: CREATE_RIGHT });
    await screen.findByRole('table', { name: 'Активные организации' });
    await userEvent.click(screen.getByRole('button', { name: 'Создать организацию' }));
    const dialog = await screen.findByRole('dialog', { name: 'Новая организация' });

    await userEvent.type(
      within(dialog).getByRole('textbox', { name: 'Название' }),
      'Дельта{Enter}',
    );
    const primary = within(dialog).getByRole('button', { name: 'Создать' });
    // Busy: the primary is disabled; a click that lands anyway does nothing.
    expect(primary).toBeDisabled();
    await userEvent.setup({ pointerEventsCheck: 0 }).click(primary);

    expect(await screen.findByText('Организация «Дельта» создана.')).toBeInTheDocument();
    expect(api.writes()).toHaveLength(1);
  });
});

describe('(QA) mod-002 F-2: names holding markup and quotes are text everywhere', () => {
  const NAME = '<b>Альфа</b> & "Кавычки"';

  function withMarkupName() {
    const data = exampleDataset();
    data.organizations[0]!.name = NAME;
    return serveOrganizations(data);
  }

  it('in the active table, on «Общие» and in the deactivation confirmation', async () => {
    withMarkupName();
    const { unmount } = renderPage({ path: '/organizations' });
    const table = await screen.findByRole('table', { name: 'Активные организации' });
    expect(within(table).getByRole('link', { name: NAME })).toBeInTheDocument();
    expect(table.querySelector('b')).toBeNull();
    unmount();

    renderPage({ path: `/organizations/${ids.alfa}/settings/general` });
    expect(await screen.findByText(NAME)).toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: 'Деактивировать' }));
    const dialog = await screen.findByRole('dialog', { name: 'Деактивировать организацию?' });
    expect(dialog).toHaveTextContent(`Организация «${NAME}» пропадёт из списков и переключателя`);
    expect(document.querySelector('b')).toBeNull();
  });
});

describe('(QA) mod-002 F-6: a dialog answered by an outer level’s not found', () => {
  it('a team rename in a project deactivated meanwhile shows «Проект не найден.»; closing shows the frame', async () => {
    const api = serveOrganizations();
    renderPage({ path: PRM_TEAMS });
    await screen.findByRole('table', { name: 'Команды' });
    await userEvent.click(screen.getByRole('button', { name: 'Действия: Платформа' }));
    await userEvent.click(
      within(await screen.findByRole('menu')).getByRole('menuitem', { name: 'Переименовать' }),
    );
    const dialog = await screen.findByRole('dialog', { name: 'Переименование команды' });

    // Another tab deactivates the project while the dialog is open.
    api.data.projects.find((project) => project.id === ids.prm)!.active = false;
    const field = within(dialog).getByRole('textbox', { name: 'Название' });
    await userEvent.clear(field);
    await userEvent.type(field, 'Ядро');
    await userEvent.click(within(dialog).getByRole('button', { name: 'Сохранить' }));

    expect(await within(dialog).findByRole('alert')).toHaveTextContent('Проект не найден.');
    expect(field).toHaveValue('Ядро');
    await userEvent.click(within(dialog).getByRole('button', { name: 'Отмена' }));

    expect(
      await screen.findByRole('heading', { level: 1, name: 'Проект не найден' }),
    ).toBeInTheDocument();
  });
});

describe('(QA) mod-002 design § 4.3: focus returns to the opener of a row menu’s dialog', () => {
  it('«Отмена» of «Переименование команды» puts focus back on «Действия: Платформа»', async () => {
    serveOrganizations();
    renderPage({ path: PRM_TEAMS });
    await screen.findByRole('table', { name: 'Команды' });
    const opener = screen.getByRole('button', { name: 'Действия: Платформа' });
    await userEvent.click(opener);
    await userEvent.click(
      within(await screen.findByRole('menu')).getByRole('menuitem', { name: 'Переименовать' }),
    );
    const dialog = await screen.findByRole('dialog', { name: 'Переименование команды' });

    await userEvent.click(within(dialog).getByRole('button', { name: 'Отмена' }));

    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
    await waitFor(() => expect(opener).toHaveFocus());
  });
});

describe('(QA) mod-002 F-14: the switcher’s levels follow other users’ changes on window focus', () => {
  it('reads the organizations again when the window regains focus, and lists one created meanwhile', async () => {
    const api = serveOrganizations();
    const queryClient = new QueryClient({
      defaultOptions: { queries: { retry: false, refetchOnWindowFocus: false } },
    });
    const wrapper = ({ children }: { children: ReactNode }) => (
      <QueryClientProvider client={queryClient}>
        <MemoryRouter initialEntries={['/home']}>{children}</MemoryRouter>
      </QueryClientProvider>
    );
    const { result } = renderHook(
      () => useOrganizationsContextSource({ enabled: true, subject: 'user-1' }),
      { wrapper },
    );
    await waitFor(() => expect(result.current.status).toBe('ready'));
    const readsBefore = api.requests.filter(
      (request) => request.method === 'GET' && request.path === '/api/organizations',
    ).length;

    // Another user creates an organization; this window is away, then back.
    api.data.organizations.push({
      id: 'a0000000-0000-4000-8000-0000000000aa',
      name: 'Вега',
      active: true,
    });
    act(() => focusManager.setFocused(false));
    act(() => focusManager.setFocused(true));

    await waitFor(() =>
      expect(result.current.organizations.map((entry) => entry.name)).toEqual([
        'Альфа',
        'Бета',
        'Вега',
      ]),
    );
    expect(
      api.requests.filter(
        (request) => request.method === 'GET' && request.path === '/api/organizations',
      ).length,
    ).toBeGreaterThan(readsBefore);
    act(() => focusManager.setFocused(undefined));
  });
});
