import { afterEach, describe, expect, it, vi } from 'vitest';
import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { focusManager } from '@tanstack/react-query';
import { HttpResponse, http } from 'msw';
import { server } from '../../../test/mocks/server';
import { serveBoards, serveProblem, type BoardsTestApi } from '../testApi';
import { findBacklogTable, identifiers } from '../testBacklog';
import {
  TEAM_PROJECTS_URL,
  acrossProjectsDataset,
  catalogText,
  ids,
  problem,
} from '../testFixtures';
import { backlogAddress, renderBoards } from '../testRender';

// feat-040 US-5 (BR-TS-9; design § 6): creating from a team's backlog, the project chosen first. «Платформа»
// works on «Мастаск» and «Операции» (`acrossProjectsDataset`); «Аналитика» works on «Мастаск» alone,
// «Операции» (the team) on «Операции» alone. The dialog's fields are the kit's, which are slow in jsdom.

vi.setConfig({ testTimeout: 60_000 });

afterEach(() => {
  focusManager.setFocused(undefined);
});

const NO_PROJECT_AREA_LINE =
  'Рабочий элемент будет создан в статусе «Новая». Остальные поля заполняются в его форме.';
const HOLDING_CAPTION =
  'Встанет в конец бэклога команды «Платформа». Другую команду можно выбрать после выбора проекта.';

async function dialog() {
  return screen.findByRole('dialog', { name: 'Новый рабочий элемент' });
}

/** Opens the dialog for a user story from the header's primary. */
async function openDialog() {
  const [header] = await screen.findAllByRole('button', { name: 'Новый рабочий элемент' });
  if (!header) throw new Error('No «Новый рабочий элемент».');
  await userEvent.click(header);
  await userEvent.click(await screen.findByRole('menuitem', { name: 'Пользовательская история' }));
  return dialog();
}

function projectBox(found: HTMLElement) {
  return within(found).getByRole('combobox', { name: 'Проект' });
}

function teamBox(found: HTMLElement) {
  return within(found).getByRole('combobox', { name: 'Команда' });
}

async function pick(box: HTMLElement, name: string) {
  await userEvent.click(box);
  await userEvent.click(await screen.findByRole('option', { name }));
}

async function optionsOf(box: HTMLElement): Promise<string[]> {
  await userEvent.click(box);
  const names = within(await screen.findByRole('listbox'))
    .getAllByRole('option')
    .map((option) => option.textContent ?? '');
  await userEvent.keyboard('{Escape}');
  await waitFor(() => expect(screen.queryByRole('listbox')).toBeNull());
  return names;
}

function posts(api: BoardsTestApi) {
  return api.writes().filter((request) => request.path.endsWith('/workitems'));
}

