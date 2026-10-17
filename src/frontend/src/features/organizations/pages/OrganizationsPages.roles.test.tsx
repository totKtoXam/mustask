import { describe, expect, it } from 'vitest';
import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type { TestRole } from '../../../test/organizationRoles';
import { organizationsKeys } from '../api/queryKeys';
import {
  AREAS_URL,
  ORGANIZATION_URL,
  PROJECTS_URL,
  PROJECT_URL,
  TEAMS_URL,
  ids,
  problem,
} from '../testFixtures';
import { serveOrganizations, serveProblem } from '../testApi';
import { renderPage } from '../testRender';

// mod-016 design § 6.2, § 6.3 (plan-frontend § 3.3): the controls of the organization,
// project, team and area pages follow the served codes of the reader's role. A missing
// code hides the control and whatever it would leave empty — an actions column, a tab
// strip of one view — and the empty states tell the others who holds the right. The
// 403 flow, once per page: the dialog's `Alert`, the typed values kept, the codes read
// again, the primary disabled, «Отмена» working.

const ALFA_GENERAL = `/organizations/${ids.alfa}/settings/general`;
const ALFA_PROJECTS = `/organizations/${ids.alfa}/settings/projects`;
const PRM_GENERAL = `/organizations/${ids.alfa}/projects/${ids.prm}/settings/general`;
const PRM_TEAMS = `/organizations/${ids.alfa}/projects/${ids.prm}/settings/teams`;
const PRM_AREAS = `/organizations/${ids.alfa}/projects/${ids.prm}/settings/areas`;
const ANL_AREAS = `/organizations/${ids.alfa}/projects/${ids.anl}/settings/areas`;
const NEW_ORGANIZATION = 'a0000000-0000-4000-8000-0000000000aa';

const REFUSED = 'Недостаточно прав';

const ROLES = [
  ['admin', true],
  ['member', false],
  ['viewer', false],
] as const;

/** The count of a page's controls: `1` for an administrator, `0` for the others. */
const held = (holds: boolean) => (holds ? 1 : 0);

const button = (name: string) => screen.queryAllByRole('button', { name });

