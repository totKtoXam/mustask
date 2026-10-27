import { describe, expect, it } from 'vitest';
import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import {
  PROJECTS_URL,
  PROJECT_AREAS_URL,
  exampleDataset,
  ids,
  noAreasDataset,
  problem,
} from '../testFixtures';
import { serveOrganizations, serveProblem } from '../testApi';
import { renderPage } from '../testRender';

const PRM_GENERAL = `/organizations/${ids.alfa}/projects/${ids.prm}/settings/general`;
const PLATFORM_TEAM = `/organizations/${ids.alfa}/settings/teams/${ids.platformTeam}`;
const ALFA_PROJECTS = `/organizations/${ids.alfa}/settings/projects`;
const ALFA_TEAMS = `/organizations/${ids.alfa}/settings/teams`;

describe('mod-018 design § 7: «Области» of a project’s «Общие»', () => {
  it('lists the areas by path, each a link to its page', async () => {
    const data = exampleDataset();
    data.areaProjects = [
      { areaId: ids.gatewayArea, targetId: ids.prm },
      { areaId: ids.mobileArea, targetId: ids.prm },
    ];
    serveOrganizations(data);
    renderPage({ path: PRM_GENERAL });

    const table = await screen.findByRole('table', { name: 'Области проекта' });

    expect(within(table).getByRole('link', { name: 'Платформа / API / Шлюз' })).toHaveAttribute(
      'href',
      `/organizations/${ids.alfa}/settings/areas/${ids.gatewayArea}`,
    );
    expect(within(table).getByRole('link', { name: 'Мобильное приложение' })).toBeInTheDocument();
  });

  it('says «Проект не входит ни в одну область» for a project with none', async () => {
    serveOrganizations(noAreasDataset());
    renderPage({ path: PRM_GENERAL });

    expect(await screen.findByText('Проект не входит ни в одну область')).toBeInTheDocument();
  });

  it('adds areas by path and unlinks one after a confirmation', async () => {
    const api = serveOrganizations();
    renderPage({ path: PRM_GENERAL });
    await screen.findByRole('table', { name: 'Области проекта' });

    await userEvent.click(screen.getByRole('button', { name: 'Добавить область' }));
    const dialog = await screen.findByRole('dialog', { name: 'Добавление областей в проект' });
    await userEvent.click(within(dialog).getByRole('combobox', { name: /Области/ }));
    const labels = within(await screen.findByRole('listbox'))
      .getAllByRole('option')
      .map((option) => option.textContent);
    // «Платформа» is linked already.
    expect(labels).toEqual([
      'Мобильное приложение',
      'Отчёты',
      'Платформа / API',
      'Платформа / API / Шлюз',
    ]);
    await userEvent.click(await screen.findByRole('option', { name: 'Отчёты' }));
    await userEvent.click(within(dialog).getByRole('button', { name: 'Добавить' }));
    expect(await screen.findByText('Область «Отчёты» добавлена.')).toBeInTheDocument();

    const table = await screen.findByRole('table', { name: 'Области проекта' });
    await waitFor(() => expect(within(table).getByText('Отчёты')).toBeInTheDocument());
    await userEvent.click(within(table).getByRole('button', { name: 'Ещё действия: Платформа' }));
    await userEvent.click(await screen.findByRole('menuitem', { name: 'Отключить от проекта' }));
    const confirm = await screen.findByRole('dialog', { name: 'Отключение области от проекта' });
    expect(
      within(confirm).getByText(
        'Отключить область «Платформа» от проекта «Пример проекта»? Сама область и проект не изменятся.',
      ),
    ).toBeInTheDocument();
    await userEvent.click(within(confirm).getByRole('button', { name: 'Отключить' }));

    expect(await screen.findByText('Область отключена от проекта.')).toBeInTheDocument();
    expect(api.writes().map((write) => [write.method, write.path, write.body])).toEqual([
      [
        'POST',
        `/api/organizations/${ids.alfa}/projects/${ids.prm}/areas`,
        { areaIds: [ids.reportsArea] },
      ],
      [
        'DELETE',
        `/api/organizations/${ids.alfa}/projects/${ids.prm}/areas/${ids.platformArea}`,
        undefined,
      ],
    ]);
  });

  it('points at «Области» when the organization has none, with «Добавить» unavailable', async () => {
    serveOrganizations(noAreasDataset());
    renderPage({ path: PRM_GENERAL });
    await screen.findByText('Проект не входит ни в одну область');

    await userEvent.click(screen.getByRole('button', { name: 'Добавить область' }));
    const dialog = await screen.findByRole('dialog', { name: 'Добавление областей в проект' });

    expect(within(dialog).getByRole('link', { name: '«Области»' })).toHaveAttribute(
      'href',
      `/organizations/${ids.alfa}/settings/areas`,
    );
    expect(within(dialog).getByRole('button', { name: 'Добавить' })).toBeDisabled();
  });

  it('puts a vanished area under the field and keeps the dialog', async () => {
    serveOrganizations();
    serveProblem('post', PROJECT_AREAS_URL, problem('organizations.area.not_found'), {
      once: true,
    });
    renderPage({ path: PRM_GENERAL });
    await screen.findByRole('table', { name: 'Области проекта' });

    await userEvent.click(screen.getByRole('button', { name: 'Добавить область' }));
    const dialog = await screen.findByRole('dialog', { name: 'Добавление областей в проект' });
    await userEvent.click(within(dialog).getByRole('combobox', { name: /Области/ }));
    await userEvent.click(await screen.findByRole('option', { name: 'Отчёты' }));
    await userEvent.click(within(dialog).getByRole('button', { name: 'Добавить' }));

    expect(await within(dialog).findByText('Область не найдена.')).toBeInTheDocument();
  });

  it('gives a reader the list and no control', async () => {
    serveOrganizations();
    renderPage({ path: PRM_GENERAL, role: 'viewer' });

    await screen.findByRole('table', { name: 'Области проекта' });
    expect(screen.queryByRole('button', { name: 'Добавить область' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /^Ещё действия:/ })).not.toBeInTheDocument();
  });
});

