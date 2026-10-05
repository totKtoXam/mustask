import { describe, expect, it } from 'vitest';
import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { AREAS_URL, ids, problem } from '../testFixtures';
import { serveOrganizations, serveProblem } from '../testApi';
import { renderPage } from '../testRender';

const PRM_AREAS = `/organizations/${ids.alfa}/projects/${ids.prm}/settings/areas`;
const ANL_AREAS = `/organizations/${ids.alfa}/projects/${ids.anl}/settings/areas`;

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

function ownerCell(name: string): HTMLElement {
  return within(row(name)).getAllByRole('gridcell')[1]!;
}

async function menuOf(name: string) {
  await userEvent.click(within(row(name)).getByRole('button', { name: `Действия: ${name}` }));
  return screen.findByRole('menu');
}

describe('mod-002 US-5 Scenario: Shown as the tree registry (the page)', () => {
  it('loads with «Область» and «Владелец», then shows the root expanded with its children', async () => {
    serveOrganizations();
    renderPage({ path: PRM_AREAS });

    const skeleton = screen.getByRole('status', { name: 'Загрузка данных' });
    expect(within(skeleton).getByText('Владелец')).toBeInTheDocument();
    await treegrid();

    expect(row('Пример проекта')).toHaveAttribute('aria-expanded', 'true');
    expect(row('Платформа')).toHaveAttribute('aria-expanded', 'false');
    expect(row('Мобильное приложение')).toHaveAttribute('aria-level', '2');
    expect(screen.queryByText('API')).not.toBeInTheDocument();
  });

  it('fails with the error banner and «Повторить»', async () => {
    serveOrganizations();
    serveProblem('get', AREAS_URL, problem('organizations.validation_failed'), { once: true });
    renderPage({ path: PRM_AREAS });

    await userEvent.click(await screen.findByRole('button', { name: 'Повторить' }));

    expect(await treegrid()).toBeInTheDocument();
  });

  it.each([
    [
      'a deactivated project',
      `/organizations/${ids.alfa}/projects/${ids.old}/settings/areas`,
      'Проект не найден',
    ],
    [
      'a deactivated organization',
      `/organizations/${ids.gamma}/projects/${ids.arc}/settings/areas`,
      'Организация не найдена',
    ],
  ])('%s answers «%s»', async (_case, path, heading) => {
    serveOrganizations();
    renderPage({ path });

    expect(await screen.findByRole('heading', { level: 1, name: heading })).toBeInTheDocument();
  });
});

describe('mod-002 US-6 Scenario: Inherits from the nearest owned ancestor', () => {
  it('reads an inherited owner as «Пример проекта унаследовано», an explicit one plainly; defaults are marked', async () => {
    serveOrganizations();
    renderPage({ path: PRM_AREAS });
    await treegrid();
    await userEvent.click(screen.getByRole('button', { name: 'Развернуть «Платформа»' }));
    await userEvent.click(screen.getByRole('button', { name: 'Развернуть «API»' }));

    expect(ownerCell('Отчёты')).toHaveTextContent('Пример проекта унаследовано');
    expect(ownerCell('Шлюз')).toHaveTextContent('Платформа унаследовано');
    expect(ownerCell('Платформа')).toHaveTextContent(/^Платформа$/);
    expect(within(row('API')).getByText('По умолчанию')).toBeInTheDocument();
    expect(within(row('Пример проекта')).getByText('По умолчанию')).toBeInTheDocument();
    expect(within(row('Платформа')).queryByText('По умолчанию')).not.toBeInTheDocument();
  });
});

describe('mod-002 US-5 Scenario: The root comes with the project and follows it; US-6: the root’s owner is fixed', () => {
  it('the root’s menu holds «Новая дочерняя область» only; another area’s every action, «Удалить» in the error colour', async () => {
    serveOrganizations();
    renderPage({ path: PRM_AREAS });
    await treegrid();

    const rootMenu = await menuOf('Пример проекта');
    expect(
      within(rootMenu)
        .getAllByRole('menuitem')
        .map((item) => item.textContent),
    ).toEqual(['Новая дочерняя область']);
    await userEvent.keyboard('{Escape}');

    const menu = await menuOf('Отчёты');
    expect(
      within(menu)
        .getAllByRole('menuitem')
        .map((item) => item.textContent),
    ).toEqual([
      'Новая дочерняя область',
      'Переименовать',
      'Переместить',
      'Изменить владельца',
      'Удалить',
    ]);
  });
});

