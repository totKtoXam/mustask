import { describe, expect, it } from 'vitest';
import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { AREAS_URL, AREA_URL, ids, problem } from '../testFixtures';
import { serveOrganizations, serveProblem } from '../testApi';
import { renderPage } from '../testRender';

const PRM_AREAS = `/organizations/${ids.alfa}/projects/${ids.prm}/settings/areas`;

function row(name: string): HTMLElement {
  return screen
    .getAllByRole('row')
    .find(
      (candidate) =>
        candidate.hasAttribute('aria-level') &&
        within(candidate).getAllByRole('gridcell')[0]!.textContent?.startsWith(name),
    )!;
}

async function open(area: string, action: string, dialogName: string) {
  await screen.findByRole('treegrid', { name: 'Области' });
  if (screen.queryAllByText(area).length === 0) {
    await userEvent.click(screen.getByRole('button', { name: 'Развернуть «Платформа»' }));
  }
  await userEvent.click(within(row(area)).getByRole('button', { name: `Действия: ${area}` }));
  await userEvent.click(await screen.findByRole('menuitem', { name: action }));
  return screen.findByRole('dialog', { name: dialogName });
}

async function choose(dialog: HTMLElement, label: string, option: string) {
  await userEvent.click(within(dialog).getByRole('combobox', { name: label }));
  await userEvent.click(await screen.findByRole('option', { name: option }));
}

describe('mod-002 US-6 Scenario: Created with or without an owner', () => {
  it('preselects «Наследовать от родительской области», lists the active teams after it, and sends the chosen team', async () => {
    const api = serveOrganizations();
    renderPage({ path: PRM_AREAS });
    const dialog = await open('Отчёты', 'Новая дочерняя область', 'Новая область');

    const owner = await within(dialog).findByRole('combobox', { name: 'Владелец' });
    expect(owner).toHaveTextContent('Наследовать от родительской области');
    await userEvent.click(owner);
    expect(
      within(await screen.findByRole('listbox'))
        .getAllByRole('option')
        .map((option) => option.textContent),
    ).toEqual(['Наследовать от родительской области', 'Пример проекта', 'Платформа']);
    await userEvent.click(screen.getByRole('option', { name: 'Платформа' }));
    await userEvent.type(within(dialog).getByRole('textbox', { name: 'Название' }), 'Сводные');
    await userEvent.click(within(dialog).getByRole('button', { name: 'Создать' }));

    await screen.findByText('Область «Сводные» создана.');
    expect(api.writes()[0]?.body).toEqual({
      parentId: ids.reportsArea,
      name: 'Сводные',
      ownerTeamId: ids.platformTeam,
    });
  });
});

describe('mod-002 US-6 Scenario: The owner is a team of the area’s own project', () => {
  it('a team refused as not found shows «Команда не найдена.» in the dialog’s alert', async () => {
    serveOrganizations();
    serveProblem('post', AREAS_URL, problem('organizations.team.not_found'));
    renderPage({ path: PRM_AREAS });
    const dialog = await open('Отчёты', 'Новая дочерняя область', 'Новая область');

    await userEvent.type(within(dialog).getByRole('textbox', { name: 'Название' }), 'Сводные');
    await userEvent.click(within(dialog).getByRole('button', { name: 'Создать' }));

    expect(await within(dialog).findByRole('alert')).toHaveTextContent('Команда не найдена.');
  });
});

