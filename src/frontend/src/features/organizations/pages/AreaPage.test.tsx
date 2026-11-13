import { describe, expect, it } from 'vitest';
import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { AREA_URL, exampleDataset, ids, problem, subArea } from '../testFixtures';
import { serveOrganizations, serveProblem } from '../testApi';
import { contextSource, renderPage } from '../testRender';

const AREAS = `/organizations/${ids.alfa}/areas`;
const PLATFORM = `${AREAS}/${ids.platformArea}`;
const PRM_NODE = `${AREAS}/${ids.prmRoot}`;
const OLD_NODE = `${AREAS}/${ids.oldRoot}`;
const API = `${AREAS}/${ids.apiArea}`;
const API_BASE = `/api/organizations/${ids.alfa}/areas`;

async function loaded(title: string) {
  return screen.findByRole('heading', { level: 1, name: title });
}

function facts(): HTMLElement {
  return document.querySelector('dl') as HTMLElement;
}

describe('feat-043 design § 6: a department’s «Обзор»', () => {
  it('shows the description, the place and the two stat links', async () => {
    serveOrganizations();
    renderPage({ path: `${PLATFORM}/overview` });

    expect(screen.getByRole('heading', { level: 1, name: 'Область' })).toBeInTheDocument();
    await loaded('Департамент: Платформа');

    expect(facts()).toHaveTextContent('ОписаниеПлатформенные сервисы');
    expect(facts()).toHaveTextContent('РасположениеВерхний уровень');
    const stats = await waitFor(() => {
      const links = within(facts()).getAllByRole('link');
      expect(links).toHaveLength(2);
      return links;
    });
    expect(stats.map((link) => link.getAttribute('href'))).toEqual([
      `${PLATFORM}/projects`,
      `${PLATFORM}/teams`,
    ]);
    expect(screen.queryByRole('button', { name: 'Переименовать' })).not.toBeInTheDocument();
  });

  it('says what is empty', async () => {
    serveOrganizations();
    renderPage({ path: `${AREAS}/${ids.reportsArea}/overview` });
    await loaded('Департамент: Отчёты');

    expect(facts()).toHaveTextContent('ОписаниеОписание не задано');
  });

  it.each([
    ['an unknown area', `${AREAS}/d0000000-0000-4000-8000-0000000000ff/overview`],
    ['an area of another organization', `${AREAS}/${ids.repRoot}/overview`],
  ])('%s answers «Область не найдена» and keeps the navigation', async (_case, path) => {
    serveOrganizations();
    renderPage({ path });

    expect(
      await screen.findByRole('heading', { level: 1, name: 'Область не найдена' }),
    ).toBeInTheDocument();
    expect(screen.getAllByRole('heading', { level: 1 })).toHaveLength(1);
  });
});