describe('mod-002 US-5 Scenario: A tree with only the root', () => {
  it('shows the root row, then the frame with «Новая область»', async () => {
    serveOrganizations();
    renderPage({ path: ANL_AREAS });
    await treegrid();

    expect(row('Аналитика')).toBeInTheDocument();
    expect(
      screen.getByRole('heading', { level: 2, name: 'Вложенных областей пока нет' }),
    ).toBeInTheDocument();
    expect(
      screen.getByText('Создайте первую область кнопкой «Новая область» выше.'),
    ).toBeInTheDocument();
    expect(screen.getAllByRole('button', { name: 'Новая область' })).toHaveLength(2);
  });
});

describe('mod-002 US-5 Scenario: A child created under any node', () => {
  it('«Новая дочерняя область» is disabled until a row is selected', async () => {
    serveOrganizations();
    renderPage({ path: PRM_AREAS });
    await treegrid();

    expect(screen.getByRole('button', { name: 'Новая дочерняя область' })).toBeDisabled();
    await userEvent.click(within(row('Платформа')).getAllByText('Платформа')[0]!);
    expect(screen.getByRole('button', { name: 'Новая дочерняя область' })).toBeEnabled();
  });

  it('«Новая область» creates under the root: the row selected and focused, the notice', async () => {
    const api = serveOrganizations();
    renderPage({ path: PRM_AREAS });
    await treegrid();

    await userEvent.click(screen.getByRole('button', { name: 'Новая область' }));
    const dialog = await screen.findByRole('dialog', { name: 'Новая область' });
    expect(within(dialog).getByText('Родительская область: Пример проекта')).toBeInTheDocument();
    await userEvent.type(
      within(dialog).getByRole('textbox', { name: 'Название' }),
      'Аналитика данных',
    );
    await userEvent.click(within(dialog).getByRole('button', { name: 'Создать' }));

    expect(await screen.findByText('Область «Аналитика данных» создана.')).toBeInTheDocument();
    await waitFor(() => expect(row('Аналитика данных')).toHaveFocus());
    expect(row('Аналитика данных')).toHaveAttribute('aria-selected', 'true');
    expect(api.writes()[0]?.body).toEqual({
      parentId: ids.prmRoot,
      name: 'Аналитика данных',
      ownerTeamId: null,
    });
  });

  it('under the selected row: its parent expanded, the new row selected and focused', async () => {
    const api = serveOrganizations();
    renderPage({ path: PRM_AREAS });
    await treegrid();
    await userEvent.click(within(row('Отчёты')).getByText('Отчёты'));

    await userEvent.click(screen.getByRole('button', { name: 'Новая дочерняя область' }));
    const dialog = await screen.findByRole('dialog', { name: 'Новая область' });
    expect(within(dialog).getByText('Родительская область: Отчёты')).toBeInTheDocument();
    await userEvent.type(within(dialog).getByRole('textbox', { name: 'Название' }), 'Квартальные');
    await userEvent.click(within(dialog).getByRole('button', { name: 'Создать' }));

    await waitFor(() => expect(row('Квартальные')).toHaveFocus());
    expect(row('Отчёты')).toHaveAttribute('aria-expanded', 'true');
    expect(row('Квартальные')).toHaveAttribute('aria-level', '3');
    expect(api.writes()[0]?.body).toMatchObject({ parentId: ids.reportsArea });
  });

  it('from a row’s menu', async () => {
    serveOrganizations();
    renderPage({ path: PRM_AREAS });
    await treegrid();

    const menu = await menuOf('Мобильное приложение');
    await userEvent.click(within(menu).getByRole('menuitem', { name: 'Новая дочерняя область' }));

    const dialog = await screen.findByRole('dialog', { name: 'Новая область' });
    expect(
      within(dialog).getByText('Родительская область: Мобильное приложение'),
    ).toBeInTheDocument();
  });
});

