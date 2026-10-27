import { describe, expect, it } from 'vitest';
import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { AREA_URL, exampleDataset, ids, problem } from '../testFixtures';
import { serveOrganizations, serveProblem } from '../testApi';
import { renderPage } from '../testRender';

const PLATFORM = `/organizations/${ids.alfa}/settings/areas/${ids.platformArea}`;
const REPORTS = `/organizations/${ids.alfa}/settings/areas/${ids.reportsArea}`;
const API_BASE = `/api/organizations/${ids.alfa}/areas`;

async function loaded(name: string) {
  return screen.findByRole('heading', { level: 1, name: `Область: ${name}` });
}

function facts(): HTMLElement {
  return screen.getByText('Родительская область').closest('dl') as HTMLElement;
}

describe('mod-018 design § 4: an area’s page', () => {
  it('shows the facts, the children and the linked projects and teams, each on its own', async () => {
    serveOrganizations();
    renderPage({ path: PLATFORM });

    expect(screen.getByRole('heading', { level: 1, name: 'Область' })).toBeInTheDocument();
    await loaded('Платформа');

    expect(facts()).toHaveTextContent('НазваниеПлатформа');
    expect(facts()).toHaveTextContent('Родительская областьВерхний уровень');
    expect(facts()).toHaveTextContent('ОписаниеПлатформенные сервисы');
    const children = await screen.findByRole('table', { name: 'Вложенные области' });
    expect(within(children).getByRole('link', { name: 'API' })).toHaveAttribute(
      'href',
      `/organizations/${ids.alfa}/settings/areas/${ids.apiArea}`,
    );
    const projects = await screen.findByRole('table', { name: 'Проекты области' });
    expect(within(projects).getByRole('link', { name: 'Пример проекта' })).toHaveAttribute(
      'href',
      `/organizations/${ids.alfa}/projects/${ids.prm}/settings/general`,
    );
    expect(within(projects).getByText('PRM')).toBeInTheDocument();
    const teams = await screen.findByRole('table', { name: 'Команды области' });
    expect(within(teams).getByRole('link', { name: 'Платформа' })).toHaveAttribute(
      'href',
      `/organizations/${ids.alfa}/settings/teams/${ids.platformTeam}`,
    );
  });

  it('names the parent as a link, and says what is empty', async () => {
    serveOrganizations();
    renderPage({ path: `/organizations/${ids.alfa}/settings/areas/${ids.apiArea}` });
    await loaded('API');

    expect(within(facts()).getByRole('link', { name: 'Платформа' })).toHaveAttribute(
      'href',
      `/organizations/${ids.alfa}/settings/areas/${ids.platformArea}`,
    );
    expect(facts()).toHaveTextContent('ОписаниеОписание не задано');
    expect(await screen.findByText('К области не привязаны проекты')).toBeInTheDocument();
    expect(screen.getByText('К области не привязаны команды')).toBeInTheDocument();
  });

  it.each([
    [
      'an unknown area',
      `/organizations/${ids.alfa}/settings/areas/d0000000-0000-4000-8000-0000000000ff`,
    ],
    ['an area of another organization', `/organizations/${ids.alfa}/settings/areas/${ids.repRoot}`],
  ])('%s answers «Область не найдена» and keeps the settings navigation', async (_case, path) => {
    serveOrganizations();
    renderPage({ path });

    expect(
      await screen.findByRole('heading', { level: 1, name: 'Область не найдена' }),
    ).toBeInTheDocument();
    expect(screen.getAllByRole('heading', { level: 1 })).toHaveLength(1);
  });

  it('fails the children section of one read alone: the facts stay, the section says why and retries', async () => {
    serveOrganizations();
    serveProblem('get', `${AREA_URL}/projects`, problem('organizations.validation_failed'), {
      once: true,
    });
    renderPage({ path: PLATFORM });
    await loaded('Платформа');

    await userEvent.click(await screen.findByRole('button', { name: 'Повторить' }));

    expect(await screen.findByRole('table', { name: 'Проекты области' })).toBeInTheDocument();
    expect(facts()).toHaveTextContent('НазваниеПлатформа');
  });
});