describe('feat-043 design § 6: a department’s «Проекты» and «Команды»', () => {
  it('lists the projects with the key, each name a link to the project’s «Общие» (C-3)', async () => {
    serveOrganizations();
    renderPage({ path: `${PLATFORM}/projects` });
    await loaded('Департамент: Платформа');

    const projects = await screen.findByRole('table', { name: 'Проекты департамента' });
    expect(await within(projects).findByRole('link', { name: 'Пример проекта' })).toHaveAttribute(
      'href',
      `/organizations/${ids.alfa}/projects/${ids.prm}/general`,
    );
  });

  it('says so when it holds no project', async () => {
    serveOrganizations();
    renderPage({ path: `${AREAS}/${ids.reportsArea}/projects` });
    await loaded('Департамент: Отчёты');

    expect(await screen.findByText('В департаменте нет проектов')).toBeInTheDocument();
  });

  it('tags a deactivated project and counts it', async () => {
    const data = exampleDataset();
    const node = data.areas.find((area) => area.id === ids.oldRoot);
    if (node) {
      node.parentId = ids.platformArea;
    }
    serveOrganizations(data);
    renderPage({ path: `${PLATFORM}/projects` });
    await loaded('Департамент: Платформа');

    const projects = await screen.findByRole('table', { name: 'Проекты департамента' });
    // A deactivated project's row is marked and is plain text, no link (C-2; OQ-3 A).
    const row = within(projects).getByText('Старый проект').closest('tr');
    expect(row).toHaveTextContent('деактивирован');
    expect(row).toHaveTextContent('OLD');
    expect(within(projects).queryByRole('link', { name: 'Старый проект' })).not.toBeInTheDocument();
    expect(
      (await within(projects).findByRole('link', { name: 'Пример проекта' })).closest('tr'),
    ).not.toHaveTextContent('деактивирован');
    expect(screen.getAllByText('1 деактивирован').length).toBeGreaterThan(0);
  });

  it('lists a project moved into the department', async () => {
    const data = exampleDataset();
    const node = data.areas.find((entry) => entry.id === ids.anlRoot)!;
    node.parentId = ids.reportsArea;
    serveOrganizations(data);
    renderPage({ path: `${AREAS}/${ids.reportsArea}/projects` });
    await loaded('Департамент: Отчёты');

    const projects = await screen.findByRole('table', { name: 'Проекты департамента' });
    expect(await within(projects).findByRole('link', { name: 'Аналитика' })).toBeInTheDocument();
  });

  it('lists the teams standing in the department, each with its colour mark and a link (BR-TD-1, BR-TD-20)', async () => {
    serveOrganizations();
    renderPage({ path: `${PLATFORM}/teams` });
    const table = await screen.findByRole('table', { name: 'Команды департамента' });

    expect(within(table).getByRole('link', { name: 'Платформа' })).toHaveAttribute(
      'href',
      `/organizations/${ids.alfa}/teams/${ids.platformTeam}/overview`,
    );
    expect(within(table).getAllByTestId('colour-dot').length).toBeGreaterThan(0);
    // No link control, no row menu: a team stands in one department and is moved from its own page.
    expect(screen.queryByRole('button', { name: 'Добавить команды' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /^Ещё действия:/ })).not.toBeInTheDocument();
  });

  it('says a department with no team has none', async () => {
    serveOrganizations();
    renderPage({ path: `${AREAS}/${ids.reportsArea}/teams` });
    await loaded('Департамент: Отчёты');

    expect(await screen.findByText('Команд нет')).toBeInTheDocument();
  });

  it('creates a team from the department: the department is pre-set', async () => {
    const api = serveOrganizations();
    renderPage({ path: `${PLATFORM}/teams` });
    await screen.findByRole('table', { name: 'Команды департамента' });

    await userEvent.click(screen.getByRole('button', { name: 'Создать команду' }));
    const dialog = await screen.findByRole('dialog', { name: 'Новая команда' });
    expect(within(dialog).getByRole('combobox', { name: /Департамент/ })).toHaveValue('Платформа');
    await userEvent.type(within(dialog).getByRole('textbox', { name: /Название/ }), 'Данные');
    await userEvent.click(within(dialog).getByRole('button', { name: 'Создать' }));

    expect(await screen.findByText('Команда «Данные» создана.')).toBeInTheDocument();
    expect(api.writes()[0]?.body).toEqual({ name: 'Данные', departmentId: ids.platformArea });
  });

  it('a member reads the lists: no create control, no row menu', async () => {
    serveOrganizations();
    renderPage({ path: `${PLATFORM}/teams`, role: 'member' });
    await screen.findByRole('table', { name: 'Команды департамента' });

    expect(screen.queryByRole('button', { name: 'Создать команду' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /^Ещё действия:/ })).not.toBeInTheDocument();
  });
});

