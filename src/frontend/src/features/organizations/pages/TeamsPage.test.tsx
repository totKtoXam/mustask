import { describe, expect, it } from 'vitest';
import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http } from 'msw';
import { gate } from '../../../test/gate';
import { server } from '../../../test/mocks/server';
import {
  ORG_TEAMS_URL,
  PROJECT_TEAM_URL,
  PROJECT_URL,
  TEAMS_URL,
  exampleDataset,
  ids,
  noTeamsDataset,
  ownedKey,
  problem,
} from '../testFixtures';
import { serveOrganizations, serveProblem } from '../testApi';
import { contextSource, renderPage } from '../testRender';

// feat-028 design § 7 (US-2, US-3, US-4, US-6, US-7): the project's «Команды» are the teams of the
// organization linked to it. The tests of mod-017 design § 7 that this page keeps are rewritten over the
// linked teams (doc-012 P-1, keeping their names where the intent stands).
//
// Retired by name (doc-012 P-1; spec § "Existing rules: keep, replace or retire"):
// - BR-OL-10 (mod-017 BR-OT-11's last-team guard): «the project’s only team’s menu holds «Переименовать»
//   alone»; the `last_team_cannot_be_deleted` row of «a direct refusal %s shows «%s» in the dialog…».
// - BR-OL-5, BR-OL-12 (a team is renamed, described and deleted in the organization's «Команды»): «every
//   team’s menu holds «Переименовать» and «Удалить команду», the deletion in the error colour», «adds «Ядро»
//   with the helper «От 1 до 100 символов.», lists it by name and notices «Команда «Ядро» добавлена.»», ««Переименование
//   команды» has no helper; the notice «Команда переименована.»», «puts «Команда с таким названием уже есть
//   в проекте» on «Название»» and the deletion's dialog tests — all rewritten in `OrganizationTeamsPage.test.tsx`
//   over the organization's teams (the text reads «…в организации»; the counts are per project).
// - BR-OL-10 (a project may have no team): «has no empty state, and answers «Проект не найден» for a deactivated
//   project» — the page has an empty state now; the deactivated project's answer is kept above.

const PRM_TEAMS = `/organizations/${ids.alfa}/projects/${ids.prm}/settings/teams`;
const ANL_TEAMS = `/organizations/${ids.alfa}/projects/${ids.anl}/settings/teams`;
const DEFAULT_TEAM_URL = `${PROJECT_URL}/default-team`;
const UNLINK_COUNT_URL = `${PROJECT_TEAM_URL}/unlink-count`;
const UNLINK_URL = `${PROJECT_TEAM_URL}/unlink`;

async function teamsTable() {
  return screen.findByRole('table', { name: 'Команды' });
}

async function openMenu(name: string) {
  await userEvent.click(screen.getByRole('button', { name: `Действия: ${name}` }));
  return screen.findByRole('menu');
}

/** «Пример проекта» with «Платформа» named as the project's default team. */
function withNamedDefault() {
  const data = exampleDataset();
  data.projects.find((project) => project.id === ids.prm)!.defaultTeamId = ids.platformTeam;
  return data;
}

