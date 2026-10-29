import { describe, expect, it } from 'vitest';
import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { PROJECTS_URL, TEAM_AREAS_URL, ids, noAreasDataset, problem } from '../testFixtures';
import { serveOrganizations, serveProblem } from '../testApi';
import { renderPage } from '../testRender';

const PRM_GENERAL = `/organizations/${ids.alfa}/projects/${ids.prm}/settings/general`;
const PLATFORM_TEAM = `/organizations/${ids.alfa}/settings/teams/${ids.platformTeam}`;
const ALFA_PROJECTS = `/organizations/${ids.alfa}/settings/projects`;
const ALFA_TEAMS = `/organizations/${ids.alfa}/settings/teams`;

describe('mod-018 design § 6: «Департаменты» of a team’s page', () => {
  it('sits between the projects and the members, and lists the team’s departments', async () => {
    serveOrganizations();
    renderPage({ path: PLATFORM_TEAM });

    const departments = await screen.findByRole('table', { name: 'Департаменты команды' });
    expect(within(departments).getByRole('link', { name: 'Платформа' })).toHaveAttribute(
      'href',
      `/organizations/${ids.alfa}/settings/areas/${ids.platformArea}`,
    );
    const headings = screen.getAllByRole('heading', { level: 2 }).map((h) => h.textContent ?? '');
    const at = (title: string) => headings.findIndex((text) => text.startsWith(title));
    expect(at('Департаменты')).toBeGreaterThan(at('Проекты'));
    expect(at('Департаменты')).toBeLessThan(at('Участники команды'));
  });

  it('says so for a team in no department', async () => {
    serveOrganizations(noAreasDataset());
    renderPage({ path: PLATFORM_TEAM });

    expect(await screen.findByText('Команда не входит ни в один департамент')).toBeInTheDocument();
  });

  it('offers only departments, adds one and unlinks it from the team’s end', async () => {
    const api = serveOrganizations();
    renderPage({ path: PLATFORM_TEAM });
    await screen.findByRole('table', { name: 'Департаменты команды' });

    await userEvent.click(screen.getByRole('button', { name: 'Добавить департамент' }));
    const dialog = await screen.findByRole('dialog', {
      name: 'Добавление департаментов в команду',
    });
    await userEvent.click(within(dialog).getByRole('combobox', { name: /Департаменты/ }));
    // «Платформа» is linked already; no project node and no sub-area is ever offered.
    expect(
      within(await screen.findByRole('listbox'))
        .getAllByRole('option')
        .map((option) => option.textContent),
    ).toEqual(['Отчёты']);
    await userEvent.click(await screen.findByRole('option', { name: 'Отчёты' }));
    await userEvent.click(within(dialog).getByRole('button', { name: 'Добавить' }));
    expect(await screen.findByText('Департамент «Отчёты» добавлен.')).toBeInTheDocument();

    const table = await screen.findByRole('table', { name: 'Департаменты команды' });
    await waitFor(() => expect(within(table).getByText('Отчёты')).toBeInTheDocument());
    await userEvent.click(within(table).getByRole('button', { name: 'Ещё действия: Отчёты' }));
    await userEvent.click(await screen.findByRole('menuitem', { name: 'Отключить от команды' }));
    const confirm = await screen.findByRole('dialog', {
      name: 'Отключение департамента от команды',
    });
    await userEvent.click(within(confirm).getByRole('button', { name: 'Отключить' }));

    expect(await screen.findByText('Департамент отключён от команды.')).toBeInTheDocument();
    expect(api.writes().map((write) => [write.method, write.path, write.body])).toEqual([
      [
        'POST',
        `/api/organizations/${ids.alfa}/teams/${ids.platformTeam}/areas`,
        { areaIds: [ids.reportsArea] },
      ],
      [
        'DELETE',
        `/api/organizations/${ids.alfa}/teams/${ids.platformTeam}/areas/${ids.reportsArea}`,
        undefined,
      ],
    ]);
  });

  it('shows the type refusal and keeps the dialog', async () => {
    serveOrganizations();
    serveProblem('post', TEAM_AREAS_URL, problem('organizations.area.link_type_not_allowed'), {
      once: true,
    });
    renderPage({ path: PLATFORM_TEAM });
    await screen.findByRole('table', { name: 'Департаменты команды' });

    await userEvent.click(screen.getByRole('button', { name: 'Добавить департамент' }));
    const dialog = await screen.findByRole('dialog', {
      name: 'Добавление департаментов в команду',
    });
    await userEvent.click(within(dialog).getByRole('combobox', { name: /Департаменты/ }));
    await userEvent.click(await screen.findByRole('option', { name: 'Отчёты' }));
    await userEvent.click(within(dialog).getByRole('button', { name: 'Добавить' }));

    expect(
      await within(dialog).findByText(/Команду можно связать только с департаментом/),
    ).toBeInTheDocument();
  });

  it('gives a reader the list and no control', async () => {
    serveOrganizations();
    renderPage({ path: PLATFORM_TEAM, role: 'viewer' });

    await screen.findByRole('table', { name: 'Департаменты команды' });
    expect(screen.queryByRole('button', { name: 'Добавить департамент' })).not.toBeInTheDocument();
    expect(
      screen.queryByRole('button', { name: 'Ещё действия: Платформа' }),
    ).not.toBeInTheDocument();
  });
});

