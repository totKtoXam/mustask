import { describe, expect, it } from 'vitest';
import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { ids } from '../testFixtures';
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
    expect(
      within(dialog).getByText(
        'От 1 до 100 символов. Так же будут названы команда проекта и его корневая область.',
      ),
    ).toBeInTheDocument();
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
