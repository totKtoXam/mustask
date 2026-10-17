import { describe, expect, it } from 'vitest';
import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import {
  ORG_TEAM_URL,
  PROJECT_TEAM_URL,
  exampleDataset,
  ids,
  ownedKey,
  problem,
} from '../testFixtures';
import { serveOrganizations, serveProblem } from '../testApi';
import { renderPage } from '../testRender';

// feat-028 design § 4 (US-1, US-3..US-6): a team's page is the organization's — its facts, its projects
// with the link and the unlink, its members. The tests of mod-002 US-4 and mod-017 design § 7.3 that this
// page keeps are rewritten here over the organization's team (doc-012 P-1).
//
// Retired by name (doc-012 P-1; spec § "Existing rules: keep, replace or retire"):
// - BR-OL-10: «the only team is marked «По умолчанию: единственная команда», has no «Удалить команду» and says
//   why» (a project's last team may be deleted; the mark is a project's, not a team's).
// - BR-OL-12: «opens «Команды» of the same project with the notice» (a team has no project; it opens «Команды»
//   of the organization, above) and «the named default team carries «По умолчанию» in its name» (the mark is a
//   project's: it stays on the project's «Команды», where `TeamsPage.test.tsx` marks it).
// - mod-002 BR-M1-22, -23 (a team inside its project): «a team of another project» and «a deactivated project»
//   cases of «A team id that names no team of the project…» — a team's address names no project.

function teamPage(teamId: string, organizationId: string = ids.alfa) {
  return `/organizations/${organizationId}/settings/teams/${teamId}`;
}

const UNLINK_URL = `${PROJECT_TEAM_URL}/unlink`;

async function facts() {
  await screen.findByText('Название');
  return screen.getByText('Название').closest('dl') as HTMLElement;
}

async function projectsTable() {
  return screen.findByRole('table', { name: 'Проекты команды' });
}