describe.each(ROLES)(
  'mod-016 design § 6.2 Scenario: the organization pages for the %s',
  (role: TestRole, holds) => {
    it('«Общие» of the organization: «Переименовать» and «Деактивировать» only with their codes', async () => {
      serveOrganizations();
      renderPage({ path: ALFA_GENERAL, role });

      await screen.findByText('Альфа');

      expect(button('Переименовать')).toHaveLength(held(holds));
      expect(button('Деактивировать')).toHaveLength(held(holds));
      expect(screen.getByText('Название')).toBeVisible();
    });

    it('«Проекты»: «Создать проект» and the tab «Деактивированные» only with their codes; the strip is not drawn for one view', async () => {
      serveOrganizations();
      renderPage({ path: ALFA_PROJECTS, role });

      const table = await screen.findByRole('table', { name: 'Активные проекты' });

      expect(button('Создать проект')).toHaveLength(held(holds));
      expect(screen.queryAllByRole('tablist')).toHaveLength(held(holds));
      expect(screen.queryAllByRole('tab', { name: 'Деактивированные' })).toHaveLength(held(holds));
      // The list itself reads for everyone, each project a link to its «Общие».
      expect(within(table).getByRole('link', { name: 'Пример проекта' })).toBeVisible();
    });

    it('«Проекты» of an organization with none: the heading stays, the hint is the holder’s or says who creates projects, and the action is the holder’s', async () => {
      const api = serveOrganizations();
      api.data.organizations.push({ id: NEW_ORGANIZATION, name: 'Новая', active: true });
      renderPage({ path: `/organizations/${NEW_ORGANIZATION}/settings/projects`, role });

      expect(
        await screen.findByRole('heading', { level: 2, name: 'Пока нет проектов' }),
      ).toBeVisible();

      expect(
        screen.queryAllByText('Создайте первый проект кнопкой «Создать проект» выше.'),
      ).toHaveLength(held(holds));
      expect(screen.queryAllByText('Проекты создаёт администратор организации.')).toHaveLength(
        held(!holds),
      );
      // The header’s primary and the frame’s action.
      expect(button('Создать проект')).toHaveLength(holds ? 2 : 0);
    });

    it('«Общие» of the project: «Переименовать» and «Деактивировать» only with their codes; the «Процесс» link stays', async () => {
      serveOrganizations();
      renderPage({ path: PRM_GENERAL, role });

      await screen.findByText('Пример проекта');

      expect(button('Переименовать')).toHaveLength(held(holds));
      expect(button('Деактивировать')).toHaveLength(held(holds));
      expect(screen.getByRole('link', { name: /Процесс/ })).toBeVisible();
    });

    it('«Команды»: «Добавить команду» and the actions column only with the code; the team links stay', async () => {
      serveOrganizations();
      renderPage({ path: PRM_TEAMS, role });

      const table = await screen.findByRole('table', { name: 'Команды' });

      expect(button('Добавить команду')).toHaveLength(held(holds));
      expect(within(table).queryAllByRole('button', { name: /^Действия: / })).toHaveLength(
        holds ? within(table).getAllByRole('row').length - 1 : 0,
      );
      expect(button('Команда по умолчанию')).toHaveLength(held(holds));
      expect(within(table).getAllByRole('columnheader')).toHaveLength(holds ? 2 : 1);
      expect(within(table).getByRole('link', { name: 'Платформа' })).toBeVisible();
    });

    it('«Области»: «Новая область», «Новая дочерняя область» and the actions column only with the code; the tree stays a treegrid', async () => {
      serveOrganizations();
      renderPage({ path: PRM_AREAS, role });

      const tree = await screen.findByRole('treegrid', { name: 'Области' });

      expect(button('Новая область')).toHaveLength(held(holds));
      expect(button('Новая дочерняя область')).toHaveLength(held(holds));
      expect(within(tree).queryAllByRole('button', { name: /^Действия: / }).length > 0).toBe(holds);
      expect(within(tree).getAllByRole('row', { selected: false }).length).toBeGreaterThan(1);
    });

    it('«Области» with no nested area: the heading stays, the hint is the holder’s or says who sets the areas, and the action is the holder’s', async () => {
      serveOrganizations();
      renderPage({ path: ANL_AREAS, role });

      expect(
        await screen.findByRole('heading', { level: 2, name: 'Вложенных областей пока нет' }),
      ).toBeVisible();

      expect(
        screen.queryAllByText('Создайте первую область кнопкой «Новая область» выше.'),
      ).toHaveLength(held(holds));
      expect(screen.queryAllByText('Области задаёт администратор организации.')).toHaveLength(
        held(!holds),
      );
      expect(button('Новая область')).toHaveLength(holds ? 2 : 0);
    });
  },
);

