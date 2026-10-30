import { describe, expect, it } from 'vitest';
import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import {
  AREAS_URL,
  AREA_URL,
  emptyTreeDataset,
  exampleDataset,
  ids,
  noAreasDataset,
  problem,
} from '../testFixtures';
import { serveOrganizations, serveProblem } from '../testApi';
import { renderPage } from '../testRender';

const AREAS = `/organizations/${ids.alfa}/settings/areas`;

function treegrid() {
  return screen.findByRole('treegrid', { name: 'Области' });
}

function row(name: string): HTMLElement {
  const found = screen
    .getAllByRole('row')
    .find(
      (candidate) =>
        candidate.hasAttribute('aria-level') &&
        within(candidate).getAllByRole('gridcell')[0]!.textContent?.startsWith(name),
    );
  if (!found) {
    throw new Error(`No row «${name}»`);
  }
  return found;
}

async function choose(name: string, command: string) {
  await userEvent.click(within(row(name)).getByRole('button', { name: `Действия: ${name}` }));
  await userEvent.click(await screen.findByRole('menuitem', { name: command }));
}

describe('mod-018 design § 3: the organization’s area tree (the page)', () => {
  it('loads as a skeleton, then shows the whole tree expanded (D-3), each name a link to the area’s page', async () => {
    serveOrganizations();
    renderPage({ path: AREAS });

    expect(screen.getByRole('status', { name: 'Загрузка данных' })).toBeInTheDocument();
    await treegrid();

    expect(row('Платформа')).toHaveAttribute('aria-expanded', 'true');
    expect(row('Платформа')).toHaveAttribute('aria-level', '1');
    expect(row('Пример проекта')).toHaveAttribute('aria-level', '2');
    expect(row('API')).toHaveAttribute('aria-level', '3');
    expect(row('Шлюз')).toHaveAttribute('aria-level', '4');
    expect(within(row('Платформа')).getByText('Платформенные сервисы')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Шлюз' })).toHaveAttribute(
      'href',
      `/organizations/${ids.alfa}/settings/areas/${ids.gatewayArea}`,
    );
    // Areas of another organization are not drawn.
    expect(screen.queryByText('Отчётность')).not.toBeInTheDocument();
  });

  it('«Свернуть всё» and «Развернуть всё» switch the whole tree', async () => {
    serveOrganizations();
    renderPage({ path: AREAS });
    await treegrid();

    await userEvent.click(screen.getByRole('button', { name: 'Свернуть всё' }));
    expect(screen.queryByText('API')).not.toBeInTheDocument();
    expect(row('Платформа')).toHaveAttribute('aria-expanded', 'false');

    await userEvent.click(screen.getByRole('button', { name: 'Развернуть всё' }));
    expect(row('Шлюз')).toBeInTheDocument();
  });

  it('fails with the error banner and «Повторить»', async () => {
    serveOrganizations();
    serveProblem('get', AREAS_URL, problem('organizations.validation_failed'), { once: true });
    renderPage({ path: AREAS });

    await userEvent.click(await screen.findByRole('button', { name: 'Повторить' }));

    expect(await treegrid()).toBeInTheDocument();
  });

  it('answers «Организация не найдена» for a deactivated organization', async () => {
    serveOrganizations();
    renderPage({ path: `/organizations/${ids.gamma}/settings/areas` });

    expect(
      await screen.findByRole('heading', { level: 1, name: 'Организация не найдена' }),
    ).toBeInTheDocument();
  });
});

describe('mod-018 US-5: no department and no project is a normal state', () => {
  it('says so, with the holder’s hint and «Новый департамент»', async () => {
    serveOrganizations(emptyTreeDataset());
    renderPage({ path: AREAS });

    expect(
      await screen.findByRole('heading', {
        name: 'В организации пока нет департаментов и проектов',
      }),
    ).toBeInTheDocument();
    expect(screen.getAllByRole('button', { name: 'Новый департамент' })).toHaveLength(2);
    expect(screen.queryByRole('button', { name: 'Свернуть всё' })).not.toBeInTheDocument();
  });

  it('tells a reader who creates the areas and offers no button', async () => {
    serveOrganizations(emptyTreeDataset());
    renderPage({ path: AREAS, role: 'viewer' });

    expect(
      await screen.findByText('Области создаёт администратор организации.'),
    ).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Новый департамент' })).not.toBeInTheDocument();
  });

  it('shows the projects at the top level when there is no department yet', async () => {
    serveOrganizations(noAreasDataset());
    renderPage({ path: AREAS });
    await treegrid();

    expect(row('Пример проекта')).toHaveAttribute('aria-level', '1');
    expect(within(row('Пример проекта')).getByText('Проект')).toBeInTheDocument();
  });
});

