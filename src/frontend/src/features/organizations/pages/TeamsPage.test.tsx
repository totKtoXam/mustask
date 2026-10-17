import { describe, expect, it } from 'vitest';
import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http } from 'msw';
import { gate } from '../../../test/gate';
import { server } from '../../../test/mocks/server';
import { PROJECT_URL, TEAM_URL, exampleDataset, ids, problem } from '../testFixtures';
import { serveOrganizations, serveProblem } from '../testApi';
import { contextSource, renderPage } from '../testRender';

const PRM_TEAMS = `/organizations/${ids.alfa}/projects/${ids.prm}/settings/teams`;
const ANL_TEAMS = `/organizations/${ids.alfa}/projects/${ids.anl}/settings/teams`;
const DEFAULT_TEAM_URL = `${PROJECT_URL}/default-team`;
const COUNT_URL = `${TEAM_URL}/owned-items/count`;

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

describe('mod-017 US-3, US-6 Scenario: The project’s teams', () => {
  it('loads with «Команда» alone, then lists the teams by name, no team first, no mark with two teams and no setting', async () => {
    serveOrganizations();
    renderPage({ path: PRM_TEAMS });

    const skeleton = screen.getByRole('status', { name: 'Загрузка данных' });
    expect(within(skeleton).getByText('Команда')).toBeInTheDocument();
    expect(within(skeleton).queryByText('Область по умолчанию')).not.toBeInTheDocument();

    const table = await teamsTable();
    const rows = within(table).getAllByRole('row').slice(1);
    expect(rows.map((row) => row.textContent)).toEqual(['Платформа', 'Пример проекта']);
    expect(within(table).queryByText(/По умолчанию/)).not.toBeInTheDocument();
    expect(within(table).queryByText('Владеет корневой областью')).not.toBeInTheDocument();
    expect(within(rows[0]!).getByRole('link', { name: 'Платформа' })).toHaveAttribute(
      'href',
      `/organizations/${ids.alfa}/projects/${ids.prm}/settings/teams/${ids.platformTeam}`,
    );
    expect(screen.getByRole('button', { name: 'Добавить команду' })).toHaveClass(
      'MuiButton-contained',
    );
    expect(screen.getByRole('button', { name: 'Команда по умолчанию' })).toBeVisible();
  });

  it('marks the named team «По умолчанию», on its row only', async () => {
    serveOrganizations(withNamedDefault());
    renderPage({ path: PRM_TEAMS });

    const table = await teamsTable();
    const rows = within(table).getAllByRole('row').slice(1);

    expect(rows.map((row) => row.textContent)).toEqual(['ПлатформаПо умолчанию', 'Пример проекта']);
  });

  it('marks a project’s only team «По умолчанию: единственная команда»', async () => {
    serveOrganizations();
    renderPage({ path: ANL_TEAMS });

    const table = await teamsTable();

    expect(within(table).getAllByRole('row').slice(1)[0]).toHaveTextContent(
      'АналитикаПо умолчанию: единственная команда',
    );
  });

  it('has no empty state, and answers «Проект не найден» for a deactivated project', async () => {
    serveOrganizations();
    renderPage({ path: `/organizations/${ids.alfa}/projects/${ids.old}/settings/teams` });

    expect(
      await screen.findByRole('heading', { level: 1, name: 'Проект не найден' }),
    ).toBeInTheDocument();
  });
});

describe('mod-017 US-7 Scenario: A row menu (no team is first; the last has no deletion)', () => {
  it('every team’s menu holds «Переименовать» and «Удалить команду», the deletion in the error colour', async () => {
    serveOrganizations();
    renderPage({ path: PRM_TEAMS });
    await teamsTable();

    for (const name of ['Платформа', 'Пример проекта']) {
      const menu = await openMenu(name);
      const items = within(menu).getAllByRole('menuitem');
      expect(items.map((item) => item.textContent)).toEqual(['Переименовать', 'Удалить команду']);
      expect(getComputedStyle(items[1]!).color).not.toBe(getComputedStyle(items[0]!).color);
      await userEvent.keyboard('{Escape}');
    }
  });

  it('the project’s only team’s menu holds «Переименовать» alone', async () => {
    serveOrganizations();
    renderPage({ path: ANL_TEAMS });
    await teamsTable();

    const menu = await openMenu('Аналитика');

    expect(
      within(menu)
        .getAllByRole('menuitem')
        .map((item) => item.textContent),
    ).toEqual(['Переименовать']);
  });
});