describe('mod-002 US-5 Scenario: Sibling names unique', () => {
  it('a create and a rename put «Область с таким названием уже есть на этом уровне» on «Название»', async () => {
    serveOrganizations();
    renderPage({ path: PRM_AREAS });
    const create = await open('Пример проекта', 'Новая дочерняя область', 'Новая область');
    await userEvent.type(within(create).getByRole('textbox', { name: 'Название' }), 'отчёты');
    await userEvent.click(within(create).getByRole('button', { name: 'Создать' }));
    expect(
      await within(create).findByText('Область с таким названием уже есть на этом уровне'),
    ).toBeInTheDocument();
    await userEvent.click(within(create).getByRole('button', { name: 'Отмена' }));
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());

    const rename = await open('Отчёты', 'Переименовать', 'Переименование области');
    const field = within(rename).getByRole('textbox', { name: 'Название' });
    await userEvent.clear(field);
    await userEvent.type(field, 'Платформа');
    await userEvent.click(within(rename).getByRole('button', { name: 'Сохранить' }));
    expect(
      await within(rename).findByText('Область с таким названием уже есть на этом уровне'),
    ).toBeInTheDocument();
  });

  it('checks the length in the browser', async () => {
    const api = serveOrganizations();
    renderPage({ path: PRM_AREAS });
    const dialog = await open('Пример проекта', 'Новая дочерняя область', 'Новая область');

    await userEvent.click(within(dialog).getByRole('button', { name: 'Создать' }));

    expect(
      within(dialog).getByText('Название должно содержать от 1 до 100 символов'),
    ).toBeInTheDocument();
    expect(api.writes()).toEqual([]);
  });
});

describe('mod-002 US-5 Scenario: Renamed', () => {
  it('prefills the name; the same row keeps focus; the notice «Область переименована.»', async () => {
    serveOrganizations();
    renderPage({ path: PRM_AREAS });
    const dialog = await open('Отчёты', 'Переименовать', 'Переименование области');
    const field = within(dialog).getByRole('textbox', { name: 'Название' });
    expect(field).toHaveValue('Отчёты');
    expect(within(dialog).getByRole('button', { name: 'Сохранить' })).toBeDisabled();

    await userEvent.clear(field);
    await userEvent.type(field, 'Отчётность');
    await userEvent.click(within(dialog).getByRole('button', { name: 'Сохранить' }));

    expect(await screen.findByText('Область переименована.')).toBeInTheDocument();
    await waitFor(() => expect(row('Отчётность')).toHaveFocus());
  });
});

describe('mod-002 US-5 Scenario: A free name at the destination; never under itself or its descendant', () => {
  it.each([
    ['organizations.area.name_taken', 'Область с таким названием уже есть на этом уровне'],
    [
      'organizations.area.move_into_own_subtree',
      'Нельзя переместить область внутрь неё самой или её вложенной области',
    ],
  ])('%s lands on «Новая родительская область»', async (code, text) => {
    serveOrganizations();
    serveProblem('put', `${AREA_URL}/parent`, problem(code));
    renderPage({ path: PRM_AREAS });
    const dialog = await open('Отчёты', 'Переместить', 'Перемещение области');

    await choose(dialog, 'Новая родительская область', 'Платформа');
    await userEvent.click(within(dialog).getByRole('button', { name: 'Переместить' }));

    expect(await within(dialog).findByText(text)).toBeInTheDocument();
    expect(within(dialog).queryByRole('alert')).not.toBeInTheDocument();
  });

  it('marks the root among the destinations with «Корневая область» and says «Ничего не найдено» for no match', async () => {
    serveOrganizations();
    renderPage({ path: PRM_AREAS });
    const dialog = await open('API', 'Переместить', 'Перемещение области');

    const input = within(dialog).getByRole('combobox', { name: 'Новая родительская область' });
    await userEvent.click(input);
    const root = await screen.findByRole('option', { name: /Пример проекта/ });
    expect(root).toHaveTextContent('Пример проектаКорневая область');
    await userEvent.type(input, 'нет такой');
    expect(await screen.findByText('Ничего не найдено')).toBeInTheDocument();
  });
});

