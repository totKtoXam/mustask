import { describe, expect, it } from 'vitest';
import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { exampleDataset, ids } from '../testFixtures';
import { serveOrganizations } from '../testApi';
import { renderPage } from '../testRender';

// mod-021 doc-016 design §§ 4.2, 4.3 (US-1, US-2; BR-TD-1, BR-TD-4 team part, BR-TD-5, BR-TD-20): the team page names the
// department the team stands in, and moves the team to another one.

const TEAM_BASE = `/api/organizations/${ids.alfa}/teams`;

function teamPage(teamId: string, segment = 'overview') {
  return `/organizations/${ids.alfa}/teams/${teamId}/${segment}`;
}

async function openMove(teamId: string, role: 'admin' | 'member' = 'admin') {
  renderPage({ path: teamPage(teamId), role });
  await screen.findByRole('heading', { level: 1, name: /^Команда: / });
  await userEvent.click(await screen.findByRole('button', { name: 'Переместить' }));
  return screen.findByRole('dialog', { name: /^Перемещение команды/ });
}

describe('mod-021 design § 4.2: the department on the team’s «Обзор»', () => {
  it('names the department as a link with its colour mark, in place of the retired «Департаменты» section', async () => {
    serveOrganizations();
    renderPage({ path: teamPage(ids.platformTeam) });

    const panel = (await screen.findByText('Участников')).closest('dl') as HTMLElement;
    await waitFor(() => expect(panel).toHaveTextContent('ДепартаментПлатформа'));
    const link = within(panel).getByRole('link', { name: 'Платформа' });
    expect(link).toHaveAttribute(
      'href',
      `/organizations/${ids.alfa}/areas/${ids.platformArea}/overview`,
    );
    expect(within(panel).getByTestId('colour-dot')).toBeInTheDocument();
    expect(screen.queryByRole('table', { name: 'Департаменты команды' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Добавить департамент' })).not.toBeInTheDocument();
  });

  it('offers «Переместить» to the holder of the team-creation code only', async () => {
    serveOrganizations();
    renderPage({ path: teamPage(ids.platformTeam), role: 'member' });
    await screen.findByRole('heading', { level: 1, name: 'Команда: Платформа' });

    expect(screen.queryByRole('button', { name: 'Переместить' })).not.toBeInTheDocument();
  });
});

describe('mod-021 design § 4.2: «Перемещение команды»', () => {
  it('moves a team that has no project: the other departments are offered, one request, the notice, the new place', async () => {
    const data = exampleDataset();
    data.links = data.links.filter((link) => link.teamId !== ids.anlTeam);
    const api = serveOrganizations(data);
    const dialog = await openMove(ids.anlTeam);

    expect(
      within(dialog).getByText('Участники, доступы и спринты остаются на месте.'),
    ).toBeVisible();
    await userEvent.click(within(dialog).getByRole('combobox', { name: /Новый департамент/ }));
    const options = within(await screen.findByRole('listbox')).getAllByRole('option');
    // The department the team stands in is not offered.
    expect(options.map((option) => option.textContent)).toEqual(['Отчёты']);
    await userEvent.click(options[0]!);
    await userEvent.click(within(dialog).getByRole('button', { name: 'Переместить' }));

    expect(await screen.findByText('Команда «Аналитика» перемещена в «Отчёты».')).toBeVisible();
    expect(api.writes()).toEqual([
      {
        method: 'PUT',
        path: `${TEAM_BASE}/${ids.anlTeam}/department`,
        body: { departmentId: ids.reportsArea },
      },
    ]);
    await waitFor(() =>
      expect(screen.getByText('Участников').closest('dl')).toHaveTextContent('ДепартаментОтчёты'),
    );
  });

  it('asks for a department before it sends anything', async () => {
    const api = serveOrganizations();
    const dialog = await openMove(ids.platformTeam);

    await userEvent.click(within(dialog).getByRole('button', { name: 'Переместить' }));

    expect(await within(dialog).findByText('Выберите департамент')).toBeVisible();
    expect(api.writes()).toEqual([]);
  });

  it('says there is no other department, and offers no send', async () => {
    const data = exampleDataset();
    data.areas = data.areas.filter((area) => area.id !== ids.reportsArea);
    serveOrganizations(data);
    const dialog = await openMove(ids.platformTeam);

    expect(
      await within(dialog).findByText(
        'Нет другого департамента, в который вы можете переместить команду.',
      ),
    ).toBeVisible();
    expect(within(dialog).getByRole('button', { name: 'Переместить' })).toBeDisabled();
  });

  it('keeps a team that works on projects where it is: the server’s text, the projects with «Отключить»; after the unlink the move goes through', async () => {
    const api = serveOrganizations();
    const dialog = await openMove(ids.platformTeam);
    await userEvent.click(within(dialog).getByRole('combobox', { name: /Новый департамент/ }));
    await userEvent.click(await screen.findByRole('option', { name: 'Отчёты' }));
    await userEvent.click(within(dialog).getByRole('button', { name: 'Переместить' }));

    expect(
      await within(dialog).findByText(
        'Команда связана с проектами. Отключите её от них перед перемещением.',
      ),
    ).toBeVisible();
    expect(within(dialog).getByText('Проекты:')).toBeVisible();
    // The refusal changes nothing.
    expect(api.writes().map((write) => write.method)).toEqual(['PUT']);

    await userEvent.click(
      within(dialog).getByRole('button', { name: 'Отключить от проекта «Пример проекта»' }),
    );
    const unlink = await screen.findByRole('dialog', { name: 'Отключение команды от проекта' });
    await userEvent.click(await within(unlink).findByRole('button', { name: 'Отключить' }));

    // The move dialog is back with the department kept.
    const again = await screen.findByRole('dialog', { name: /^Перемещение команды/ });
    expect(within(again).getByRole('combobox', { name: /Новый департамент/ })).toHaveValue(
      'Отчёты',
    );
    await userEvent.click(within(again).getByRole('button', { name: 'Переместить' }));

    expect(await screen.findByText('Команда «Платформа» перемещена в «Отчёты».')).toBeVisible();
    expect(api.data.teams.find((team) => team.id === ids.platformTeam)?.departmentId).toBe(
      ids.reportsArea,
    );
  });
});