describe('feat-040 US-5 Scenario: the project is chosen first (design § 6.2)', () => {
  it('opens with «Проект» first, required, none chosen when the team works on two and the filter names none; the area line, «Команда» read-only holding the team, and the caption say so', async () => {
    const api = serveBoards(acrossProjectsDataset());
    renderBoards(backlogAddress());
    await findBacklogTable();

    const found = await openDialog();

    const project = await waitFor(() => projectBox(found));
    expect(project).toHaveValue('');
    expect(project).toHaveAttribute('aria-required', 'true');
    expect(project).toHaveAttribute('placeholder', 'Найти проект');
    await waitFor(() => expect(project).toHaveFocus());
    expect(within(found).getByText(NO_PROJECT_AREA_LINE)).toBeVisible();
    const team = teamBox(found);
    expect(team).toHaveValue('Платформа');
    expect(team).toHaveAttribute('readonly');
    expect(within(found).getByText(HOLDING_CAPTION)).toBeVisible();
    // «Проект» stands before «Тип», «Команда» and «Заголовок».
    const fields = within(found).getAllByRole('combobox');
    expect(fields[0]).toBe(project);
    expect(posts(api)).toEqual([]);
  });

  it('offers the team’s linked projects by name, read afresh at every opening', async () => {
    const api = serveBoards(acrossProjectsDataset());
    renderBoards(backlogAddress());
    await findBacklogTable();

    const found = await openDialog();
    expect(await optionsOf(await waitFor(() => projectBox(found)))).toEqual([
      'Мастаск',
      'Операции',
    ]);

    await userEvent.click(within(found).getByRole('button', { name: 'Отмена' }));
    await waitFor(() => expect(screen.queryByRole('dialog')).toBeNull());
    const again = await openDialog();
    await waitFor(() => expect(projectBox(again)).toBeVisible());
    await waitFor(() =>
      expect(
        api
          .reads('/projects')
          .filter((request) => request.path.includes(`/teams/${ids.platformTeam}/`)),
      ).toHaveLength(2),
    );
  });

  it('is preselected when the team works on exactly one project; «Команда» then offers that project’s teams', async () => {
    serveBoards(acrossProjectsDataset());
    renderBoards(backlogAddress('stories', ids.analyticsTeam));
    await findBacklogTable();

    const found = await openDialog();

    await waitFor(() => expect(projectBox(found)).toHaveValue('Мастаск'));
    await waitFor(() => expect(teamBox(found)).toHaveValue('Аналитика'));
    expect(teamBox(found)).not.toHaveAttribute('readonly');
    expect(
      await within(found).findByText(
        'Рабочий элемент будет создан в статусе «Новая» в области «Мастаск». Остальные поля заполняются в его форме.',
      ),
    ).toBeVisible();
    await waitFor(() =>
      expect(within(found).getByRole('textbox', { name: 'Заголовок' })).toHaveFocus(),
    );
  });

  it('is preselected when the project filter names exactly one of the team’s projects, and not when it names two', async () => {
    serveBoards(acrossProjectsDataset());
    const view = renderBoards(`${backlogAddress()}?projectId=${ids.ops}`);
    await findBacklogTable();

    const found = await openDialog();
    await waitFor(() => expect(projectBox(found)).toHaveValue('Операции'));
    await waitFor(() =>
      expect(within(found).getByRole('textbox', { name: 'Заголовок' })).toHaveFocus(),
    );
    expect(teamBox(found)).not.toHaveAttribute('readonly');
    await userEvent.click(within(found).getByRole('button', { name: 'Отмена' }));
    view.unmount();

    serveBoards(acrossProjectsDataset());
    renderBoards(`${backlogAddress()}?projectId=${ids.ops}&projectId=${ids.msk}`);
    await findBacklogTable();
    const two = await openDialog();
    await waitFor(() => expect(projectBox(two)).toHaveValue(''));
  });

  it('a filter value that names no project of the team preselects nothing', async () => {
    serveBoards(acrossProjectsDataset());
    renderBoards(`${backlogAddress()}?projectId=not-a-project`);
    await screen.findByRole('heading', { level: 2, name: 'Ничего не найдено' });

    const [header] = await screen.findAllByRole('button', { name: 'Новый рабочий элемент' });
    await userEvent.click(header as HTMLElement);
    await userEvent.click(
      await screen.findByRole('menuitem', { name: 'Пользовательская история' }),
    );
    const found = await dialog();

    await waitFor(() => expect(projectBox(found)).toHaveValue(''));
  });

  it('a project chosen: the area line names its root, «Команда» offers «Без команды» and that project’s teams, and the caption follows the team', async () => {
    serveBoards(acrossProjectsDataset());
    renderBoards(backlogAddress());
    await findBacklogTable();
    const found = await openDialog();

    await pick(await waitFor(() => projectBox(found)), 'Операции');

    expect(
      await within(found).findByText(
        'Рабочий элемент будет создан в статусе «Новая» в области «Операции». Остальные поля заполняются в его форме.',
      ),
    ).toBeVisible();
    await waitFor(() => expect(teamBox(found)).not.toHaveAttribute('readonly'));
    expect(await optionsOf(teamBox(found))).toEqual(['Без команды', 'Операции', 'Платформа']);
    expect(teamBox(found)).toHaveValue('Платформа');
    expect(within(found).getByText('Встанет в конец бэклога команды «Платформа».')).toBeVisible();
    expect(within(found).queryByText(HOLDING_CAPTION)).toBeNull();
  });
});

