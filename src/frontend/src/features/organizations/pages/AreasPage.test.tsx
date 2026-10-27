import { describe, expect, it } from 'vitest';
import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { AREAS_URL, AREA_URL, exampleDataset, ids, noAreasDataset, problem } from '../testFixtures';
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
    expect(row('API')).toHaveAttribute('aria-level', '2');
    expect(row('Шлюз')).toHaveAttribute('aria-level', '3');
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

describe('mod-018 US-5: no area at all is the first state after the upgrade', () => {
  it('says so, with the holder’s hint and «Новая область»', async () => {
    serveOrganizations(noAreasDataset());
    renderPage({ path: AREAS });

    expect(
      await screen.findByRole('heading', { name: 'В организации пока нет областей' }),
    ).toBeInTheDocument();
    expect(
      screen.getByText(
        'Область объединяет проекты и команды организации. Создайте первую кнопкой «Новая область».',
      ),
    ).toBeInTheDocument();
    expect(screen.getAllByRole('button', { name: 'Новая область' })).toHaveLength(2);
    expect(screen.queryByRole('button', { name: 'Свернуть всё' })).not.toBeInTheDocument();
  });

  it('tells a reader who creates the areas and offers no button', async () => {
    serveOrganizations(noAreasDataset());
    renderPage({ path: AREAS, role: 'viewer' });

    expect(
      await screen.findByText('Области создаёт администратор организации.'),
    ).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Новая область' })).not.toBeInTheDocument();
  });
});

describe('mod-018 US-1: «Новая область»', () => {
  it('creates a top-level area: name only, the row selected, the notice, the tree read again', async () => {
    const api = serveOrganizations();
    renderPage({ path: AREAS });
    await treegrid();

    await userEvent.click(screen.getByRole('button', { name: 'Новая область' }));
    const dialog = await screen.findByRole('dialog', { name: 'Новая область' });
    expect(within(dialog).getByRole('textbox', { name: /Название/ })).toHaveFocus();
    await userEvent.type(within(dialog).getByRole('textbox', { name: /Название/ }), ' Данные ');
    await userEvent.click(within(dialog).getByRole('button', { name: 'Создать' }));

    expect(await screen.findByText('Область «Данные» создана.')).toBeInTheDocument();
    await waitFor(() => expect(row('Данные')).toHaveAttribute('aria-selected', 'true'));
    expect(api.writes()).toEqual([
      { method: 'POST', path: `/api/organizations/${ids.alfa}/areas`, body: { name: ' Данные ' } },
    ]);
  });

  it('from a row’s menu the parent is that area', async () => {
    const api = serveOrganizations();
    renderPage({ path: AREAS });
    await treegrid();

    await choose('Мобильное приложение', 'Новая дочерняя область');
    const dialog = await screen.findByRole('dialog', { name: 'Новая область' });
    await userEvent.type(within(dialog).getByRole('textbox', { name: /Название/ }), 'iOS');
    await userEvent.click(within(dialog).getByRole('button', { name: 'Создать' }));

    expect(await screen.findByText('Область «iOS» создана.')).toBeInTheDocument();
    expect(api.writes()[0]?.body).toEqual({ name: 'iOS', parentId: ids.mobileArea });
    await waitFor(() => expect(row('iOS')).toHaveAttribute('aria-level', '2'));
  });

  it('checks the length in the browser and sends nothing', async () => {
    const api = serveOrganizations();
    renderPage({ path: AREAS });
    await treegrid();

    await userEvent.click(screen.getByRole('button', { name: 'Новая область' }));
    const dialog = await screen.findByRole('dialog', { name: 'Новая область' });
    await userEvent.click(within(dialog).getByRole('button', { name: 'Создать' }));

    expect(
      await within(dialog).findByText('Название должно содержать от 1 до 100 символов'),
    ).toBeInTheDocument();
    expect(api.writes()).toEqual([]);
  });

  it('puts «Область с таким названием уже есть на этом уровне» on «Название»', async () => {
    serveOrganizations();
    renderPage({ path: AREAS });
    await treegrid();

    await userEvent.click(screen.getByRole('button', { name: 'Новая область' }));
    const dialog = await screen.findByRole('dialog', { name: 'Новая область' });
    await userEvent.type(within(dialog).getByRole('textbox', { name: /Название/ }), 'отчёты');
    await userEvent.click(within(dialog).getByRole('button', { name: 'Создать' }));

    expect(
      await within(dialog).findByText('Область с таким названием уже есть на этом уровне'),
    ).toBeInTheDocument();
  });

  it('refuses a ninth level under «Родительская область»', async () => {
    serveOrganizations();
    serveProblem('post', AREAS_URL, problem('organizations.area.depth_exceeded'));
    renderPage({ path: AREAS });
    await treegrid();

    await choose('Шлюз', 'Новая дочерняя область');
    const dialog = await screen.findByRole('dialog', { name: 'Новая область' });
    await userEvent.type(within(dialog).getByRole('textbox', { name: /Название/ }), 'Глубже');
    await userEvent.click(within(dialog).getByRole('button', { name: 'Создать' }));

    expect(
      await within(dialog).findByText('Дерево областей не может быть глубже 10 уровней'),
    ).toBeInTheDocument();
  });
});

