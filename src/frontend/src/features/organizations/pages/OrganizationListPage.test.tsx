import { describe, expect, it } from 'vitest';
import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { ORGANIZATIONS_URL, emptyDataset, ids, problem } from '../testFixtures';
import { serveOrganizations, serveProblem } from '../testApi';
import { CREATE_RIGHT, renderPage } from '../testRender';

const DEACTIVATED_URL = `${ORGANIZATIONS_URL}/deactivated`;

function table(name: string) {
  return screen.findByRole('table', { name });
}

describe('mod-002 design § 5.3: «Организации» loads, fails and retries', () => {
  it('shows the header and tabs at once, the registry skeleton with «Название» while reading', async () => {
    serveOrganizations();
    renderPage({ path: '/organizations' });

    expect(screen.getByRole('heading', { level: 1, name: 'Организации' })).toBeInTheDocument();
    expect(screen.getByRole('tablist', { name: 'Организации по статусу' })).toBeInTheDocument();
    const skeleton = screen.getByRole('status', { name: 'Загрузка данных' });
    expect(within(skeleton).getByText('Название')).toBeInTheDocument();
    expect(await table('Активные организации')).toBeInTheDocument();
  });

  it('fails with the server’s text and «Повторить», which reads again', async () => {
    serveOrganizations();
    serveProblem('get', ORGANIZATIONS_URL, problem('organizations.validation_failed'), {
      once: true,
    });
    renderPage({ path: '/organizations' });

    expect(await screen.findByText('Проверьте правильность заполнения полей.')).toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: 'Повторить' }));

    expect(await table('Активные организации')).toBeInTheDocument();
  });
});

describe('mod-002 US-1 Scenario: Every signed-in user sees every organization', () => {
  it('a session with no code sees both active organizations, each name a link to its «Проекты»', async () => {
    serveOrganizations();
    renderPage({ path: '/organizations' });

    const rows = within(await table('Активные организации')).getAllByRole('link');
    expect(rows.map((link) => link.textContent)).toEqual(['Альфа', 'Бета']);
    expect(rows[0]).toHaveAttribute('href', `/organizations/${ids.alfa}/settings/projects`);
    expect(screen.getByRole('tab', { name: 'Активные' })).toHaveAttribute('aria-selected', 'true');
  });
});

describe('mod-002 US-1 Scenario: Without the right: neither offered nor accepted (the screen)', () => {
  it('shows no «Создать организацию» without the create right', async () => {
    serveOrganizations();
    renderPage({ path: '/organizations' });
    await table('Активные организации');

    expect(screen.queryByRole('button', { name: 'Создать организацию' })).not.toBeInTheDocument();
  });

  it('offers «Создать организацию» as the header’s one primary with the right', async () => {
    serveOrganizations();
    renderPage({ path: '/organizations', permissions: CREATE_RIGHT });
    await table('Активные организации');

    const create = screen.getByRole('button', { name: 'Создать организацию' });
    expect(create).toHaveClass('MuiButton-contained');
    expect(
      screen.getAllByRole('button').filter((b) => b.classList.contains('MuiButton-contained')),
    ).toEqual([create]);
  });
});

describe('mod-002 US-1 Scenario: Granted and revoked in the console (the token renewal’s effect)', () => {
  it('shows, then hides the action as a renewed token gains, then loses the code, with no reload', async () => {
    serveOrganizations();
    const { update } = renderPage({ path: '/organizations' });
    await table('Активные организации');
    expect(screen.queryByRole('button', { name: 'Создать организацию' })).not.toBeInTheDocument();

    update({ permissions: CREATE_RIGHT });
    expect(screen.getByRole('button', { name: 'Создать организацию' })).toBeInTheDocument();

    update({ permissions: [] });
    expect(screen.queryByRole('button', { name: 'Создать организацию' })).not.toBeInTheDocument();
  });
});

describe('mod-002 US-1 Scenario: No organization yet', () => {
  it('the holder of the right sees «Организаций пока нет» as an h2 with «Создать организацию» in the frame', async () => {
    serveOrganizations(emptyDataset());
    renderPage({ path: '/organizations', permissions: CREATE_RIGHT });

    expect(
      await screen.findByRole('heading', { level: 2, name: 'Организаций пока нет' }),
    ).toBeInTheDocument();
    expect(
      screen.getByText('Создайте первую организацию кнопкой «Создать организацию» выше.'),
    ).toBeInTheDocument();
    expect(screen.getAllByRole('button', { name: 'Создать организацию' })).toHaveLength(2);
    expect(screen.queryByRole('table')).not.toBeInTheDocument();
  });

  it('a user without it sees the hint «Право на создание организаций выдаёт администратор» and no action', async () => {
    serveOrganizations(emptyDataset());
    renderPage({ path: '/organizations' });

    expect(
      await screen.findByRole('heading', { level: 2, name: 'Организаций пока нет' }),
    ).toBeInTheDocument();
    expect(
      screen.getByText('Право на создание организаций выдаёт администратор'),
    ).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Создать организацию' })).not.toBeInTheDocument();
  });

  it('the deactivated tab’s empty frame has no action', async () => {
    serveOrganizations(emptyDataset());
    renderPage({ path: '/organizations/deactivated', permissions: CREATE_RIGHT });

    expect(
      await screen.findByRole('heading', { level: 2, name: 'Деактивированных организаций нет' }),
    ).toBeInTheDocument();
    expect(
      screen.getByText(
        'Сюда попадают организации, деактивированные в их настройках. Их можно восстановить.',
      ),
    ).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Создать организацию' })).toBeInTheDocument();
    expect(screen.getAllByRole('button', { name: 'Создать организацию' })).toHaveLength(1);
  });
});