describe('feat-040 US-5 Scenario: «Создать» without a project is refused at the field', () => {
  it('shows «Выберите проект» under «Проект», keeps the focus on it, sends nothing, and clears it at the next pick', async () => {
    const api = serveBoards(acrossProjectsDataset());
    renderBoards(backlogAddress());
    await findBacklogTable();
    const found = await openDialog();
    await userEvent.type(within(found).getByRole('textbox', { name: 'Заголовок' }), 'Экспорт');

    await userEvent.click(within(found).getByRole('button', { name: 'Создать' }));

    expect(await within(found).findByText('Выберите проект')).toBeVisible();
    expect(projectBox(found)).toHaveAttribute('aria-invalid', 'true');
    await waitFor(() => expect(projectBox(found)).toHaveFocus());
    expect(posts(api)).toEqual([]);

    await pick(projectBox(found), 'Мастаск');
    await waitFor(() => expect(within(found).queryByText('Выберите проект')).toBeNull());
  });

  it('refuses both a missing project and a missing title: both errors show and the focus takes «Проект»', async () => {
    const api = serveBoards(acrossProjectsDataset());
    renderBoards(backlogAddress());
    await findBacklogTable();
    const found = await openDialog();
    await waitFor(() => expect(projectBox(found)).toHaveFocus());
    await userEvent.click(within(found).getByRole('textbox', { name: 'Заголовок' }));

    await userEvent.click(within(found).getByRole('button', { name: 'Создать' }));

    expect(await within(found).findByText('Выберите проект')).toBeVisible();
    expect(within(found).getByRole('textbox', { name: 'Заголовок' })).toHaveAttribute(
      'aria-invalid',
      'true',
    );
    await waitFor(() => expect(projectBox(found)).toHaveFocus());
    expect(posts(api)).toEqual([]);
  });
});

describe('feat-040 US-5 Scenario: the item is created in the chosen project (BR-TS-9)', () => {
  it('posts to the chosen project with the team shown, takes its next number there, and stands last on the team’s backlog', async () => {
    const api = serveBoards(acrossProjectsDataset());
    renderBoards(backlogAddress());
    const table = await findBacklogTable();
    const found = await openDialog();
    await pick(await waitFor(() => projectBox(found)), 'Операции');
    await waitFor(() => expect(teamBox(found)).not.toHaveAttribute('readonly'));
    await userEvent.type(
      within(found).getByRole('textbox', { name: 'Заголовок' }),
      'Отгрузка по заказу',
    );

    await userEvent.click(within(found).getByRole('button', { name: 'Создать' }));

    expect(await screen.findByText('Рабочий элемент OPS-US-5 создан.')).toBeInTheDocument();
    expect(screen.queryByRole('dialog')).toBeNull();
    await waitFor(() =>
      expect(identifiers(table)).toEqual(['MSK-US-3', 'OPS-US-1', 'OPS-US-2', 'OPS-US-5']),
    );
    const [post] = posts(api);
    expect(post?.path).toBe(`/api/organizations/${ids.example}/projects/${ids.ops}/workitems`);
    expect(post?.body).toMatchObject({
      title: 'Отгрузка по заказу',
      team: { teamId: ids.platformTeam },
    });
    // Unplaced on both backlogs: the team's order and the project's are untouched.
    expect(api.data.places).toEqual([]);
    expect(api.data.projectPlaces).toEqual([]);
    expect(api.item(ids.ops, 5).teamId).toBe(ids.platformTeam);
  });

  it('a team chosen from the project’s teams, or «Без команды», is what the item is owned by', async () => {
    const api = serveBoards(acrossProjectsDataset());
    renderBoards(backlogAddress());
    await findBacklogTable();
    const found = await openDialog();
    await pick(await waitFor(() => projectBox(found)), 'Мастаск');
    await waitFor(() => expect(teamBox(found)).not.toHaveAttribute('readonly'));
    await pick(teamBox(found), 'Без команды');
    expect(within(found).getByText('Попадёт только в бэклог проекта.')).toBeVisible();
    await userEvent.type(within(found).getByRole('textbox', { name: 'Заголовок' }), 'Ничья');

    await userEvent.click(within(found).getByRole('button', { name: 'Создать' }));

    expect(
      await screen.findByText('Рабочий элемент MSK-US-10 создан. Он не принадлежит этой команде.'),
    ).toBeInTheDocument();
    expect(posts(api)[0]?.body).toMatchObject({ team: { teamId: null } });
  });

  it('names the other team when the item is owned by one: «Он не принадлежит этой команде.»', async () => {
    serveBoards(acrossProjectsDataset());
    renderBoards(backlogAddress());
    await findBacklogTable();
    const found = await openDialog();
    await pick(await waitFor(() => projectBox(found)), 'Мастаск');
    await waitFor(() => expect(teamBox(found)).not.toHaveAttribute('readonly'));
    await pick(teamBox(found), 'Аналитика');
    await userEvent.type(within(found).getByRole('textbox', { name: 'Заголовок' }), 'Чужая');

    await userEvent.click(within(found).getByRole('button', { name: 'Создать' }));

    expect(
      await screen.findByText('Рабочий элемент MSK-US-10 создан. Он не принадлежит этой команде.'),
    ).toBeInTheDocument();
  });

  it('names the filter when it hides the item’s project: «Он не подходит под текущий фильтр.»', async () => {
    serveBoards(acrossProjectsDataset());
    renderBoards(`${backlogAddress()}?projectId=${ids.msk}`);
    await findBacklogTable();
    const found = await openDialog();
    await pick(await waitFor(() => projectBox(found)), 'Операции');
    await userEvent.type(within(found).getByRole('textbox', { name: 'Заголовок' }), 'Скрытая');

    await userEvent.click(within(found).getByRole('button', { name: 'Создать' }));

    expect(
      await screen.findByText(
        'Рабочий элемент OPS-US-5 создан. Он не подходит под текущий фильтр.',
      ),
    ).toBeInTheDocument();
  });
});

