import { afterEach, describe, expect, it, vi } from 'vitest';
import { act, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { focusManager } from '@tanstack/react-query';
import { HttpResponse, http } from 'msw';
import { server } from '../../../test/mocks/server';
import { serveBoards } from '../testApi';
import { findBacklogTable, identifiers, rowOf, titleLink } from '../testBacklog';
import {
  ORG_PROJECTS_URL,
  PROBLEM_HEADERS,
  acrossProjectsDataset,
  ids,
  problem,
  projectDataset,
} from '../testFixtures';
import { backlogAddress, projectBacklogAddress, renderBoards, teamContext } from '../testRender';
import { here, projectField, projectOptions } from '../testProjectFilter';

// feat-040 QA (qa.md, Frontend): what the developer's tests leave open. A card opened over a filtered team backlog keeps the
// project filter beside `?workitem=`; a project the list still holds but the server calls gone is the work item's frame; the
// head's menu and the filter hold the NFR's 200 entries and are reached whole by keyboard; a team deleted while its page is open.

vi.setConfig({ testTimeout: 60_000 });

afterEach(() => {
  focusManager.setFocused(undefined);
});

const pad = (value: number, width = 12) => String(value).padStart(width, '0');

describe('feat-040 QA: the card over a filtered team backlog (BR-TS-4, BR-TS-8)', () => {
  it('keeps `projectId` beside `?workitem=` while the card is open, and gives the filtered address back on close', async () => {
    serveBoards(acrossProjectsDataset());
    renderBoards(`${backlogAddress()}?projectId=${ids.ops}`);
    const table = await findBacklogTable();
    expect(identifiers(table)).toEqual(['OPS-US-1', 'OPS-US-2']);

    await userEvent.click(titleLink(rowOf(table, 'OPS-US-1')));
    await screen.findByRole('dialog', { name: 'Пользовательская история OPS-US-1' });

    const open = new URLSearchParams(here().search);
    expect(open.getAll('projectId')).toEqual([ids.ops]);
    expect(open.get('workitem')).toBe('OPS-US-1');

    await userEvent.keyboard('{Escape}');

    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
    expect(here().search).toBe(`?projectId=${ids.ops}`);
    expect(identifiers(table)).toEqual(['OPS-US-1', 'OPS-US-2']);
  });

  it('a card opened by address with the filter on closes to the filtered address', async () => {
    serveBoards(acrossProjectsDataset());
    renderBoards(`${backlogAddress()}?projectId=${ids.msk}&workitem=OPS-US-2`);
    const dialog = await screen.findByRole('dialog', { name: 'Пользовательская история OPS-US-2' });
    expect(within(dialog).getByRole('textbox', { name: 'Проект' })).toHaveValue('Операции');

    await userEvent.keyboard('{Escape}');

    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
    expect(here().search).toBe(`?projectId=${ids.msk}`);
  });

  it('an item read answered «Проект не найден.» for a project the list still holds is the work item’s frame, naming no project', async () => {
    serveBoards(acrossProjectsDataset());
    server.use(
      http.get(`${ORG_PROJECTS_URL}/:projectId/workitems/:identifier`, () =>
        HttpResponse.json(problem('organizations.project.not_found'), {
          status: 404,
          headers: PROBLEM_HEADERS,
        }),
      ),
    );
    renderBoards(`${backlogAddress()}?workitem=OPS-US-1`);

    expect(await screen.findByText('Рабочий элемент не найден')).toBeVisible();
    expect(screen.getByText('Проверьте адрес или закройте окно.')).toBeVisible();
    expect(screen.queryByText('Проект не найден')).toBeNull();
    expect(screen.queryByText('Проект не найден.')).toBeNull();
  });
});

describe('feat-040 QA: a project’s own views read the card in the page’s project (BR-TS-4, feat-028’s re-check)', () => {
  it('another project’s identifier at a project’s backlog is the not-found frame, and the read goes to the page’s project', async () => {
    const api = serveBoards(projectDataset());
    renderBoards(`${projectBacklogAddress()}?workitem=OPS-US-1`);

    expect(await screen.findByText('Рабочий элемент не найден')).toBeVisible();
    expect(api.reads('/workitems/').map((request) => request.path)).toEqual([
      `/api/organizations/${ids.example}/projects/${ids.msk}/workitems/OPS-US-1`,
    ]);
    // The project’s views do not look the key up among the organization’s projects.
    expect(api.reads('/projects').some((request) => request.path.endsWith('/projects'))).toBe(
      false,
    );
  });
});

describe('feat-040 QA: a team deleted while its page is open', () => {
  it('the next read answers «Команда не найдена»: the page gives way to the frame, naming no project', async () => {
    const api = serveBoards(acrossProjectsDataset());
    renderBoards(backlogAddress());
    await findBacklogTable();

    api.deleteTeam(ids.platformTeam);
    act(() => {
      focusManager.setFocused(false);
      focusManager.setFocused(true);
    });

    expect(
      await screen.findByRole('heading', { level: 1, name: 'Команда не найдена' }),
    ).toBeVisible();
    expect(screen.queryByRole('table', { name: 'Бэклог' })).toBeNull();
  });
});

describe('feat-040 QA: the NFR’s 200 entries are reached whole by keyboard (design D-3)', () => {
  function twoHundredTeams() {
    const base = teamContext();
    const organization = base.organizations[0];
    if (!organization) throw new Error('No organization.');
    const teams = Array.from({ length: 200 }, (_, index) => ({
      id: `c3000000-0000-4000-8000-${pad(index + 1)}`,
      name: `Команда ${pad(index + 1, 3)}`,
    }));
    teams[99] = { id: ids.platformTeam, name: 'Команда 100' };
    return { ...base, organizations: [{ ...organization, teams }] };
  }

  it('the head’s menu of 200 teams: End reaches the last, Home the first, Enter opens that team’s page at the same level', async () => {
    serveBoards(acrossProjectsDataset());
    renderBoards(backlogAddress('epics'), { context: twoHundredTeams() });
    await screen.findByRole('heading', { level: 1, name: 'Команда 100' });

    screen.getByRole('button', { name: 'Сменить команду' }).focus();
    await userEvent.keyboard('{ArrowDown}');
    const menu = await screen.findByRole('menu', { name: 'Команды' });
    expect(within(menu).getAllByRole('menuitem')).toHaveLength(200);
    await waitFor(() =>
      expect(within(menu).getByRole('menuitem', { name: 'Команда 100' })).toHaveFocus(),
    );

    await userEvent.keyboard('{End}');
    expect(within(menu).getByRole('menuitem', { name: 'Команда 200' })).toHaveFocus();
    await userEvent.keyboard('{Home}');
    expect(within(menu).getByRole('menuitem', { name: 'Команда 001' })).toHaveFocus();
    await userEvent.keyboard('{End}{Enter}');

    await waitFor(() =>
      expect(here().pathname).toBe(backlogAddress('epics', `c3000000-0000-4000-8000-${pad(200)}`)),
    );
  });

  it('the project filter of a team on 200 projects lists them all by name, and End reaches the last', async () => {
    const data = acrossProjectsDataset();
    const platform = data.teams.find((team) => team.id === ids.platformTeam);
    if (!platform) throw new Error('No team «Платформа».');
    const more = Array.from({ length: 198 }, (_, index) => ({
      id: `d0000000-0000-4000-8000-${pad(index + 1)}`,
      organizationId: ids.example,
      name: `Проект ${pad(index + 1, 3)}`,
      key: `Q${pad(index + 1, 4)}`,
      active: true,
    }));
    data.projects.push(...more);
    platform.moreProjectIds = [ids.ops, ...more.map((project) => project.id)];
    serveBoards(data);
    renderBoards(backlogAddress());
    await findBacklogTable();

    const names = await projectOptions();
    expect(names).toHaveLength(200);
    expect(names.slice(0, 3)).toEqual(['Мастаск', 'Операции', 'Проект 001']);
    expect(names.at(-1)).toBe('Проект 198');

    await userEvent.click(projectField());
    await screen.findByRole('listbox');
    await userEvent.keyboard('{End}{Enter}');
    await userEvent.keyboard('{Escape}');

    await waitFor(() => expect(here().search).toBe(`?projectId=${more.at(-1)?.id}`));
  });
});