describe('mod-018 US-2: the commands of the page', () => {
  it('renames and describes in place', async () => {
    const api = serveOrganizations();
    renderPage({ path: REPORTS });
    await loaded('Отчёты');

    await userEvent.click(screen.getByRole('button', { name: 'Переименовать' }));
    let dialog = await screen.findByRole('dialog', { name: 'Переименование области' });
    const field = within(dialog).getByRole('textbox', { name: /Название/ });
    await userEvent.clear(field);
    await userEvent.type(field, 'Метрики');
    await userEvent.click(within(dialog).getByRole('button', { name: 'Сохранить' }));
    expect(await screen.findByText('Область переименована.')).toBeInTheDocument();
    await loaded('Метрики');

    await userEvent.click(screen.getByRole('button', { name: 'Изменить описание' }));
    dialog = await screen.findByRole('dialog', { name: 'Описание области' });
    await userEvent.type(within(dialog).getByRole('textbox', { name: 'Описание' }), 'Данные');
    await userEvent.click(within(dialog).getByRole('button', { name: 'Сохранить' }));
    expect(await screen.findByText('Описание области сохранено.')).toBeInTheDocument();
    await waitFor(() => expect(facts()).toHaveTextContent('ОписаниеДанные'));
    expect(api.writes().map((write) => [write.method, write.path])).toEqual([
      ['PUT', `${API_BASE}/${ids.reportsArea}/name`],
      ['PUT', `${API_BASE}/${ids.reportsArea}/description`],
    ]);
  });

  it('creates a child area under this one, and moves this one', async () => {
    const api = serveOrganizations();
    renderPage({ path: REPORTS });
    await loaded('Отчёты');

    await userEvent.click(screen.getByRole('button', { name: 'Новая дочерняя область' }));
    const dialog = await screen.findByRole('dialog', { name: 'Новая область' });
    await userEvent.type(within(dialog).getByRole('textbox', { name: /Название/ }), 'Квартальные');
    await userEvent.click(within(dialog).getByRole('button', { name: 'Создать' }));
    expect(await screen.findByText('Область «Квартальные» создана.')).toBeInTheDocument();
    expect(api.writes()[0]?.body).toEqual({ name: 'Квартальные', parentId: ids.reportsArea });
    expect(
      await within(await screen.findByRole('table', { name: 'Вложенные области' })).findByText(
        'Квартальные',
      ),
    ).toBeInTheDocument();

    await userEvent.click(screen.getByRole('button', { name: 'Переместить' }));
    const move = await screen.findByRole('dialog', { name: 'Перемещение области' });
    await userEvent.click(
      within(move).getByRole('combobox', { name: /Новая родительская область/ }),
    );
    await userEvent.click(await screen.findByRole('option', { name: 'Платформа' }));
    await userEvent.click(within(move).getByRole('button', { name: 'Переместить' }));
    expect(await screen.findByText('Область «Отчёты» перемещена.')).toBeInTheDocument();
    await waitFor(() => expect(facts()).toHaveTextContent('Родительская областьПлатформа'));
  });

  it('deletes a leaf and returns to the tree with the notice', async () => {
    const api = serveOrganizations();
    renderPage({ path: REPORTS });
    await loaded('Отчёты');

    await userEvent.click(screen.getByRole('button', { name: 'Удалить область' }));
    const dialog = await screen.findByRole('dialog', { name: 'Удаление области' });
    await userEvent.click(await within(dialog).findByRole('button', { name: 'Удалить область' }));

    expect(await screen.findByText('Область «Отчёты» удалена.')).toBeInTheDocument();
    expect(api.writes()[0]?.method).toBe('DELETE');
    await waitFor(() =>
      expect(screen.getByTestId('location')).toHaveTextContent(
        `/organizations/${ids.alfa}/settings/areas`,
      ),
    );
  });

  it('a member reads the page: no command, no link control', async () => {
    serveOrganizations();
    renderPage({ path: PLATFORM, role: 'member' });
    await loaded('Платформа');

    for (const name of [
      'Переименовать',
      'Изменить описание',
      'Переместить',
      'Удалить область',
      'Новая дочерняя область',
      'Добавить проект',
      'Добавить команду',
    ]) {
      expect(screen.queryByRole('button', { name })).not.toBeInTheDocument();
    }
    expect(screen.queryByRole('button', { name: /^Ещё действия:/ })).not.toBeInTheDocument();
  });
});