describe('feat-028 US-3, US-6 Scenario: The project’s linked teams', () => {
  it('loads with «Команда» alone, then lists the linked teams by name, no team first, no mark with two teams and no setting', async () => {
    serveOrganizations();
    renderPage({ path: PRM_TEAMS });

    const skeleton = screen.getByRole('status', { name: 'Загрузка данных' });
    expect(within(skeleton).getByText('Команда')).toBeInTheDocument();
    expect(within(skeleton).queryByText('Область по умолчанию')).not.toBeInTheDocument();

    const table = await teamsTable();
    const rows = within(table).getAllByRole('row').slice(1);
    expect(rows.map((row) => row.textContent)).toEqual(['Платформа', 'Пример проекта']);
    expect(within(table).queryByText(/По умолчанию/)).not.toBeInTheDocument();
    // A team's page is the organization's now (BR-OL-12).
    expect(within(rows[0]!).getByRole('link', { name: 'Платформа' })).toHaveAttribute(
      'href',
      `/organizations/${ids.alfa}/settings/teams/${ids.platformTeam}`,
    );
    expect(screen.getByRole('button', { name: 'Добавить команду' })).toHaveClass(
      'MuiButton-contained',
    );
    expect(screen.getByRole('button', { name: 'Команда по умолчанию' })).toBeVisible();
  });

  it('says in one line where teams are created and renamed, with the section as a link', async () => {
    serveOrganizations();
    renderPage({ path: PRM_TEAMS });
    await teamsTable();

    const intro = screen.getByText(/Команды организации, работающие над проектом\./);
    expect(intro).toHaveTextContent(
      'Команды организации, работающие над проектом. Создать, переименовать или удалить команду можно в разделе «Организация» → «Команды».',
    );
    expect(within(intro).getByRole('link', { name: '«Команды»' })).toHaveAttribute(
      'href',
      `/organizations/${ids.alfa}/settings/teams`,
    );
  });

  it('marks the named team «По умолчанию», on its row only', async () => {
    serveOrganizations(withNamedDefault());
    renderPage({ path: PRM_TEAMS });

    const table = await teamsTable();
    const rows = within(table).getAllByRole('row').slice(1);

    expect(rows.map((row) => row.textContent)).toEqual(['ПлатформаПо умолчанию', 'Пример проекта']);
  });

  it('marks a project’s only linked team «По умолчанию: единственная команда»', async () => {
    serveOrganizations();
    renderPage({ path: ANL_TEAMS });

    const table = await teamsTable();

    expect(within(table).getAllByRole('row').slice(1)[0]).toHaveTextContent(
      'АналитикаПо умолчанию: единственная команда',
    );
  });

  it('answers «Проект не найден» for a deactivated project', async () => {
    serveOrganizations();
    renderPage({ path: `/organizations/${ids.alfa}/projects/${ids.old}/settings/teams` });

    expect(
      await screen.findByRole('heading', { level: 1, name: 'Проект не найден' }),
    ).toBeInTheDocument();
  });

  it('lists a team that works on two projects on both, saying the caller is in it in each (BR-OL-11)', async () => {
    const data = exampleDataset();
    data.links.push({ projectId: ids.anl, teamId: ids.platformTeam });
    serveOrganizations(data);
    renderPage({ path: ANL_TEAMS });

    const table = await teamsTable();

    expect(
      within(table)
        .getAllByRole('row')
        .slice(1)
        .map((row) => row.textContent),
    ).toEqual(['Аналитика', 'Платформа']);
  });
});

describe('feat-028 US-7, BR-OL-10 Scenario: A project with no team is the normal first state', () => {
  async function openEmpty(role: 'admin' | 'member' | 'viewer' = 'admin') {
    const api = serveOrganizations(noTeamsDataset());
    renderPage({ path: PRM_TEAMS, role });
    const heading = await screen.findByRole('heading', {
      level: 2,
      name: 'В проекте пока нет команд.',
    });
    return { api, heading };
  }

  it('says so with the hint and the action for the holder, in no warning colour, and offers the project’s own views', async () => {
    await openEmpty();

    expect(
      screen.getByText(
        'Добавьте команду организации — она сможет владеть рабочими элементами проекта. Без команд проект тоже работает: у его рабочих элементов будет «Без команды».',
      ),
    ).toBeVisible();
    expect(screen.queryByRole('table', { name: 'Команды' })).not.toBeInTheDocument();
    // A normal state, not a fault: no alert, no warning.
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
    expect(document.querySelector('.MuiAlert-colorWarning')).toBeNull();
    expect(screen.getAllByRole('button', { name: 'Добавить команду' })).toHaveLength(2);
    expect(screen.queryByRole('button', { name: 'Команда по умолчанию' })).not.toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Бэклог проекта' })).toHaveAttribute(
      'href',
      `/organizations/${ids.alfa}/projects/${ids.prm}/backlogs/stories`,
    );
    expect(screen.getByRole('link', { name: 'Доска проекта' })).toHaveAttribute(
      'href',
      `/organizations/${ids.alfa}/projects/${ids.prm}/boards/stories`,
    );
    expect(screen.getByText(/Команды организации, работающие над проектом\./)).toBeVisible();
  });

  it.each([['member'], ['viewer']] as const)(
    'for the %s: the other hint, no action, and the two views still there',
    async (role) => {
      await openEmpty(role);

      expect(
        screen.getByText(
          'Команды подключает администратор организации. Проект работает и без команд.',
        ),
      ).toBeVisible();
      expect(screen.queryByRole('button', { name: 'Добавить команду' })).not.toBeInTheDocument();
      expect(screen.getByRole('link', { name: 'Бэклог проекта' })).toBeVisible();
      expect(screen.getByRole('link', { name: 'Доска проекта' })).toBeVisible();
    },
  );

  it('shows neither view once a team is linked', async () => {
    serveOrganizations();
    renderPage({ path: PRM_TEAMS });
    await teamsTable();

    expect(screen.queryByRole('link', { name: 'Бэклог проекта' })).not.toBeInTheDocument();
    expect(screen.queryByRole('link', { name: 'Доска проекта' })).not.toBeInTheDocument();
  });

  it('shows the error with «Повторить» when the read fails, and the table after it', async () => {
    serveOrganizations();
    serveProblem('get', TEAMS_URL, problem('organizations.validation_failed'), { once: true });
    renderPage({ path: PRM_TEAMS });

    await userEvent.click(await screen.findByRole('button', { name: 'Повторить' }));

    expect(await teamsTable()).toBeVisible();
  });
});

