import { describe, expect, it, vi } from 'vitest';
import type { ReactNode } from 'react';
import { MemoryRouter } from 'react-router-dom';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { act, renderHook, waitFor } from '@testing-library/react';
import { HttpResponse, http } from 'msw';
import { server } from '../../../test/mocks/server';
import type { SwitchTargetResolver, WorkContextSource } from '../../../shared/lib/workContext';
import { organizationsKeys } from '../api/queryKeys';
import { ORG_TEAMS_URL, exampleDataset, ids, team, type Dataset } from '../testFixtures';
import { serveOrganizations } from '../testApi';
import { storageKey } from './storage';
import { useOrganizationsContextSource } from './useOrganizationsContextSource';

// The switcher's team level (feat-040 BR-TS-10, doc-013 OQ-3 A): the organization's active teams,
// the user's own first, each group by name, whatever project is chosen. A team the user chose —
// in the switcher, in the head's menu or by a team address — stays across a project change; with
// none chosen the level shows its fallback, which is derived every time: the effective project's
// default team (mod-020: the one its settings name, else its only team), else the user's own first
// team (mod-016 D-5, BR-M1-27), else the level's first entry. A fallback is never remembered, so a
// reload neither makes it chosen nor a chosen team a fallback. The resolution is
// `resolveSelection.test.ts`'s; here the hook reads the lists with `isMember` and the default
// mark from the in-memory API and chooses, loads and falls back.

const SUBJECT = 'user-1';

/** «Аналитика» is the one organization team of that name, linked to the project «Аналитика». */
const ANALYTICS_TEAM = ids.anlTeam;
/** «Аналитика» (the project) has a second team, so its two teams name no default of their own. */
const ANL_SECOND_TEAM = 'c0000000-0000-4000-8000-0000000000a2';

/**
 * The example dataset with a second team of «Аналитика», the acting user being in `own`. The
 * organization's teams by name: «Аналитика», «Вторая команда», «Платформа», «Пример проекта»,
 * «Старый проект».
 */
function datasetWith(own: readonly string[]): Dataset {
  const data = exampleDataset();
  data.teams.push(team(ANL_SECOND_TEAM, ids.alfa, 'Вторая команда'));
  data.links.push({ projectId: ids.anl, teamId: ANL_SECOND_TEAM });
  data.actingUserId = SUBJECT;
  data.teamMembers = own.map((teamId) => ({ teamId, userId: SUBJECT }));
  return data;
}

function mount(path = '/home', resolveSwitchTarget?: SwitchTargetResolver) {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  const wrapper = ({ children }: { children: ReactNode }) => (
    <QueryClientProvider client={queryClient}>
      <MemoryRouter initialEntries={[path]}>{children}</MemoryRouter>
    </QueryClientProvider>
  );
  const rendered = renderHook(
    () => useOrganizationsContextSource({ enabled: true, subject: SUBJECT, resolveSwitchTarget }),
    { wrapper },
  );
  return { ...rendered, queryClient };
}

function remember(selection: Record<string, string>) {
  localStorage.setItem(storageKey(SUBJECT), JSON.stringify(selection));
}

function remembered(): unknown {
  const raw = localStorage.getItem(storageKey(SUBJECT));
  return raw === null ? null : JSON.parse(raw);
}

async function settled(result: { current: WorkContextSource }) {
  await waitFor(() => {
    expect(result.current.status).toBe('ready');
    expect(result.current.levelStatus?.project).toBe('ready');
    expect(result.current.levelStatus?.team).toBe('ready');
  });
}

/** The team level of the effective organization, as names, in the order the switcher lists it. */
function level(source: WorkContextSource): string[] {
  const organization = source.organizations.find(
    (entry) => entry.id === source.selection.organizationId,
  );
  return organization?.teams.map((entry) => entry.name) ?? [];
}

/** The selected team's name. */
function teamName(source: WorkContextSource): string | undefined {
  const organization = source.organizations.find(
    (entry) => entry.id === source.selection.organizationId,
  );
  return organization?.teams.find((entry) => entry.id === source.selection.teamId)?.name;
}

/** `dataset` with `teamId` named the default team of `projectId` (mod-017 BR-OT-4). */
function withDefault(dataset: Dataset, projectId: string, teamId: string): Dataset {
  dataset.projects.find((project) => project.id === projectId)!.defaultTeamId = teamId;
  return dataset;
}