describe('feat-028 US-1: a team’s page (design § 4)', () => {
  it('loads under «Команда» with a two-line skeleton, then reads «Команда: Платформа» with its facts', async () => {
    serveOrganizations();
    renderPage({ path: teamPage(ids.platformTeam) });

    expect(screen.getByRole('heading', { level: 1, name: 'Команда' })).toBeInTheDocument();
    const skeleton = screen.getAllByRole('status', { name: 'Загрузка данных' })[0]!;
    expect(skeleton.querySelectorAll('.MuiSkeleton-rounded')).toHaveLength(2);

    const panel = await facts();
    expect(
      screen.getByRole('heading', { level: 1, name: 'Команда: Платформа' }),
    ).toBeInTheDocument();
    expect(panel).toHaveTextContent('НазваниеПлатформа');
    expect(panel).toHaveTextContent('ОписаниеОписание не задано');
    // The mark is a project's, not the team's: it stays on the project's «Команды».
    expect(panel).not.toHaveTextContent('По умолчанию');
    expect(screen.getByRole('button', { name: 'Переименовать' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Изменить описание' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Удалить команду' })).toBeInTheDocument();
  });

  it('shows the description as typed, with its line breaks', async () => {
    const data = exampleDataset();
    data.teams.find((team) => team.id === ids.platformTeam)!.description = 'Первая строка\nВторая';
    serveOrganizations(data);
    renderPage({ path: teamPage(ids.platformTeam) });

    const panel = await facts();

    expect(within(panel).getByText(/Первая строка/)).toHaveStyle({ whiteSpace: 'pre-wrap' });
  });

  it('fails under the same title «Команда», with «Повторить»', async () => {
    serveOrganizations();
    serveProblem('get', ORG_TEAM_URL, problem('organizations.validation_failed'), { once: true });
    renderPage({ path: teamPage(ids.platformTeam) });

    expect(await screen.findByRole('button', { name: 'Повторить' })).toBeInTheDocument();
    expect(screen.getByRole('heading', { level: 1, name: 'Команда' })).toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: 'Повторить' }));
    expect(
      await screen.findByRole('heading', { level: 1, name: 'Команда: Платформа' }),
    ).toBeInTheDocument();
  });

  it('opens «Команды» of the organization with the notice after the deletion, from its own page', async () => {
    const data = exampleDataset();
    data.ownedItemCounts = { [ownedKey(ids.prm, ids.platformTeam)]: 5 };
    serveOrganizations(data);
    renderPage({ path: teamPage(ids.platformTeam) });
    await facts();

    await userEvent.click(screen.getByRole('button', { name: 'Удалить команду' }));
    const dialog = await screen.findByRole('dialog', { name: 'Удаление команды' });
    const list = await within(dialog).findByRole('list', { name: 'Рабочие элементы по проектам' });
    expect(within(list).getAllByRole('listitem')[0]).toHaveTextContent('Пример проекта5');
    await userEvent.click(within(dialog).getByRole('button', { name: 'Удалить команду' }));

    expect(await screen.findByText('Команда «Платформа» удалена.')).toBeInTheDocument();
    await waitFor(() =>
      expect(screen.getByTestId('location')).toHaveTextContent(
        `/organizations/${ids.alfa}/settings/teams`,
      ),
    );
    expect(await screen.findByRole('table', { name: 'Команды организации' })).toBeInTheDocument();
  });

  it('renames and describes the team from its commands, the title following', async () => {
    serveOrganizations();
    renderPage({ path: teamPage(ids.platformTeam) });
    await facts();

    await userEvent.click(screen.getByRole('button', { name: 'Переименовать' }));
    const rename = await screen.findByRole('dialog', { name: 'Переименование команды' });
    const field = within(rename).getByRole('textbox', { name: 'Название' });
    await userEvent.clear(field);
    await userEvent.type(field, 'Ядро');
    await userEvent.click(within(rename).getByRole('button', { name: 'Сохранить' }));
    expect(await screen.findByText('Команда переименована.')).toBeInTheDocument();
    expect(
      await screen.findByRole('heading', { level: 1, name: 'Команда: Ядро' }),
    ).toBeInTheDocument();

    await userEvent.click(screen.getByRole('button', { name: 'Изменить описание' }));
    const describe = await screen.findByRole('dialog', { name: 'Описание команды' });
    await userEvent.type(within(describe).getByRole('textbox', { name: 'Описание' }), 'Описание');
    await userEvent.click(within(describe).getByRole('button', { name: 'Сохранить' }));
    expect(await screen.findByText('Описание команды сохранено.')).toBeInTheDocument();
    await waitFor(() =>
      expect(screen.getByText('Название').closest('dl')).toHaveTextContent('ОписаниеОписание'),
    );
  });
});

describe('feat-028 US-3 Scenario: A team’s projects', () => {
  it('lists its active projects by name, each a link to the project’s «Команды», with a count in the head', async () => {
    const data = exampleDataset();
    data.links.push({ projectId: ids.old, teamId: ids.platformTeam });
    serveOrganizations(data);
    renderPage({ path: teamPage(ids.platformTeam) });

    const table = await projectsTable();

    expect(screen.getByRole('heading', { level: 2, name: 'Проекты (1)' })).toBeVisible();
    expect(
      within(table)
        .getAllByRole('columnheader')
        .map((header) => header.textContent),
    ).toEqual(['Проект', 'Ключ', '']);
    const rows = within(table).getAllByRole('row').slice(1);
    expect(rows.map((row) => row.textContent)).toEqual(['Пример проектаPRM']);
    expect(within(rows[0]!).getByRole('link', { name: 'Пример проекта' })).toHaveAttribute(
      'href',
      `/organizations/${ids.alfa}/projects/${ids.prm}/settings/teams`,
    );
  });

  it('a team linked to none says so, and offers «Добавить проект» to the holder', async () => {
    const data = exampleDataset();
    data.links = data.links.filter((link) => link.teamId !== ids.platformTeam);
    serveOrganizations(data);
    renderPage({ path: teamPage(ids.platformTeam) });

    expect(await screen.findByText('Команда пока не участвует ни в одном проекте.')).toBeVisible();
    expect(screen.getByRole('button', { name: 'Добавить проект' })).toBeVisible();
    expect(screen.queryByRole('table', { name: 'Проекты команды' })).not.toBeInTheDocument();
  });

  it('a team whose projects are all deactivated reads as a team linked to none', async () => {
    serveOrganizations();
    renderPage({ path: teamPage(ids.oldTeam) });

    expect(await screen.findByText('Команда пока не участвует ни в одном проекте.')).toBeVisible();
  });

  it('the projects’ read failing is the section’s own error with «Повторить»; the facts and the members stay', async () => {
    serveOrganizations();
    serveProblem('get', `${ORG_TEAM_URL}/projects`, problem('organizations.validation_failed'), {
      once: true,
    });
    renderPage({ path: teamPage(ids.platformTeam) });

    const alert = await screen.findByRole('alert');
    expect(await facts()).toHaveTextContent('Платформа');
    expect(screen.getByRole('heading', { level: 2, name: 'Участники команды' })).toBeVisible();

    await userEvent.click(within(alert).getByRole('button', { name: 'Повторить' }));

    expect(await projectsTable()).toBeVisible();
  });
});