describe('mod-017 US-6 Scenario: A team is added with no area', () => {
  it('adds «Ядро» with the helper «От 1 до 100 символов.», lists it by name and notices «Команда «Ядро» добавлена.»', async () => {
    const api = serveOrganizations();
    renderPage({ path: PRM_TEAMS });
    await teamsTable();

    await userEvent.click(screen.getByRole('button', { name: 'Добавить команду' }));
    const dialog = await screen.findByRole('dialog', { name: 'Новая команда' });
    expect(within(dialog).getByText('От 1 до 100 символов.')).toBeInTheDocument();
    await userEvent.type(within(dialog).getByRole('textbox', { name: 'Название' }), 'Ядро');
    await userEvent.click(within(dialog).getByRole('button', { name: 'Добавить' }));

    expect(await screen.findByText('Команда «Ядро» добавлена.')).toBeInTheDocument();
    const table = await teamsTable();
    await waitFor(() =>
      expect(
        within(table)
          .getAllByRole('link')
          .map((link) => link.textContent),
      ).toEqual(['Платформа', 'Пример проекта', 'Ядро']),
    );
    expect(api.writes()[0]?.body).toEqual({ name: 'Ядро' });
  });

  it('puts «Команда с таким названием уже есть в проекте» on «Название»', async () => {
    serveOrganizations();
    renderPage({ path: PRM_TEAMS });
    await teamsTable();
    await userEvent.click(screen.getByRole('button', { name: 'Добавить команду' }));
    const dialog = await screen.findByRole('dialog', { name: 'Новая команда' });

    await userEvent.type(within(dialog).getByRole('textbox', { name: 'Название' }), 'платформа');
    await userEvent.click(within(dialog).getByRole('button', { name: 'Добавить' }));

    expect(
      await within(dialog).findByText('Команда с таким названием уже есть в проекте'),
    ).toBeInTheDocument();
  });
});

// mod-017 BR-OT-11: retired by name (doc-012 P-1) — «The new team’s area name must be free (and
// US-7: a deleted team’s area name is not)» — «puts «Область с таким названием уже есть на этом
// уровне» on «Название»»: adding a team creates no area.

describe('mod-017 US-6 Scenario: Renamed', () => {
  it('«Переименование команды» has no helper; the notice «Команда переименована.»', async () => {
    serveOrganizations();
    renderPage({ path: PRM_TEAMS });
    await teamsTable();
    const menu = await openMenu('Платформа');
    await userEvent.click(within(menu).getByRole('menuitem', { name: 'Переименовать' }));
    const dialog = await screen.findByRole('dialog', { name: 'Переименование команды' });
    expect(within(dialog).queryByText(/Названия областей/)).not.toBeInTheDocument();
    const field = within(dialog).getByRole('textbox', { name: 'Название' });
    expect(field).toHaveValue('Платформа');

    await userEvent.clear(field);
    await userEvent.type(field, 'Платформа 2');
    await userEvent.click(within(dialog).getByRole('button', { name: 'Сохранить' }));

    expect(await screen.findByText('Команда переименована.')).toBeInTheDocument();
    expect(
      await within(await teamsTable()).findByRole('link', { name: 'Платформа 2' }),
    ).toBeInTheDocument();
  });
});