describe('feat-040 US-6 Scenario: The team level lists the organization’s teams, the user’s own first', () => {
  it('a member of «Платформа»: it first, then the rest by name, whatever project is chosen', async () => {
    serveOrganizations(datasetWith([ids.platformTeam]));
    const { result } = mount();
    await settled(result);
    const expected = [
      'Платформа',
      'Аналитика',
      'Вторая команда',
      'Пример проекта',
      'Старый проект',
    ];
    expect(level(result.current)).toEqual(expected);

    act(() => result.current.select('project', ids.prm));
    await settled(result);
    expect(level(result.current)).toEqual(expected);

    act(() => result.current.select('project', ids.anl));
    await settled(result);
    expect(level(result.current)).toEqual(expected);
  });

  it('a member of no team: all by name; a team of another organization is not listed', async () => {
    serveOrganizations(datasetWith([]));
    const { result } = mount();
    await settled(result);

    expect(level(result.current)).toEqual([
      'Аналитика',
      'Вторая команда',
      'Платформа',
      'Пример проекта',
      'Старый проект',
    ]);
  });

  it('a deleted team is not listed', async () => {
    const api = serveOrganizations(datasetWith([]));
    api.data.teams.find((candidate) => candidate.id === ids.oldTeam)!.deleted = true;
    const { result } = mount();
    await settled(result);

    expect(level(result.current)).not.toContain('Старый проект');
  });

  it('an organization with no project still lists its teams, and its team is selected', async () => {
    const api = serveOrganizations(datasetWith([]));
    api.data.projects.find((project) => project.id === ids.rep)!.active = false;
    remember({ organizationId: ids.beta });
    const { result } = mount();
    await settled(result);

    expect(result.current.selection).toEqual({
      organizationId: ids.beta,
      projectId: undefined,
      teamId: ids.repTeam,
    });
    expect(level(result.current)).toEqual(['Отчётность']);
  });

  it('an organization with no team has an empty level, whatever its projects', async () => {
    const api = serveOrganizations(datasetWith([]));
    api.data.teams = api.data.teams.filter((candidate) => candidate.organizationId !== ids.beta);
    remember({ organizationId: ids.beta });
    const { result } = mount();
    await settled(result);

    expect(result.current.selection).toEqual({ organizationId: ids.beta, projectId: ids.rep });
    expect(level(result.current)).toEqual([]);
  });

  it('the organization’s teams failing to read leave the team level failed and hold no team; the other levels work', async () => {
    serveOrganizations(datasetWith([]));
    server.use(
      http.get(ORG_TEAMS_URL, () =>
        HttpResponse.json(
          { status: 500, title: 'x', detail: 'x', errorCode: 'http.error' },
          { status: 500, headers: { 'Content-Type': 'application/problem+json' } },
        ),
      ),
    );
    const { result } = mount();

    await waitFor(() => expect(result.current.levelStatus?.team).toBe('failed'));
    expect(result.current.levelStatus?.project).toBe('ready');
    expect(result.current.selection.organizationId).toBe(ids.alfa);
    expect(result.current.selection.projectId).toBe(ids.anl);
    expect(result.current.selection.teamId).toBeUndefined();
  });

  it('after the membership is removed, the next read of the teams orders the level again', async () => {
    const api = serveOrganizations(datasetWith([ids.platformTeam]));
    const { result, queryClient } = mount();
    await settled(result);
    expect(level(result.current)[0]).toBe('Платформа');

    api.data.teamMembers = [];
    await act(() =>
      queryClient.invalidateQueries({ queryKey: organizationsKeys.orgTeams(ids.alfa) }),
    );

    await waitFor(() => expect(level(result.current)[0]).toBe('Аналитика'));
  });
});

