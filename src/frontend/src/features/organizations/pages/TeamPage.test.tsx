import { describe, expect, it } from 'vitest';
import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { TEAM_URL, exampleDataset, ids, problem } from '../testFixtures';
import { serveOrganizations, serveProblem } from '../testApi';
import { renderPage } from '../testRender';

function teamPage(teamId: string, projectId: string = ids.prm, organizationId: string = ids.alfa) {
  return `/organizations/${organizationId}/projects/${projectId}/settings/teams/${teamId}`;
}

async function facts() {
  await screen.findByText('Название');
  return screen.getByText('Название').closest('dl') as HTMLElement;
}

describe('mod-002 US-4: a team’s page (design § 10)', () => {
  it('loads under «Команда» with a one-line skeleton, then reads «Команда: Платформа» with its facts', async () => {
    serveOrganizations();
    renderPage({ path: teamPage(ids.platformTeam) });

    expect(screen.getByRole('heading', { level: 1, name: 'Команда' })).toBeInTheDocument();
    const skeleton = screen.getByRole('status', { name: 'Загрузка данных' });
    expect(skeleton.querySelectorAll('.MuiSkeleton-rounded')).toHaveLength(1);

    const panel = await facts();
    expect(
      screen.getByRole('heading', { level: 1, name: 'Команда: Платформа' }),
    ).toBeInTheDocument();
    expect(panel).toHaveTextContent('НазваниеПлатформа');
    // mod-017 BR-OT-10, BR-OT-11: no default area, no 'Изменить'; no mark with two teams and no setting.
    expect(panel).not.toHaveTextContent('Область по умолчанию');
    expect(within(panel).queryByRole('button', { name: /Изменить/ })).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Переименовать' })).toBeInTheDocument();
    expect(await screen.findByRole('button', { name: 'Удалить команду' })).toBeInTheDocument();
  });

  it('fails under the same title «Команда», with «Повторить»', async () => {
    serveOrganizations();
    serveProblem('get', TEAM_URL, problem('organizations.validation_failed'), { once: true });
    renderPage({ path: teamPage(ids.platformTeam) });

    expect(await screen.findByRole('button', { name: 'Повторить' })).toBeInTheDocument();
    expect(screen.getByRole('heading', { level: 1, name: 'Команда' })).toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: 'Повторить' }));
    expect(
      await screen.findByRole('heading', { level: 1, name: 'Команда: Платформа' }),
    ).toBeInTheDocument();
  });
});

// mod-017 BR-OT-11: retired by name (doc-012 P-1) — «The first team’s default is the root» —
// «shows the chip and the caption, no «Изменить» and no «Удалить команду»».
describe('mod-017 US-3, US-7 Scenario: A team’s mark and the project’s last team', () => {
  it('the named default team carries «По умолчанию» in its name', async () => {
    const data = exampleDataset();
    data.projects.find((project) => project.id === ids.prm)!.defaultTeamId = ids.platformTeam;
    serveOrganizations(data);
    renderPage({ path: teamPage(ids.platformTeam) });

    const panel = await facts();

    expect(panel).toHaveTextContent('НазваниеПлатформаПо умолчанию');
  });

  it('the only team is marked «По умолчанию: единственная команда», has no «Удалить команду» and says why', async () => {
    serveOrganizations();
    renderPage({ path: teamPage(ids.anlTeam, ids.anl) });

    const panel = await facts();

    expect(panel).toHaveTextContent('НазваниеАналитикаПо умолчанию: единственная команда');
    expect(
      await within(panel).findByText('Единственную команду проекта удалить нельзя.'),
    ).toBeVisible();
    expect(screen.getByRole('button', { name: 'Переименовать' })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Удалить команду' })).not.toBeInTheDocument();
  });
});

describe('mod-002 US-7 Scenario: Deleted after a confirmation (from its own page)', () => {
  it('opens «Команды» of the same project with the notice', async () => {
    const data = exampleDataset();
    data.ownedItemCounts = { [ids.platformTeam]: 5 };
    serveOrganizations(data);
    renderPage({ path: teamPage(ids.platformTeam) });
    await facts();

    await userEvent.click(await screen.findByRole('button', { name: 'Удалить команду' }));
    const dialog = await screen.findByRole('dialog', { name: 'Удаление команды' });
    expect(await within(dialog).findByText(/Её рабочие элементы \(5\)/)).toHaveTextContent(
      'Удалить команду «Платформа»? Её рабочие элементы (5) станут «Без команды». Восстановить команду будет нельзя.',
    );
    await userEvent.click(within(dialog).getByRole('button', { name: 'Удалить команду' }));

    expect(await screen.findByText('Команда «Платформа» удалена.')).toBeInTheDocument();
    await waitFor(() =>
      expect(screen.getByTestId('location')).toHaveTextContent(
        `/organizations/${ids.alfa}/projects/${ids.prm}/settings/teams`,
      ),
    );
    expect(await screen.findByRole('table', { name: 'Команды' })).toBeInTheDocument();
  });
});

describe('mod-002 US-4 Scenario: A team id that names no team of the project; US-7 Scenario: A deleted team answers not found', () => {
  it.each([
    ['a deleted team', 'deleted', teamPage(ids.platformTeam), 'Команда не найдена'],
    ['a team of another project', 'none', teamPage(ids.anlTeam), 'Команда не найдена'],
    ['a deactivated project', 'none', teamPage(ids.oldTeam, ids.old), 'Проект не найден'],
    [
      'a deactivated organization',
      'none',
      teamPage(ids.arcTeam, ids.arc, ids.gamma),
      'Организация не найдена',
    ],
  ])('%s answers «%s»', async (_case, change, path, heading) => {
    const api = serveOrganizations();
    if (change === 'deleted') {
      api.data.teams.find((team) => team.id === ids.platformTeam)!.deleted = true;
    }
    renderPage({ path });

    expect(await screen.findByRole('heading', { level: 1, name: heading })).toBeInTheDocument();
    expect(screen.getAllByRole('heading', { level: 1 })).toHaveLength(1);
  });
});
