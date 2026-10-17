import { describe, expect, it, vi } from 'vitest';
import type { ReactNode } from 'react';
import { MemoryRouter } from 'react-router-dom';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { act, renderHook, waitFor } from '@testing-library/react';
import type { SwitchTargetResolver, WorkContextSource } from '../../../shared/lib/workContext';
import { ids, exampleDataset, type Dataset } from '../testFixtures';
import { serveOrganizations } from '../testApi';
import { storageKey } from './storage';
import { useOrganizationsContextSource } from './useOrganizationsContextSource';

// mod-016 US-7 (D-5, BR-M1-27): the default team of a project is the first
// team the user is in, in the project's order, else the project's first. The
// resolution is `resolveSelection.test.ts`'s; here the hook reads the lists
// with `isMember` from the in-memory API and chooses, loads and falls back.

const SUBJECT = 'user-1';

/** «Пример проекта» also has «Аналитика», which sorts before «Платформа» after the first team. */
const ANALYTICS_TEAM = 'c0000000-0000-4000-8000-0000000000a1';
/** «Аналитика» (the project) has a second team, so the first project of «Альфа» can hold the user's own. */
const ANL_SECOND_TEAM = 'c0000000-0000-4000-8000-0000000000a2';

/** The example dataset with two more teams, the acting user being in `own`. */
function datasetWith(own: readonly string[]): Dataset {
  const data = exampleDataset();
  data.teams.push(
    {
      id: ANALYTICS_TEAM,
      projectId: ids.prm,
      name: 'Аналитика',
      deleted: false,
    },
    {
      id: ANL_SECOND_TEAM,
      projectId: ids.anl,
      name: 'Вторая команда',
      deleted: false,
    },
  );
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
  return renderHook(
    () => useOrganizationsContextSource({ enabled: true, subject: SUBJECT, resolveSwitchTarget }),
    { wrapper },
  );
}

function remember(selection: Record<string, string>) {
  localStorage.setItem(storageKey(SUBJECT), JSON.stringify(selection));
}

async function settled(result: { current: WorkContextSource }) {
  await waitFor(() => {
    expect(result.current.status).toBe('ready');
    expect(result.current.levelStatus?.project).toBe('ready');
    expect(result.current.levelStatus?.team).toBe('ready');
  });
}

/** The selected team's name, or its id when the tree does not hold it. */
function teamName(source: WorkContextSource): string | undefined {
  const organization = source.organizations.find(
    (entry) => entry.id === source.selection.organizationId,
  );
  const project = organization?.projects.find((entry) => entry.id === source.selection.projectId);
  return project?.teams.find((entry) => entry.id === source.selection.teamId)?.name;
}

describe('mod-016 US-7 Scenario: Choosing a project selects the user’s own team', () => {
  it('a member of «Платформа» only: choosing «Пример проекта» shows «Платформа»', async () => {
    serveOrganizations(datasetWith([ids.platformTeam]));
    const { result } = mount();
    await settled(result);

    act(() => result.current.select('project', ids.prm));
    await settled(result);

    expect(result.current.selection.teamId).toBe(ids.platformTeam);
    expect(teamName(result.current)).toBe('Платформа');
  });

  it('the team menu keeps its order and marks nothing as the user’s', async () => {
    serveOrganizations(datasetWith([ids.platformTeam]));
    const { result } = mount();
    await settled(result);

    act(() => result.current.select('project', ids.prm));
    await settled(result);

    const alfa = result.current.organizations.find((entry) => entry.id === ids.alfa)!;
    const teams = alfa.projects.find((entry) => entry.id === ids.prm)!.teams;
    expect(teams.map((team) => team.name)).toEqual(['Аналитика', 'Платформа', 'Пример проекта']);
  });
});

describe('mod-016 US-7 Scenario: Several teams — the first of them in the project’s order', () => {
  it('a member of «Аналитика» and «Платформа» gets «Аналитика»', async () => {
    serveOrganizations(datasetWith([ids.platformTeam, ANALYTICS_TEAM]));
    const { result } = mount();
    await settled(result);

    act(() => result.current.select('project', ids.prm));
    await settled(result);

    expect(result.current.selection.teamId).toBe(ANALYTICS_TEAM);
  });
});

describe('mod-016 US-7 Scenario: In no team of the project — the list’s first team (mod-017 BR-OT-11: by name)', () => {
  it('a member of the organization who is in none gets «Аналитика», the list’s first', async () => {
    serveOrganizations(datasetWith([]));
    const { result } = mount();
    await settled(result);

    act(() => result.current.select('project', ids.prm));
    await settled(result);

    expect(result.current.selection.teamId).toBe(ANALYTICS_TEAM);
  });

  it('a user in a team of another project only still gets the list’s first', async () => {
    serveOrganizations(datasetWith([ANL_SECOND_TEAM]));
    const { result } = mount();
    await settled(result);

    act(() => result.current.select('project', ids.prm));
    await settled(result);

    expect(result.current.selection.teamId).toBe(ANALYTICS_TEAM);
  });
});