describe('feat-028 BR-OL-5 Scenario: A row’s menu holds the unlink alone', () => {
  it('«Отключить от проекта» in the error colour is the only entry; no rename and no deletion through a project', async () => {
    serveOrganizations();
    renderPage({ path: PRM_TEAMS });
    await teamsTable();

    for (const name of ['Платформа', 'Пример проекта']) {
      const menu = await openMenu(name);
      const items = within(menu).getAllByRole('menuitem');
      expect(items.map((item) => item.textContent)).toEqual(['Отключить от проекта']);
      expect(getComputedStyle(items[0]!).color).not.toBe(getComputedStyle(document.body).color);
      await userEvent.keyboard('{Escape}');
    }
  });
});

describe('feat-028 US-3 Scenario: «Добавить команду» links existing teams', () => {
  async function openLink(path = PRM_TEAMS, data = exampleDataset()) {
    const api = serveOrganizations(data);
    renderPage({ path });
    await teamsTable();
    await userEvent.click(screen.getByRole('button', { name: 'Добавить команду' }));
    const dialog = await screen.findByRole('dialog', { name: 'Добавление команд в проект' });
    return { api, dialog };
  }

  it('says what a link gives, offers the organization’s teams minus the linked ones, and waits for a choice', async () => {
    const { dialog } = await openLink();

    expect(dialog).toHaveTextContent(
      'Выбранные команды смогут владеть рабочими элементами проекта «Пример проекта». Доступ их участников к проекту это не меняет.',
    );
    expect(within(dialog).getByRole('button', { name: 'Добавить' })).toBeDisabled();
    const field = within(dialog).getByRole('combobox', { name: 'Команды' });
    await userEvent.click(field);
    expect((await screen.findAllByRole('option')).map((option) => option.textContent)).toEqual([
      'Аналитика',
      'Старый проект',
    ]);
  });

  it('links two at once: one request, the rows appear and the notice counts them', async () => {
    const { api, dialog } = await openLink();
    await userEvent.click(within(dialog).getByRole('combobox', { name: 'Команды' }));
    await userEvent.click(await screen.findByRole('option', { name: 'Аналитика' }));
    await userEvent.click(screen.getByRole('option', { name: 'Старый проект' }));
    await userEvent.keyboard('{Escape}');

    await userEvent.click(within(dialog).getByRole('button', { name: 'Добавить' }));

    expect(await screen.findByText('Команды добавлены в проект: 2.')).toBeInTheDocument();
    expect(api.writes()).toEqual([
      {
        method: 'POST',
        path: `/api/organizations/${ids.alfa}/projects/${ids.prm}/teams`,
        body: { teamIds: [ids.anlTeam, ids.oldTeam] },
      },
    ]);
    await waitFor(() =>
      expect(
        within(screen.getByRole('table', { name: 'Команды' }))
          .getAllByRole('row')
          .slice(1)
          .map((row) => row.textContent),
      ).toEqual(['Аналитика', 'Платформа', 'Пример проекта', 'Старый проект']),
    );
  });

  it('names the one team in the notice', async () => {
    const { dialog } = await openLink();
    await userEvent.click(within(dialog).getByRole('combobox', { name: 'Команды' }));
    await userEvent.click(await screen.findByRole('option', { name: 'Аналитика' }));
    await userEvent.keyboard('{Escape}');

    await userEvent.click(within(dialog).getByRole('button', { name: 'Добавить' }));

    expect(await screen.findByText('Команда «Аналитика» добавлена в проект.')).toBeInTheDocument();
  });

  it('says «Все команды организации уже работают над проектом.» when none is left, and «Ничего не найдено» for a text that matches nothing', async () => {
    const data = exampleDataset();
    data.links.push(
      { projectId: ids.prm, teamId: ids.anlTeam },
      { projectId: ids.prm, teamId: ids.oldTeam },
    );
    const { dialog } = await openLink(PRM_TEAMS, data);

    await userEvent.click(within(dialog).getByRole('combobox', { name: 'Команды' }));
    expect(
      await screen.findByText('Все команды организации уже работают над проектом.'),
    ).toBeVisible();
    await userEvent.type(within(dialog).getByRole('combobox', { name: 'Команды' }), 'яяя');
    expect(await screen.findByText('Ничего не найдено')).toBeVisible();
    expect(within(dialog).getByRole('button', { name: 'Добавить' })).toBeDisabled();
  });

  it('replaces the field with a note that links to «Команды» when the organization has no team at all (US-7)', async () => {
    serveOrganizations(noTeamsDataset());
    renderPage({ path: PRM_TEAMS });
    await screen.findByRole('heading', { level: 2, name: 'В проекте пока нет команд.' });
    await userEvent.click(screen.getAllByRole('button', { name: 'Добавить команду' })[1]!);
    const dialog = await screen.findByRole('dialog', { name: 'Добавление команд в проект' });

    const note = await within(dialog).findByRole('status');
    expect(note).toHaveTextContent(
      'В организации пока нет команд. Создайте первую в разделе «Организация» → «Команды».',
    );
    expect(within(note).getByRole('link', { name: '«Команды»' })).toHaveAttribute(
      'href',
      `/organizations/${ids.alfa}/settings/teams`,
    );
    expect(within(dialog).queryByRole('combobox', { name: 'Команды' })).not.toBeInTheDocument();
    expect(within(dialog).getByRole('button', { name: 'Добавить' })).toBeDisabled();
  });

  it('says «Загрузка…» while the options load, the field working, and offers «Повторить» when they fail', async () => {
    serveOrganizations();
    renderPage({ path: PRM_TEAMS });
    await teamsTable();
    const held = gate();
    server.use(
      http.get(ORG_TEAMS_URL, async () => {
        await held.opened;
        return undefined;
      }),
    );
    await userEvent.click(screen.getByRole('button', { name: 'Добавить команду' }));
    const dialog = await screen.findByRole('dialog', { name: 'Добавление команд в проект' });
    const field = within(dialog).getByRole('combobox', { name: 'Команды' });
    expect(field).toBeEnabled();
    await userEvent.click(field);
    expect(await screen.findByText('Загрузка…')).toBeVisible();
    held.open();
    expect(await screen.findByRole('option', { name: 'Аналитика' })).toBeVisible();
  });

  it('a read that fails says so in the field’s place with «Повторить», and «Добавить» stays unavailable', async () => {
    serveOrganizations();
    renderPage({ path: PRM_TEAMS });
    await teamsTable();
    server.use(
      http.get(
        ORG_TEAMS_URL,
        () =>
          new Response(JSON.stringify(problem('organizations.validation_failed')), {
            status: 400,
            headers: { 'Content-Type': 'application/problem+json' },
          }),
        { once: true },
      ),
    );
    await userEvent.click(screen.getByRole('button', { name: 'Добавить команду' }));
    const dialog = await screen.findByRole('dialog', { name: 'Добавление команд в проект' });

    expect(
      await within(dialog).findByText('Не удалось загрузить команды. Попробуйте ещё раз.'),
    ).toBeVisible();
    expect(within(dialog).getByRole('button', { name: 'Добавить' })).toBeDisabled();
    await userEvent.click(within(dialog).getByRole('button', { name: 'Повторить' }));

    expect(await within(dialog).findByRole('combobox', { name: 'Команды' })).toBeVisible();
  });

  it('a team gone meanwhile lands under «Команды», is dropped from the choice and nothing is linked', async () => {
    const { api, dialog } = await openLink();
    await userEvent.click(within(dialog).getByRole('combobox', { name: 'Команды' }));
    await userEvent.click(await screen.findByRole('option', { name: 'Аналитика' }));
    await userEvent.keyboard('{Escape}');
    api.data.teams.find((team) => team.id === ids.anlTeam)!.deleted = true;

    await userEvent.click(within(dialog).getByRole('button', { name: 'Добавить' }));

    expect(await within(dialog).findByText('Команда не найдена.')).toBeVisible();
    await waitFor(() =>
      expect(
        within(dialog).queryByRole('button', { name: 'Убрать «Аналитика»' }),
      ).not.toBeInTheDocument(),
    );
    expect(api.data.links.filter((link) => link.projectId === ids.prm)).toHaveLength(2);
  });

  it('a 403 shows «Недостаточно прав» in the dialog and keeps the choice', async () => {
    const { dialog } = await openLink();
    serveProblem('post', TEAMS_URL, problem('organizations.access.forbidden'));
    await userEvent.click(within(dialog).getByRole('combobox', { name: 'Команды' }));
    await userEvent.click(await screen.findByRole('option', { name: 'Аналитика' }));
    await userEvent.keyboard('{Escape}');

    await userEvent.click(within(dialog).getByRole('button', { name: 'Добавить' }));

    expect(await within(dialog).findByText('Недостаточно прав')).toBeVisible();
    expect(within(dialog).getByRole('button', { name: 'Убрать «Аналитика»' })).toBeVisible();
  });
});

