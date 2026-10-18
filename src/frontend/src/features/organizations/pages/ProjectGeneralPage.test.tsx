import { describe, expect, it } from 'vitest';
import { fireEvent, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { organizationsKeys } from '../api/queryKeys';
import { exampleDataset, ids } from '../testFixtures';
import { serveOrganizations } from '../testApi';
import { contextSource, renderPage } from '../testRender';

const PRM_GENERAL = `/organizations/${ids.alfa}/projects/${ids.prm}/settings/general`;

async function facts(name = 'Пример проекта') {
  await screen.findByText(name);
  return screen.getByText('Ключ').closest('dl') as HTMLElement;
}

describe('mod-002 US-3 Scenario: The project settings', () => {
  it('shows «Название», «Описание», «Ключ» with its caption and «Типы рабочих элементов», with a four-line skeleton first', async () => {
    serveOrganizations();
    renderPage({ path: PRM_GENERAL });

    expect(screen.getByRole('heading', { level: 1, name: 'Общие' })).toBeInTheDocument();
    const skeleton = screen.getByRole('status', { name: 'Загрузка данных' });
    expect(skeleton.querySelectorAll('.MuiSkeleton-rounded')).toHaveLength(4);

    const panel = await facts();
    expect(panel).toHaveTextContent('НазваниеПример проекта');
    expect(panel).toHaveTextContent('ОписаниеОписание не задано');
    expect(panel).toHaveTextContent('КлючPRMЗадан при создании и не меняется');
    expect(panel).toHaveTextContent(
      'Типы рабочих элементовЗадаются процессом организации: Процесс',
    );
  });
});

describe('mod-002 US-3 Scenario: The «Процесс» link', () => {
  it('links «Процесс» to mod-001’s screen of the organization', async () => {
    serveOrganizations();
    renderPage({ path: PRM_GENERAL });
    await facts();

    expect(screen.getByRole('link', { name: 'Процесс' })).toHaveAttribute(
      'href',
      `/organizations/${ids.alfa}/settings/process`,
    );
  });
});

describe('mod-002 US-3 Scenario: Rename renames the root, keeps the key', () => {
  it('says what follows, sends the name, notices «Проект переименован.» and reads the tree again', async () => {
    const api = serveOrganizations();
    const { queryClient } = renderPage({ path: PRM_GENERAL });
    await facts();
    queryClient.setQueryData(organizationsKeys.areas(ids.alfa, ids.prm), []);

    await userEvent.click(screen.getByRole('button', { name: 'Переименовать' }));
    const dialog = await screen.findByRole('dialog', { name: 'Переименование проекта' });
    expect(dialog).toHaveTextContent(
      'Корневая область проекта получит то же название. Ключ, описание и команды проекта не изменятся.',
    );
    const field = within(dialog).getByRole('textbox', { name: 'Название' });
    await userEvent.clear(field);
    await userEvent.type(field, 'Пример');
    await userEvent.click(within(dialog).getByRole('button', { name: 'Сохранить' }));

    expect(await screen.findByText('Проект переименован.')).toBeInTheDocument();
    expect(await facts('Пример')).toHaveTextContent('КлючPRM');
    expect(api.writes()[0]).toEqual({
      method: 'PUT',
      path: `/api/organizations/${ids.alfa}/projects/${ids.prm}/name`,
      body: { name: 'Пример' },
    });
    await waitFor(() =>
      expect(
        queryClient.getQueryState(organizationsKeys.areas(ids.alfa, ids.prm))?.isInvalidated,
      ).toBe(true),
    );
  });

  it('offers no field or action that edits «Ключ»', async () => {
    serveOrganizations();
    renderPage({ path: PRM_GENERAL });
    await facts();

    expect(screen.queryByRole('textbox')).not.toBeInTheDocument();
    expect(screen.getAllByRole('button').map((button) => button.textContent)).toEqual([
      'Переименовать',
      'Изменить описание',
      'Деактивировать',
    ]);
  });
});

describe('feat-028 US-2 Scenario: A project has a description', () => {
  it('shows the description as typed, line breaks kept, and «Описание не задано» when there is none', async () => {
    const data = exampleDataset();
    data.projects.find((project) => project.id === ids.prm)!.description = 'Первая строка\nВторая';
    serveOrganizations(data);
    renderPage({ path: PRM_GENERAL });

    const panel = await facts();

    expect(within(panel).getByText(/Первая строка/)).toHaveStyle({ whiteSpace: 'pre-wrap' });
    expect(panel).toHaveTextContent('ОписаниеПервая строка');
  });

  it('«Изменить описание»: «Сохранить» waits for a change, the text is sent as typed, the notice reads', async () => {
    const api = serveOrganizations();
    renderPage({ path: PRM_GENERAL });
    await facts();

    await userEvent.click(screen.getByRole('button', { name: 'Изменить описание' }));
    const dialog = await screen.findByRole('dialog', { name: 'Описание проекта' });
    const field = within(dialog).getByRole('textbox', { name: 'Описание' });
    expect(field).toHaveValue('');
    expect(within(dialog).getByRole('button', { name: 'Сохранить' })).toBeDisabled();

    await userEvent.type(field, ' Первая строка');
    await userEvent.click(within(dialog).getByRole('button', { name: 'Сохранить' }));

    expect(await screen.findByText('Описание проекта сохранено.')).toBeInTheDocument();
    expect(api.writes()[0]).toEqual({
      method: 'PUT',
      path: `/api/organizations/${ids.alfa}/projects/${ids.prm}/description`,
      body: { description: ' Первая строка' },
    });
    await waitFor(() =>
      expect(screen.getByText('Ключ').closest('dl')).toHaveTextContent('Описание Первая строка'),
    );
  });

  it('an empty text clears the description: `description: null`', async () => {
    const data = exampleDataset();
    data.projects.find((project) => project.id === ids.prm)!.description = 'Старое';
    const api = serveOrganizations(data);
    renderPage({ path: PRM_GENERAL });
    await facts();

    await userEvent.click(screen.getByRole('button', { name: 'Изменить описание' }));
    const dialog = await screen.findByRole('dialog', { name: 'Описание проекта' });
    await userEvent.clear(within(dialog).getByRole('textbox', { name: 'Описание' }));
    await userEvent.click(within(dialog).getByRole('button', { name: 'Сохранить' }));

    expect(await screen.findByText('Описание проекта сохранено.')).toBeInTheDocument();
    expect(api.writes()[0]?.body).toEqual({ description: null });
  });

  it('refuses 2 001 characters under the field before anything is sent', async () => {
    const api = serveOrganizations();
    renderPage({ path: PRM_GENERAL });
    await facts();

    await userEvent.click(screen.getByRole('button', { name: 'Изменить описание' }));
    const dialog = await screen.findByRole('dialog', { name: 'Описание проекта' });
    fireEvent.change(within(dialog).getByRole('textbox', { name: 'Описание' }), {
      target: { value: 'я'.repeat(2001) },
    });
    await userEvent.click(within(dialog).getByRole('button', { name: 'Сохранить' }));

    expect(
      await within(dialog).findByText('Описание должно содержать не более 2000 символов'),
    ).toBeInTheDocument();
    expect(api.writes()).toEqual([]);
  });

  it('offers neither «Изменить описание» nor the rename to a reader', async () => {
    serveOrganizations();
    renderPage({ path: PRM_GENERAL, role: 'viewer' });
    await facts();

    expect(screen.queryByRole('button', { name: 'Изменить описание' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Переименовать' })).not.toBeInTheDocument();
  });
});

describe('mod-002 US-3 Scenario: The name is unique in the organization (rename)', () => {
  it.each([
    ['аналитика', 'Проект с таким названием уже есть в организации'],
    ['Старый проект', 'Проект с таким названием уже есть в организации: он деактивирован'],
  ])('renaming to %s puts «%s» on «Название»', async (value, text) => {
    serveOrganizations();
    renderPage({ path: PRM_GENERAL });
    await facts();
    await userEvent.click(screen.getByRole('button', { name: 'Переименовать' }));
    const dialog = await screen.findByRole('dialog', { name: 'Переименование проекта' });
    const field = within(dialog).getByRole('textbox', { name: 'Название' });

    await userEvent.clear(field);
    await userEvent.type(field, value);
    await userEvent.click(within(dialog).getByRole('button', { name: 'Сохранить' }));

    expect(await within(dialog).findByText(text)).toBeInTheDocument();
  });
});

describe('mod-002 US-3 Scenario: A project is deactivated', () => {
  it('asks with its name and key, opens the deactivated projects tab and notices it when it was not the switcher’s', async () => {
    const api = serveOrganizations();
    renderPage({ path: PRM_GENERAL });
    await facts();

    await userEvent.click(screen.getByRole('button', { name: 'Деактивировать' }));
    const dialog = await screen.findByRole('dialog', { name: 'Деактивировать проект?' });
    expect(dialog).toHaveTextContent(
      'Проект «Пример проекта» (PRM) пропадёт из списков и переключателя, а его страницы и области перестанут открываться. Ничего не будет удалено, связи с командами сохранятся: проект можно восстановить в списке деактивированных проектов организации.',
    );
    expect(within(dialog).getByRole('button', { name: 'Отмена' })).toHaveFocus();
    await userEvent.click(within(dialog).getByRole('button', { name: 'Деактивировать' }));

    expect(await screen.findByText('Проект «Пример проекта» деактивирован.')).toBeInTheDocument();
    const table = await screen.findByRole('table', { name: 'Деактивированные проекты' });
    expect(within(table).getByText('Пример проекта')).toBeInTheDocument();
    expect(api.writes()).toEqual([
      {
        method: 'POST',
        path: `/api/organizations/${ids.alfa}/projects/${ids.prm}/deactivate`,
        body: undefined,
      },
    ]);
  });

  it('DC-7: no notice of its own when it is the switcher’s project and another is active', async () => {
    serveOrganizations();
    renderPage({
      path: PRM_GENERAL,
      context: contextSource({
        organizations: [
          {
            id: ids.alfa,
            name: 'Альфа',
            projects: [
              { id: ids.anl, name: 'Аналитика' },
              { id: ids.prm, name: 'Пример проекта' },
            ],
            teams: [],
          },
        ],
        selection: { organizationId: ids.alfa, projectId: ids.prm },
      }),
    });
    await facts();

    await userEvent.click(screen.getByRole('button', { name: 'Деактивировать' }));
    const dialog = await screen.findByRole('dialog', { name: 'Деактивировать проект?' });
    await userEvent.click(within(dialog).getByRole('button', { name: 'Деактивировать' }));

    expect(
      await screen.findByRole('table', { name: 'Деактивированные проекты' }),
    ).toBeInTheDocument();
    expect(screen.queryByText('Проект «Пример проекта» деактивирован.')).not.toBeInTheDocument();
  });
});

describe('mod-002 US-3 Scenario: A project id that names no project of the organization', () => {
  it.each([
    ['a project of another organization', ids.alfa, ids.rep, 'Проект не найден'],
    ['a deactivated project', ids.alfa, ids.old, 'Проект не найден'],
    ['an unknown project', ids.alfa, 'b0000000-0000-4000-8000-0000000000ff', 'Проект не найден'],
    ['a deactivated organization', ids.gamma, ids.arc, 'Организация не найдена'],
  ])('%s answers «%s»', async (_case, organizationId, projectId, heading) => {
    serveOrganizations();
    renderPage({ path: `/organizations/${organizationId}/projects/${projectId}/settings/general` });

    expect(await screen.findByRole('heading', { level: 1, name: heading })).toBeInTheDocument();
    expect(screen.getAllByRole('heading', { level: 1 })).toHaveLength(1);
  });
});