describe('mod-018 design § 7: «Области» of a team’s page', () => {
  it('sits between the projects and the members, and lists the team’s areas', async () => {
    serveOrganizations();
    renderPage({ path: PLATFORM_TEAM });

    const areas = await screen.findByRole('table', { name: 'Области команды' });
    expect(within(areas).getByRole('link', { name: 'Платформа' })).toBeInTheDocument();
    const headings = screen.getAllByRole('heading', { level: 2 }).map((h) => h.textContent ?? '');
    const at = (title: string) => headings.findIndex((text) => text.startsWith(title));
    expect(at('Области')).toBeGreaterThan(at('Проекты'));
    expect(at('Области')).toBeLessThan(at('Участники команды'));
  });

  it('adds an area and unlinks it from the team’s end', async () => {
    const api = serveOrganizations();
    renderPage({ path: PLATFORM_TEAM });
    await screen.findByRole('table', { name: 'Области команды' });

    await userEvent.click(screen.getByRole('button', { name: 'Добавить область' }));
    const dialog = await screen.findByRole('dialog', { name: 'Добавление областей в команду' });
    await userEvent.click(within(dialog).getByRole('combobox', { name: /Области/ }));
    await userEvent.click(await screen.findByRole('option', { name: 'Отчёты' }));
    await userEvent.click(within(dialog).getByRole('button', { name: 'Добавить' }));
    expect(await screen.findByText('Область «Отчёты» добавлена.')).toBeInTheDocument();

    const table = await screen.findByRole('table', { name: 'Области команды' });
    await waitFor(() => expect(within(table).getByText('Отчёты')).toBeInTheDocument());
    await userEvent.click(within(table).getByRole('button', { name: 'Ещё действия: Отчёты' }));
    await userEvent.click(await screen.findByRole('menuitem', { name: 'Отключить от команды' }));
    const confirm = await screen.findByRole('dialog', { name: 'Отключение области от команды' });
    await userEvent.click(within(confirm).getByRole('button', { name: 'Отключить' }));

    expect(await screen.findByText('Область отключена от команды.')).toBeInTheDocument();
    expect(api.writes().map((write) => [write.method, write.path])).toEqual([
      ['POST', `/api/organizations/${ids.alfa}/teams/${ids.platformTeam}/areas`],
      [
        'DELETE',
        `/api/organizations/${ids.alfa}/teams/${ids.platformTeam}/areas/${ids.reportsArea}`,
      ],
    ]);
  });
});