describe('mod-016 design § 2.3 Scenario: a refused write, then the codes are read again', () => {
  /** The shape of the flow: the dialog stays with its values and the alert; the re-read locks its primary. */
  async function lockedAfterRefusal(
    dialog: HTMLElement,
    primary: string,
    field: HTMLElement,
    value: string,
    page: { update(next: { role: TestRole }): void },
  ) {
    await userEvent.click(within(dialog).getByRole('button', { name: primary }));

    expect(await within(dialog).findByText(REFUSED)).toBeVisible();
    expect(field).toHaveValue(value);
    expect(within(dialog).getByRole('button', { name: primary })).toBeEnabled();

    page.update({ role: 'member' });

    await waitFor(() =>
      expect(within(dialog).getByRole('button', { name: primary })).toBeDisabled(),
    );
    expect(field).toHaveValue(value);
    await userEvent.click(within(dialog).getByRole('button', { name: 'Отмена' }));
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
  }

  it('renames the organization', async () => {
    serveOrganizations();
    const page = renderPage({ path: ALFA_GENERAL });
    await screen.findByText('Альфа');
    await userEvent.click(screen.getByRole('button', { name: 'Переименовать' }));
    const dialog = await screen.findByRole('dialog', { name: 'Переименование организации' });
    const field = within(dialog).getByRole('textbox', { name: 'Название' });
    await userEvent.clear(field);
    await userEvent.type(field, 'Омега');
    serveProblem('put', `${ORGANIZATION_URL}/name`, problem('organizations.access.forbidden'), {
      once: true,
    });

    await lockedAfterRefusal(dialog, 'Сохранить', field, 'Омега', page);

    expect(button('Переименовать')).toHaveLength(0);
    expect(button('Деактивировать')).toHaveLength(0);
  });

  it('creates a project', async () => {
    serveOrganizations();
    const page = renderPage({ path: ALFA_PROJECTS });
    await screen.findByRole('table', { name: 'Активные проекты' });
    await userEvent.click(screen.getByRole('button', { name: 'Создать проект' }));
    const dialog = await screen.findByRole('dialog', { name: 'Новый проект' });
    const field = within(dialog).getByRole('textbox', { name: 'Название' });
    await userEvent.type(field, 'Омега');
    await userEvent.type(within(dialog).getByRole('textbox', { name: 'Ключ' }), 'OMG');
    serveProblem('post', PROJECTS_URL, problem('organizations.access.forbidden'), { once: true });

    await lockedAfterRefusal(dialog, 'Создать', field, 'Омега', page);

    expect(button('Создать проект')).toHaveLength(0);
    expect(screen.queryByRole('tablist')).not.toBeInTheDocument();
  });

  it('renames the project', async () => {
    serveOrganizations();
    const page = renderPage({ path: PRM_GENERAL });
    await screen.findByText('Пример проекта');
    await userEvent.click(screen.getByRole('button', { name: 'Переименовать' }));
    const dialog = await screen.findByRole('dialog', { name: 'Переименование проекта' });
    const field = within(dialog).getByRole('textbox', { name: 'Название' });
    await userEvent.clear(field);
    await userEvent.type(field, 'Омега');
    serveProblem('put', `${PROJECT_URL}/name`, problem('organizations.access.forbidden'), {
      once: true,
    });

    await lockedAfterRefusal(dialog, 'Сохранить', field, 'Омега', page);

    expect(button('Переименовать')).toHaveLength(0);
    expect(button('Деактивировать')).toHaveLength(0);
  });

  it('adds a team', async () => {
    serveOrganizations();
    const page = renderPage({ path: PRM_TEAMS });
    await screen.findByRole('table', { name: 'Команды' });
    await userEvent.click(screen.getByRole('button', { name: 'Добавить команду' }));
    const dialog = await screen.findByRole('dialog', { name: 'Новая команда' });
    const field = within(dialog).getByRole('textbox', { name: 'Название' });
    await userEvent.type(field, 'Омега');
    serveProblem('post', TEAMS_URL, problem('organizations.access.forbidden'), { once: true });

    await lockedAfterRefusal(dialog, 'Добавить', field, 'Омега', page);

    expect(button('Добавить команду')).toHaveLength(0);
    expect(
      within(screen.getByRole('table', { name: 'Команды' })).queryAllByRole('button'),
    ).toHaveLength(0);
  });

  it('creates an area', async () => {
    serveOrganizations();
    const page = renderPage({ path: PRM_AREAS });
    await screen.findByRole('treegrid', { name: 'Области' });
    await userEvent.click(screen.getByRole('button', { name: 'Новая область' }));
    const dialog = await screen.findByRole('dialog', { name: 'Новая область' });
    const field = within(dialog).getByRole('textbox', { name: 'Название' });
    await userEvent.type(field, 'Омега');
    serveProblem('post', AREAS_URL, problem('organizations.access.forbidden'), { once: true });

    await lockedAfterRefusal(dialog, 'Создать', field, 'Омега', page);

    expect(button('Новая область')).toHaveLength(0);
    expect(
      within(screen.getByRole('treegrid', { name: 'Области' })).queryAllByRole('button', {
        name: /^Действия: /,
      }),
    ).toHaveLength(0);
  });
});

describe('mod-016 plan § 3.3 Scenario: a deactivated organization answers not found from now on', () => {
  it('its served access leaves the tab’s memory once the page has been left for the deactivated list', async () => {
    serveOrganizations();
    const page = renderPage({ path: ALFA_GENERAL });
    await screen.findByText('Альфа');
    page.queryClient.setQueryData(organizationsKeys.access(ids.alfa), {
      kind: 'member',
      role: 'admin',
      permissions: [],
    });
    await userEvent.click(screen.getByRole('button', { name: 'Деактивировать' }));
    const dialog = await screen.findByRole('dialog', { name: 'Деактивировать организацию?' });

    await userEvent.click(within(dialog).getByRole('button', { name: 'Деактивировать' }));

    await waitFor(() =>
      expect(screen.getByTestId('location')).toHaveTextContent('/organizations/deactivated'),
    );
    await waitFor(() =>
      expect(page.queryClient.getQueryData(organizationsKeys.access(ids.alfa))).toBeUndefined(),
    );
  });
});
