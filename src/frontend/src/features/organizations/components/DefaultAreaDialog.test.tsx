import { describe, expect, it } from 'vitest';
import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http } from 'msw';
import { server } from '../../../test/mocks/server';
import { AREAS_URL, TEAM_URL, ids, problem } from '../testFixtures';
import { serveOrganizations, serveProblem } from '../testApi';
import { renderPage } from '../testRender';

const PLATFORM_PAGE = `/organizations/${ids.alfa}/projects/${ids.prm}/settings/teams/${ids.platformTeam}`;

async function openDialog() {
  await screen.findByText('Платформа / API');
  await userEvent.click(screen.getByRole('button', { name: 'Изменить область по умолчанию' }));
  return screen.findByRole('dialog', { name: 'Область по умолчанию' });
}

describe('mod-002 US-4 Scenario: Default area among explicit areas', () => {
  it('offers «Платформа» and «Платформа / API» only, in tree order, the current one chosen', async () => {
    serveOrganizations();
    renderPage({ path: PLATFORM_PAGE });
    const dialog = await openDialog();

    expect(dialog).toHaveTextContent(
      'В эту область попадают рабочие элементы, созданные с досок и бэклогов команды «Платформа».',
    );
    const field = await within(dialog).findByRole('combobox', { name: 'Область' });
    expect(field).toHaveTextContent('Платформа / API');
    expect(within(dialog).getByRole('button', { name: 'Сохранить' })).toBeDisabled();

    await userEvent.click(field);
    const options = within(await screen.findByRole('listbox')).getAllByRole('option');
    expect(options.map((option) => option.textContent)).toEqual(['Платформа', 'Платформа / API']);
  });

  it('saves a new choice with the notice «Область по умолчанию изменена.»', async () => {
    const api = serveOrganizations();
    renderPage({ path: PLATFORM_PAGE });
    const dialog = await openDialog();

    await userEvent.click(await within(dialog).findByRole('combobox', { name: 'Область' }));
    await userEvent.click(await screen.findByRole('option', { name: 'Платформа' }));
    await userEvent.click(within(dialog).getByRole('button', { name: 'Сохранить' }));

    expect(await screen.findByText('Область по умолчанию изменена.')).toBeInTheDocument();
    expect(api.writes()).toEqual([
      {
        method: 'PUT',
        path: `/api/organizations/${ids.alfa}/projects/${ids.prm}/teams/${ids.platformTeam}/default-area`,
        body: { areaId: ids.platformArea },
      },
    ]);
    await waitFor(() =>
      expect(screen.getByText('Область по умолчанию').closest('dl')).toHaveTextContent('Платформа'),
    );
  });

  it('shows a skeleton while the tree loads and an alert with «Повторить» when it fails', async () => {
    serveOrganizations();
    serveProblem('get', AREAS_URL, problem('organizations.validation_failed'), { once: true });
    renderPage({ path: PLATFORM_PAGE });
    const dialog = await openDialog();

    const alert = await within(dialog).findByRole('alert');
    expect(alert).toHaveTextContent('Проверьте правильность заполнения полей.');
    await userEvent.click(within(alert).getByRole('button', { name: 'Повторить' }));

    expect(await within(dialog).findByRole('combobox', { name: 'Область' })).toBeInTheDocument();
  });

  it('puts «Область по умолчанию должна принадлежать самой команде» on «Область»', async () => {
    serveOrganizations();
    serveProblem(
      'put',
      `${TEAM_URL}/default-area`,
      problem('organizations.team.default_area_not_owned'),
    );
    renderPage({ path: PLATFORM_PAGE });
    const dialog = await openDialog();

    await userEvent.click(await within(dialog).findByRole('combobox', { name: 'Область' }));
    await userEvent.click(await screen.findByRole('option', { name: 'Платформа' }));
    await userEvent.click(within(dialog).getByRole('button', { name: 'Сохранить' }));

    expect(
      await within(dialog).findByText('Область по умолчанию должна принадлежать самой команде'),
    ).toBeInTheDocument();
    expect(within(dialog).queryByRole('alert')).not.toBeInTheDocument();
  });

  it('the loading skeleton takes the field’s place while the tree is read', async () => {
    serveOrganizations();
    let release: () => void = () => {};
    const released = new Promise<void>((resolve) => {
      release = resolve;
    });
    server.use(
      http.get(AREAS_URL, async () => {
        await released;
        return undefined;
      }),
    );
    renderPage({ path: PLATFORM_PAGE });
    const dialog = await openDialog();

    expect(within(dialog).getByLabelText('Загрузка областей')).toBeInTheDocument();
    expect(within(dialog).queryByRole('combobox')).not.toBeInTheDocument();
    expect(within(dialog).getByRole('button', { name: 'Сохранить' })).toBeDisabled();

    release();
    expect(await within(dialog).findByRole('combobox', { name: 'Область' })).toBeInTheDocument();
  });
});