describe('mod-018 US-1: «Новый департамент»', () => {
  it('creates a department: name only, the row selected, the notice, the tree read again', async () => {
    const api = serveOrganizations();
    renderPage({ path: AREAS });
    await treegrid();

    await userEvent.click(screen.getByRole('button', { name: 'Новый департамент' }));
    const dialog = await screen.findByRole('dialog', { name: 'Новый департамент' });
    expect(within(dialog).getByRole('textbox', { name: /Название/ })).toHaveFocus();
    await userEvent.type(within(dialog).getByRole('textbox', { name: /Название/ }), ' Данные ');
    await userEvent.click(within(dialog).getByRole('button', { name: 'Создать' }));

    expect(await screen.findByText('Департамент «Данные» создан.')).toBeInTheDocument();
    await waitFor(() => expect(row('Данные')).toHaveAttribute('aria-selected', 'true'));
    expect(within(row('Данные')).getByText('Департамент')).toBeInTheDocument();
    expect(api.writes()).toEqual([
      { method: 'POST', path: `/api/organizations/${ids.alfa}/areas`, body: { name: ' Данные ' } },
    ]);
  });

  it('checks the length in the browser and sends nothing', async () => {
    const api = serveOrganizations();
    renderPage({ path: AREAS });
    await treegrid();

    await userEvent.click(screen.getByRole('button', { name: 'Новый департамент' }));
    const dialog = await screen.findByRole('dialog', { name: 'Новый департамент' });
    await userEvent.click(within(dialog).getByRole('button', { name: 'Создать' }));

    expect(
      await within(dialog).findByText('Название должно содержать от 1 до 100 символов'),
    ).toBeInTheDocument();
    expect(api.writes()).toEqual([]);
  });

  it('puts the taken name on «Название»', async () => {
    serveOrganizations();
    renderPage({ path: AREAS });
    await treegrid();

    await userEvent.click(screen.getByRole('button', { name: 'Новый департамент' }));
    const dialog = await screen.findByRole('dialog', { name: 'Новый департамент' });
    await userEvent.type(within(dialog).getByRole('textbox', { name: /Название/ }), 'отчёты');
    await userEvent.click(within(dialog).getByRole('button', { name: 'Создать' }));

    expect(
      await within(dialog).findByText('Область с таким названием уже есть на этом уровне'),
    ).toBeInTheDocument();
  });
});

describe('mod-018 US-1: «Новая вложенная область»', () => {
  it('is offered on a sub-area, and creates under that row', async () => {
    const api = serveOrganizations();
    renderPage({ path: AREAS });
    await treegrid();

    await choose('Мобильное приложение', 'Новая вложенная область');
    const dialog = await screen.findByRole('dialog', { name: 'Новая вложенная область' });
    await userEvent.type(within(dialog).getByRole('textbox', { name: /Название/ }), 'iOS');
    await userEvent.click(within(dialog).getByRole('button', { name: 'Создать' }));

    expect(await screen.findByText('Область «iOS» создана.')).toBeInTheDocument();
    expect(api.writes()[0]?.body).toEqual({ name: 'iOS', parentId: ids.mobileArea });
    await waitFor(() => expect(row('iOS')).toHaveAttribute('aria-level', '4'));
  });

  it('is not offered on a department, which neither moves', async () => {
    serveOrganizations();
    renderPage({ path: AREAS });
    await treegrid();

    await userEvent.click(
      within(row('Платформа')).getByRole('button', { name: 'Действия: Платформа' }),
    );

    expect(await screen.findByRole('menuitem', { name: 'Переименовать' })).toBeInTheDocument();
    expect(screen.queryByRole('menuitem', { name: 'Новая вложенная область' })).toBeNull();
    expect(screen.queryByRole('menuitem', { name: 'Переместить' })).toBeNull();
  });

  it('refuses a deeper level under «Родительская область»', async () => {
    serveOrganizations();
    serveProblem('post', AREAS_URL, problem('organizations.area.depth_exceeded'));
    renderPage({ path: AREAS });
    await treegrid();

    await choose('Шлюз', 'Новая вложенная область');
    const dialog = await screen.findByRole('dialog', { name: 'Новая вложенная область' });
    await userEvent.type(within(dialog).getByRole('textbox', { name: /Название/ }), 'Глубже');
    await userEvent.click(within(dialog).getByRole('button', { name: 'Создать' }));

    expect(
      await within(dialog).findByText('Дерево областей не может быть глубже 10 уровней'),
    ).toBeInTheDocument();
  });
});