describe('feat-028 US-3 Scenario: «Добавить проект» links projects to the team', () => {
  async function openLink(data = exampleDataset()) {
    const api = serveOrganizations(data);
    renderPage({ path: teamPage(ids.platformTeam) });
    await projectsTable();
    await userEvent.click(screen.getByRole('button', { name: 'Добавить проект' }));
    const dialog = await screen.findByRole('dialog', { name: 'Добавление проектов в команду' });
    return { api, dialog };
  }

  it('says what a link gives, offers the active projects minus the linked ones — with their keys — and waits for a choice', async () => {
    const { dialog } = await openLink();

    expect(dialog).toHaveTextContent(
      'Команда «Платформа» сможет владеть рабочими элементами выбранных проектов. Доступ её участников к ним это не меняет.',
    );
    expect(within(dialog).getByRole('button', { name: 'Добавить' })).toBeDisabled();
    await userEvent.click(within(dialog).getByRole('combobox', { name: 'Проекты' }));
    expect((await screen.findAllByRole('option')).map((option) => option.textContent)).toEqual([
      'АналитикаANL',
    ]);
  });

  it('links one: the request, the row, and the notice naming it', async () => {
    const { api, dialog } = await openLink();
    await userEvent.click(within(dialog).getByRole('combobox', { name: 'Проекты' }));
    await userEvent.click(await screen.findByRole('option', { name: /Аналитика/ }));
    await userEvent.keyboard('{Escape}');

    await userEvent.click(within(dialog).getByRole('button', { name: 'Добавить' }));

    expect(await screen.findByText('Проект «Аналитика» добавлен в команду.')).toBeInTheDocument();
    expect(api.writes()).toEqual([
      {
        method: 'POST',
        path: `/api/organizations/${ids.alfa}/teams/${ids.platformTeam}/projects`,
        body: { projectIds: [ids.anl] },
      },
    ]);
    await waitFor(() =>
      expect(
        within(screen.getByRole('table', { name: 'Проекты команды' }))
          .getAllByRole('row')
          .slice(1)
          .map((row) => row.textContent),
      ).toEqual(['АналитикаANL', 'Пример проектаPRM']),
    );
  });

  it('counts in the notice when several are linked', async () => {
    const data = exampleDataset();
    data.projects.push({
      id: 'b0000000-0000-4000-8000-0000000000aa',
      organizationId: ids.alfa,
      name: 'Ядро',
      key: 'CORE',
      active: true,
    });
    const { dialog } = await openLink(data);
    await userEvent.click(within(dialog).getByRole('combobox', { name: 'Проекты' }));
    await userEvent.click(await screen.findByRole('option', { name: /Аналитика/ }));
    await userEvent.click(screen.getByRole('option', { name: /Ядро/ }));
    await userEvent.keyboard('{Escape}');

    await userEvent.click(within(dialog).getByRole('button', { name: 'Добавить' }));

    expect(await screen.findByText('Проекты добавлены в команду: 2.')).toBeInTheDocument();
  });

  it('says «Команда уже работает над всеми проектами организации.» when none is left', async () => {
    const data = exampleDataset();
    data.links.push({ projectId: ids.anl, teamId: ids.platformTeam });
    const { dialog } = await openLink(data);

    await userEvent.click(within(dialog).getByRole('combobox', { name: 'Проекты' }));

    expect(
      await screen.findByText('Команда уже работает над всеми проектами организации.'),
    ).toBeVisible();
  });

  it('a project deactivated meanwhile lands under «Проекты», the choice drops and nothing is linked', async () => {
    const { api, dialog } = await openLink();
    await userEvent.click(within(dialog).getByRole('combobox', { name: 'Проекты' }));
    await userEvent.click(await screen.findByRole('option', { name: /Аналитика/ }));
    await userEvent.keyboard('{Escape}');
    api.data.projects.find((project) => project.id === ids.anl)!.active = false;

    await userEvent.click(within(dialog).getByRole('button', { name: 'Добавить' }));

    expect(await within(dialog).findByText('Проект не найден.')).toBeVisible();
    await waitFor(() =>
      expect(
        within(dialog).queryByRole('button', { name: 'Убрать «Аналитика»' }),
      ).not.toBeInTheDocument(),
    );
    expect(
      api.data.links.some((link) => link.projectId === ids.anl && link.teamId === ids.platformTeam),
    ).toBe(false);
  });

  it('the team deleted meanwhile is the page’s own end: the dialog’s alert, and the frame after it closes', async () => {
    const { api, dialog } = await openLink();
    await userEvent.click(within(dialog).getByRole('combobox', { name: 'Проекты' }));
    await userEvent.click(await screen.findByRole('option', { name: /Аналитика/ }));
    await userEvent.keyboard('{Escape}');
    api.data.teams.find((team) => team.id === ids.platformTeam)!.deleted = true;

    await userEvent.click(within(dialog).getByRole('button', { name: 'Добавить' }));

    expect(await within(dialog).findByRole('alert')).toHaveTextContent('Команда не найдена.');
    await userEvent.click(within(dialog).getByRole('button', { name: 'Отмена' }));

    expect(
      await screen.findByRole('heading', { level: 1, name: 'Команда не найдена' }),
    ).toBeInTheDocument();
  });

  it('an organization with no active project says so in an information note, and «Добавить» stays unavailable', async () => {
    const data = exampleDataset();
    for (const project of data.projects.filter(
      (candidate) => candidate.organizationId === ids.alfa,
    )) {
      project.active = false;
    }
    const api = serveOrganizations(data);
    renderPage({ path: teamPage(ids.oldTeam) });
    await userEvent.click(await screen.findByRole('button', { name: 'Добавить проект' }));
    const dialog = await screen.findByRole('dialog', { name: 'Добавление проектов в команду' });

    expect(await within(dialog).findByRole('status')).toHaveTextContent(
      'В организации нет активных проектов.',
    );
    expect(within(dialog).getByRole('button', { name: 'Добавить' })).toBeDisabled();
    expect(api.writes()).toEqual([]);
  });
});