describe('mod-018 US-3: the links from the area’s end', () => {
  it('adds projects: the options leave out the linked ones, «Добавить» waits for a choice', async () => {
    const api = serveOrganizations();
    renderPage({ path: PLATFORM });
    await loaded('Платформа');
    await screen.findByRole('table', { name: 'Проекты области' });

    await userEvent.click(screen.getByRole('button', { name: 'Добавить проект' }));
    const dialog = await screen.findByRole('dialog', { name: 'Добавление проектов в область' });
    expect(within(dialog).getByRole('button', { name: 'Добавить' })).toBeDisabled();
    await userEvent.click(within(dialog).getByRole('combobox', { name: /Проекты/ }));
    const options = within(await screen.findByRole('listbox')).getAllByRole('option');
    // «Пример проекта» is linked; «Старый проект» is deactivated.
    expect(options.map((option) => option.textContent)).toEqual(['АналитикаANL']);
    await userEvent.click(options[0]!);
    await userEvent.click(within(dialog).getByRole('button', { name: 'Добавить' }));

    expect(await screen.findByText('Проект «Аналитика» добавлен.')).toBeInTheDocument();
    expect(api.writes()[0]).toEqual({
      method: 'POST',
      path: `${API_BASE}/${ids.platformArea}/projects`,
      body: { projectIds: [ids.anl] },
    });
    const table = await screen.findByRole('table', { name: 'Проекты области' });
    await waitFor(() => expect(within(table).getByText('ANL')).toBeInTheDocument());
  });

  it('adds teams and unlinks one after a confirmation', async () => {
    const api = serveOrganizations();
    renderPage({ path: PLATFORM });
    await loaded('Платформа');
    await screen.findByRole('table', { name: 'Команды области' });

    await userEvent.click(screen.getByRole('button', { name: 'Добавить команду' }));
    const dialog = await screen.findByRole('dialog', { name: 'Добавление команд в область' });
    await userEvent.click(within(dialog).getByRole('combobox', { name: /Команды/ }));
    await userEvent.click(await screen.findByRole('option', { name: 'Аналитика' }));
    await userEvent.click(within(dialog).getByRole('button', { name: 'Добавить' }));
    expect(await screen.findByText('Команда «Аналитика» добавлена.')).toBeInTheDocument();

    const teams = await screen.findByRole('table', { name: 'Команды области' });
    await waitFor(() => expect(within(teams).getByText('Аналитика')).toBeInTheDocument());
    await userEvent.click(within(teams).getByRole('button', { name: 'Ещё действия: Платформа' }));
    await userEvent.click(await screen.findByRole('menuitem', { name: 'Отключить от области' }));
    const confirm = await screen.findByRole('dialog', { name: 'Отключение команды от области' });
    expect(
      within(confirm).getByText(
        'Отключить команду «Платформа» от области «Платформа»? Сама команда и её рабочие элементы не изменятся.',
      ),
    ).toBeInTheDocument();
    expect(within(confirm).getByRole('button', { name: 'Отмена' })).toHaveFocus();
    await userEvent.click(within(confirm).getByRole('button', { name: 'Отключить' }));

    expect(await screen.findByText('Команда отключена от области.')).toBeInTheDocument();
    expect(api.writes().map((write) => [write.method, write.path])).toEqual([
      ['POST', `${API_BASE}/${ids.platformArea}/teams`],
      ['DELETE', `${API_BASE}/${ids.platformArea}/teams/${ids.platformTeam}`],
    ]);
  });

  it('says when every project is linked already, and when none exists', async () => {
    const data = exampleDataset();
    data.areaProjects = [
      { areaId: ids.platformArea, targetId: ids.prm },
      { areaId: ids.platformArea, targetId: ids.anl },
    ];
    serveOrganizations(data);
    renderPage({ path: PLATFORM });
    await loaded('Платформа');
    await screen.findByRole('table', { name: 'Проекты области' });

    await userEvent.click(screen.getByRole('button', { name: 'Добавить проект' }));
    const dialog = await screen.findByRole('dialog', { name: 'Добавление проектов в область' });
    await userEvent.click(within(dialog).getByRole('combobox', { name: /Проекты/ }));

    expect(await screen.findByText('Все проекты уже входят в область.')).toBeInTheDocument();
  });

  it('puts a vanished project under the field, reads the options again and drops the choice', async () => {
    serveOrganizations();
    serveProblem('post', `${AREA_URL}/projects`, problem('organizations.project.not_found'), {
      once: true,
    });
    renderPage({ path: PLATFORM });
    await loaded('Платформа');
    await screen.findByRole('table', { name: 'Проекты области' });

    await userEvent.click(screen.getByRole('button', { name: 'Добавить проект' }));
    const dialog = await screen.findByRole('dialog', { name: 'Добавление проектов в область' });
    await userEvent.click(within(dialog).getByRole('combobox', { name: /Проекты/ }));
    await userEvent.click(await screen.findByRole('option', { name: /Аналитика/ }));
    await userEvent.click(within(dialog).getByRole('button', { name: 'Добавить' }));

    expect(await within(dialog).findByText('Проект не найден.')).toBeInTheDocument();
    expect(dialog).toBeInTheDocument();
  });

  it('shows a refusal of the area itself as the alert and reads the page again on close', async () => {
    serveOrganizations();
    renderPage({ path: PLATFORM });
    await loaded('Платформа');
    await screen.findByRole('table', { name: 'Проекты области' });
    serveProblem(
      'delete',
      `${AREA_URL}/projects/:projectId`,
      problem('organizations.area.not_found'),
    );

    const projects = screen.getByRole('table', { name: 'Проекты области' });
    await userEvent.click(
      within(projects).getByRole('button', { name: 'Ещё действия: Пример проекта' }),
    );
    await userEvent.click(await screen.findByRole('menuitem', { name: 'Отключить от области' }));
    const dialog = await screen.findByRole('dialog', { name: 'Отключение проекта от области' });
    await userEvent.click(within(dialog).getByRole('button', { name: 'Отключить' }));

    expect(await within(dialog).findByText('Область не найдена.')).toBeInTheDocument();
  });
});
