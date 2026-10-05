import { describe, expect, it } from 'vitest';
import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { TEAM_URL, ids, problem } from '../testFixtures';
import { serveOrganizations, serveProblem } from '../testApi';
import { renderPage } from '../testRender';

function teamPage(teamId: string, projectId: string = ids.prm, organizationId: string = ids.alfa) {
  return `/organizations/${organizationId}/projects/${projectId}/settings/teams/${teamId}`;
}

async function facts() {
  await screen.findByText('Область по умолчанию');
  return screen.getByText('Название').closest('dl') as HTMLElement;
}

describe('mod-002 US-4: a team’s page (design § 10)', () => {
  it('loads under «Команда» with a two-line skeleton, then reads «Команда: Платформа» with its facts', async () => {
    serveOrganizations();
    renderPage({ path: teamPage(ids.platformTeam) });

    expect(screen.getByRole('heading', { level: 1, name: 'Команда' })).toBeInTheDocument();
    const skeleton = screen.getByRole('status', { name: 'Загрузка данных' });
    expect(skeleton.querySelectorAll('.MuiSkeleton-rounded')).toHaveLength(2);

    const panel = await facts();
    expect(
      screen.getByRole('heading', { level: 1, name: 'Команда: Платформа' }),
    ).toBeInTheDocument();
    expect(panel).toHaveTextContent('НазваниеПлатформа');
    expect(panel).toHaveTextContent('Область по умолчаниюПлатформа / API');
    expect(
      within(panel).getByRole('button', { name: 'Изменить область по умолчанию' }),
    ).toHaveTextContent('Изменить');
    expect(screen.getByRole('button', { name: 'Переименовать' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Удалить команду' })).toBeInTheDocument();
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

describe('mod-002 US-4 Scenario: The first team’s default is the root', () => {
  it('shows the chip and the caption, no «Изменить» and no «Удалить команду»', async () => {
    serveOrganizations();
    renderPage({ path: teamPage(ids.prmTeam) });

    const panel = await facts();
    expect(panel).toHaveTextContent('НазваниеПример проектаВладеет корневой областью');
    expect(panel).toHaveTextContent(
      'Пример проектаКорневая область проекта. У первой команды область по умолчанию не меняется.',
    );
    expect(screen.queryByRole('button', { name: /Изменить/ })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Удалить команду' })).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Переименовать' })).toBeInTheDocument();
  });
});

describe('mod-002 US-7 Scenario: Deleted after a confirmation (from its own page)', () => {
  it('opens «Команды» of the same project with the notice', async () => {
    serveOrganizations();
    renderPage({ path: teamPage(ids.platformTeam) });
    await facts();

    await userEvent.click(screen.getByRole('button', { name: 'Удалить команду' }));
    const dialog = await screen.findByRole('dialog', { name: 'Удаление команды' });
    expect(dialog).toHaveTextContent(
      'Удалить команду «Платформа»? Её области перейдут команде «Пример проекта». Восстановить команду будет нельзя.',
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