describe('feat-040 US-5 Scenario: a project that drops the team shown sets it back (D-5)', () => {
  it('«Аналитика» chosen in «Мастаск», then «Операции»: «Команда» reads «Платформа» again, the line replaces the caption and is announced, and the next pick clears it', async () => {
    serveBoards(acrossProjectsDataset());
    renderBoards(backlogAddress());
    await findBacklogTable();
    const found = await openDialog();
    await pick(await waitFor(() => projectBox(found)), 'Мастаск');
    await waitFor(() => expect(teamBox(found)).not.toHaveAttribute('readonly'));
    await pick(teamBox(found), 'Аналитика');
    expect(teamBox(found)).toHaveValue('Аналитика');
    expect(within(found).getByText('Встанет в конец бэклога команды «Аналитика».')).toBeVisible();

    await pick(projectBox(found), 'Операции');

    const line = 'Команда «Аналитика» не участвует в проекте «Операции». Выбрана «Платформа».';
    await waitFor(() => expect(teamBox(found)).toHaveValue('Платформа'));
    // The caption's place holds the line, which a polite region speaks too.
    expect(within(found).getAllByText(line).length).toBeGreaterThanOrEqual(1);
    expect(
      within(found)
        .getAllByRole('status')
        .some((region) => region.textContent === line),
    ).toBe(true);
    expect(within(found).queryByText('Встанет в конец бэклога команды «Платформа».')).toBeNull();

    await pick(teamBox(found), 'Операции');
    await waitFor(() => expect(within(found).queryByText(line)).toBeNull());
    expect(within(found).getByText('Встанет в конец бэклога команды «Операции».')).toBeVisible();
  });

  it('keeps «Без команды» across projects: it is linked to none and is valid for any', async () => {
    serveBoards(acrossProjectsDataset());
    renderBoards(backlogAddress());
    await findBacklogTable();
    const found = await openDialog();
    await pick(await waitFor(() => projectBox(found)), 'Мастаск');
    await waitFor(() => expect(teamBox(found)).not.toHaveAttribute('readonly'));
    await pick(teamBox(found), 'Без команды');

    await pick(projectBox(found), 'Операции');

    await waitFor(() =>
      expect(within(found).getByText('Попадёт только в бэклог проекта.')).toBeVisible(),
    );
    expect(teamBox(found)).toHaveValue('Без команды');
    expect(within(found).queryByText(/не участвует в проекте/)).toBeNull();
  });

  it('keeps a team linked to both projects when the project changes', async () => {
    serveBoards(acrossProjectsDataset());
    renderBoards(backlogAddress());
    await findBacklogTable();
    const found = await openDialog();
    await pick(await waitFor(() => projectBox(found)), 'Мастаск');
    await waitFor(() => expect(teamBox(found)).toHaveValue('Платформа'));

    await pick(projectBox(found), 'Операции');

    await waitFor(() =>
      expect(
        within(found).getByText(
          'Рабочий элемент будет создан в статусе «Новая» в области «Операции». Остальные поля заполняются в его форме.',
        ),
      ).toBeVisible(),
    );
    expect(teamBox(found)).toHaveValue('Платформа');
    expect(within(found).queryByText(/не участвует в проекте/)).toBeNull();
  });
});

