import { describe, expect, it } from 'vitest';
import { fireEvent, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { exampleDataset, ids, noTeamsDataset } from '../testFixtures';
import { serveOrganizations } from '../testApi';
import { renderPage } from '../testRender';

const ALFA_PROJECTS = `/organizations/${ids.alfa}/settings/projects`;

async function openCreate() {
  const api = serveOrganizations();
  renderPage({ path: ALFA_PROJECTS });
  await screen.findByRole('table', { name: 'Активные проекты' });
  await userEvent.click(screen.getByRole('button', { name: 'Создать проект' }));
  const dialog = await screen.findByRole('dialog', { name: 'Новый проект' });
  return {
    api,
    dialog,
    name: within(dialog).getByRole('textbox', { name: 'Название' }),
    key: within(dialog).getByRole('textbox', { name: 'Ключ' }),
    submit: () => userEvent.click(within(dialog).getByRole('button', { name: 'Создать' })),
  };
}

describe('mod-002 US-3 Scenario: Created ready to use', () => {
  it('shows both helpers; sends name and key; the row appears, the page stays, the notice', async () => {
    const { api, dialog, name, key, submit } = await openCreate();
    expect(name).toHaveFocus();
    expect(within(dialog).getByText('От 1 до 100 символов.')).toBeInTheDocument();
    expect(
      within(dialog).getByText(
        'От 2 до 10 латинских заглавных букв и цифр, начиная с буквы. С ключа начинаются номера рабочих элементов, после создания он не меняется.',
      ),
    ).toBeInTheDocument();
    expect(key).toHaveAttribute('autocomplete', 'off');
    expect(key).toHaveAttribute('spellcheck', 'false');
    expect(key).toHaveAttribute('autocapitalize', 'characters');

    await userEvent.type(name, 'Платформа');
    await userEvent.type(key, 'PLT');
    await submit();

    expect(await screen.findByText('Проект «Платформа» создан.')).toBeInTheDocument();
    const table = await screen.findByRole('table', { name: 'Активные проекты' });
    expect(await within(table).findByRole('link', { name: 'Платформа' })).toBeInTheDocument();
    expect(screen.getByTestId('location')).toHaveTextContent(ALFA_PROJECTS);
    expect(api.writes()).toEqual([
      {
        method: 'POST',
        path: `/api/organizations/${ids.alfa}/projects`,
        body: { name: 'Платформа', key: 'PLT' },
      },
    ]);
  });
});

// feat-028 BR-OL-2 (doc-012 P-1): the helper «…Так же будут названы команда проекта и его корневая
// область.» is retired by name with the project's own team; the new helper names the root alone.

describe('feat-028 US-2 Scenario: A project is created with no team, a description and chosen teams', () => {
  it('draws «Описание» and «Команды», both optional; a project created with neither sends neither and gets no team', async () => {
    const { api, dialog, name, key, submit } = await openCreate();
    expect(within(dialog).getByRole('textbox', { name: 'Описание' })).toHaveValue('');
    expect(within(dialog).getByRole('combobox', { name: 'Команды' })).toBeInTheDocument();

    await userEvent.type(name, 'Платформа');
    await userEvent.type(key, 'PLT');
    await submit();

    expect(await screen.findByText('Проект «Платформа» создан.')).toBeInTheDocument();
    expect(api.writes()[0]?.body).toEqual({ name: 'Платформа', key: 'PLT' });
    const created = api.data.projects.find((project) => project.key === 'PLT')!;
    // BR-OL-2: creating a project creates no team and no link.
    expect(api.data.links.filter((link) => link.projectId === created.id)).toEqual([]);
    expect(api.data.teams).toHaveLength(exampleDataset().teams.length);
  });

  it('sends the description as typed and the chosen teams, and links them in the same operation', async () => {
    const { api, dialog, name, key, submit } = await openCreate();
    await userEvent.type(name, 'Ядро');
    await userEvent.type(key, 'CORE');
    await userEvent.type(within(dialog).getByRole('textbox', { name: 'Описание' }), ' Первая');
    const teams = within(dialog).getByRole('combobox', { name: 'Команды' });
    await userEvent.click(teams);
    await userEvent.click(await screen.findByRole('option', { name: 'Аналитика' }));
    await userEvent.click(screen.getByRole('option', { name: 'Платформа' }));

    expect(within(dialog).getByRole('button', { name: 'Убрать «Аналитика»' })).toBeVisible();
    await userEvent.keyboard('{Escape}');
    await submit();

    expect(await screen.findByText('Проект «Ядро» создан.')).toBeInTheDocument();
    expect(api.writes()[0]?.body).toEqual({
      name: 'Ядро',
      key: 'CORE',
      description: ' Первая',
      teamIds: [ids.anlTeam, ids.platformTeam],
    });
    const created = api.data.projects.find((project) => project.key === 'CORE')!;
    expect(
      api.data.links.filter((link) => link.projectId === created.id).map((link) => link.teamId),
    ).toEqual([ids.anlTeam, ids.platformTeam]);
  });

  it('says «В организации пока нет команд.» in the list of an organization with none — the first state', async () => {
    serveOrganizations(noTeamsDataset());
    renderPage({ path: ALFA_PROJECTS });
    await screen.findByRole('table', { name: 'Активные проекты' });
    await userEvent.click(screen.getByRole('button', { name: 'Создать проект' }));
    const dialog = await screen.findByRole('dialog', { name: 'Новый проект' });

    await userEvent.click(within(dialog).getByRole('combobox', { name: 'Команды' }));

    expect(await screen.findByText('В организации пока нет команд.')).toBeInTheDocument();
  });

  it('refuses a description of 2 001 characters under «Описание» and sends nothing', async () => {
    const { api, dialog, name, key, submit } = await openCreate();
    await userEvent.type(name, 'Платформа');
    await userEvent.type(key, 'PLT');
    fireEvent.change(within(dialog).getByRole('textbox', { name: 'Описание' }), {
      target: { value: 'я'.repeat(2001) },
    });

    await submit();

    expect(
      within(dialog).getByText('Описание должно содержать не более 2000 символов'),
    ).toBeInTheDocument();
    expect(api.writes()).toEqual([]);
  });

  it('a chosen team gone meanwhile lands under «Команды», the list is read again and the choice drops', async () => {
    const { api, dialog, name, key, submit } = await openCreate();
    await userEvent.type(name, 'Ядро');
    await userEvent.type(key, 'CORE');
    await userEvent.click(within(dialog).getByRole('combobox', { name: 'Команды' }));
    await userEvent.click(await screen.findByRole('option', { name: 'Платформа' }));
    await userEvent.keyboard('{Escape}');
    api.data.teams.find((team) => team.id === ids.platformTeam)!.deleted = true;

    await submit();

    expect(await within(dialog).findByText('Команда не найдена.')).toBeInTheDocument();
    await waitFor(() =>
      expect(
        within(dialog).queryByRole('button', { name: 'Убрать «Платформа»' }),
      ).not.toBeInTheDocument(),
    );
    expect(screen.getByRole('dialog', { name: 'Новый проект' })).toBeInTheDocument();
    expect(api.data.projects.find((project) => project.key === 'CORE')).toBeUndefined();
  });
});

describe('mod-002 US-3 Scenario: The key is required and well-formed', () => {
  it('refuses an empty key with «Укажите ключ проекта» and sends nothing', async () => {
    const { api, dialog, name, key, submit } = await openCreate();
    await userEvent.type(name, 'Платформа');

    await submit();

    expect(within(dialog).getByText('Укажите ключ проекта')).toBeInTheDocument();
    expect(key).toHaveAttribute('aria-invalid', 'true');
    expect(api.writes()).toEqual([]);
  });

  it.each([['prm'], ['1PRM'], ['P'], ['PROJECTKEY01'], ['РМК']])(
    'refuses %s with the format text and sends nothing',
    async (value) => {
      const { api, dialog, name, key, submit } = await openCreate();
      await userEvent.type(name, 'Платформа');
      await userEvent.type(key, value);

      await submit();

      expect(
        within(dialog).getByText(
          'Ключ проекта: от 2 до 10 латинских заглавных букв и цифр, начиная с буквы',
        ),
      ).toBeInTheDocument();
      expect(api.writes()).toEqual([]);
    },
  );

  it('refuses the name and the key at once', async () => {
    const { dialog, submit } = await openCreate();

    await submit();

    expect(
      within(dialog).getByText('Название должно содержать от 1 до 100 символов'),
    ).toBeInTheDocument();
    expect(within(dialog).getByText('Укажите ключ проекта')).toBeInTheDocument();
  });
});

describe('mod-002 US-3 Scenario: The key is unique, deactivated included', () => {
  it('puts «Ключ «OLD» уже используется в организации» on «Ключ»', async () => {
    const { dialog, name, key, submit } = await openCreate();
    await userEvent.type(name, 'Новый');
    await userEvent.type(key, 'OLD');

    await submit();

    expect(
      await within(dialog).findByText('Ключ «OLD» уже используется в организации'),
    ).toBeInTheDocument();
    expect(key).toHaveAttribute('aria-invalid', 'true');
    expect(name).toHaveAttribute('aria-invalid', 'false');
  });
});

describe('mod-002 US-3 Scenario: The name is unique in the organization', () => {
  it('puts «Проект с таким названием уже есть в организации» on «Название»', async () => {
    const { dialog, name, key, submit } = await openCreate();
    await userEvent.type(name, 'аналитика');
    await userEvent.type(key, 'NEW');

    await submit();

    expect(
      await within(dialog).findByText('Проект с таким названием уже есть в организации'),
    ).toBeInTheDocument();
    expect(name).toHaveAttribute('aria-invalid', 'true');
  });
});

describe('mod-002 US-3 Scenario: A deactivated project’s name stays reserved', () => {
  it('puts «… он деактивирован» on «Название»', async () => {
    const { dialog, name, key, submit } = await openCreate();
    await userEvent.type(name, 'Старый проект');
    await userEvent.type(key, 'NEW');

    await submit();

    expect(
      await within(dialog).findByText(
        'Проект с таким названием уже есть в организации: он деактивирован',
      ),
    ).toBeInTheDocument();
  });
});