describe('mod-018 US-2: rename, description, move', () => {
  it('renames from the row: prefilled, the notice, the new name in the tree', async () => {
    const api = serveOrganizations();
    renderPage({ path: AREAS });
    await treegrid();

    await choose('Отчёты', 'Переименовать');
    const dialog = await screen.findByRole('dialog', { name: 'Переименование области' });
    const field = within(dialog).getByRole('textbox', { name: /Название/ });
    expect(field).toHaveValue('Отчёты');
    await userEvent.clear(field);
    await userEvent.type(field, 'Аналитика');
    await userEvent.click(within(dialog).getByRole('button', { name: 'Сохранить' }));

    expect(await screen.findByText('Область переименована.')).toBeInTheDocument();
    expect(api.writes()[0]).toEqual({
      method: 'PUT',
      path: `/api/organizations/${ids.alfa}/areas/${ids.reportsArea}/name`,
      body: { name: 'Аналитика' },
    });
    await waitFor(() => expect(row('Аналитика')).toBeInTheDocument());
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

    expect(await screen.findByText('Описание области сохранено.')).toBeInTheDocument();
    expect(api.writes()[0]?.body).toEqual({ description: null });
    await waitFor(() =>
      expect(within(row('Платформа')).queryByText('Платформенные сервисы')).not.toBeInTheDocument(),
    );
  });

  it('moves with its subtree: the destinations leave out the area, its subtree and its parent', async () => {
    const api = serveOrganizations();
    renderPage({ path: AREAS });
    await treegrid();

    await choose('API', 'Переместить');
    const dialog = await screen.findByRole('dialog', { name: 'Перемещение области' });
    expect(
      within(dialog).getByText(
        'Область «API» переместится вместе со всеми вложенными областями: 1.',
      ),
    ).toBeInTheDocument();
    const picker = within(dialog).getByRole('combobox', { name: /Новая родительская область/ });
    await userEvent.click(picker);
    const options = within(await screen.findByRole('listbox')).getAllByRole('option');
    expect(options.map((option) => option.textContent)).toEqual([
      'Верхний уровень',
      'Мобильное приложение',
      'Отчёты',
    ]);
    await userEvent.click(options[1]!);
    await userEvent.click(within(dialog).getByRole('button', { name: 'Переместить' }));

    expect(await screen.findByText('Область «API» перемещена.')).toBeInTheDocument();
    expect(api.writes()[0]).toEqual({
      method: 'PUT',
      path: `/api/organizations/${ids.alfa}/areas/${ids.apiArea}/parent`,
      body: { parentId: ids.mobileArea },
    });
    await waitFor(() => expect(row('API')).toHaveAttribute('aria-level', '2'));
  });

  it('moves to the top level with `parentId: null`', async () => {
    const api = serveOrganizations();
    renderPage({ path: AREAS });
    await treegrid();

    await choose('Шлюз', 'Переместить');
    const dialog = await screen.findByRole('dialog', { name: 'Перемещение области' });
    await userEvent.click(
      within(dialog).getByRole('combobox', { name: /Новая родительская область/ }),
    );
    await userEvent.click(await screen.findByRole('option', { name: 'Верхний уровень' }));
    await userEvent.click(within(dialog).getByRole('button', { name: 'Переместить' }));

    expect(await screen.findByText('Область «Шлюз» перемещена.')).toBeInTheDocument();
    expect(api.writes()[0]?.body).toEqual({ parentId: null });
  });
});