describe('mod-002 US-6 Scenario: Re-owned; owner cleared', () => {
  it('preselects the explicit owner with «Сейчас: Платформа»; «Сохранить» disabled until changed; «Наследовать…» sends null', async () => {
    const api = serveOrganizations();
    renderPage({ path: PRM_AREAS });
    const dialog = await open('Платформа', 'Изменить владельца', 'Владелец области');

    const owner = await within(dialog).findByRole('combobox', { name: 'Владелец' });
    expect(owner).toHaveTextContent('Платформа');
    expect(within(dialog).getByText('Сейчас: Платформа')).toBeInTheDocument();
    expect(within(dialog).getByRole('button', { name: 'Сохранить' })).toBeDisabled();

    await choose(dialog, 'Владелец', 'Пример проекта');
    await userEvent.click(within(dialog).getByRole('button', { name: 'Сохранить' }));

    expect(await screen.findByText('Владелец области «Платформа» изменён.')).toBeInTheDocument();
    await waitFor(() => expect(row('Платформа')).toHaveFocus());
    expect(api.writes()[0]?.body).toEqual({ ownerTeamId: ids.prmTeam });
  });

  it('an inherited owner reads «Сейчас: Пример проекта, унаследовано», and «Наследовать…» is sent as null', async () => {
    const api = serveOrganizations();
    renderPage({ path: PRM_AREAS });
    const dialog = await open('Мобильное приложение', 'Изменить владельца', 'Владелец области');
    expect(await within(dialog).findByText('Сейчас: Пример проекта')).toBeInTheDocument();

    await choose(dialog, 'Владелец', 'Наследовать от родительской области');
    await userEvent.click(within(dialog).getByRole('button', { name: 'Сохранить' }));

    await screen.findByText('Владелец области «Мобильное приложение» изменён.');
    expect(api.writes()[0]?.body).toEqual({ ownerTeamId: null });
  });

  it('reads an inherited owner’s helper with «, унаследовано»', async () => {
    serveOrganizations();
    renderPage({ path: PRM_AREAS });
    const dialog = await open('Отчёты', 'Изменить владельца', 'Владелец области');

    expect(
      await within(dialog).findByText('Сейчас: Пример проекта, унаследовано'),
    ).toBeInTheDocument();
    expect(within(dialog).getByRole('combobox', { name: 'Владелец' })).toHaveTextContent(
      'Наследовать от родительской области',
    );
  });
});

describe('mod-002 US-6 Scenario: A team’s default area stays with its team', () => {
  it('a re-own of «API» shows «Область используется как область по умолчанию команды «Платформа»» in the alert', async () => {
    serveOrganizations();
    renderPage({ path: PRM_AREAS });
    const dialog = await open('API', 'Изменить владельца', 'Владелец области');

    await choose(dialog, 'Владелец', 'Наследовать от родительской области');
    await userEvent.click(within(dialog).getByRole('button', { name: 'Сохранить' }));

    expect(await within(dialog).findByRole('alert')).toHaveTextContent(
      'Область используется как область по умолчанию команды «Платформа»',
    );
  });

  it('a deletion of «API» shows the same refusal in the confirmation', async () => {
    serveOrganizations();
    serveProblem(
      'delete',
      AREA_URL,
      problem('organizations.area.is_team_default', { team: 'Платформа' }),
    );
    renderPage({ path: PRM_AREAS });
    const dialog = await open('API', 'Удалить', 'Удаление области');

    await userEvent.click(within(dialog).getByRole('button', { name: 'Удалить область' }));

    expect(await within(dialog).findByRole('alert')).toHaveTextContent(
      'Область используется как область по умолчанию команды «Платформа»',
    );
  });
});

describe('mod-002 US-5 Scenario: The root comes with the project — a direct request', () => {
  it('«Корневая область меняется только вместе с проектом» lands in the dialog’s alert', async () => {
    serveOrganizations();
    serveProblem('put', `${AREA_URL}/name`, problem('organizations.area.root_is_fixed'));
    renderPage({ path: PRM_AREAS });
    const dialog = await open('Отчёты', 'Переименовать', 'Переименование области');
    const field = within(dialog).getByRole('textbox', { name: 'Название' });

    await userEvent.type(field, ' 2');
    await userEvent.click(within(dialog).getByRole('button', { name: 'Сохранить' }));

    expect(await within(dialog).findByRole('alert')).toHaveTextContent(
      'Корневая область меняется только вместе с проектом',
    );
  });
});