describe('feat-028 US-4 Scenario: «Отключить от проекта» reassigns what the team owns (SC-09)', () => {
  async function openUnlink(
    options: {
      data?: ReturnType<typeof exampleDataset>;
      team?: string;
      context?: ReturnType<typeof contextSource>;
      before?: () => void;
    } = {},
  ) {
    const data = options.data ?? exampleDataset();
    const api = serveOrganizations(data);
    options.before?.();
    renderPage({ path: PRM_TEAMS, context: options.context });
    await teamsTable();
    const menu = await openMenu(options.team ?? 'Платформа');
    await userEvent.click(within(menu).getByRole('menuitem', { name: 'Отключить от проекта' }));
    const dialog = await screen.findByRole('dialog', { name: 'Отключение команды от проекта' });
    return { api, dialog };
  }

  function owning(count: number) {
    const data = exampleDataset();
    data.ownedItemCounts = { [ownedKey(ids.prm, ids.platformTeam)]: count };
    return data;
  }

  it('owns items: asks with the count, preselects nothing, and «Отключить» waits for the choice; focus on «Отмена»', async () => {
    const { dialog } = await openUnlink({ data: owning(7) });

    expect(
      await within(dialog).findByText(
        'Команда «Платформа» владеет рабочими элементами проекта «Пример проекта»: 7. Выберите, кому их передать.',
      ),
    ).toBeVisible();
    const field = within(dialog).getByRole('combobox', { name: 'Передать рабочие элементы' });
    // Nothing is chosen: the select shows no option (MUI keeps a zero-width space for the line's height).
    expect(field.textContent?.replaceAll('​', '')).toBe('');
    expect(within(dialog).getByText('Выберите команду или «Без команды»')).toBeInTheDocument();
    expect(within(dialog).getByRole('button', { name: 'Отключить' })).toBeDisabled();
    expect(within(dialog).getByRole('button', { name: 'Отмена' })).toHaveFocus();
    expect(
      within(dialog).getByText(
        'Рабочие элементы не удаляются. Если «Платформа» — команда проекта по умолчанию, настройка сбросится. Повторное подключение команды рабочие элементы не вернёт.',
      ),
    ).toBeVisible();

    await userEvent.click(field);
    expect(
      within(await screen.findByRole('listbox'))
        .getAllByRole('option')
        .map((option) => option.textContent),
    ).toEqual(['Без команды', 'Пример проекта']);
  });

  it('moves them to another linked team: the line names it, the request names it, the row leaves and the notice reads', async () => {
    const { api, dialog } = await openUnlink({ data: owning(7) });
    await userEvent.click(
      await within(dialog).findByRole('combobox', { name: 'Передать рабочие элементы' }),
    );
    await userEvent.click(await screen.findByRole('option', { name: 'Пример проекта' }));

    expect(
      within(dialog).getByText(
        'Рабочие элементы встанут в конец бэклогов команды «Пример проекта».',
      ),
    ).toBeVisible();
    await userEvent.click(within(dialog).getByRole('button', { name: 'Отключить' }));

    expect(await screen.findByText('Команда отключена от проекта.')).toBeInTheDocument();
    expect(api.writes()).toEqual([
      {
        method: 'POST',
        path: `/api/organizations/${ids.alfa}/projects/${ids.prm}/teams/${ids.platformTeam}/unlink`,
        body: { destination: { teamId: ids.prmTeam } },
      },
    ]);
    await waitFor(() =>
      expect(
        within(screen.getByRole('table', { name: 'Команды' })).queryByRole('link', {
          name: 'Платформа',
        }),
      ).not.toBeInTheDocument(),
    );
    expect(api.data.ownedItemCounts?.[ownedKey(ids.prm, ids.prmTeam)]).toBe(7);
  });

  it('«Без команды» says the items stay on the project’s backlog and board but on no team’s, and sends `teamId: null`', async () => {
    const { api, dialog } = await openUnlink({ data: owning(3) });
    await userEvent.click(
      await within(dialog).findByRole('combobox', { name: 'Передать рабочие элементы' }),
    );
    await userEvent.click(await screen.findByRole('option', { name: 'Без команды' }));

    expect(
      within(dialog).getByText(
        'Рабочие элементы останутся в бэклоге и на доске проекта, но не попадут в бэклог ни одной команды.',
      ),
    ).toBeVisible();
    await userEvent.click(within(dialog).getByRole('button', { name: 'Отключить' }));

    await screen.findByText('Команда отключена от проекта.');
    expect(api.writes()[0]?.body).toEqual({ destination: { teamId: null } });
  });

  it('owns none: no field, the plain question, and the request names «Без команды»', async () => {
    const { api, dialog } = await openUnlink();

    expect(
      await within(dialog).findByText('Отключить команду «Платформа» от проекта «Пример проекта»?'),
    ).toBeVisible();
    expect(
      within(dialog).getByText(
        'Если «Платформа» — команда проекта по умолчанию, настройка сбросится.',
      ),
    ).toBeVisible();
    expect(
      within(dialog).queryByRole('combobox', { name: 'Передать рабочие элементы' }),
    ).not.toBeInTheDocument();
    expect(within(dialog).getByRole('button', { name: 'Отключить' })).toBeEnabled();

    await userEvent.click(within(dialog).getByRole('button', { name: 'Отключить' }));

    await screen.findByText('Команда отключена от проекта.');
    expect(api.writes()[0]?.body).toEqual({ destination: { teamId: null } });
  });

  it('owns none: the team deleted while the dialog is open is the alert «Команда не найдена.» (there is no field to carry it); closing reads the list again', async () => {
    const { api, dialog } = await openUnlink();
    await within(dialog).findByText('Отключить команду «Платформа» от проекта «Пример проекта»?');
    api.data.teams.find((team) => team.id === ids.platformTeam)!.deleted = true;

    await userEvent.click(within(dialog).getByRole('button', { name: 'Отключить' }));

    expect(await within(dialog).findByRole('alert')).toHaveTextContent('Команда не найдена.');
    expect(
      screen.getByRole('dialog', { name: 'Отключение команды от проекта' }),
    ).toBeInTheDocument();
    expect(within(dialog).queryByRole('combobox')).not.toBeInTheDocument();
    expect(screen.queryByText('Команда отключена от проекта.')).not.toBeInTheDocument();

    await userEvent.click(within(dialog).getByRole('button', { name: 'Отмена' }));

    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
    await waitFor(() =>
      expect(
        within(screen.getByRole('table', { name: 'Команды' })).queryByRole('link', {
          name: 'Платформа',
        }),
      ).not.toBeInTheDocument(),
    );
  });

  it('while the count is read a skeleton stands in for the text and «Отключить» is unavailable', async () => {
    const held = gate();
    const { dialog } = await openUnlink({
      before: () =>
        server.use(
          http.get(UNLINK_COUNT_URL, async () => {
            await held.opened;
            return undefined;
          }),
        ),
    });

    expect(within(dialog).getByRole('status', { name: 'Загрузка данных' })).toBeInTheDocument();
    expect(within(dialog).getByRole('button', { name: 'Отключить' })).toBeDisabled();

    held.open();
    expect(
      await within(dialog).findByText('Отключить команду «Платформа» от проекта «Пример проекта»?'),
    ).toBeVisible();
  });

  it('a failed count says so with «Повторить», and «Отключить» stays unavailable until it succeeds', async () => {
    const { dialog } = await openUnlink({
      before: () =>
        serveProblem('get', UNLINK_COUNT_URL, problem('organizations.access.forbidden'), {
          once: true,
        }),
    });

    const notice = await within(dialog).findByRole('alert');
    expect(within(dialog).getByRole('button', { name: 'Отключить' })).toBeDisabled();

    await userEvent.click(within(notice).getByRole('button', { name: 'Повторить' }));

    expect(
      await within(dialog).findByText('Отключить команду «Платформа» от проекта «Пример проекта»?'),
    ).toBeVisible();
  });

  it.each([
    ['organizations.team.not_in_project', 'Команда не участвует в проекте.'],
    ['organizations.team.not_found', 'Команда не найдена.'],
  ])(
    '%s is «%s» under the field, the choice is cleared and the dialog stays open',
    async (code, text) => {
      const { dialog } = await openUnlink({
        data: owning(2),
        before: () => serveProblem('post', UNLINK_URL, problem(code)),
      });
      await userEvent.click(
        await within(dialog).findByRole('combobox', { name: 'Передать рабочие элементы' }),
      );
      await userEvent.click(await screen.findByRole('option', { name: 'Пример проекта' }));

      await userEvent.click(within(dialog).getByRole('button', { name: 'Отключить' }));

      expect(await within(dialog).findByText(text)).toBeVisible();
      expect(
        screen.getByRole('dialog', { name: 'Отключение команды от проекта' }),
      ).toBeInTheDocument();
      expect(within(dialog).getByRole('button', { name: 'Отключить' })).toBeDisabled();
      // Design § 12: a refusal puts focus on the field it names.
      await waitFor(() =>
        expect(
          within(dialog).getByRole('combobox', { name: 'Передать рабочие элементы' }),
        ).toHaveFocus(),
      );
    },
  );

  it('DC-7: no notice of its own when the unlinked team is the switcher’s', async () => {
    const { dialog } = await openUnlink({
      context: contextSource({
        selection: { organizationId: ids.alfa, projectId: ids.prm, teamId: ids.platformTeam },
      }),
    });
    await within(dialog).findByText('Отключить команду «Платформа» от проекта «Пример проекта»?');

    await userEvent.click(within(dialog).getByRole('button', { name: 'Отключить' }));

    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
    expect(screen.queryByText('Команда отключена от проекта.')).not.toBeInTheDocument();
  });

  it.each([['«Отмена»'], ['Escape'], ['«Закрыть»']])('%s sends no request', async (how) => {
    const { api, dialog } = await openUnlink({ data: owning(3) });
    await within(dialog).findByRole('combobox', { name: 'Передать рабочие элементы' });

    if (how === 'Escape') {
      await userEvent.keyboard('{Escape}');
    } else {
      await userEvent.click(
        within(dialog).getByRole('button', { name: how === '«Отмена»' ? 'Отмена' : 'Закрыть' }),
      );
    }

    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
    expect(api.writes()).toEqual([]);
    expect(within(await teamsTable()).getByRole('link', { name: 'Платформа' })).toBeInTheDocument();
  });

  it('opens with focus on «Отмена» each time, under StrictMode, and gives it back to the row’s menu', async () => {
    serveOrganizations();
    renderPage({ path: PRM_TEAMS, strict: true });
    await teamsTable();

    for (const opening of ['first', 'second']) {
      const menu = await openMenu('Платформа');
      await userEvent.click(within(menu).getByRole('menuitem', { name: 'Отключить от проекта' }));
      const dialog = await screen.findByRole('dialog', { name: 'Отключение команды от проекта' });
      await within(dialog).findByText(/Отключить команду «Платформа»/);
      expect(document.activeElement, `${opening} opening`).toBe(
        within(dialog).getByRole('button', { name: 'Отмена' }),
      );

      await userEvent.keyboard('{Escape}');

      await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
      await waitFor(() =>
        expect(screen.getByRole('button', { name: 'Действия: Платформа' })).toHaveFocus(),
      );
    }
  });
});