describe('mod-018 US-2: rename, description, move', () => {
  it('renames a department from the row: prefilled, the notice, the new name in the tree', async () => {
    const api = serveOrganizations();
    renderPage({ path: AREAS });
    await treegrid();

    await choose('Отчёты', 'Переименовать');
    const dialog = await screen.findByRole('dialog', { name: 'Переименование департамента' });
    const field = within(dialog).getByRole('textbox', { name: /Название/ });
    expect(field).toHaveValue('Отчёты');
    await userEvent.clear(field);
    await userEvent.type(field, 'Аналитики');
    await userEvent.click(within(dialog).getByRole('button', { name: 'Сохранить' }));

    expect(await screen.findByText('Департамент переименован.')).toBeInTheDocument();
    expect(api.writes()[0]).toEqual({
      method: 'PUT',
      path: `/api/organizations/${ids.alfa}/areas/${ids.reportsArea}/name`,
      body: { name: 'Аналитики' },
    });
    await waitFor(() => expect(row('Аналитики')).toBeInTheDocument());
  });

  it('offers no rename and no delete on a project node: it follows its project', async () => {
    serveOrganizations();
    renderPage({ path: AREAS });
    await treegrid();

    await userEvent.click(
      within(row('Пример проекта')).getByRole('button', { name: 'Действия: Пример проекта' }),
    );

    expect(await screen.findByRole('menuitem', { name: 'Переместить' })).toBeInTheDocument();
    expect(screen.queryByRole('menuitem', { name: 'Переименовать' })).toBeNull();
    expect(screen.queryByRole('menuitem', { name: /^Удалить/ })).toBeNull();
  });

  it('describes: the text as typed, and an empty one clears it', async () => {
    const api = serveOrganizations();
    renderPage({ path: AREAS });
    await treegrid();

    await choose('Платформа', 'Изменить описание');
    const dialog = await screen.findByRole('dialog', { name: 'Описание области' });
    const field = within(dialog).getByRole('textbox', { name: 'Описание' });
    expect(field).toHaveValue('Платформенные сервисы');
    await userEvent.clear(field);
    await userEvent.click(within(dialog).getByRole('button', { name: 'Сохранить' }));

    expect(await screen.findByText('Описание сохранено.')).toBeInTheDocument();
    expect(api.writes()[0]?.body).toEqual({ description: null });
  });

  it('moves a project: every place but the current one, the project’s notice', async () => {
    const api = serveOrganizations();
    renderPage({ path: AREAS });
    await treegrid();

    await choose('Пример проекта', 'Переместить');
    const dialog = await screen.findByRole('dialog', { name: 'Перемещение проекта' });
    expect(
      within(dialog).getByText(
        /Проект «Пример проекта» переместится вместе со всеми вложенными областями: 3/,
      ),
    ).toBeInTheDocument();
    await userEvent.click(within(dialog).getByRole('combobox', { name: /Новое расположение/ }));
    const options = within(await screen.findByRole('listbox')).getAllByRole('option');
    expect(options.map((option) => option.textContent)).toEqual(['Организация', 'Отчёты']);
    await userEvent.click(options[1]!);
    await userEvent.click(within(dialog).getByRole('button', { name: 'Переместить' }));

    expect(await screen.findByText('Проект «Пример проекта» перемещён.')).toBeInTheDocument();
    expect(api.writes()[0]).toEqual({
      method: 'PUT',
      path: `/api/organizations/${ids.alfa}/areas/${ids.prmRoot}/parent`,
      body: { parentId: ids.reportsArea },
    });
  });

  it('moves a project to «Организация» with `parentId: null`', async () => {
    const api = serveOrganizations();
    renderPage({ path: AREAS });
    await treegrid();

    await choose('Пример проекта', 'Переместить');
    const dialog = await screen.findByRole('dialog', { name: 'Перемещение проекта' });
    await userEvent.click(within(dialog).getByRole('combobox', { name: /Новое расположение/ }));
    await userEvent.click(await screen.findByRole('option', { name: 'Организация' }));
    await userEvent.click(within(dialog).getByRole('button', { name: 'Переместить' }));

    await screen.findByText('Проект «Пример проекта» перемещён.');
    expect(api.writes()[0]?.body).toEqual({ parentId: null });
    await waitFor(() => expect(row('Пример проекта')).toHaveAttribute('aria-level', '1'));
  });

  it('moves a sub-area inside its project only: the offer is the project’s subtree', async () => {
    const api = serveOrganizations();
    renderPage({ path: AREAS });
    await treegrid();

    await choose('API', 'Переместить');
    const dialog = await screen.findByRole('dialog', { name: 'Перемещение области' });
    await userEvent.click(
      within(dialog).getByRole('combobox', { name: /Новая родительская область/ }),
    );
    const options = within(await screen.findByRole('listbox')).getAllByRole('option');
    expect(options.map((option) => option.textContent)).toEqual([
      'Платформа / Пример проекта / Мобильное приложение',
    ]);
    await userEvent.click(options[0]!);
    await userEvent.click(within(dialog).getByRole('button', { name: 'Переместить' }));

    expect(await screen.findByText('Область «API» перемещена.')).toBeInTheDocument();
    expect(api.writes()[0]?.body).toEqual({ parentId: ids.mobileArea });
  });
});