describe('feat-043 design §§ 8.2, 8.3: placing a project in a department and lifting it out', () => {
  async function openPlace() {
    await loaded('Департамент: Платформа');
    await userEvent.click(await screen.findByRole('button', { name: 'Добавить проект' }));
    return screen.findByRole('dialog', { name: 'Добавление проекта в департамент' });
  }

  it('offers the projects that stand elsewhere with their place, says what moves, and moves one', async () => {
    const data = exampleDataset();
    const reports = data.areas.find((entry) => entry.id === ids.reportsArea);
    expect(reports).toBeDefined();
    data.areas.find((entry) => entry.id === ids.anlRoot)!.parentId = ids.reportsArea;
    const api = serveOrganizations(data);
    renderPage({ path: `${PLATFORM}/projects` });
    const dialog = await openPlace();

    expect(within(dialog).getByRole('button', { name: 'Переместить' })).toBeDisabled();
    await userEvent.click(within(dialog).getByRole('combobox', { name: /Проект/ }));
    const options = await screen.findAllByRole('option');
    expect(options.map((option) => option.textContent)).toEqual(['Аналитика — сейчас: Отчёты']);
    await userEvent.click(options[0]!);
    expect(
      within(dialog).getByText(
        'Проект «Аналитика» переместится из «Отчёты» в «Платформа». Его области, команды и рабочие элементы не изменятся.',
      ),
    ).toBeInTheDocument();
    await userEvent.click(within(dialog).getByRole('button', { name: 'Переместить' }));

    expect(await screen.findByText('Проект «Аналитика» перемещён.')).toBeInTheDocument();
    expect(api.writes().map((write) => [write.method, write.path, write.body])).toEqual([
      ['PUT', `${API_BASE}/${ids.anlRoot}/parent`, { parentId: ids.platformArea }],
    ]);
    const projects = await screen.findByRole('table', { name: 'Проекты департамента' });
    expect(await within(projects).findByRole('link', { name: 'Аналитика' })).toBeInTheDocument();
  });

  it('names the top level for a project that stands there and phrases the move as an entry', async () => {
    serveOrganizations();
    renderPage({ path: `${PLATFORM}/projects` });
    const dialog = await openPlace();

    await userEvent.click(within(dialog).getByRole('combobox', { name: /Проект/ }));
    await userEvent.click(
      await screen.findByRole('option', { name: 'Аналитика — сейчас: верхний уровень' }),
    );
    expect(
      within(dialog).getByText(
        'Проект «Аналитика» войдёт в департамент «Платформа». Его области, команды и рабочие элементы не изменятся.',
      ),
    ).toBeInTheDocument();
  });

  it('shows the same-name refusal under the field and keeps the dialog', async () => {
    serveOrganizations();
    renderPage({ path: `${PLATFORM}/projects` });
    const dialog = await openPlace();
    serveProblem('put', `${AREA_URL}/parent`, problem('organizations.area.name_taken'), {
      once: true,
    });

    await userEvent.click(within(dialog).getByRole('combobox', { name: /Проект/ }));
    await userEvent.click(
      await screen.findByRole('option', { name: 'Аналитика — сейчас: верхний уровень' }),
    );
    await userEvent.click(within(dialog).getByRole('button', { name: 'Переместить' }));

    expect(
      await within(dialog).findByText('Область с таким названием уже есть на этом уровне'),
    ).toBeInTheDocument();
    expect(dialog).toBeInTheDocument();
  });

  it('lifts a project to the top level after a confirmation focused on «Отмена»', async () => {
    const api = serveOrganizations();
    renderPage({ path: `${PLATFORM}/projects` });
    await loaded('Департамент: Платформа');

    const projects = await screen.findByRole('table', { name: 'Проекты департамента' });
    await userEvent.click(
      await within(projects).findByRole('button', { name: 'Ещё действия: Пример проекта' }),
    );
    await userEvent.click(
      await screen.findByRole('menuitem', { name: 'Вынести на верхний уровень' }),
    );
    const dialog = await screen.findByRole('dialog', { name: 'Вынести на верхний уровень' });
    expect(
      within(dialog).getByText(
        'Вынести проект «Пример проекта» на верхний уровень? Команды, области и рабочие элементы проекта не изменятся.',
      ),
    ).toBeInTheDocument();
    expect(within(dialog).getByRole('button', { name: 'Отмена' })).toHaveFocus();
    await userEvent.click(within(dialog).getByRole('button', { name: 'Вынести' }));

    expect(await screen.findByText('Проект «Пример проекта» перемещён.')).toBeInTheDocument();
    expect(api.writes().map((write) => [write.method, write.path, write.body])).toEqual([
      ['PUT', `${API_BASE}/${ids.prmRoot}/parent`, { parentId: null }],
    ]);
  });

  it('a member sees neither «Добавить проект» nor the row menu', async () => {
    serveOrganizations();
    renderPage({ path: `${PLATFORM}/projects`, role: 'member' });
    await screen.findByRole('table', { name: 'Проекты департамента' });

    expect(screen.queryByRole('button', { name: 'Добавить проект' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /^Ещё действия:/ })).not.toBeInTheDocument();
  });
});