describe('mod-002 US-2 Scenario: Listed separately, no purge', () => {
  it('the deactivated tab lists «Гамма» as plain text with «Восстановить «Гамма»», and nothing deletes', async () => {
    serveOrganizations();
    renderPage({ path: '/organizations/deactivated' });

    const deactivated = await table('Деактивированные организации');
    expect(within(deactivated).getByText('Гамма')).toBeInTheDocument();
    expect(within(deactivated).queryByRole('link')).not.toBeInTheDocument();
    expect(
      within(deactivated).getByRole('button', { name: 'Восстановить «Гамма»' }),
    ).toHaveTextContent('Восстановить');
    expect(screen.queryByRole('button', { name: /Удалить/ })).not.toBeInTheDocument();
    expect(screen.getByRole('tab', { name: 'Деактивированные' })).toHaveAttribute(
      'aria-selected',
      'true',
    );
  });
});

describe('mod-002 US-2 Scenario: Restored exactly as it was', () => {
  it('restores at once: the row leaves, the notice reads «Организация «Гамма» восстановлена.», focus goes to the tab', async () => {
    const api = serveOrganizations();
    renderPage({ path: '/organizations/deactivated' });
    const deactivated = await table('Деактивированные организации');

    await userEvent.click(
      within(deactivated).getByRole('button', { name: 'Восстановить «Гамма»' }),
    );

    expect(await screen.findByText('Организация «Гамма» восстановлена.')).toBeInTheDocument();
    expect(
      await screen.findByRole('heading', { level: 2, name: 'Деактивированных организаций нет' }),
    ).toBeInTheDocument();
    await waitFor(() =>
      expect(screen.getByRole('tab', { name: 'Деактивированные' })).toHaveFocus(),
    );
    expect(api.writes()).toEqual([
      { method: 'POST', path: `/api/organizations/${ids.gamma}/restore`, body: undefined },
    ]);
  });

  it('keeps the other rows usable while one runs, and moves focus to the next row’s «Восстановить»', async () => {
    const api = serveOrganizations();
    api.data.organizations.push(
      { id: 'a0000000-0000-4000-8000-0000000000e1', name: 'Дзета', active: false },
      { id: 'a0000000-0000-4000-8000-0000000000e2', name: 'Эпсилон', active: false },
    );
    renderPage({ path: '/organizations/deactivated' });
    const deactivated = await table('Деактивированные организации');

    await userEvent.click(
      within(deactivated).getByRole('button', { name: 'Восстановить «Гамма»' }),
    );
    expect(within(deactivated).getByRole('button', { name: 'Восстановить «Дзета»' })).toBeEnabled();

    await waitFor(() =>
      expect(screen.getByRole('button', { name: 'Восстановить «Дзета»' })).toHaveFocus(),
    );
    expect(screen.queryByRole('button', { name: 'Восстановить «Гамма»' })).not.toBeInTheDocument();
  });

  it('a refusal keeps the row and its focus and shows the server’s text in the error notice', async () => {
    serveOrganizations();
    serveProblem(
      'post',
      `${ORGANIZATIONS_URL}/:organizationId/restore`,
      problem('organizations.organization.not_found'),
    );
    renderPage({ path: '/organizations/deactivated' });
    const deactivated = await table('Деактивированные организации');
    const restore = within(deactivated).getByRole('button', { name: 'Восстановить «Гамма»' });

    await userEvent.click(restore);

    const alert = await screen.findByRole('alert');
    expect(alert).toHaveTextContent('Организация не найдена.');
    expect(screen.getByRole('button', { name: 'Восстановить «Гамма»' })).toBeInTheDocument();
    await waitFor(() =>
      expect(screen.getByRole('button', { name: 'Восстановить «Гамма»' })).toHaveFocus(),
    );
  });

  it('the restored organization is listed again in the active tab', async () => {
    serveOrganizations();
    renderPage({ path: '/organizations/deactivated' });
    const deactivated = await table('Деактивированные организации');
    await userEvent.click(
      within(deactivated).getByRole('button', { name: 'Восстановить «Гамма»' }),
    );
    await screen.findByText('Организация «Гамма» восстановлена.');

    await userEvent.click(screen.getByRole('tab', { name: 'Активные' }));

    const active = await table('Активные организации');
    expect(
      within(active)
        .getAllByRole('link')
        .map((link) => link.textContent),
    ).toEqual(['Альфа', 'Бета', 'Гамма']);
  });
});

describe('mod-002 US-1 Scenario: An anonymous request is refused (the page sends only its list)', () => {
  it('reads the active list on the active tab and the deactivated list on its own tab', async () => {
    const api = serveOrganizations();
    renderPage({ path: '/organizations' });
    await table('Активные организации');

    expect(api.requests.map((request) => request.path)).toEqual(['/api/organizations']);
    expect(DEACTIVATED_URL).toContain('/deactivated');
  });
});
