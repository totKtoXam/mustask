import { describe, expect, it } from 'vitest';
import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { AREA_URL, ids, problem } from '../testFixtures';
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

// mod-017 BR-OT-10, US-6: an area owns nothing, so «Новая область» asks «Название» alone and the
// menu has no owner command. Retired by name (doc-012 P-1): «Created with or without an owner»
// — «preselects «Наследовать от родительской области», lists the active teams after it, and
// sends the chosen team», «The owner is a team of the area’s own project» — «a team refused as
// not found shows «Команда не найдена.» in the dialog’s alert», «Re-owned; owner cleared» (three
// cases) and «A team’s default area stays with its team» (two cases).
describe('mod-017 US-6 Scenario: An area asks for its name alone', () => {
  it('«Новая область» has «Название» and no «Владелец», and sends the parent and the name only', async () => {
    const api = serveOrganizations();
    renderPage({ path: PRM_AREAS });
    const dialog = await open('Отчёты', 'Новая дочерняя область', 'Новая область');

    expect(within(dialog).queryByRole('combobox', { name: 'Владелец' })).not.toBeInTheDocument();
    await userEvent.type(within(dialog).getByRole('textbox', { name: 'Название' }), 'Сводные');
    await userEvent.click(within(dialog).getByRole('button', { name: 'Создать' }));

    await screen.findByText('Область «Сводные» создана.');
    expect(api.writes()[0]?.body).toEqual({ parentId: ids.reportsArea, name: 'Сводные' });
  });

  it('the menu of a node holds «Новая дочерняя область», «Переименовать», «Переместить» and «Удалить» — no owner', async () => {
    serveOrganizations();
    renderPage({ path: PRM_AREAS });
    await screen.findByRole('treegrid', { name: 'Области' });

    await userEvent.click(within(row('Отчёты')).getByRole('button', { name: 'Действия: Отчёты' }));

    expect((await screen.findAllByRole('menuitem')).map((item) => item.textContent)).toEqual([
      'Новая дочерняя область',
      'Переименовать',
      'Переместить',
      'Удалить',
    ]);
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
