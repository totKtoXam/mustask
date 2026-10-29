import { describe, expect, it } from 'vitest';
import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { AREA_URL, exampleDataset, ids, problem } from '../testFixtures';
import { serveOrganizations, serveProblem } from '../testApi';
import { renderPage } from '../testRender';

const AREAS = `/organizations/${ids.alfa}/settings/areas`;
const PLATFORM = `${AREAS}/${ids.platformArea}`;
const PRM_NODE = `${AREAS}/${ids.prmRoot}`;
const OLD_NODE = `${AREAS}/${ids.oldRoot}`;
const API = `${AREAS}/${ids.apiArea}`;
const API_BASE = `/api/organizations/${ids.alfa}/areas`;

async function loaded(title: string) {
  return screen.findByRole('heading', { level: 1, name: title });
}

function facts(): HTMLElement {
  return screen.getByText('Тип').closest('dl') as HTMLElement;
}

describe('mod-018 design § 4: a department’s page', () => {
  it('shows the facts, its projects and its teams, each on its own', async () => {
    serveOrganizations();
    renderPage({ path: PLATFORM });

    expect(screen.getByRole('heading', { level: 1, name: 'Область' })).toBeInTheDocument();
    await loaded('Департамент: Платформа');

    expect(facts()).toHaveTextContent('НазваниеПлатформа');
    expect(facts()).toHaveTextContent('ТипДепартамент');
    expect(facts()).toHaveTextContent('ОписаниеПлатформенные сервисы');
    const projects = await screen.findByRole('table', { name: 'Проекты департамента' });
    expect(within(projects).getByRole('link', { name: 'Пример проекта' })).toHaveAttribute(
      'href',
      `${AREAS}/${ids.prmRoot}`,
    );
    const teams = await screen.findByRole('table', { name: 'Команды департамента' });
    expect(within(teams).getByRole('link', { name: 'Платформа' })).toHaveAttribute(
      'href',
      `/organizations/${ids.alfa}/settings/teams/${ids.platformTeam}`,
    );
  });

  it('says what is empty', async () => {
    serveOrganizations();
    renderPage({ path: `${AREAS}/${ids.reportsArea}` });
    await loaded('Департамент: Отчёты');

    expect(facts()).toHaveTextContent('ОписаниеОписание не задано');
    expect(await screen.findByText('В департаменте пока нет проектов.')).toBeInTheDocument();
    expect(screen.getByText('К области не привязаны команды')).toBeInTheDocument();
  });

  it.each([
    ['an unknown area', `${AREAS}/d0000000-0000-4000-8000-0000000000ff`],
    ['an area of another organization', `${AREAS}/${ids.repRoot}`],
  ])('%s answers «Область не найдена» and keeps the settings navigation', async (_case, path) => {
    serveOrganizations();
    renderPage({ path });

    expect(
      await screen.findByRole('heading', { level: 1, name: 'Область не найдена' }),
    ).toBeInTheDocument();
    expect(screen.getAllByRole('heading', { level: 1 })).toHaveLength(1);
  });

  it('renames, describes in place and deletes an empty department', async () => {
    const api = serveOrganizations();
    renderPage({ path: `${AREAS}/${ids.reportsArea}` });
    await loaded('Департамент: Отчёты');

    await userEvent.click(await screen.findByRole('button', { name: 'Переименовать' }));
    let dialog = await screen.findByRole('dialog', { name: 'Переименование департамента' });
    const field = within(dialog).getByRole('textbox', { name: /Название/ });
    await userEvent.clear(field);
    await userEvent.type(field, 'Аналитики');
    await userEvent.click(within(dialog).getByRole('button', { name: 'Сохранить' }));
    expect(await screen.findByText('Департамент переименован.')).toBeInTheDocument();
    await loaded('Департамент: Аналитики');

    await userEvent.click(screen.getByRole('button', { name: 'Удалить департамент' }));
    dialog = await screen.findByRole('dialog', { name: 'Удаление департамента' });
    await userEvent.click(
      await within(dialog).findByRole('button', { name: 'Удалить департамент' }),
    );

    expect(await screen.findByText('Департамент «Аналитики» удалён.')).toBeInTheDocument();
    expect(api.writes().map((write) => [write.method, write.path])).toEqual([
      ['PUT', `${API_BASE}/${ids.reportsArea}/name`],
      ['DELETE', `${API_BASE}/${ids.reportsArea}`],
    ]);
  });

  it('adds teams and unlinks one after a confirmation', async () => {
    const api = serveOrganizations();
    renderPage({ path: PLATFORM });
    await screen.findByRole('table', { name: 'Команды департамента' });

    await userEvent.click(screen.getByRole('button', { name: 'Добавить команду' }));
    const dialog = await screen.findByRole('dialog', { name: 'Добавление команд в департамент' });
    await userEvent.click(within(dialog).getByRole('combobox', { name: /Команды/ }));
    await userEvent.click(await screen.findByRole('option', { name: 'Аналитика' }));
    await userEvent.click(within(dialog).getByRole('button', { name: 'Добавить' }));
    expect(await screen.findByText('Команда «Аналитика» добавлена.')).toBeInTheDocument();

    const table = await screen.findByRole('table', { name: 'Команды департамента' });
    await waitFor(() => expect(within(table).getByText('Аналитика')).toBeInTheDocument());
    await userEvent.click(within(table).getByRole('button', { name: 'Ещё действия: Аналитика' }));
    await userEvent.click(
      await screen.findByRole('menuitem', { name: 'Отключить от департамента' }),
    );
    const confirm = await screen.findByRole('dialog', {
      name: 'Отключение команды от департамента',
    });
    await userEvent.click(within(confirm).getByRole('button', { name: 'Отключить' }));

    expect(await screen.findByText('Команда отключена от департамента.')).toBeInTheDocument();
    expect(api.writes().map((write) => [write.method, write.path, write.body])).toEqual([
      ['POST', `${API_BASE}/${ids.platformArea}/teams`, { teamIds: [ids.anlTeam] }],
      ['DELETE', `${API_BASE}/${ids.platformArea}/teams/${ids.anlTeam}`, undefined],
    ]);
  });

  it('shows a refusal of the department itself as the alert and reads the page again on close', async () => {
    serveOrganizations();
    renderPage({ path: PLATFORM });
    await screen.findByRole('table', { name: 'Команды департамента' });
    serveProblem('post', `${AREA_URL}/teams`, problem('organizations.area.not_found'), {
      once: true,
    });

    await userEvent.click(screen.getByRole('button', { name: 'Добавить команду' }));
    const dialog = await screen.findByRole('dialog', { name: 'Добавление команд в департамент' });
    await userEvent.click(within(dialog).getByRole('combobox', { name: /Команды/ }));
    await userEvent.click(await screen.findByRole('option', { name: 'Аналитика' }));
    await userEvent.click(within(dialog).getByRole('button', { name: 'Добавить' }));

    expect(await within(dialog).findByText('Область не найдена.')).toBeInTheDocument();
  });

  it('a member reads the page: no command, no link control', async () => {
    serveOrganizations();
    renderPage({ path: PLATFORM, role: 'member' });
    await loaded('Департамент: Платформа');

    expect(screen.queryByRole('button', { name: 'Переименовать' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Добавить команду' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /^Ещё действия:/ })).not.toBeInTheDocument();
  });
});

describe('mod-018 design § 4: a project node’s page', () => {
  it('shows its location, its sub-areas and the project’s teams as the same links', async () => {
    serveOrganizations();
    renderPage({ path: PRM_NODE });
    await loaded('Проект: Пример проекта');

    expect(facts()).toHaveTextContent('ТипПроект');
    expect(facts()).toHaveTextContent('РасположениеПлатформа');
    const children = await screen.findByRole('table', { name: 'Вложенные области' });
    expect(within(children).getByRole('link', { name: 'API' })).toHaveAttribute('href', API);
    expect(
      within(children).getByRole('link', { name: 'Мобильное приложение' }),
    ).toBeInTheDocument();
    const teams = await screen.findByRole('table', { name: 'Команды проекта' });
    expect(within(teams).getByRole('link', { name: 'Платформа' })).toBeInTheDocument();
    expect(within(teams).getByRole('link', { name: 'Пример проекта' })).toBeInTheDocument();
    expect(
      screen.getByText('Это те же связи, что в разделе «Команды» проекта.'),
    ).toBeInTheDocument();
  });

  it('has neither rename nor delete, and opens the project', async () => {
    serveOrganizations();
    renderPage({ path: PRM_NODE });
    await loaded('Проект: Пример проекта');

    expect(await screen.findByRole('button', { name: 'Переместить' })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Переименовать' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /^Удалить/ })).not.toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Открыть проект' })).toHaveAttribute(
      'href',
      `/organizations/${ids.alfa}/projects/${ids.prm}/settings/general`,
    );
  });

  it('creates a sub-area under the node', async () => {
    const api = serveOrganizations();
    renderPage({ path: PRM_NODE });
    await loaded('Проект: Пример проекта');

    await userEvent.click(await screen.findByRole('button', { name: 'Новая вложенная область' }));
    const dialog = await screen.findByRole('dialog', { name: 'Новая вложенная область' });
    await userEvent.type(within(dialog).getByRole('textbox', { name: /Название/ }), 'iOS');
    await userEvent.click(within(dialog).getByRole('button', { name: 'Создать' }));

    expect(await screen.findByText('Область «iOS» создана.')).toBeInTheDocument();
    expect(api.writes()[0]?.body).toEqual({ name: 'iOS', parentId: ids.prmRoot });
  });
});

describe('mod-018 OQ-6 A: a deactivated project’s node', () => {
  it('is read-only: the notice names the project and no command is drawn', async () => {
    serveOrganizations();
    renderPage({ path: OLD_NODE });
    await loaded('Проект: Старый проект');

    expect(
      await screen.findByText(
        /Проект «Старый проект» деактивирован: область доступна только для чтения/,
      ),
    ).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Переместить' })).not.toBeInTheDocument();
    expect(
      screen.queryByRole('button', { name: 'Новая вложенная область' }),
    ).not.toBeInTheDocument();
  });
});

describe('mod-018 design § 4: a sub-area’s page', () => {
  it('names the parent and the project as links', async () => {
    serveOrganizations();
    renderPage({ path: API });
    await loaded('Область: API');

    expect(facts()).toHaveTextContent('ТипОбласть');
    expect(
      within(facts()).getByRole('link', { name: 'Платформа / Пример проекта' }),
    ).toHaveAttribute('href', PRM_NODE);
    expect(await within(facts()).findByRole('link', { name: 'Пример проекта' })).toHaveAttribute(
      'href',
      `/organizations/${ids.alfa}/projects/${ids.prm}/settings/general`,
    );
    expect(facts()).toHaveTextContent('ОписаниеОписание не задано');
    const children = await screen.findByRole('table', { name: 'Вложенные области' });
    expect(within(children).getByRole('link', { name: 'Шлюз' })).toBeInTheDocument();
  });

  it('deletes a leaf and returns to the tree with the notice', async () => {
    const api = serveOrganizations();
    renderPage({ path: `${AREAS}/${ids.mobileArea}` });
    await loaded('Область: Мобильное приложение');

    await userEvent.click(screen.getByRole('button', { name: 'Удалить область' }));
    const dialog = await screen.findByRole('dialog', { name: 'Удаление области' });
    await userEvent.click(await within(dialog).findByRole('button', { name: 'Удалить область' }));

    expect(await screen.findByText('Область «Мобильное приложение» удалена.')).toBeInTheDocument();
    expect(screen.getByTestId('location')).toHaveTextContent(AREAS);
    expect(api.writes().map((write) => write.method)).toEqual(['DELETE']);
  });

  it('keeps the page and shows the alert when the type rules refuse the move', async () => {
    serveOrganizations();
    renderPage({ path: API });
    await loaded('Область: API');
    serveProblem('put', `${AREA_URL}/parent`, problem('organizations.area.outside_project'), {
      once: true,
    });

    await userEvent.click(await screen.findByRole('button', { name: 'Переместить' }));
    const dialog = await screen.findByRole('dialog', { name: 'Перемещение области' });
    await userEvent.click(
      within(dialog).getByRole('combobox', { name: /Новая родительская область/ }),
    );
    await userEvent.click(await screen.findByRole('option'));
    await userEvent.click(within(dialog).getByRole('button', { name: 'Переместить' }));

    expect(
      await within(dialog).findByText('Вложенную область нельзя переместить за пределы её проекта'),
    ).toBeInTheDocument();
  });
});

describe('mod-018: the page of a department is built from the dataset', () => {
  it('lists a project moved into the department', async () => {
    const data = exampleDataset();
    const node = data.areas.find((entry) => entry.id === ids.anlRoot)!;
    node.parentId = ids.reportsArea;
    serveOrganizations(data);
    renderPage({ path: `${AREAS}/${ids.reportsArea}` });
    await loaded('Департамент: Отчёты');

    const projects = await screen.findByRole('table', { name: 'Проекты департамента' });
    expect(within(projects).getByRole('link', { name: 'Аналитика' })).toBeInTheDocument();
  });
});