describe('mod-017 US-3 Scenario: «Команда по умолчанию»', () => {
  async function openDialog() {
    await teamsTable();
    await userEvent.click(screen.getByRole('button', { name: 'Команда по умолчанию' }));
    return screen.findByRole('dialog', { name: 'Команда по умолчанию' });
  }

  it('names a team: «Сохранить» waits for a change, the body is the team, the mark moves, the notice reads', async () => {
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

  it('with one team, the dialog adds that the only team is the default', async () => {
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
});

describe('mod-017 US-7 Scenario: Deleted after a confirmation that counts the items', () => {
  /** `before` puts a one-off answer in front of the served ones (MSW takes the newest handler first). */
  async function openDelete(
    context = contextSource(),
    data = exampleDataset(),
    before: () => void = () => undefined,
  ) {
    const api = serveOrganizations(data);
    before();
    renderPage({ path: PRM_TEAMS, context });
    await teamsTable();
    const menu = await openMenu('Платформа');
    await userEvent.click(within(menu).getByRole('menuitem', { name: 'Удалить команду' }));
    return { api, dialog: await screen.findByRole('dialog', { name: 'Удаление команды' }) };
  }

  it('owns items: asks with the count, an error-coloured primary and focus on «Отмена»; the row leaves and the notice reads', async () => {
    const data = exampleDataset();
    data.ownedItemCounts = { [ids.platformTeam]: 5 };
    const { api, dialog } = await openDelete(contextSource(), data);
    expect(await within(dialog).findByText(/Её рабочие элементы/)).toHaveTextContent(
      'Удалить команду «Платформа»? Её рабочие элементы (5) станут «Без команды». Восстановить команду будет нельзя.',
    );
    const confirm = within(dialog).getByRole('button', { name: 'Удалить команду' });
    expect(confirm).toHaveClass('MuiButton-colorError');
    expect(within(dialog).getByRole('button', { name: 'Отмена' })).toHaveFocus();

    await userEvent.click(confirm);

    expect(await screen.findByText('Команда «Платформа» удалена.')).toBeInTheDocument();
    await waitFor(() =>
      expect(
        within(screen.getByRole('table', { name: 'Команды' })).queryByText('Платформа'),
      ).toBeNull(),
    );
    expect(api.writes()).toEqual([
      {
        method: 'DELETE',
        path: `/api/organizations/${ids.alfa}/projects/${ids.prm}/teams/${ids.platformTeam}`,
        body: undefined,
      },
    ]);
  });

  it('owns none: the text names no items', async () => {
    const { dialog } = await openDelete();

    expect(await within(dialog).findByText(/Восстановить команду будет нельзя/)).toHaveTextContent(
      /^Удалить команду «Платформа»\? Восстановить команду будет нельзя\.$/,
    );
  });

  it('while the count is read a skeleton stands for the text and «Удалить команду» is unavailable', async () => {
    const held = gate();
    const { dialog } = await openDelete(contextSource(), exampleDataset(), () =>
      server.use(
        http.get(COUNT_URL, async () => {
          await held.opened;
          return undefined;
        }),
      ),
    );

    expect(within(dialog).getByRole('status', { name: 'Загрузка данных' })).toBeInTheDocument();
    expect(within(dialog).getByRole('button', { name: 'Удалить команду' })).toBeDisabled();

    held.open();
    expect(await within(dialog).findByText(/Восстановить команду будет нельзя/)).toBeVisible();
    expect(within(dialog).getByRole('button', { name: 'Удалить команду' })).toBeEnabled();
  });

  it('a failed count says so with «Повторить», and the deletion stays unavailable until it succeeds', async () => {
    const { dialog } = await openDelete(contextSource(), exampleDataset(), () =>
      serveProblem('get', COUNT_URL, problem('organizations.access.forbidden'), { once: true }),
    );

    const notice = await within(dialog).findByRole('alert');
    expect(within(dialog).getByRole('button', { name: 'Удалить команду' })).toBeDisabled();

    await userEvent.click(within(notice).getByRole('button', { name: 'Повторить' }));

    expect(await within(dialog).findByText(/Восстановить команду будет нельзя/)).toBeVisible();
    expect(within(dialog).getByRole('button', { name: 'Удалить команду' })).toBeEnabled();
  });

  it('DC-7: no notice of its own when the deleted team is the switcher’s', async () => {
    const { dialog } = await openDelete(
      contextSource({
        selection: { organizationId: ids.alfa, projectId: ids.prm, teamId: ids.platformTeam },
      }),
    );
    await within(dialog).findByText(/Восстановить команду будет нельзя/);

    await userEvent.click(within(dialog).getByRole('button', { name: 'Удалить команду' }));

    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
    expect(screen.queryByText('Команда «Платформа» удалена.')).not.toBeInTheDocument();
  });

  it.each([
    [
      'organizations.team.last_team_cannot_be_deleted',
      'Нельзя удалить единственную команду проекта.',
    ],
    ['organizations.team.not_found', 'Команда не найдена.'],
  ])('a direct refusal %s shows «%s» in the dialog, which stays open', async (code, text) => {
    const { dialog } = await openDelete(contextSource(), exampleDataset(), () =>
      serveProblem('delete', TEAM_URL, problem(code)),
    );
    await within(dialog).findByText(/Восстановить команду будет нельзя/);

    await userEvent.click(within(dialog).getByRole('button', { name: 'Удалить команду' }));

    expect(await within(dialog).findByRole('alert')).toHaveTextContent(text);
  });
});

describe('mod-002 design § 4.3, qa.md D-2: from the row menu, under StrictMode', () => {
  it('opens «Удаление команды» with focus on «Отмена» each time, and gives it back to «Действия»', async () => {
    serveOrganizations();
    renderPage({ path: PRM_TEAMS, strict: true });
    await teamsTable();

    for (const opening of ['first', 'second']) {
      const menu = await openMenu('Платформа');
      await userEvent.click(within(menu).getByRole('menuitem', { name: 'Удалить команду' }));
      const dialog = await screen.findByRole('dialog', { name: 'Удаление команды' });
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

describe('mod-002 US-7 Scenario: Declining changes nothing', () => {
  it.each([['«Отмена»'], ['Escape'], ['«Закрыть»']])('%s sends no request', async (how) => {
    const api = serveOrganizations();
    renderPage({ path: PRM_TEAMS });
    await teamsTable();
    const menu = await openMenu('Платформа');
    await userEvent.click(within(menu).getByRole('menuitem', { name: 'Удалить команду' }));
    const dialog = await screen.findByRole('dialog', { name: 'Удаление команды' });

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
});

// mod-017 BR-OT-11: retired by name (doc-012 P-1) — «The first team cannot be deleted» — «a direct
// refusal shows «Нельзя удалить команду, которой принадлежит корневая область проекта» in the
// dialog»; its place is the last-team refusal above.