describe('feat-040 US-6 Scenario: With no team chosen, the level shows its fallback', () => {
  it('a first load: the first project, and the user’s own first team of the level (mod-016 D-5)', async () => {
    serveOrganizations(datasetWith([ANL_SECOND_TEAM]));
    const { result } = mount();

    await settled(result);

    expect(result.current.selection).toEqual({
      organizationId: ids.alfa,
      projectId: ids.anl,
      teamId: ANL_SECOND_TEAM,
    });
    expect(result.current.fallback ?? null).toBeNull();
  });

  it('a user in none gets the level’s first entry', async () => {
    serveOrganizations(datasetWith([]));
    const { result } = mount();

    await settled(result);

    expect(result.current.selection.teamId).toBe(ANALYTICS_TEAM);
  });

  it('the chosen project’s default team comes before the user’s own team (mod-020)', async () => {
    serveOrganizations(withDefault(datasetWith([ANALYTICS_TEAM]), ids.prm, ids.platformTeam));
    const { result } = mount();
    await settled(result);

    act(() => result.current.select('project', ids.prm));
    await settled(result);

    expect(result.current.selection.teamId).toBe(ids.platformTeam);
    expect(teamName(result.current)).toBe('Платформа');
  });

  it('a project that names none: the user’s own first team, even one not linked to it', async () => {
    serveOrganizations(datasetWith([ids.platformTeam]));
    const { result } = mount();
    await settled(result);

    act(() => result.current.select('project', ids.anl));
    await settled(result);

    expect(result.current.selection.teamId).toBe(ids.platformTeam);
  });

  it('an organization choice lands on its first project’s only linked team (mod-020)', async () => {
    serveOrganizations(datasetWith([]));
    const { result } = mount();
    await settled(result);

    act(() => result.current.select('organization', ids.beta));
    await settled(result);
    expect(result.current.selection).toEqual({
      organizationId: ids.beta,
      projectId: ids.rep,
      teamId: ids.repTeam,
    });
  });

  it('the fallback is applied again on a project change: a team shown by the fallback is not chosen', async () => {
    serveOrganizations(withDefault(datasetWith([]), ids.prm, ids.platformTeam));
    const { result } = mount();
    await settled(result);
    expect(result.current.selection.teamId).toBe(ANALYTICS_TEAM);

    act(() => result.current.select('project', ids.prm));
    await settled(result);
    expect(result.current.selection.teamId).toBe(ids.platformTeam);

    act(() => result.current.select('project', ids.anl));
    await settled(result);
    expect(result.current.selection.teamId).toBe(ANALYTICS_TEAM);
  });

  it('a project without a default and without a linked team falls to the level’s first entry', async () => {
    const api = serveOrganizations(datasetWith([]));
    api.data.links = api.data.links.filter((link) => link.projectId !== ids.prm);
    const { result } = mount();
    await settled(result);

    act(() => result.current.select('project', ids.prm));
    await settled(result);

    expect(result.current.selection.teamId).toBe(ANALYTICS_TEAM);
  });

  it('a fallback is not remembered: nothing is written for the team, a reload derives it again', async () => {
    serveOrganizations(withDefault(datasetWith([]), ids.prm, ids.platformTeam));
    const first = mount();
    await settled(first.result);
    act(() => first.result.current.select('project', ids.prm));
    await settled(first.result);
    expect(first.result.current.selection.teamId).toBe(ids.platformTeam);
    await waitFor(() =>
      expect(remembered()).toEqual({ organizationId: ids.alfa, projectId: ids.prm }),
    );
    first.unmount();

    const second = mount();
    await settled(second.result);
    expect(second.result.current.selection).toEqual({
      organizationId: ids.alfa,
      projectId: ids.prm,
      teamId: ids.platformTeam,
    });

    // The default changes meanwhile: the reload still shows a fallback, so it follows.
    act(() => second.result.current.select('project', ids.anl));
    await settled(second.result);
    expect(second.result.current.selection.teamId).toBe(ANALYTICS_TEAM);
  });
});

describe('feat-040 US-6 Scenario: A chosen team stays', () => {
  it('a project change keeps it, remembered, and a reload does not make it a fallback', async () => {
    serveOrganizations(withDefault(datasetWith([]), ids.prm, ids.platformTeam));
    const first = mount();
    await settled(first.result);

    act(() => first.result.current.select('team', ids.anlTeam));
    await settled(first.result);
    act(() => first.result.current.select('project', ids.prm));
    await settled(first.result);

    expect(first.result.current.selection).toEqual({
      organizationId: ids.alfa,
      projectId: ids.prm,
      teamId: ids.anlTeam,
    });
    await waitFor(() =>
      expect(remembered()).toEqual({
        organizationId: ids.alfa,
        projectId: ids.prm,
        teamId: ids.anlTeam,
      }),
    );
    first.unmount();

    const second = mount();
    await settled(second.result);
    act(() => second.result.current.select('project', ids.anl));
    await settled(second.result);
    expect(second.result.current.selection.teamId).toBe(ids.anlTeam);
  });

  it('an organization choice drops it: one remembered selection, not one per organization', async () => {
    serveOrganizations(datasetWith([]));
    const { result } = mount();
    await settled(result);
    act(() => result.current.select('team', ids.platformTeam));
    await settled(result);

    act(() => result.current.select('organization', ids.beta));
    await settled(result);
    act(() => result.current.select('organization', ids.alfa));
    await settled(result);

    expect(result.current.selection.teamId).toBe(ANALYTICS_TEAM);
  });

  it('a remembered team that still exists is kept, though the project names another', async () => {
    serveOrganizations(withDefault(datasetWith([ANALYTICS_TEAM]), ids.prm, ids.prmTeam));
    remember({ organizationId: ids.alfa, projectId: ids.prm, teamId: ids.platformTeam });
    const { result } = mount();

    await settled(result);

    expect(result.current.selection.teamId).toBe(ids.platformTeam);
    expect(result.current.fallback ?? null).toBeNull();
  });

  it('a remembered team that is gone falls back to the default, with the fallback notice, and the fallback is not remembered', async () => {
    const api = serveOrganizations(
      withDefault(datasetWith([ANALYTICS_TEAM]), ids.prm, ids.prmTeam),
    );
    api.data.teams.find((candidate) => candidate.id === ids.platformTeam)!.deleted = true;
    remember({ organizationId: ids.alfa, projectId: ids.prm, teamId: ids.platformTeam });
    const { result } = mount();

    await waitFor(() =>
      expect(result.current.fallback?.names).toEqual(['Альфа', 'Пример проекта', 'Пример проекта']),
    );
    expect(result.current.selection.teamId).toBe(ids.prmTeam);
    await waitFor(() =>
      expect(remembered()).toEqual({ organizationId: ids.alfa, projectId: ids.prm }),
    );
  });

  it('a remembered team that is gone falls back to the user’s team when the project names none', async () => {
    const api = serveOrganizations(datasetWith([ANALYTICS_TEAM, ids.platformTeam]));
    api.data.teams.find((candidate) => candidate.id === ids.platformTeam)!.deleted = true;
    // Two linked teams and none named: the project has no default of its own.
    api.data.links.push({ projectId: ids.prm, teamId: ANALYTICS_TEAM });
    remember({ organizationId: ids.alfa, projectId: ids.prm, teamId: ids.platformTeam });
    const { result } = mount();

    await waitFor(() =>
      expect(result.current.fallback?.names).toEqual(['Альфа', 'Пример проекта', 'Аналитика']),
    );
    expect(result.current.selection.teamId).toBe(ANALYTICS_TEAM);
  });
});