describe('feat-028 BR-OL-7 Scenario: «Команда по умолчанию»', () => {
  async function openDialog() {
    await teamsTable();
    await userEvent.click(screen.getByRole('button', { name: 'Команда по умолчанию' }));
    return screen.findByRole('dialog', { name: 'Команда по умолчанию' });
  }

  it('names a linked team: «Сохранить» waits for a change, the body is the team, the mark moves, the notice reads', async () => {
    const api = serveOrganizations();
    renderPage({ path: PRM_TEAMS });
    const dialog = await openDialog();

    expect(dialog).toHaveTextContent(
      'Новые рабочие элементы проекта начинаются с этой команды, если их создают не со страницы команды и не как дочерние. Существующие рабочие элементы не меняются.',
    );
    const field = within(dialog).getByRole('combobox', { name: 'Команда' });
    expect(field).toHaveTextContent('Не выбрана');
    expect(within(dialog).getByRole('button', { name: 'Сохранить' })).toBeDisabled();

    await userEvent.click(field);
    expect(
      within(await screen.findByRole('listbox'))
        .getAllByRole('option')
        .map((option) => option.textContent),
    ).toEqual(['Не выбрана', 'Платформа', 'Пример проекта']);
    await userEvent.click(screen.getByRole('option', { name: 'Платформа' }));
    await userEvent.click(within(dialog).getByRole('button', { name: 'Сохранить' }));

    expect(await screen.findByText('Команда по умолчанию изменена.')).toBeInTheDocument();
    expect(api.writes()[0]).toMatchObject({ method: 'PUT', body: { teamId: ids.platformTeam } });
    await waitFor(() =>
      expect(
        within(screen.getByRole('table', { name: 'Команды' })).getByText('По умолчанию'),
      ).toBeInTheDocument(),
    );
  });

  it('«Не выбрана» clears the setting: the body is `teamId: null`', async () => {
    const api = serveOrganizations(withNamedDefault());
    renderPage({ path: PRM_TEAMS });
    const dialog = await openDialog();
    expect(within(dialog).getByRole('combobox', { name: 'Команда' })).toHaveTextContent(
      'Платформа',
    );

    await userEvent.click(within(dialog).getByRole('combobox', { name: 'Команда' }));
    await userEvent.click(await screen.findByRole('option', { name: 'Не выбрана' }));
    await userEvent.click(within(dialog).getByRole('button', { name: 'Сохранить' }));

    await screen.findByText('Команда по умолчанию изменена.');
    expect(api.writes()[0]?.body).toEqual({ teamId: null });
    await waitFor(() =>
      expect(
        within(screen.getByRole('table', { name: 'Команды' })).queryByText('По умолчанию'),
      ).toBeNull(),
    );
  });

  it('with one linked team, the dialog adds that the only team is the default', async () => {
    serveOrganizations();
    renderPage({ path: ANL_TEAMS });
    const dialog = await openDialog();

    expect(dialog).toHaveTextContent(
      'Пока в проекте одна команда, она и есть команда по умолчанию.',
    );
  });

  it.each([
    ['organizations.team.not_in_project', 'Команда не участвует в проекте.'],
    ['organizations.team.not_found', 'Команда не найдена.'],
  ])(
    '%s is «%s» under «Команда», the dialog stays open and the list is read again',
    async (code, text) => {
      const api = serveOrganizations();
      serveProblem('put', DEFAULT_TEAM_URL, problem(code));
      renderPage({ path: PRM_TEAMS });
      const dialog = await openDialog();
      const reads = () => api.requests.filter((request) => request.path.endsWith('/teams')).length;
      const before = reads();

      await userEvent.click(within(dialog).getByRole('combobox', { name: 'Команда' }));
      await userEvent.click(await screen.findByRole('option', { name: 'Платформа' }));
      await userEvent.click(within(dialog).getByRole('button', { name: 'Сохранить' }));

      expect(await within(dialog).findByText(text)).toBeInTheDocument();
      expect(screen.getByRole('dialog', { name: 'Команда по умолчанию' })).toBeInTheDocument();
      await waitFor(() => expect(reads()).toBeGreaterThan(before));
    },
  );

  it('is the holder of `project.write`’s alone, offered while a team is linked', async () => {
    serveOrganizations();
    renderPage({ path: PRM_TEAMS, role: 'member' });
    await teamsTable();

    expect(screen.queryByRole('button', { name: 'Команда по умолчанию' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Добавить команду' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /^Действия: / })).not.toBeInTheDocument();
  });
});