describe('mod-018 US-6: «Удаление области» names every link', () => {
  it('lists the teams and the projects the deletion ends, a deactivated project tagged', async () => {
    const data = exampleDataset();
    data.areaProjects = [
      { areaId: ids.reportsArea, targetId: ids.prm },
      { areaId: ids.reportsArea, targetId: ids.old },
    ];
    data.areaTeams = [{ areaId: ids.reportsArea, targetId: ids.platformTeam }];
    const api = serveOrganizations(data);
    renderPage({ path: AREAS });
    await treegrid();

    await choose('Отчёты', 'Удалить область');
    const dialog = await screen.findByRole('dialog', { name: 'Удаление области' });
    expect(
      await within(dialog).findByText(
        'Удалить область «Отчёты»? Её связи будут удалены, сами проекты и команды не изменятся.',
      ),
    ).toBeInTheDocument();
    expect(within(dialog).getByText('Команды (1)')).toBeInTheDocument();
    expect(within(dialog).getByText('Проекты (2)')).toBeInTheDocument();
    expect(within(dialog).getByText('Платформа')).toBeInTheDocument();
    expect(within(dialog).getByText('деактивирован')).toBeInTheDocument();
    expect(within(dialog).getByRole('button', { name: 'Отмена' })).toHaveFocus();

    await userEvent.click(within(dialog).getByRole('button', { name: 'Удалить область' }));

    expect(await screen.findByText('Область «Отчёты» удалена.')).toBeInTheDocument();
    expect(api.writes()[0]).toEqual({
      method: 'DELETE',
      path: `/api/organizations/${ids.alfa}/areas/${ids.reportsArea}`,
      body: undefined,
    });
    await waitFor(() => expect(screen.queryByText('Отчёты')).not.toBeInTheDocument());
  });

  it('asks the plain question of an area with no link', async () => {
    serveOrganizations();
    renderPage({ path: AREAS });
    await treegrid();

    await choose('Мобильное приложение', 'Удалить область');
    const dialog = await screen.findByRole('dialog', { name: 'Удаление области' });

    expect(
      await within(dialog).findByText(
        'Удалить область «Мобильное приложение»? Восстановить область будет нельзя.',
      ),
    ).toBeInTheDocument();
  });

  it('says an area with children is not deleted and offers no confirmation', async () => {
    const api = serveOrganizations();
    renderPage({ path: AREAS });
    await treegrid();

    await choose('Платформа', 'Удалить область');
    const dialog = await screen.findByRole('dialog', { name: 'Удаление области' });

    expect(
      within(dialog).getByText('Нельзя удалить область, в которой есть вложенные области'),
    ).toBeInTheDocument();
    expect(within(dialog).getByRole('button', { name: 'Удалить область' })).toBeDisabled();
    expect(api.requests.some((request) => request.path.endsWith('/deletion'))).toBe(false);
  });

  it('shows a refusal in the dialog, which stays', async () => {
    serveOrganizations();
    serveProblem('delete', AREA_URL, problem('organizations.area.not_empty'));
    renderPage({ path: AREAS });
    const tree = await treegrid();

    await choose('Отчёты', 'Удалить область');
    const dialog = await screen.findByRole('dialog', { name: 'Удаление области' });
    await userEvent.click(await within(dialog).findByRole('button', { name: 'Удалить область' }));

    expect(
      await within(dialog).findByText('Нельзя удалить область, в которой есть вложенные области'),
    ).toBeInTheDocument();
    expect(within(tree).getByText('Отчёты')).toBeInTheDocument();
  });
});

describe('mod-018 US-6: the actions follow the served codes', () => {
  it('gives a member the tree and no command at all', async () => {
    serveOrganizations();
    renderPage({ path: AREAS, role: 'member' });
    await treegrid();

    expect(screen.queryByRole('button', { name: 'Новая область' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /^Действия:/ })).not.toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Платформа' })).toBeInTheDocument();
  });
});