describe('feat-040 US-5 Scenario: the save refuses, and the dialog stays open (BR-TS-9)', () => {
  it('a project deactivated meanwhile: «Проект не найден.» under «Проект», the value kept, the projects read again so it is no longer offered', async () => {
    const api = serveBoards(acrossProjectsDataset());
    renderBoards(backlogAddress());
    await findBacklogTable();
    const found = await openDialog();
    await pick(await waitFor(() => projectBox(found)), 'Операции');
    await userEvent.type(within(found).getByRole('textbox', { name: 'Заголовок' }), 'Опоздала');
    const ops = api.data.projects.find((project) => project.id === ids.ops);
    if (!ops) throw new Error('No project.');
    ops.active = false;

    await userEvent.click(within(found).getByRole('button', { name: 'Создать' }));

    expect(
      await within(found).findByText(catalogText('organizations.project.not_found')),
    ).toBeVisible();
    expect(screen.getByRole('dialog', { name: 'Новый рабочий элемент' })).toBeVisible();
    expect(projectBox(found)).toHaveValue('Операции');
    expect(within(found).getByRole('textbox', { name: 'Заголовок' })).toHaveValue('Опоздала');
    await waitFor(() => expect(projectBox(found)).toHaveFocus());
    await waitFor(() => {
      const reads = api.reads('/projects').filter((request) => request.path.includes('/teams/'));
      expect(reads.length).toBeGreaterThanOrEqual(2);
    });
    expect(await optionsOf(projectBox(found))).toEqual(['Мастаск']);
  });

  it('a team unlinked meanwhile: «Команда не участвует в проекте.» under «Команда», the dialog open, its options read again', async () => {
    const api = serveBoards(acrossProjectsDataset());
    renderBoards(backlogAddress('stories', ids.analyticsTeam));
    await findBacklogTable();
    const found = await openDialog();
    await waitFor(() => expect(projectBox(found)).toHaveValue('Мастаск'));
    await waitFor(() => expect(teamBox(found)).toHaveValue('Аналитика'));
    await userEvent.type(within(found).getByRole('textbox', { name: 'Заголовок' }), 'Отвязана');
    // Another user unlinks «Мастаск» from «Аналитика»: its project becomes «Операции».
    const team = api.data.teams.find((candidate) => candidate.id === ids.analyticsTeam);
    if (!team) throw new Error('No team.');
    team.projectId = ids.ops;

    await userEvent.click(within(found).getByRole('button', { name: 'Создать' }));

    expect(
      await within(found).findByText(catalogText('organizations.team.not_in_project')),
    ).toBeVisible();
    expect(screen.getByRole('dialog', { name: 'Новый рабочий элемент' })).toBeVisible();
    await waitFor(() => expect(teamBox(found)).toHaveFocus());
    expect(posts(api).length).toBe(1);
    expect(api.data.items.filter((record) => record.title === 'Отвязана')).toEqual([]);
  });

  it('a team deleted meanwhile: «Команда не найдена.» under «Команда», nothing created', async () => {
    const api = serveBoards(acrossProjectsDataset());
    renderBoards(backlogAddress());
    await findBacklogTable();
    const found = await openDialog();
    await pick(await waitFor(() => projectBox(found)), 'Мастаск');
    await waitFor(() => expect(teamBox(found)).not.toHaveAttribute('readonly'));
    await pick(teamBox(found), 'Аналитика');
    await userEvent.type(within(found).getByRole('textbox', { name: 'Заголовок' }), 'Удалена');
    api.deleteTeam(ids.analyticsTeam);

    await userEvent.click(within(found).getByRole('button', { name: 'Создать' }));

    expect(
      await within(found).findByText(catalogText('organizations.team.not_found')),
    ).toBeVisible();
    expect(screen.getByRole('dialog', { name: 'Новый рабочий элемент' })).toBeVisible();
    expect(api.data.items.filter((record) => record.title === 'Удалена')).toEqual([]);
  });
});