describe('feat-043 design § 6: a department’s «Параметры»', () => {
  it('renames and deletes an empty department', async () => {
    const api = serveOrganizations();
    renderPage({ path: `${AREAS}/${ids.reportsArea}/settings` });
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

  it('is empty of commands for a member', async () => {
    serveOrganizations();
    renderPage({ path: `${PLATFORM}/settings`, role: 'member' });
    // mod-019 design § 6, "Direct address": no `area.write`, no «Параметры».
    expect(await screen.findByText('Доступ ограничен')).toBeInTheDocument();

    expect(screen.queryByRole('button', { name: 'Переименовать' })).not.toBeInTheDocument();
  });
});

describe('feat-043 design § 6: a project’s node, «Структура»', () => {
  it('shows its place and sub-areas, and hints at the project’s teams', async () => {
    serveOrganizations();
    renderPage({ path: `${PRM_NODE}/overview` });
    await loaded('Структура: Пример проекта');

    expect(facts()).toHaveTextContent('ТипПроект');
    expect(facts()).toHaveTextContent('РасположениеПлатформа');
    const children = await screen.findByRole('table', { name: 'Вложенные области' });
    expect(within(children).getByRole('link', { name: 'API' })).toHaveAttribute(
      'href',
      `${API}/overview`,
    );
    expect(screen.getByText('Команды проекта — в разделе «Команды».')).toBeInTheDocument();
    expect(screen.queryByRole('table', { name: 'Команды проекта' })).not.toBeInTheDocument();
  });

  it('opens the project and adds a sub-area, with neither rename nor delete', async () => {
    const api = serveOrganizations();
    renderPage({ path: `${PRM_NODE}/overview` });
    await loaded('Структура: Пример проекта');

    expect(screen.getByRole('link', { name: 'Открыть проект' })).toHaveAttribute(
      'href',
      `/organizations/${ids.alfa}/projects/${ids.prm}/general`,
    );
    expect(screen.queryByRole('button', { name: 'Переименовать' })).not.toBeInTheDocument();

    await userEvent.click(await screen.findByRole('button', { name: 'Новая вложенная область' }));
    const dialog = await screen.findByRole('dialog', { name: 'Новая вложенная область' });
    await userEvent.type(within(dialog).getByRole('textbox', { name: /Название/ }), 'iOS');
    await userEvent.click(within(dialog).getByRole('button', { name: 'Создать' }));

    expect(await screen.findByText('Область «iOS» создана.')).toBeInTheDocument();
    expect(api.writes()[0]?.body).toEqual({ name: 'iOS', parentId: ids.prmRoot });
  });

  it('«Параметры» describes and moves, the name follows the project', async () => {
    serveOrganizations();
    renderPage({ path: `${PRM_NODE}/settings` });
    await loaded('Структура: Пример проекта');

    expect(await screen.findByRole('button', { name: 'Изменить описание' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Переместить' })).toBeInTheDocument();
    expect(screen.getByText('Название меняется вместе с проектом.')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Переименовать' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /^Удалить/ })).not.toBeInTheDocument();
  });

  it.each(['projects', 'teams'])('has no «%s» entry: «Страница не найдена»', async (segment) => {
    serveOrganizations();
    renderPage({ path: `${PRM_NODE}/${segment}` });

    expect(
      await screen.findByRole('heading', { level: 1, name: 'Страница не найдена' }),
    ).toBeInTheDocument();
  });
});

describe('feat-043 OQ-3 A, BR-NV-13: a deactivated project’s node', () => {
  it('answers «Проект не найден», no facts and no command (C-2)', async () => {
    serveOrganizations();
    renderPage({ path: `${OLD_NODE}/overview` });

    expect(
      await screen.findByRole('heading', { level: 1, name: 'Проект не найден' }),
    ).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Переместить' })).not.toBeInTheDocument();
    expect(screen.queryByRole('link', { name: 'Открыть проект' })).not.toBeInTheDocument();
    expect(screen.queryByText(/деактивирован: область доступна/)).not.toBeInTheDocument();
  });

  it('answers the same for a sub-area of a deactivated project', async () => {
    const data = exampleDataset();
    data.areas.push(
      subArea('d0000000-0000-4000-8000-0000000000a1', ids.alfa, ids.old, ids.oldRoot, 'Архив'),
    );
    serveOrganizations(data);
    renderPage({ path: `${AREAS}/d0000000-0000-4000-8000-0000000000a1/overview` });

    expect(
      await screen.findByRole('heading', { level: 1, name: 'Проект не найден' }),
    ).toBeInTheDocument();
  });
});

describe('feat-043 design § 6: a sub-area', () => {
  it('names the parent and the project as links and lists its nested areas', async () => {
    serveOrganizations();
    renderPage({ path: `${API}/overview` });
    await loaded('Область: API');

    expect(
      within(facts()).getByRole('link', { name: 'Платформа / Пример проекта' }),
    ).toHaveAttribute('href', `${PRM_NODE}/overview`);
    expect(await within(facts()).findByRole('link', { name: 'Пример проекта' })).toHaveAttribute(
      'href',
      `/organizations/${ids.alfa}/projects/${ids.prm}/general`,
    );
    expect(facts()).toHaveTextContent('ОписаниеОписание не задано');
    const children = await screen.findByRole('table', { name: 'Вложенные области' });
    expect(within(children).getByRole('link', { name: 'Шлюз' })).toBeInTheDocument();
  });

  it('«Параметры» deletes a leaf and returns to the tree with the notice', async () => {
    const api = serveOrganizations();
    renderPage({ path: `${AREAS}/${ids.mobileArea}/settings` });
    await loaded('Область: Мобильное приложение');

    await userEvent.click(await screen.findByRole('button', { name: 'Удалить область' }));
    const dialog = await screen.findByRole('dialog', { name: 'Удаление области' });
    await userEvent.click(await within(dialog).findByRole('button', { name: 'Удалить область' }));

    expect(await screen.findByText('Область «Мобильное приложение» удалена.')).toBeInTheDocument();
    expect(screen.getByTestId('location')).toHaveTextContent(AREAS);
    expect(api.writes().map((write) => write.method)).toEqual(['DELETE']);
  });

  it('keeps the page and shows the alert when the type rules refuse the move', async () => {
    serveOrganizations();
    renderPage({ path: `${API}/settings` });
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

// D-6: the project's nodes and sub-areas are reached through the shell's link, which keeps the
// team that fits; a stub that echoes its node tells which rows ask for it.
describe('feat-043 D-6: links to a node or sub-area keep the fitting team', () => {
  const context = contextSource({
    objectLink: (_kind, id, nodeId) => ({
      to: nodeId
        ? `${AREAS}/${nodeId}/overview?teamId=t1`
        : `/organizations/${ids.alfa}/projects/${id}/general`,
      notice: null,
    }),
  });

  it('routes a sub-area row of a project’s node through it', async () => {
    serveOrganizations();
    renderPage({ path: `${PRM_NODE}/overview`, context });
    await loaded('Структура: Пример проекта');

    const children = await screen.findByRole('table', { name: 'Вложенные области' });
    expect(await within(children).findByRole('link', { name: 'API' })).toHaveAttribute(
      'href',
      `${API}/overview?teamId=t1`,
    );
  });

  it('routes the parent link of a sub-area through it', async () => {
    serveOrganizations();
    renderPage({ path: `${API}/overview`, context });
    await loaded('Область: API');

    expect(
      await within(facts()).findByRole('link', { name: 'Платформа / Пример проекта' }),
    ).toHaveAttribute('href', `${PRM_NODE}/overview?teamId=t1`);
  });
});