describe('mod-002 US-5 Scenario: Moves with its subtree; US-6: a move re-resolves inherited owners', () => {
  it('moves «Отчёты» under «Платформа»: selected under its expanded new parent, the tree read again with its owner', async () => {
    const api = serveOrganizations();
    renderPage({ path: PRM_AREAS });
    await treegrid();
    const menu = await menuOf('Отчёты');
    await userEvent.click(within(menu).getByRole('menuitem', { name: 'Переместить' }));
    const dialog = await screen.findByRole('dialog', { name: 'Перемещение области' });
    expect(dialog).toHaveTextContent(
      'Область «Отчёты» переместится вместе со всеми вложенными областями.',
    );
    expect(within(dialog).getByRole('button', { name: 'Переместить' })).toBeDisabled();

    await userEvent.click(
      within(dialog).getByRole('combobox', { name: 'Новая родительская область' }),
    );
    const options = within(await screen.findByRole('listbox')).getAllByRole('option');
    expect(options.map((option) => option.textContent)).toEqual([
      'Мобильное приложение',
      'Платформа',
      'Платформа / API',
      'Платформа / API / Шлюз',
    ]);
    await userEvent.click(options[1]!);
    await userEvent.click(within(dialog).getByRole('button', { name: 'Переместить' }));

    expect(await screen.findByText('Область «Отчёты» перемещена.')).toBeInTheDocument();
    await waitFor(() => expect(row('Отчёты')).toHaveFocus());
    expect(row('Отчёты')).toHaveAttribute('aria-level', '3');
    expect(row('Платформа')).toHaveAttribute('aria-expanded', 'true');
    expect(ownerCell('Отчёты')).toHaveTextContent('Платформа унаследовано');
    expect(api.writes()).toEqual([
      {
        method: 'PUT',
        path: `/api/organizations/${ids.alfa}/projects/${ids.prm}/areas/${ids.reportsArea}/parent`,
        body: { parentId: ids.platformArea },
      },
    ]);
  });
});

describe('mod-002 US-5 Scenario: Only an empty leaf is deleted', () => {
  it('asks destructively; on success the parent is selected and focused, with the notice', async () => {
    serveOrganizations();
    renderPage({ path: PRM_AREAS });
    await treegrid();

    const menu = await menuOf('Отчёты');
    await userEvent.click(within(menu).getByRole('menuitem', { name: 'Удалить' }));
    const dialog = await screen.findByRole('dialog', { name: 'Удаление области' });
    expect(dialog).toHaveTextContent('Удалить область «Отчёты»? Восстановить её будет нельзя.');
    expect(within(dialog).getByRole('button', { name: 'Удалить область' })).toHaveClass(
      'MuiButton-colorError',
    );
    expect(within(dialog).getByRole('button', { name: 'Отмена' })).toHaveFocus();
    await userEvent.click(within(dialog).getByRole('button', { name: 'Удалить область' }));

    expect(await screen.findByText('Область «Отчёты» удалена.')).toBeInTheDocument();
    await waitFor(() => expect(row('Пример проекта')).toHaveFocus());
    expect(row('Пример проекта')).toHaveAttribute('aria-selected', 'true');
    expect(screen.queryByText('Отчёты')).not.toBeInTheDocument();
  });

  it('refuses a non-empty area with «Нельзя удалить область, …» in the dialog', async () => {
    serveOrganizations();
    renderPage({ path: PRM_AREAS });
    await treegrid();

    const menu = await menuOf('Платформа');
    await userEvent.click(within(menu).getByRole('menuitem', { name: 'Удалить' }));
    const dialog = await screen.findByRole('dialog', { name: 'Удаление области' });
    await userEvent.click(within(dialog).getByRole('button', { name: 'Удалить область' }));

    expect(await within(dialog).findByRole('alert')).toHaveTextContent(
      'Нельзя удалить область, в которой есть рабочие элементы (включая удалённые) или вложенные области',
    );
  });
});