describe('mod-018 design § 7: «Расположение» of a project', () => {
  it('shows the project’s department on «Общие» and moves it with «Переместить»', async () => {
    const api = serveOrganizations();
    renderPage({ path: PRM_GENERAL });

    expect(await screen.findByText('Расположение')).toBeInTheDocument();
    expect(await screen.findByText('Платформа')).toBeInTheDocument();

    await userEvent.click(await screen.findByRole('button', { name: 'Переместить' }));
    const dialog = await screen.findByRole('dialog', { name: 'Перемещение проекта' });
    await userEvent.click(within(dialog).getByRole('combobox', { name: /Новое расположение/ }));
    await userEvent.click(await screen.findByRole('option', { name: 'Организация' }));
    await userEvent.click(within(dialog).getByRole('button', { name: 'Переместить' }));

    expect(await screen.findByText('Проект «Пример проекта» перемещён.')).toBeInTheDocument();
    expect(api.writes()[0]).toEqual({
      method: 'PUT',
      path: `/api/organizations/${ids.alfa}/areas/${ids.prmRoot}/parent`,
      body: { parentId: null },
    });
  });

  it('offers no «Переместить» to a reader', async () => {
    serveOrganizations();
    renderPage({ path: PRM_GENERAL, role: 'viewer' });

    await screen.findByText('Расположение');
    expect(screen.queryByRole('button', { name: 'Переместить' })).not.toBeInTheDocument();
  });
});

describe('mod-018 design § 7: «Расположение» in «Новый проект», «Департаменты» in «Новая команда»', () => {
  it('«Новый проект» sends the chosen department as `parentAreaId`', async () => {
    const api = serveOrganizations();
    renderPage({ path: ALFA_PROJECTS });
    await screen.findByRole('table', { name: 'Активные проекты' });
    await userEvent.click(screen.getByRole('button', { name: 'Создать проект' }));
    const dialog = await screen.findByRole('dialog', { name: 'Новый проект' });

    expect(
      within(dialog).getByText(
        'Необязательно. Без департамента проект появится на верхнем уровне.',
      ),
    ).toBeInTheDocument();
    await userEvent.type(within(dialog).getByRole('textbox', { name: 'Название' }), 'Ядро');
    await userEvent.type(within(dialog).getByRole('textbox', { name: 'Ключ' }), 'CORE');
    await userEvent.click(within(dialog).getByRole('combobox', { name: /Расположение/ }));
    await userEvent.click(await screen.findByRole('option', { name: 'Отчёты' }));
    await userEvent.click(within(dialog).getByRole('button', { name: 'Создать' }));

    expect(await screen.findByText('Проект «Ядро» создан.')).toBeInTheDocument();
    expect(api.writes()[0]?.body).toEqual({
      name: 'Ядро',
      key: 'CORE',
      parentAreaId: ids.reportsArea,
    });
  });

  it('«Новая команда» sends the departments as `areaIds`, and none sends no `areaIds`', async () => {
    const api = serveOrganizations();
    renderPage({ path: ALFA_TEAMS });
    await screen.findByRole('table');
    await userEvent.click(screen.getByRole('button', { name: 'Новая команда' }));
    let dialog = await screen.findByRole('dialog', { name: 'Новая команда' });

    await userEvent.type(within(dialog).getByRole('textbox', { name: /Название/ }), 'Данные');
    await userEvent.click(within(dialog).getByRole('combobox', { name: /Департаменты/ }));
    await userEvent.click(await screen.findByRole('option', { name: 'Отчёты' }));
    await userEvent.click(within(dialog).getByRole('button', { name: 'Создать' }));
    expect(await screen.findByText('Команда «Данные» создана.')).toBeInTheDocument();
    expect(api.writes()[0]?.body).toEqual({ name: 'Данные', areaIds: [ids.reportsArea] });

    await userEvent.click(screen.getByRole('button', { name: 'Новая команда' }));
    dialog = await screen.findByRole('dialog', { name: 'Новая команда' });
    await userEvent.type(within(dialog).getByRole('textbox', { name: /Название/ }), 'Без отдела');
    await userEvent.click(within(dialog).getByRole('button', { name: 'Создать' }));
    await screen.findByText('Команда «Без отдела» создана.');
    expect(api.writes()[1]?.body).toEqual({ name: 'Без отдела' });
  });

  it('puts a vanished department on «Расположение»', async () => {
    serveOrganizations();
    serveProblem('post', PROJECTS_URL, problem('organizations.area.not_found'), { once: true });
    renderPage({ path: ALFA_PROJECTS });
    await screen.findByRole('table', { name: 'Активные проекты' });
    await userEvent.click(screen.getByRole('button', { name: 'Создать проект' }));
    const dialog = await screen.findByRole('dialog', { name: 'Новый проект' });
    await userEvent.type(within(dialog).getByRole('textbox', { name: 'Название' }), 'Ядро');
    await userEvent.type(within(dialog).getByRole('textbox', { name: 'Ключ' }), 'CORE');
    await userEvent.click(within(dialog).getByRole('combobox', { name: /Расположение/ }));
    await userEvent.click(await screen.findByRole('option', { name: 'Отчёты' }));
    await userEvent.click(within(dialog).getByRole('button', { name: 'Создать' }));

    expect(await within(dialog).findByText('Область не найдена.')).toBeInTheDocument();
  });
});