describe('feat-028 US-4 Scenario: «Отключить от проекта» from the team’s page', () => {
  async function openUnlink(data = exampleDataset(), before: () => void = () => undefined) {
    const api = serveOrganizations(data);
    before();
    renderPage({ path: teamPage(ids.platformTeam) });
    const table = await projectsTable();
    await userEvent.click(
      within(table).getByRole('button', { name: 'Ещё действия: Пример проекта' }),
    );
    await userEvent.click(
      within(await screen.findByRole('menu')).getByRole('menuitem', {
        name: 'Отключить от проекта',
      }),
    );
    const dialog = await screen.findByRole('dialog', { name: 'Отключение команды от проекта' });
    return { api, dialog };
  }

  function owning(count: number) {
    const data = exampleDataset();
    data.ownedItemCounts = { [ownedKey(ids.prm, ids.platformTeam)]: count };
    return data;
  }

  it('moves the items to the chosen team, the project leaves the section, the team stays', async () => {
    const { api, dialog } = await openUnlink(owning(4));
    await userEvent.click(
      await within(dialog).findByRole('combobox', { name: 'Передать рабочие элементы' }),
    );
    await userEvent.click(await screen.findByRole('option', { name: 'Пример проекта' }));
    await userEvent.click(within(dialog).getByRole('button', { name: 'Отключить' }));

    expect(await screen.findByText('Команда отключена от проекта.')).toBeInTheDocument();
    expect(api.writes()[0]).toEqual({
      method: 'POST',
      path: `/api/organizations/${ids.alfa}/projects/${ids.prm}/teams/${ids.platformTeam}/unlink`,
      body: { destination: { teamId: ids.prmTeam } },
    });
    expect(await screen.findByText('Команда пока не участвует ни в одном проекте.')).toBeVisible();
    expect(
      await screen.findByRole('heading', { level: 1, name: 'Команда: Платформа' }),
    ).toBeVisible();
  });

  it('owns none: the plain question; the request names «Без команды»', async () => {
    const { api, dialog } = await openUnlink();

    await userEvent.click(await within(dialog).findByRole('button', { name: 'Отключить' }));

    await screen.findByText('Команда отключена от проекта.');
    expect(api.writes()[0]?.body).toEqual({ destination: { teamId: null } });
  });

  it('puts «Команда не найдена.» of a destination deleted meanwhile under the field and clears the choice', async () => {
    const { dialog } = await openUnlink(owning(2), () =>
      serveProblem('post', UNLINK_URL, problem('organizations.team.not_found')),
    );
    await userEvent.click(
      await within(dialog).findByRole('combobox', { name: 'Передать рабочие элементы' }),
    );
    await userEvent.click(await screen.findByRole('option', { name: 'Пример проекта' }));

    await userEvent.click(within(dialog).getByRole('button', { name: 'Отключить' }));

    expect(await within(dialog).findByText('Команда не найдена.')).toBeVisible();
    expect(within(dialog).getByRole('button', { name: 'Отключить' })).toBeDisabled();
  });

  it('the project deactivated meanwhile is the dialog’s alert «Проект не найден.»', async () => {
    const { dialog } = await openUnlink(owning(2), () =>
      serveProblem('post', UNLINK_URL, problem('organizations.project.not_found')),
    );
    await userEvent.click(
      await within(dialog).findByRole('combobox', { name: 'Передать рабочие элементы' }),
    );
    await userEvent.click(await screen.findByRole('option', { name: 'Без команды' }));

    await userEvent.click(within(dialog).getByRole('button', { name: 'Отключить' }));

    expect(await within(dialog).findByRole('alert')).toHaveTextContent('Проект не найден.');
  });
});

describe('feat-028 US-1, US-2 Scenario: A team id that names no team of the organization', () => {
  it.each([
    ['a deleted team', 'deleted', teamPage(ids.platformTeam), 'Команда не найдена'],
    ['a team of another organization', 'none', teamPage(ids.repTeam), 'Команда не найдена'],
    [
      'an unknown team',
      'none',
      teamPage('c0000000-0000-4000-8000-0000000000ff'),
      'Команда не найдена',
    ],
    [
      'a deactivated organization',
      'none',
      teamPage(ids.arcTeam, ids.gamma),
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

  it('does not depend on a project: it opens, and lists the other one, when one of its projects is deactivated', async () => {
    const api = serveOrganizations();
    api.data.links.push({ projectId: ids.anl, teamId: ids.platformTeam });
    api.data.projects.find((project) => project.id === ids.anl)!.active = false;
    renderPage({ path: teamPage(ids.platformTeam) });

    const table = await projectsTable();

    expect(
      within(table)
        .getAllByRole('row')
        .slice(1)
        .map((row) => row.textContent),
    ).toEqual(['Пример проектаPRM']);
  });
});
