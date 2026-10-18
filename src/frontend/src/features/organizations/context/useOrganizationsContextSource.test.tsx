import { describe, expect, it, vi } from 'vitest';
import type { ReactNode } from 'react';
import { MemoryRouter, useLocation } from 'react-router-dom';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { act, renderHook, screen, waitFor } from '@testing-library/react';
import type { SwitchTargetResolver, WorkContextSource } from '../../../shared/lib/workContext';
import { organizationsKeys } from '../api/queryKeys';
import { PROJECTS_URL, ids, problem } from '../testFixtures';
import { serveOrganizations, serveProblem } from '../testApi';
import { storageKey } from './storage';
import { useOrganizationsContextSource } from './useOrganizationsContextSource';

const SUBJECT = 'user-1';

function LocationProbe() {
  const { pathname } = useLocation();
  return <div data-testid="location">{pathname}</div>;
}

function mount({
  path = '/home',
  subject = SUBJECT,
  resolver,
  queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } }),
}: {
  path?: string;
  subject?: string;
  resolver?: SwitchTargetResolver;
  queryClient?: QueryClient;
} = {}) {
  const wrapper = ({ children }: { children: ReactNode }) => (
    <QueryClientProvider client={queryClient}>
      <MemoryRouter initialEntries={[path]}>
        {children}
        <LocationProbe />
      </MemoryRouter>
    </QueryClientProvider>
  );
  const rendered = renderHook(
    () => useOrganizationsContextSource({ enabled: true, subject, resolveSwitchTarget: resolver }),
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

/** The switcher's three levels as names, the chosen one of each. */
function shown(source: WorkContextSource) {
  const organization = source.organizations.find(
    (entry) => entry.id === source.selection.organizationId,
  );
  const project = organization?.projects.find((entry) => entry.id === source.selection.projectId);
  const team = organization?.teams.find((entry) => entry.id === source.selection.teamId);
  return [organization?.name, project?.name, team?.name];
}

async function settled(result: { current: WorkContextSource }) {
  await waitFor(() => {
    expect(result.current.status).toBe('ready');
    expect(result.current.levelStatus?.project).toBe('ready');
    expect(result.current.levelStatus?.team).toBe('ready');
  });
}

describe('mod-002 US-8 Scenario: The switcher lists active organizations, projects and teams', () => {
  it('reads one level at a time, in the order received, nothing deactivated or deleted', async () => {
    serveOrganizations();
    const { result } = mount();

    await settled(result);

    expect(result.current.organizations.map((entry) => entry.name)).toEqual(['Альфа', 'Бета']);
    expect(result.current.organizations[0]!.projects.map((entry) => entry.name)).toEqual([
      'Аналитика',
      'Пример проекта',
    ]);
    expect(result.current.organizations[1]!.projects).toEqual([]);
    // feat-040 BR-TS-10: the team level is the organization's, by name; only the effective
    // organization carries its lists.
    expect(result.current.organizations[0]!.teams.map((entry) => entry.name)).toEqual([
      'Аналитика',
      'Платформа',
      'Пример проекта',
      'Старый проект',
    ]);
    expect(result.current.organizations[1]!.teams).toEqual([]);
    expect(shown(result.current)).toEqual(['Альфа', 'Аналитика', 'Аналитика']);
    expect(result.current.isReal).toBe(true);
    expect(result.current.organizationListPath).toBe('/organizations');
  });

  it('shows the levels loading on their own, top-down', async () => {
    serveOrganizations();
    const { result } = mount();

    expect(result.current.status).toBe('loading');
    await waitFor(() => expect(result.current.status).toBe('ready'));
    await settled(result);
  });
});

describe('mod-002 US-8 Scenario: With no team chosen the level’s first team stands (mod-017 BR-OT-11: by name)', () => {
  it('a project choice keeps the level: the project names no default, so the level’s first team, «Аналитика»', async () => {
    serveOrganizations();
    const { result } = mount();
    await settled(result);

    act(() => result.current.select('project', ids.prm));
    await settled(result);

    expect(shown(result.current)).toEqual(['Альфа', 'Пример проекта', 'Аналитика']);
  });
});

describe('mod-002 US-8 Scenario: The address sets the switcher', () => {
  it('«Бета» chosen, the settings of «Пример проекта» in «Альфа» opened: «Альфа», «Пример проекта», the level’s first team, remembered without it', async () => {
    serveOrganizations();
    remember({ organizationId: ids.beta, projectId: ids.rep, teamId: ids.repTeam });
    const { result } = mount({
      path: `/organizations/${ids.alfa}/projects/${ids.prm}/settings/general`,
    });

    await waitFor(() => expect(result.current.selection.projectId).toBe(ids.prm));
    await settled(result);

    expect(shown(result.current)).toEqual(['Альфа', 'Пример проекта', 'Аналитика']);
    expect(result.current.fallback ?? null).toBeNull();
    // The team of another organization does not travel, and the fallback is not a choice.
    await waitFor(() =>
      expect(remembered()).toEqual({ organizationId: ids.alfa, projectId: ids.prm }),
    );
  });

  it('a project’s address in the organization keeps a team chosen there', async () => {
    serveOrganizations();
    remember({ organizationId: ids.alfa, projectId: ids.anl, teamId: ids.platformTeam });
    const { result } = mount({
      path: `/organizations/${ids.alfa}/projects/${ids.prm}/settings/general`,
    });

    await waitFor(() => expect(result.current.selection.projectId).toBe(ids.prm));
    await settled(result);

    expect(shown(result.current)).toEqual(['Альфа', 'Пример проекта', 'Платформа']);
  });

  it('a deactivated organization’s address sets nothing and writes nothing', async () => {
    serveOrganizations();
    remember({ organizationId: ids.beta, projectId: ids.rep, teamId: ids.repTeam });
    const { result } = mount({ path: `/organizations/${ids.gamma}/settings/general` });

    await settled(result);

    expect(shown(result.current)).toEqual(['Бета', 'Отчётность', 'Отчётность']);
    expect(remembered()).toEqual({
      organizationId: ids.beta,
      projectId: ids.rep,
      teamId: ids.repTeam,
    });
  });

  it('a project of another organization in the address sets nothing', async () => {
    serveOrganizations();
    remember({ organizationId: ids.beta });
    const { result } = mount({
      path: `/organizations/${ids.alfa}/projects/${ids.rep}/settings/areas`,
    });

    await settled(result);
    await waitFor(() => expect(shown(result.current)[0]).toBe('Бета'));
  });

  it('mod-001’s process address sets its organization', async () => {
    serveOrganizations();
    remember({ organizationId: ids.beta });
    const { result } = mount({ path: `/organizations/${ids.alfa}/settings/process` });

    await waitFor(() => expect(result.current.selection.organizationId).toBe(ids.alfa));
  });

  // feat-028 BR-OL-12 (doc-012 P-1): «a team’s page sets its organization and project, never its team»
  // is retired by name — the team's page is the organization's and carries no project.
  it('the organization’s team page sets its organization and nothing below it', async () => {
    serveOrganizations();
    remember({ organizationId: ids.beta });
    const { result } = mount({
      path: `/organizations/${ids.alfa}/settings/teams/${ids.platformTeam}`,
    });

    await waitFor(() => expect(result.current.selection.organizationId).toBe(ids.alfa));
    await settled(result);
    expect(result.current.selection.teamId).not.toBe(ids.platformTeam);
  });
});

describe('mod-002 US-8 Scenario: Remembered across a reload', () => {
  it('a remount shows «Альфа / Пример проекта / Платформа» from mustask.context.<sub>', async () => {
    serveOrganizations();
    remember({ organizationId: ids.alfa, projectId: ids.prm, teamId: ids.platformTeam });

    const first = mount();
    await settled(first.result);
    expect(shown(first.result.current)).toEqual(['Альфа', 'Пример проекта', 'Платформа']);
    first.unmount();

    const second = mount();
    await settled(second.result);
    expect(shown(second.result.current)).toEqual(['Альфа', 'Пример проекта', 'Платформа']);
  });

  it('remembers a choice for the next visit', async () => {
    serveOrganizations();
    const { result } = mount();
    await settled(result);

    act(() => result.current.select('organization', ids.beta));
    await settled(result);

    // The team «Отчётность» is the level's fallback here, not a choice, so it is not written.
    await waitFor(() =>
      expect(remembered()).toEqual({ organizationId: ids.beta, projectId: ids.rep }),
    );
  });

  it('storage that holds garbage or throws remembers nothing and breaks nothing', async () => {
    serveOrganizations();
    localStorage.setItem(storageKey(SUBJECT), '{not json');
    const getItem = vi.spyOn(Storage.prototype, 'setItem').mockImplementation(() => {
      throw new DOMException('blocked', 'SecurityError');
    });

    const { result } = mount();
    await settled(result);
    act(() => result.current.select('organization', ids.beta));
    await settled(result);

    expect(shown(result.current)).toEqual(['Бета', 'Отчётность', 'Отчётность']);
    getItem.mockRestore();
  });

  it('without a subject nothing is remembered', async () => {
    serveOrganizations();
    const { result } = mount({ subject: '' });
    await settled(result);
    act(() => result.current.select('organization', ids.beta));
    await settled(result);

    expect(localStorage.length).toBe(0);
  });
});

describe('mod-002 US-8 Scenario: A remembered selection that is gone falls back', () => {
  it('team deleted → the fallback team, with the notice naming every level; the fallback is not remembered', async () => {
    const api = serveOrganizations();
    api.data.teams.find((team) => team.id === ids.platformTeam)!.deleted = true;
    remember({ organizationId: ids.alfa, projectId: ids.prm, teamId: ids.platformTeam });
    const { result } = mount();

    // «Пример проекта» is linked to «Пример проекта» alone now, which makes it the default.
    await waitFor(() =>
      expect(result.current.fallback?.names).toEqual(['Альфа', 'Пример проекта', 'Пример проекта']),
    );
    expect(shown(result.current)).toEqual(['Альфа', 'Пример проекта', 'Пример проекта']);
    await waitFor(() =>
      expect(remembered()).toEqual({ organizationId: ids.alfa, projectId: ids.prm }),
    );
  });

  it('project deactivated → «Аналитика»; a team chosen before stays', async () => {
    serveOrganizations();
    remember({ organizationId: ids.alfa, projectId: ids.old, teamId: ids.oldTeam });
    const { result } = mount();

    await waitFor(() =>
      expect(result.current.fallback?.names).toEqual(['Альфа', 'Аналитика', 'Старый проект']),
    );
    expect(shown(result.current)).toEqual(['Альфа', 'Аналитика', 'Старый проект']);
  });

  it('organization deactivated → «Бета», its first project, its first team', async () => {
    const api = serveOrganizations();
    api.data.organizations.find((organization) => organization.id === ids.alfa)!.active = false;
    remember({ organizationId: ids.alfa, projectId: ids.prm, teamId: ids.platformTeam });
    const { result } = mount();

    await waitFor(() =>
      expect(result.current.fallback?.names).toEqual(['Бета', 'Отчётность', 'Отчётность']),
    );
    act(() => result.current.dismissFallback?.());
    expect(result.current.fallback).toBeNull();
  });

  it('announces nothing on a first visit, nor when an address sets the selection', async () => {
    serveOrganizations();
    const first = mount();
    await settled(first.result);
    expect(first.result.current.fallback ?? null).toBeNull();
    first.unmount();

    remember({ organizationId: ids.gamma });
    const second = mount({ path: `/organizations/${ids.beta}/settings/general` });
    await waitFor(() => expect(second.result.current.selection.organizationId).toBe(ids.beta));
    await settled(second.result);
    expect(second.result.current.fallback ?? null).toBeNull();
  });

  it('a level that fails keeps its id and does not fall back', async () => {
    serveOrganizations();
    serveProblem('get', PROJECTS_URL, problem('organizations.validation_failed'), {});
    remember({ organizationId: ids.alfa, projectId: ids.prm, teamId: ids.platformTeam });
    const { result } = mount();

    await waitFor(() => expect(result.current.levelStatus?.project).toBe('failed'));
    expect(result.current.fallback ?? null).toBeNull();
    expect(remembered()).toEqual({
      organizationId: ids.alfa,
      projectId: ids.prm,
      teamId: ids.platformTeam,
    });
  });
});

describe('mod-002 US-8 Scenario: The lists fail to load — retry per level', () => {
  it('«Повторить» of the project level reads the projects again', async () => {
    serveOrganizations();
    serveProblem('get', PROJECTS_URL, problem('organizations.validation_failed'), { once: true });
    const { result } = mount();

    await waitFor(() => expect(result.current.levelStatus?.project).toBe('failed'));
    act(() => result.current.retry?.('project'));

    await settled(result);
    expect(shown(result.current)).toEqual(['Альфа', 'Аналитика', 'Аналитика']);
  });
});

describe('mod-002 US-8 Scenario: Own changes without a reload — the shared keys', () => {
  it('an invalidated organization list brings a new organization into the switcher', async () => {
    const api = serveOrganizations();
    const { result, queryClient } = mount();
    await settled(result);

    api.data.organizations.push({
      id: 'a0000000-0000-4000-8000-0000000000d1',
      name: 'Дельта',
      active: true,
    });
    await act(() =>
      queryClient.invalidateQueries({ queryKey: organizationsKeys.organizations('active') }),
    );

    await waitFor(() =>
      expect(result.current.organizations.map((entry) => entry.name)).toEqual([
        'Альфа',
        'Бета',
        'Дельта',
      ]),
    );
  });
});

describe('mod-002 US-8 Scenario: A project switch on a project-level page — the source moves the page', () => {
  it('a project choice asks the resolver with the address’s own ids and opens its answer', async () => {
    serveOrganizations();
    const resolver = vi.fn<SwitchTargetResolver>(() => '/target');
    const path = `/organizations/${ids.alfa}/projects/${ids.prm}/settings/areas`;
    const { result } = mount({ path, resolver });
    await waitFor(() => expect(result.current.selection.projectId).toBe(ids.prm));

    act(() => result.current.select('project', ids.anl));

    expect(resolver).toHaveBeenCalledWith({
      pathname: path,
      carried: { organizationId: ids.alfa, projectId: ids.prm },
      next: { organizationId: ids.alfa, projectId: ids.anl },
    });
    await waitFor(() => expect(screen.getByTestId('location')).toHaveTextContent('/target'));
  });

  it('an organization choice reads the new organization’s projects first, then asks with its first project', async () => {
    serveOrganizations();
    const resolver = vi.fn<SwitchTargetResolver>(() => '/target');
    const path = `/organizations/${ids.alfa}/projects/${ids.prm}/settings/general`;
    const { result } = mount({ path, resolver });
    await waitFor(() => expect(result.current.selection.projectId).toBe(ids.prm));

    act(() => result.current.select('organization', ids.beta));

    await waitFor(() =>
      expect(resolver).toHaveBeenCalledWith({
        pathname: path,
        carried: { organizationId: ids.alfa, projectId: ids.prm },
        next: { organizationId: ids.beta, projectId: ids.rep },
      }),
    );
  });

  it('opens «Главная» when the new organization’s projects cannot be read', async () => {
    serveOrganizations();
    const resolver = vi.fn<SwitchTargetResolver>(() => '/target');
    const path = `/organizations/${ids.alfa}/projects/${ids.prm}/settings/general`;
    const { result } = mount({ path, resolver });
    await waitFor(() => expect(result.current.selection.projectId).toBe(ids.prm));
    serveProblem('get', PROJECTS_URL, problem('organizations.validation_failed'));

    act(() => result.current.select('organization', ids.beta));

    await waitFor(() => expect(screen.getByTestId('location')).toHaveTextContent('/home'));
    expect(resolver).not.toHaveBeenCalled();
  });

  it('a choice at a level the address does not carry moves nothing', async () => {
    serveOrganizations();
    const resolver = vi.fn<SwitchTargetResolver>(() => '/target');
    const path = `/organizations/${ids.alfa}/settings/general`;
    const { result } = mount({ path, resolver });
    await settled(result);

    act(() => result.current.select('project', ids.prm));
    act(() => result.current.select('team', ids.platformTeam));

    expect(resolver).not.toHaveBeenCalled();
    expect(screen.getByTestId('location')).toHaveTextContent(path);
  });
});
