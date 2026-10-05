import { describe, expect, it } from 'vitest';
import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { organizationsKeys } from '../api/queryKeys';
import { TEAM_URL, ids, problem } from '../testFixtures';
import { serveOrganizations, serveProblem } from '../testApi';
import { contextSource, renderPage } from '../testRender';

const PRM_TEAMS = `/organizations/${ids.alfa}/projects/${ids.prm}/settings/teams`;

async function teamsTable() {
  return screen.findByRole('table', { name: 'Команды' });
}

async function openMenu(name: string) {
  await userEvent.click(screen.getByRole('button', { name: `Действия: ${name}` }));
  return screen.findByRole('menu');
}

describe('mod-002 US-4 Scenario: The project’s teams', () => {
  it('loads with «Команда» and «Область по умолчанию», then lists the first team first, its chip only on its row', async () => {
    serveOrganizations();
    renderPage({ path: PRM_TEAMS });

    const skeleton = screen.getByRole('status', { name: 'Загрузка данных' });
    expect(within(skeleton).getByText('Область по умолчанию')).toBeInTheDocument();

    const table = await teamsTable();
    const rows = within(table).getAllByRole('row').slice(1);
    expect(rows.map((row) => row.textContent)).toEqual([
      'Пример проектаВладеет корневой областьюПример проекта',
      'ПлатформаПлатформа / API',
    ]);
    expect(within(table).getAllByText('Владеет корневой областью')).toHaveLength(1);
    expect(within(rows[1]!).getByRole('link', { name: 'Платформа' })).toHaveAttribute(
      'href',
      `/organizations/${ids.alfa}/projects/${ids.prm}/settings/teams/${ids.platformTeam}`,
    );
    expect(screen.getByRole('button', { name: 'Добавить команду' })).toHaveClass(
      'MuiButton-contained',
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

describe('mod-002 US-4 Scenario: The first team’s default is the root (its row menu)', () => {
  it('the first team’s menu holds «Переименовать» only; another team’s adds the default area and the deletion', async () => {
    serveOrganizations();
    renderPage({ path: PRM_TEAMS });
    await teamsTable();

    const first = await openMenu('Пример проекта');
    expect(
      within(first)
        .getAllByRole('menuitem')
        .map((item) => item.textContent),
    ).toEqual(['Переименовать']);
    await userEvent.keyboard('{Escape}');

    const other = await openMenu('Платформа');
    const items = within(other).getAllByRole('menuitem');
    expect(items.map((item) => item.textContent)).toEqual([
      'Переименовать',
      'Изменить область по умолчанию',
      'Удалить команду',
    ]);
    expect(getComputedStyle(items[2]!).color).not.toBe(getComputedStyle(items[0]!).color);
  });
});

describe('mod-002 US-4 Scenario: A second team with its own area', () => {
  it('adds «Ядро» with the helper, lists it in order, notices «Команда «Ядро» добавлена.» and reads the tree again', async () => {
    const api = serveOrganizations();
    const { queryClient } = renderPage({ path: PRM_TEAMS });
    await teamsTable();
    queryClient.setQueryData(organizationsKeys.areas(ids.alfa, ids.prm), []);

    await userEvent.click(screen.getByRole('button', { name: 'Добавить команду' }));
    const dialog = await screen.findByRole('dialog', { name: 'Новая команда' });
    expect(
      within(dialog).getByText(
        'От 1 до 100 символов. Под корневой областью появится область с тем же названием, и она станет областью по умолчанию команды.',
      ),
    ).toBeInTheDocument();
    await userEvent.type(within(dialog).getByRole('textbox', { name: 'Название' }), 'Ядро');
    await userEvent.click(within(dialog).getByRole('button', { name: 'Добавить' }));

    expect(await screen.findByText('Команда «Ядро» добавлена.')).toBeInTheDocument();
    const table = await teamsTable();
    await waitFor(() =>
      expect(
        within(table)
          .getAllByRole('link')
          .map((link) => link.textContent),
      ).toEqual(['Пример проекта', 'Платформа', 'Ядро']),
    );
    expect(api.writes()[0]?.body).toEqual({ name: 'Ядро' });
    expect(
      queryClient.getQueryState(organizationsKeys.areas(ids.alfa, ids.prm))?.isInvalidated,
    ).toBe(true);
  });
});

describe('mod-002 US-4 Scenario: Unique among active teams', () => {
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

describe('mod-002 US-4 Scenario: The new team’s area name must be free (and US-7: a deleted team’s area name is not)', () => {
  it('puts «Область с таким названием уже есть на этом уровне» on «Название»', async () => {
    serveOrganizations();
    renderPage({ path: PRM_TEAMS });
    await teamsTable();
    await userEvent.click(screen.getByRole('button', { name: 'Добавить команду' }));
    const dialog = await screen.findByRole('dialog', { name: 'Новая команда' });

    await userEvent.type(
      within(dialog).getByRole('textbox', { name: 'Название' }),
      'Мобильное приложение',
    );
    await userEvent.click(within(dialog).getByRole('button', { name: 'Добавить' }));

    expect(
      await within(dialog).findByText('Область с таким названием уже есть на этом уровне'),
    ).toBeInTheDocument();
  });
});

describe('mod-002 US-4 Scenario: Renamed', () => {
  it('«Переименование команды» with «Названия областей команды не изменятся.»; the notice «Команда переименована.»', async () => {
    serveOrganizations();
    renderPage({ path: PRM_TEAMS });
    await teamsTable();
    const menu = await openMenu('Платформа');
    await userEvent.click(within(menu).getByRole('menuitem', { name: 'Переименовать' }));
    const dialog = await screen.findByRole('dialog', { name: 'Переименование команды' });
    expect(within(dialog).getByText('Названия областей команды не изменятся.')).toBeInTheDocument();
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

describe('mod-002 US-7 Scenario: Deleted after a confirmation', () => {
  async function openDelete(context = contextSource()) {
    const api = serveOrganizations();
    renderPage({ path: PRM_TEAMS, context });
    await teamsTable();
    const menu = await openMenu('Платформа');
    await userEvent.click(within(menu).getByRole('menuitem', { name: 'Удалить команду' }));
    return { api, dialog: await screen.findByRole('dialog', { name: 'Удаление команды' }) };
  }

  it('asks verbatim, with an error-coloured primary and focus on «Отмена»; the row leaves and the notice reads', async () => {
    const { api, dialog } = await openDelete();
    expect(dialog).toHaveTextContent(
      'Удалить команду «Платформа»? Её области перейдут команде «Пример проекта». Восстановить команду будет нельзя.',
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

  it('DC-7: no notice of its own when the deleted team is the switcher’s', async () => {
    const { dialog } = await openDelete(
      contextSource({
        selection: { organizationId: ids.alfa, projectId: ids.prm, teamId: ids.platformTeam },
      }),
    );

    await userEvent.click(within(dialog).getByRole('button', { name: 'Удалить команду' }));

    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
    expect(screen.queryByText('Команда «Платформа» удалена.')).not.toBeInTheDocument();
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

describe('mod-002 US-7 Scenario: The first team cannot be deleted', () => {
  it('a direct refusal shows «Нельзя удалить команду, которой принадлежит корневая область проекта» in the dialog', async () => {
    serveOrganizations();
    serveProblem('delete', TEAM_URL, problem('organizations.team.first_team_cannot_be_deleted'));
    renderPage({ path: PRM_TEAMS });
    await teamsTable();
    const menu = await openMenu('Платформа');
    await userEvent.click(within(menu).getByRole('menuitem', { name: 'Удалить команду' }));
    const dialog = await screen.findByRole('dialog', { name: 'Удаление команды' });

    await userEvent.click(within(dialog).getByRole('button', { name: 'Удалить команду' }));

    expect(await within(dialog).findByRole('alert')).toHaveTextContent(
      'Нельзя удалить команду, которой принадлежит корневая область проекта',
    );
  });
});