describe('feat-040 US-5 Scenario: the states of «Проект» (design § 6.4)', () => {
  it('a failed read of the projects shows the server’s text with «Повторить», and «Создать» waits; the retry brings the field', async () => {
    serveBoards(acrossProjectsDataset());
    serveProblem('get', TEAM_PROJECTS_URL, problem('organizations.team.not_found'), { once: true });
    renderBoards(backlogAddress());
    await findBacklogTable();

    const found = await openDialog();

    expect(
      await within(found).findByText(catalogText('organizations.team.not_found')),
    ).toBeVisible();
    expect(within(found).getByRole('button', { name: 'Создать' })).toBeDisabled();
    await userEvent.click(within(found).getByRole('button', { name: 'Повторить' }));
    await waitFor(() => expect(projectBox(found)).toBeVisible());
    expect(within(found).getByRole('button', { name: 'Создать' })).toBeEnabled();
  });

  it('while the projects are read, a skeleton stands in the field’s place, «Создать» waits and the title takes typing', async () => {
    serveBoards(acrossProjectsDataset());
    let release: () => void = () => {};
    const held = new Promise<void>((resolve) => {
      release = resolve;
    });
    server.use(
      http.get(TEAM_PROJECTS_URL, async () => {
        await held;
        return undefined;
      }),
    );
    renderBoards(backlogAddress());
    await findBacklogTable();

    const found = await openDialog();

    expect(within(found).queryByRole('combobox', { name: 'Проект' })).toBeNull();
    expect(within(found).getByRole('button', { name: 'Создать' })).toBeDisabled();
    await userEvent.type(
      within(found).getByRole('textbox', { name: 'Заголовок' }),
      'Пока грузится',
    );
    expect(within(found).getByRole('textbox', { name: 'Заголовок' })).toHaveValue('Пока грузится');

    release();
    await waitFor(() => expect(projectBox(found)).toBeVisible());
  });

  it('a failed read of the chosen project’s teams says so in «Команда»’s list', async () => {
    serveBoards(acrossProjectsDataset());
    server.use(
      http.get(
        `${'http://localhost:5000'}/api/organizations/:organizationId/projects/${ids.ops}/teams`,
        () => HttpResponse.json(problem('organizations.validation_failed'), { status: 400 }),
      ),
    );
    renderBoards(backlogAddress());
    await findBacklogTable();
    const found = await openDialog();

    await pick(await waitFor(() => projectBox(found)), 'Операции');

    await waitFor(() => expect(teamBox(found)).not.toHaveAttribute('readonly'));
    await userEvent.click(teamBox(found));
    expect(
      await screen.findByText('Не удалось загрузить команды. Попробуйте ещё раз.'),
    ).toBeVisible();
  });
});

describe('feat-040 US-5 Scenario: who is offered the command (BR-TS-9, design § 6.1)', () => {
  it('a team that works on no active project has no command, in the head or in the empty frame, and no filter row', async () => {
    const data = acrossProjectsDataset();
    const ops = data.projects.find((project) => project.id === ids.ops);
    if (!ops) throw new Error('No project.');
    ops.active = false;
    serveBoards(data);
    renderBoards(backlogAddress('stories', ids.opsTeam));

    expect(await screen.findByRole('heading', { level: 2, name: 'Бэклог пуст' })).toBeVisible();
    expect(screen.getByText('Команда не участвует ни в одном проекте.')).toBeVisible();
    expect(screen.queryByRole('button', { name: 'Новый рабочий элемент' })).toBeNull();
    expect(screen.queryByRole('group', { name: 'Фильтры' })).toBeNull();
  });

  it('a reader is offered none, however many projects the team has', async () => {
    serveBoards(acrossProjectsDataset());
    renderBoards(backlogAddress(), { role: 'viewer' });
    await findBacklogTable();

    expect(screen.queryByRole('button', { name: 'Новый рабочий элемент' })).toBeNull();
  });

  it.each([['admin'], ['member']] as const)('the %s is offered it', async (role) => {
    serveBoards(acrossProjectsDataset());
    renderBoards(backlogAddress(), { role });
    await findBacklogTable();

    expect(await screen.findByRole('button', { name: 'Новый рабочий элемент' })).toBeVisible();
  });
});