describe('mod-016 US-7 Scenario: Choosing the organization selects the user’s team too', () => {
  it('«Альфа» chosen: its first project and the user’s team in it', async () => {
    serveOrganizations(datasetWith([ANL_SECOND_TEAM]));
    remember({ organizationId: ids.beta, projectId: ids.rep, teamId: ids.repTeam });
    const { result } = mount();
    await settled(result);
    expect(result.current.selection.organizationId).toBe(ids.beta);

    act(() => result.current.select('organization', ids.alfa));
    await settled(result);

    expect(result.current.selection).toEqual({
      organizationId: ids.alfa,
      projectId: ids.anl,
      teamId: ANL_SECOND_TEAM,
    });
  });
});

describe('mod-016 US-7 Scenario: A first load with nothing remembered selects the user’s team', () => {
  it('«Альфа», its first project, and the user’s team in it', async () => {
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
});

describe('mod-016 US-7 Scenario: A remembered team that is gone falls back to the user’s team', () => {
  it('«Платформа» deleted: «Аналитика», not the first team, with the fallback notice', async () => {
    const api = serveOrganizations(datasetWith([ids.platformTeam, ANALYTICS_TEAM]));
    api.data.teams.find((team) => team.id === ids.platformTeam)!.deleted = true;
    remember({ organizationId: ids.alfa, projectId: ids.prm, teamId: ids.platformTeam });
    const { result } = mount();

    await waitFor(() =>
      expect(result.current.fallback?.names).toEqual(['Альфа', 'Пример проекта', 'Аналитика']),
    );
    expect(result.current.selection.teamId).toBe(ANALYTICS_TEAM);
  });
});

describe('mod-016 US-7 Scenario: A remembered team that still exists is kept', () => {
  it('a member of «Платформа» only, «Пример проекта» / first team remembered: it stays', async () => {
    serveOrganizations(datasetWith([ids.platformTeam]));
    remember({ organizationId: ids.alfa, projectId: ids.prm, teamId: ids.prmTeam });
    const { result } = mount();

    await settled(result);

    expect(result.current.selection.teamId).toBe(ids.prmTeam);
    expect(result.current.fallback ?? null).toBeNull();
  });
});

describe('mod-016 US-7 Scenario: A team in the address wins', () => {
  it('the address names «Аналитика»: the switcher shows it, not the user’s own team', async () => {
    serveOrganizations(datasetWith([ids.platformTeam]));
    const { result } = mount(
      `/organizations/${ids.alfa}/projects/${ids.prm}/teams/${ANALYTICS_TEAM}/backlogs/features`,
    );

    await waitFor(() => expect(result.current.selection.teamId).toBe(ANALYTICS_TEAM));
    await settled(result);

    expect(result.current.selection.teamId).toBe(ANALYTICS_TEAM);
  });
});

describe('mod-016 US-7 on a team-level page (mod-015 BR-18): moving to another project', () => {
  it('asks the switch resolver with the user’s team of the project it lands in', async () => {
    serveOrganizations(datasetWith([ANL_SECOND_TEAM]));
    const resolver = vi.fn<SwitchTargetResolver>(({ next }) => `/to/${next.projectId}`);
    const { result } = mount(
      `/organizations/${ids.alfa}/projects/${ids.prm}/teams/${ids.platformTeam}/backlogs/features`,
      resolver,
    );
    await waitFor(() => expect(result.current.selection.teamId).toBe(ids.platformTeam));
    await settled(result);

    act(() => result.current.select('project', ids.anl));

    await waitFor(() => expect(resolver).toHaveBeenCalledTimes(1));
    expect(resolver.mock.calls[0]![0].next).toEqual({
      organizationId: ids.alfa,
      projectId: ids.anl,
      teamId: ANL_SECOND_TEAM,
    });
  });
});

describe('mod-016 US-7 Scenario: Leaving the team changes the default the next time the list is read', () => {
  it('after the membership is removed, choosing the project again gives the list’s first team', async () => {
    const api = serveOrganizations(datasetWith([ids.platformTeam]));
    const { result } = mount();
    await settled(result);
    act(() => result.current.select('project', ids.prm));
    await waitFor(() => expect(result.current.selection.teamId).toBe(ids.platformTeam));

    api.data.teamMembers = [];
    act(() => result.current.select('project', ids.anl));
    await settled(result);
    act(() => result.current.select('project', ids.prm));

    await waitFor(() => expect(result.current.selection.teamId).toBe(ANALYTICS_TEAM));
  });
});
