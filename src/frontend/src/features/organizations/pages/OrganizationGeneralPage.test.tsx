import { describe, expect, it } from 'vitest';
import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { organizationsKeys } from '../api/queryKeys';
import { ORGANIZATION_URL, ids, problem } from '../testFixtures';
import { serveOrganizations, serveProblem } from '../testApi';
import { contextSource, renderPage } from '../testRender';

const ALFA_GENERAL = `/organizations/${ids.alfa}/settings/general`;

async function facts(name = 'Альфа') {
  await screen.findByText(name);
  return screen.getByText('Название').closest('dl') as HTMLElement;
}

describe('mod-002 US-2 Scenario: The organization settings', () => {
  it('shows «Общие» with the facts «Название», and its two commands once the organization is read', async () => {
    serveOrganizations();
    renderPage({ path: ALFA_GENERAL });

    expect(screen.getByRole('heading', { level: 1, name: 'Общие' })).toBeInTheDocument();
    expect(screen.getByRole('status', { name: 'Загрузка данных' })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Переименовать' })).not.toBeInTheDocument();

    expect(await facts()).toHaveTextContent('НазваниеАльфа');
    expect(screen.getByRole('button', { name: 'Переименовать' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Деактивировать' })).toBeInTheDocument();
  });

  it('fails with «Повторить» under the header, without the entity’s commands', async () => {
    serveOrganizations();
    serveProblem('get', ORGANIZATION_URL, problem('organizations.validation_failed'), {
      once: true,
    });
    renderPage({ path: ALFA_GENERAL });

    expect(await screen.findByRole('button', { name: 'Повторить' })).toBeInTheDocument();
    expect(screen.getByRole('heading', { level: 1, name: 'Общие' })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Переименовать' })).not.toBeInTheDocument();

    await userEvent.click(screen.getByRole('button', { name: 'Повторить' }));
    expect(await facts()).toHaveTextContent('Альфа');
  });
});

describe('mod-002 US-2 Scenario: Renamed; own name in another case', () => {
  it('prefills and selects the name, «Сохранить» disabled until it changes; sends «альфа»; the notice and the new facts', async () => {
    const api = serveOrganizations();
    const { queryClient } = renderPage({ path: ALFA_GENERAL });
    await facts();
    queryClient.setQueryData(organizationsKeys.organizations('active'), []);

    await userEvent.click(screen.getByRole('button', { name: 'Переименовать' }));
    const dialog = await screen.findByRole('dialog', { name: 'Переименование организации' });
    const field = within(dialog).getByRole('textbox', { name: 'Название' }) as HTMLInputElement;
    expect(field).toHaveValue('Альфа');
    expect(field).toHaveFocus();
    expect([field.selectionStart, field.selectionEnd]).toEqual([0, 'Альфа'.length]);
    expect(within(dialog).getByRole('button', { name: 'Сохранить' })).toBeDisabled();

    await userEvent.clear(field);
    await userEvent.type(field, 'альфа');
    await userEvent.click(within(dialog).getByRole('button', { name: 'Сохранить' }));

    expect(await screen.findByText('Организация переименована.')).toBeInTheDocument();
    expect(await facts('альфа')).toHaveTextContent('Названиеальфа');
    expect(api.writes()).toEqual([
      { method: 'PUT', path: `/api/organizations/${ids.alfa}/name`, body: { name: 'альфа' } },
    ]);
    // The switcher's level is invalidated with the page's read.
    await waitFor(() =>
      expect(
        queryClient.getQueryState(organizationsKeys.organizations('active'))?.isInvalidated,
      ).toBe(true),
    );
  });
});

describe('mod-002 US-2 Scenario: A taken or invalid name is refused', () => {
  it('puts «Название «Бета» уже занято» on the field; the length is refused in the browser', async () => {
    serveOrganizations();
    renderPage({ path: ALFA_GENERAL });
    await facts();
    await userEvent.click(screen.getByRole('button', { name: 'Переименовать' }));
    const dialog = await screen.findByRole('dialog', { name: 'Переименование организации' });
    const field = within(dialog).getByRole('textbox', { name: 'Название' });

    await userEvent.clear(field);
    await userEvent.click(within(dialog).getByRole('button', { name: 'Сохранить' }));
    expect(
      within(dialog).getByText('Название должно содержать от 1 до 100 символов'),
    ).toBeInTheDocument();

    await userEvent.type(field, 'бета');
    await userEvent.click(within(dialog).getByRole('button', { name: 'Сохранить' }));
    expect(await within(dialog).findByText('Название «Бета» уже занято')).toBeInTheDocument();
    expect(field).toHaveAttribute('aria-invalid', 'true');
  });
});

describe('mod-002 US-2 Scenario: Deactivated', () => {
  async function openDeactivate(context = contextSource()) {
    const api = serveOrganizations();
    renderPage({ path: ALFA_GENERAL, context });
    await facts();
    await userEvent.click(screen.getByRole('button', { name: 'Деактивировать' }));
    return {
      api,
      dialog: await screen.findByRole('dialog', { name: 'Деактивировать организацию?' }),
    };
  }

  it('asks with the design’s text, a contained primary and focus on «Отмена»', async () => {
    const { dialog } = await openDeactivate();

    expect(dialog).toHaveTextContent(
      'Организация «Альфа» пропадёт из списков и переключателя, а её страницы и страницы её проектов перестанут открываться. Ничего не будет удалено: организацию можно восстановить в списке деактивированных организаций.',
    );
    const confirm = within(dialog).getByRole('button', { name: 'Деактивировать' });
    expect(confirm).toHaveClass('MuiButton-contained', 'MuiButton-colorPrimary');
    expect(within(dialog).getByRole('button', { name: 'Отмена' })).toHaveFocus();
  });

  it('opens the deactivated tab and shows its own notice when the organization is not the switcher’s', async () => {
    const { api, dialog } = await openDeactivate();

    await userEvent.click(within(dialog).getByRole('button', { name: 'Деактивировать' }));

    expect(await screen.findByText('Организация «Альфа» деактивирована.')).toBeInTheDocument();
    await waitFor(() =>
      expect(screen.getByTestId('location')).toHaveTextContent('/organizations/deactivated'),
    );
    expect(api.writes()).toEqual([
      { method: 'POST', path: `/api/organizations/${ids.alfa}/deactivate`, body: undefined },
    ]);
  });

  it('DC-7: raises no notice of its own when the switcher’s organization goes and another is active', async () => {
    const { dialog } = await openDeactivate(
      contextSource({
        organizations: [
          { id: ids.alfa, name: 'Альфа', projects: [], teams: [] },
          { id: ids.beta, name: 'Бета', projects: [], teams: [] },
        ],
        selection: { organizationId: ids.alfa },
      }),
    );

    await userEvent.click(within(dialog).getByRole('button', { name: 'Деактивировать' }));

    await waitFor(() =>
      expect(screen.getByTestId('location')).toHaveTextContent('/organizations/deactivated'),
    );
    expect(
      await screen.findByRole('table', { name: 'Деактивированные организации' }),
    ).toBeInTheDocument();
    expect(screen.queryByText('Организация «Альфа» деактивирована.')).not.toBeInTheDocument();
  });

  it('DC-7: shows its own notice when the switcher’s organization was the last active one', async () => {
    const { dialog } = await openDeactivate(
      contextSource({
        organizations: [{ id: ids.alfa, name: 'Альфа', projects: [], teams: [] }],
        selection: { organizationId: ids.alfa },
      }),
    );

    await userEvent.click(within(dialog).getByRole('button', { name: 'Деактивировать' }));

    expect(await screen.findByText('Организация «Альфа» деактивирована.')).toBeInTheDocument();
  });

  it('«Отмена» changes nothing', async () => {
    const { api, dialog } = await openDeactivate();

    await userEvent.click(within(dialog).getByRole('button', { name: 'Отмена' }));

    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
    expect(api.writes()).toEqual([]);
  });
});

describe('mod-002 US-2 Scenario: A deactivated organization takes no change', () => {
  it('a rename answered not found shows «Организация не найдена.» in the dialog; closing it refetches and the page answers', async () => {
    const api = serveOrganizations();
    renderPage({ path: ALFA_GENERAL });
    await facts();
    api.data.organizations.find((organization) => organization.id === ids.alfa)!.active = false;

    await userEvent.click(screen.getByRole('button', { name: 'Переименовать' }));
    const dialog = await screen.findByRole('dialog', { name: 'Переименование организации' });
    await userEvent.type(within(dialog).getByRole('textbox', { name: 'Название' }), ' 2');
    await userEvent.click(within(dialog).getByRole('button', { name: 'Сохранить' }));

    expect(await within(dialog).findByRole('alert')).toHaveTextContent('Организация не найдена.');
    await userEvent.click(within(dialog).getByRole('button', { name: 'Отмена' }));

    expect(
      await screen.findByRole('heading', { level: 1, name: 'Организация не найдена' }),
    ).toBeInTheDocument();
  });
});

describe('mod-002 US-2 Scenario: An unknown organization answers not found', () => {
  it.each([
    [
      'an unknown id',
      'a0000000-0000-4000-8000-0000000000ff',
      'a0000000-0000-4000-8000-0000000000ff',
    ],
    ['a deactivated organization', ids.gamma, ids.gamma],
    ['`not-a-uuid`', 'not-a-uuid', 'not-a-uuid'],
    ['`a%2Fb`', 'a%2Fb', 'a%2Fb'],
  ])(
    '%s: «Организация не найдена» with no header, the id sent re-encoded',
    async (_case, segment, sent) => {
      const api = serveOrganizations();
      renderPage({ path: `/organizations/${segment}/settings/general` });

      expect(
        await screen.findByRole('heading', { level: 1, name: 'Организация не найдена' }),
      ).toBeInTheDocument();
      expect(screen.getAllByRole('heading', { level: 1 })).toHaveLength(1);
      expect(screen.getByRole('link', { name: 'На главную' })).toHaveAttribute('href', '/home');
      expect(api.requests[0]?.path).toBe(`/api/organizations/${sent}`);
    },
  );
});