describe('feat-040 US-6 Scenario: A team address sets the organization and the team', () => {
  it('the address names «Аналитика»: the switcher shows it as chosen, the project level at its first entry', async () => {
    serveOrganizations(withDefault(datasetWith([ids.platformTeam]), ids.anl, ids.anlTeam));
    remember({ organizationId: ids.beta, projectId: ids.rep });
    const { result } = mount(
      `/organizations/${ids.alfa}/teams/${ANALYTICS_TEAM}/backlogs/features`,
    );

    await waitFor(() => expect(result.current.selection.teamId).toBe(ANALYTICS_TEAM));
    await settled(result);

    expect(result.current.selection).toEqual({
      organizationId: ids.alfa,
      projectId: ids.anl,
      teamId: ANALYTICS_TEAM,
    });
    await waitFor(() =>
      expect(remembered()).toEqual({
        organizationId: ids.alfa,
        projectId: ids.anl,
        teamId: ANALYTICS_TEAM,
      }),
    );
  });

  it('a team in the address wins over the default and the user’s own team', async () => {
    serveOrganizations(withDefault(datasetWith([ids.platformTeam]), ids.anl, ANL_SECOND_TEAM));
    const { result } = mount(`/organizations/${ids.alfa}/teams/${ids.prmTeam}/boards/stories`);

    await waitFor(() => expect(result.current.selection.teamId).toBe(ids.prmTeam));
    await settled(result);

    expect(result.current.selection.teamId).toBe(ids.prmTeam);
  });

  it('a team of another organization in the address sets nothing', async () => {
    serveOrganizations(datasetWith([]));
    const { result } = mount(`/organizations/${ids.alfa}/teams/${ids.repTeam}/backlogs/stories`);

    await settled(result);

    expect(result.current.selection.teamId).toBe(ANALYTICS_TEAM);
    expect(remembered()).toBeNull();
  });

  it('the retired project-nested address names no team', async () => {
    serveOrganizations(datasetWith([]));
    const { result } = mount(
      `/organizations/${ids.alfa}/projects/${ids.prm}/teams/${ids.platformTeam}/backlogs/stories`,
    );

    await waitFor(() => expect(result.current.selection.projectId).toBe(ids.prm));
    await settled(result);

    expect(result.current.selection.teamId).toBe(ANALYTICS_TEAM);
  });
});

describe('feat-040 US-6 Scenario: On a project page a team choice moves nothing; on a team page a project choice moves nothing', () => {
  it('on a project page the team choice asks no resolver, and the project choice does', async () => {
    serveOrganizations(datasetWith([]));
    const resolver = vi.fn<SwitchTargetResolver>(({ next }) => `/to/${next.projectId}`);
    const { result } = mount(
      `/organizations/${ids.alfa}/projects/${ids.prm}/backlogs/stories`,
      resolver,
    );
    await waitFor(() => expect(result.current.selection.projectId).toBe(ids.prm));
    await settled(result);

    act(() => result.current.select('team', ids.platformTeam));
    expect(resolver).not.toHaveBeenCalled();
    expect(result.current.selection.teamId).toBe(ids.platformTeam);

    act(() => result.current.select('project', ids.anl));
    await waitFor(() => expect(resolver).toHaveBeenCalledTimes(1));
    expect(resolver.mock.calls[0]![0].next).toEqual({
      organizationId: ids.alfa,
      projectId: ids.anl,
      teamId: ids.platformTeam,
    });
  });
});