describe('mod-018 design § 7: «Области» in «Новый проект» and «Новая команда»', () => {
  it('«Новый проект» sends the chosen areas as `areaIds`, the hint naming the project page', async () => {
    const api = serveOrganizations();
    renderPage({ path: ALFA_PROJECTS });
    await screen.findByRole('table', { name: 'Активные проекты' });
    await userEvent.click(screen.getByRole('button', { name: 'Создать проект' }));
    const dialog = await screen.findByRole('dialog', { name: 'Новый проект' });

    expect(
      within(dialog).getByText('Необязательно. Области можно добавить позже на странице проекта.'),
    ).toBeInTheDocument();
    await userEvent.type(within(dialog).getByRole('textbox', { name: 'Название' }), 'Ядро');
    await userEvent.type(within(dialog).getByRole('textbox', { name: 'Ключ' }), 'CORE');
    await userEvent.click(within(dialog).getByRole('combobox', { name: /Области/ }));
    await userEvent.click(await screen.findByRole('option', { name: 'Платформа / API' }));
    await userEvent.click(within(dialog).getByRole('button', { name: 'Создать' }));

    expect(await screen.findByText('Проект «Ядро» создан.')).toBeInTheDocument();
    expect(api.writes()[0]?.body).toEqual({ name: 'Ядро', key: 'CORE', areaIds: [ids.apiArea] });
  });

  it('«Новая команда» sends them too, and a team with no area sends no `areaIds`', async () => {
    const api = serveOrganizations();
    renderPage({ path: ALFA_TEAMS });
    await screen.findByRole('table');
    await userEvent.click(screen.getByRole('button', { name: 'Новая команда' }));
    let dialog = await screen.findByRole('dialog', { name: 'Новая команда' });

    expect(
      within(dialog).getByText('Необязательно. Области можно добавить позже на странице команды.'),
    ).toBeInTheDocument();
    await userEvent.type(within(dialog).getByRole('textbox', { name: /Название/ }), 'Данные');
    await userEvent.click(within(dialog).getByRole('combobox', { name: /Области/ }));
    await userEvent.click(await screen.findByRole('option', { name: 'Отчёты' }));
    await userEvent.click(within(dialog).getByRole('button', { name: 'Создать' }));
    expect(await screen.findByText('Команда «Данные» создана.')).toBeInTheDocument();
    expect(api.writes()[0]?.body).toEqual({ name: 'Данные', areaIds: [ids.reportsArea] });

    await userEvent.click(screen.getByRole('button', { name: 'Новая команда' }));
    dialog = await screen.findByRole('dialog', { name: 'Новая команда' });
    await userEvent.type(within(dialog).getByRole('textbox', { name: /Название/ }), 'Без областей');
    await userEvent.click(within(dialog).getByRole('button', { name: 'Создать' }));
    await screen.findByText('Команда «Без областей» создана.');
    expect(api.writes()[1]?.body).toEqual({ name: 'Без областей' });
  });

  it('says «В организации пока нет областей.» in the list when there are none', async () => {
    serveOrganizations(noAreasDataset());
    renderPage({ path: ALFA_PROJECTS });
    await screen.findByRole('table', { name: 'Активные проекты' });
    await userEvent.click(screen.getByRole('button', { name: 'Создать проект' }));
    const dialog = await screen.findByRole('dialog', { name: 'Новый проект' });

    await userEvent.click(within(dialog).getByRole('combobox', { name: /Области/ }));

    expect(await screen.findByText('В организации пока нет областей.')).toBeInTheDocument();
  });

  it('puts a vanished area on «Области», reads the list again and drops the choice', async () => {
    serveOrganizations();
    serveProblem('post', PROJECTS_URL, problem('organizations.area.not_found'), { once: true });
    renderPage({ path: ALFA_PROJECTS });
    await screen.findByRole('table', { name: 'Активные проекты' });
    await userEvent.click(screen.getByRole('button', { name: 'Создать проект' }));
    const dialog = await screen.findByRole('dialog', { name: 'Новый проект' });
    await userEvent.type(within(dialog).getByRole('textbox', { name: 'Название' }), 'Ядро');
    await userEvent.type(within(dialog).getByRole('textbox', { name: 'Ключ' }), 'CORE');
    await userEvent.click(within(dialog).getByRole('combobox', { name: /Области/ }));
    await userEvent.click(await screen.findByRole('option', { name: 'Отчёты' }));
    await userEvent.click(within(dialog).getByRole('button', { name: 'Создать' }));

    expect(await within(dialog).findByText('Область не найдена.')).toBeInTheDocument();
  });
});