describe('mod-018 US-6: deleting a department or a sub-area', () => {
  it('lists the teams the department deletion ends and then deletes', async () => {
    const data = exampleDataset();
    data.areaTeams = [{ areaId: ids.reportsArea, targetId: ids.platformTeam }];
    const api = serveOrganizations(data);
    renderPage({ path: AREAS });
    await treegrid();

    await choose('Отчёты', 'Удалить департамент');
    const dialog = await screen.findByRole('dialog', { name: 'Удаление департамента' });
    expect(
      await within(dialog).findByText(
        'Удалить департамент «Отчёты»? Его связи с командами будут удалены, сами команды не изменятся.',
      ),
    ).toBeInTheDocument();
    expect(within(dialog).getByText('Команды (1)')).toBeInTheDocument();
    expect(within(dialog).getByRole('button', { name: 'Отмена' })).toHaveFocus();

    await userEvent.click(within(dialog).getByRole('button', { name: 'Удалить департамент' }));

    expect(await screen.findByText('Департамент «Отчёты» удалён.')).toBeInTheDocument();
    expect(api.writes()[0]).toEqual({
      method: 'DELETE',
      path: `/api/organizations/${ids.alfa}/areas/${ids.reportsArea}`,
      body: undefined,
    });
    await waitFor(() => expect(screen.queryByText('Отчёты')).not.toBeInTheDocument());
  });

  it('asks the plain question of a sub-area', async () => {
    serveOrganizations();
    renderPage({ path: AREAS });
    await treegrid();

    await choose('Мобильное приложение', 'Удалить область');
    const dialog = await screen.findByRole('dialog', { name: 'Удаление области' });

    expect(
      within(dialog).getByText(
        'Удалить область «Мобильное приложение»? Восстановить область будет нельзя.',
      ),
    ).toBeInTheDocument();
  });

  it('says a department with a project is not deleted and offers no confirmation', async () => {
    const api = serveOrganizations();
    renderPage({ path: AREAS });
    await treegrid();

    await choose('Платформа', 'Удалить департамент');
    const dialog = await screen.findByRole('dialog', { name: 'Удаление департамента' });

    expect(
      within(dialog).getByText(
        'Нельзя удалить область, в которой есть проекты, вложенные области или рабочие элементы',
      ),
    ).toBeInTheDocument();
    expect(within(dialog).getByRole('button', { name: 'Удалить департамент' })).toBeDisabled();
    expect(api.requests.some((request) => request.path.endsWith('/deletion'))).toBe(false);
  });

  it('shows a refusal in the dialog, which stays', async () => {
    serveOrganizations();
    serveProblem('delete', AREA_URL, problem('organizations.area.not_empty'));
    renderPage({ path: AREAS });
    const tree = await treegrid();

    await choose('Отчёты', 'Удалить департамент');
    const dialog = await screen.findByRole('dialog', { name: 'Удаление департамента' });
    await userEvent.click(
      await within(dialog).findByRole('button', { name: 'Удалить департамент' }),
    );

    expect(
      await within(dialog).findByText(
        'Нельзя удалить область, в которой есть проекты, вложенные области или рабочие элементы',
      ),
    ).toBeInTheDocument();
    expect(within(tree).getByText('Отчёты')).toBeInTheDocument();
  });
});

describe('mod-018 OQ-6 A: a deactivated project’s node is read-only', () => {
  it('is marked «деактивирован» and has no row menu', async () => {
    serveOrganizations();
    renderPage({ path: AREAS });
    await treegrid();

    expect(within(row('Старый проект')).getByText('деактивирован')).toBeInTheDocument();
    expect(
      within(row('Старый проект')).queryByRole('button', { name: /^Действия:/ }),
    ).not.toBeInTheDocument();
  });
});

describe('mod-018 US-6: the actions follow the served codes', () => {
  it('gives a member the tree and no command at all', async () => {
    serveOrganizations();
    renderPage({ path: AREAS, role: 'member' });
    await treegrid();

    expect(screen.queryByRole('button', { name: 'Новый департамент' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /^Действия:/ })).not.toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Платформа' })).toBeInTheDocument();
  });
});
